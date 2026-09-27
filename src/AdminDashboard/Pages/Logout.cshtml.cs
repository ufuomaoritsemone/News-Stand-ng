using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminDashboard.Pages;

[Authorize]
public class LogoutModel : PageModel
{
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(ILogger<LogoutModel> logger)
    {
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        return await SignOutAndRedirectAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        return await SignOutAndRedirectAsync();
    }

    private async Task<IActionResult> SignOutAndRedirectAsync()
    {
        var username = User.Identity?.Name ?? "Administrator";
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation("Administrator '{Username}' signed out successfully from {IP}",
            username, HttpContext.Connection.RemoteIpAddress);

        return RedirectToPage("/Login");
    }
}
