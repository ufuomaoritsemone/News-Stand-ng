using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminDashboard.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly ILogger<LoginModel> _logger;

    // Track failed attempts in-memory for brute-force deterrence
    private static int _failedAttempts;
    private static DateTimeOffset _lastFailedAttempt = DateTimeOffset.MinValue;
    private static readonly object _lock = new();

    public LoginModel(IConfiguration config, ILogger<LoginModel> logger)
    {
        _config = config;
        _logger = logger;
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

        // Apply throttling if multiple consecutive failed attempts occurred
        lock (_lock)
        {
            if (_failedAttempts >= 5 && (DateTimeOffset.UtcNow - _lastFailedAttempt).TotalMinutes < 5)
            {
                ErrorMessage = "Too many failed attempts. Please wait a few minutes before trying again.";
                _logger.LogWarning("Throttled login attempt from {IP}", HttpContext.Connection.RemoteIpAddress);
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
            lock (_lock)
            {
                _failedAttempts++;
                _lastFailedAttempt = DateTimeOffset.UtcNow;
            }

            _logger.LogWarning("Failed login attempt for username '{Username}' from {IP}",
                Input.Username, HttpContext.Connection.RemoteIpAddress);

            ErrorMessage = "Invalid administrator username or password.";
            return Page();
        }

        // Reset failed attempts on success
        lock (_lock)
        {
            _failedAttempts = 0;
        }

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
            configuredUser, HttpContext.Connection.RemoteIpAddress);

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
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
