using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace AdminDashboard.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private readonly IConfiguration _config;
    private readonly ILogger<LoginModel> _logger;
    private readonly IMemoryCache _cache;

    private sealed record LoginAttemptState(int FailedAttempts, DateTimeOffset LastFailedAttempt);

    public LoginModel(IConfiguration config, ILogger<LoginModel> logger, IMemoryCache cache)
    {
        _config = config;
        _logger = logger;
        _cache = cache;
    }

    [BindProperty]
    public LoginInputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var clientIp = GetClientIpAddress();
        var cacheKey = $"login_attempt_{clientIp}";

        // Apply throttling if multiple consecutive failed attempts occurred from this client IP
        if (_cache.TryGetValue(cacheKey, out LoginAttemptState? state) && state is not null)
        {
            if (state.FailedAttempts >= MaxFailedAttempts && (DateTimeOffset.UtcNow - state.LastFailedAttempt) < LockoutDuration)
            {
                ErrorMessage = "Too many failed attempts. Please wait a few minutes before trying again.";
                _logger.LogWarning("Throttled login attempt from {IP} (Failed attempts: {Count})", clientIp, state.FailedAttempts);
                return Page();
            }
        }

        var configuredUser = _config["ADMIN_USERNAME"] 
                             ?? _config["AdminAuth:Username"] 
                             ?? "admin";

        var configuredPass = _config["ADMIN_PASSWORD"] 
                             ?? _config["AdminAuth:Password"] 
                             ?? "AdminPass123!";

        var isUserValid = ConstantTimeEquals(Input.Username.Trim(), configuredUser.Trim());
        var isPassValid = ConstantTimeEquals(Input.Password, configuredPass);

        if (!isUserValid || !isPassValid)
        {
            var attempts = (_cache.TryGetValue(cacheKey, out LoginAttemptState? existing) && existing is not null)
                ? existing.FailedAttempts + 1
                : 1;

            _cache.Set(cacheKey, new LoginAttemptState(attempts, DateTimeOffset.UtcNow), LockoutDuration);

            _logger.LogWarning("Failed login attempt ({Attempt}/{Max}) for username '{Username}' from {IP}",
                attempts, MaxFailedAttempts, Input.Username, clientIp);

            ErrorMessage = "Invalid administrator username or password.";
            return Page();
        }

        // Reset failed attempts on success for this client IP
        _cache.Remove(cacheKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, configuredUser),
            new(ClaimTypes.Role, "Administrator"),
            new(ClaimTypes.AuthenticationInstant, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new("DisplayName", "Admin Portal Manager")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = Input.RememberMe,
            ExpiresUtc = Input.RememberMe 
                ? DateTimeOffset.UtcNow.AddDays(14) 
                : DateTimeOffset.UtcNow.AddHours(8),
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        _logger.LogInformation("Administrator '{Username}' successfully authenticated from {IP}",
            configuredUser, clientIp);

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
    }

    private string GetClientIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrWhiteSpace(forwarded))
        {
            var ip = forwarded.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

public class LoginInputModel
{
    [Required(ErrorMessage = "Username is required.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember Me")]
    public bool RememberMe { get; set; }
}
