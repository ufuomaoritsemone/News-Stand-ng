using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;
    private readonly IHostEnvironment _env;

    public AuthController(
        NewsDbContext db,
        IConfiguration config,
        ILogger<AuthController> logger,
        IHostEnvironment env)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _env = env;
    }

    public record GoogleAuthRequest(string IdToken, string? CustomDisplayName);
    public record EmailOtpRequest(string Email, string? DisplayName);
    public record EmailVerifyRequest(string Email, string Code, string? CustomDisplayName);

    /// <summary>
    /// Returns public authentication configurations (Google Client ID and enabled verification channels).
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetAuthConfig()
    {
        var clientId = _config["Google:ClientId"] ?? "434931027697-0ofh3nptkv409vju55od3fhrfbkccu15.apps.googleusercontent.com";
        return Ok(new
        {
            googleClientId = clientId,
            enabledMethods = new[] { "Google", "Email" }
        });
    }

    /// <summary>
    /// Authenticates a reader using their signed Google ID Token from Google Identity Services.
    /// </summary>
    [HttpPost("google")]
    public async Task<IActionResult> AuthenticateWithGoogle(
        [FromBody] GoogleAuthRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request?.IdToken))
        {
            return BadRequest(new { error = "Google ID token is required." });
        }

        var configuredClientId = _config["Google:ClientId"] ?? "434931027697-0ofh3nptkv409vju55od3fhrfbkccu15.apps.googleusercontent.com";

        try
        {
            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = string.IsNullOrEmpty(configuredClientId) ? null : [configuredClientId]
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, validationSettings);

            var email = payload.Email?.ToLowerInvariant() ?? string.Empty;
            var subId = payload.Subject;
            var defaultName = payload.Name ?? "Google Reader";
            var avatarUrl = payload.Picture;

            var chosenName = !string.IsNullOrWhiteSpace(request.CustomDisplayName)
                ? request.CustomDisplayName.Trim()
                : defaultName;

            // Check if user verification record already exists
            var existing = await _db.UserVerifications
                .FirstOrDefaultAsync(u => u.Identifier == email, ct);

            if (existing != null)
            {
                existing.DisplayName = chosenName;
                existing.AvatarUrl = avatarUrl;
                existing.Channel = "Google";
                existing.IsVerified = true;
                existing.ExpiresAt = DateTime.UtcNow.AddYears(5);
            }
            else
            {
                existing = new UserVerification
                {
                    Id = $"g_{subId}",
                    Identifier = email,
                    DisplayName = chosenName,
                    AvatarUrl = avatarUrl,
                    Channel = "Google",
                    IsVerified = true,
                    ExpiresAt = DateTime.UtcNow.AddYears(5),
                    CreatedAt = DateTime.UtcNow
                };
                _db.UserVerifications.Add(existing);
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Reader verified successfully via Google OAuth: {Email}", email);

            return Ok(new
            {
                success = true,
                token = $"tkn_{Guid.NewGuid():N}",
                user = new
                {
                    id = existing.Id,
                    email = existing.Identifier,
                    displayName = existing.DisplayName,
                    avatarUrl = existing.AvatarUrl,
                    channel = "Google",
                    isVerified = true
                }
            });
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning("Invalid Google ID Token received: {Msg}", ex.Message);
            return Unauthorized(new { error = "Invalid or expired Google authentication token. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error verifying Google OAuth token.");
            return StatusCode(500, new { error = "Internal error during Google verification." });
        }
    }

    /// <summary>
    /// Generates and dispatches a 6-digit verification code to the reader's email address.
    /// </summary>
    [HttpPost("email/request-otp")]
    public async Task<IActionResult> RequestEmailOtp(
        [FromBody] EmailOtpRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Email))
        {
            return BadRequest(new { error = "Email address is required." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (!IsValidEmail(email))
        {
            return BadRequest(new { error = "Please provide a valid email address." });
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? email.Split('@')[0]
            : request.DisplayName.Trim();

        // Generate 6-digit random code
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var codeHash = HashCode(code);

        var existing = await _db.UserVerifications
            .FirstOrDefaultAsync(u => u.Identifier == email, ct);

        if (existing != null)
        {
            existing.DisplayName = displayName;
            existing.OtpHash = codeHash;
            existing.Channel = "Email";
            existing.AttemptCount = 0;
            existing.ExpiresAt = DateTime.UtcNow.AddMinutes(10);
        }
        else
        {
            existing = new UserVerification
            {
                Id = Guid.NewGuid().ToString(),
                Identifier = email,
                DisplayName = displayName,
                OtpHash = codeHash,
                Channel = "Email",
                IsVerified = false,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow
            };
            _db.UserVerifications.Add(existing);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Generated verification code for {Email}", email);

        // Attempt sending via SMTP if configured
        var smtpHost = _config["Feedback:Smtp:Host"];
        var sentViaEmail = false;

        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            try
            {
                await SendOtpEmailAsync(email, code, displayName, ct);
                sentViaEmail = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send OTP via SMTP to {Email}. Falling back to simulation.", email);
            }
        }

        return Ok(new
        {
            success = true,
            message = sentViaEmail
                ? $"A 6-digit verification code was sent to {email}."
                : $"Verification code generated for {email}.",
            email,
            expiresInMinutes = 10,
            // Include code in response when running in development or when SMTP is unconfigured for frictionless testing
            debugCode = (_env.IsDevelopment() || !sentViaEmail) ? code : null
        });
    }

    /// <summary>
    /// Validates the 6-digit verification code and returns a verified session profile.
    /// </summary>
    [HttpPost("email/verify-otp")]
    public async Task<IActionResult> VerifyEmailOtp(
        [FromBody] EmailVerifyRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request?.Code))
        {
            return BadRequest(new { error = "Email and verification code are required." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var code = request.Code.Trim();

        var record = await _db.UserVerifications
            .FirstOrDefaultAsync(u => u.Identifier == email, ct);

        if (record == null)
        {
            return NotFound(new { error = "No pending verification found for this email. Please request a new code." });
        }

        if (DateTime.UtcNow > record.ExpiresAt)
        {
            return BadRequest(new { error = "Verification code has expired. Please request a new code." });
        }

        if (record.AttemptCount >= 5)
        {
            return BadRequest(new { error = "Too many incorrect attempts. Please request a new code." });
        }

        var expectedHash = record.OtpHash;
        var enteredHash = HashCode(code);

        if (!string.Equals(expectedHash, enteredHash, StringComparison.Ordinal))
        {
            record.AttemptCount++;
            await _db.SaveChangesAsync(ct);
            var remaining = 5 - record.AttemptCount;
            return BadRequest(new { error = $"Incorrect verification code. {remaining} attempt(s) remaining." });
        }

        // Successfully verified
        record.IsVerified = true;
        record.OtpHash = null;
        if (!string.IsNullOrWhiteSpace(request.CustomDisplayName))
        {
            record.DisplayName = request.CustomDisplayName.Trim();
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Email verified successfully: {Email} as {Name}", email, record.DisplayName);

        return Ok(new
        {
            success = true,
            token = $"tkn_{Guid.NewGuid():N}",
            user = new
            {
                id = record.Id,
                email = record.Identifier,
                displayName = record.DisplayName,
                avatarUrl = record.AvatarUrl,
                channel = "Email",
                isVerified = true
            }
        });
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            return Regex.IsMatch(email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
        }
        catch
        {
            return false;
        }
    }

    private async Task SendOtpEmailAsync(string toEmail, string code, string displayName, CancellationToken ct)
    {
        var host = _config["Feedback:Smtp:Host"]!;
        var port = int.TryParse(_config["Feedback:Smtp:Port"], out var p) ? p : 587;
        var enableSsl = bool.TryParse(_config["Feedback:Smtp:EnableSsl"], out var ssl) && ssl;
        var userName = _config["Feedback:Smtp:UserName"];
        var password = _config["Feedback:Smtp:Password"];
        var fromEmail = _config["Feedback:Smtp:FromEmail"] ?? "no-reply@nigeriannewsgrid.com";
        var fromName = _config["Feedback:Smtp:FromName"] ?? "The Newsstand";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl
        };

        if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new System.Net.NetworkCredential(userName, password);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = $"{code} is your The Newsstand verification code",
            Body = $"""
                <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; max-width: 520px; margin: 0 auto; padding: 32px 24px; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; color: #0f172a;">
                    <div style="text-align: center; margin-bottom: 24px;">
                        <h1 style="font-size: 24px; font-weight: 800; color: #dc2626; margin: 0;">The Newsstand</h1>
                        <p style="font-size: 14px; color: #64748b; margin-top: 4px;">Community Discussion Verification</p>
                    </div>
                    <p style="font-size: 16px; line-height: 1.5; color: #334155;">Hello <strong>{displayName}</strong>,</p>
                    <p style="font-size: 15px; line-height: 1.5; color: #334155;">Enter the following 6-digit code to verify your reader profile and participate in story discussions:</p>
                    <div style="text-align: center; margin: 28px 0;">
                        <span style="display: inline-block; padding: 14px 28px; font-size: 32px; font-weight: 800; letter-spacing: 6px; color: #dc2626; background: #fef2f2; border: 1.5px dashed #fca5a5; border-radius: 8px;">
                            {code}
                        </span>
                    </div>
                    <p style="font-size: 13px; color: #64748b; line-height: 1.5;">This code will expire in 10 minutes. If you did not request this verification, you can safely ignore this email.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center; margin: 0;">The Newsstand · Nigeria's Premium Real-Time News Engine</p>
                </div>
                """,
            IsBodyHtml = true
        };

        mail.To.Add(toEmail);
        await client.SendMailAsync(mail, ct);
    }
}
