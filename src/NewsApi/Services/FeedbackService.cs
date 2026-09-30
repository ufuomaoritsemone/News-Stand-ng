using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Services;

public interface IFeedbackService
{
    Task<FeedbackResponseDto> SubmitFeedbackAsync(FeedbackRequestDto request, CancellationToken cancellationToken = default);
    Task<List<FeedbackItem>> GetRecentFeedbacksAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<bool> DeleteFeedbackAsync(string id, CancellationToken cancellationToken = default);
}

public class FeedbackService : IFeedbackService
{
    private readonly NewsDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<FeedbackService> _logger;

    public FeedbackService(NewsDbContext db, IConfiguration config, ILogger<FeedbackService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<FeedbackResponseDto> SubmitFeedbackAsync(FeedbackRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var feedback = new FeedbackItem
        {
            Rating = Math.Clamp(request.Rating, 1, 5),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            Message = request.Message.Trim(),
            UserEmail = string.IsNullOrWhiteSpace(request.UserEmail) ? null : request.UserEmail.Trim(),
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? null : request.UserName.Trim(),
            AppVersion = string.IsNullOrWhiteSpace(request.AppVersion) ? null : request.AppVersion.Trim(),
            Platform = string.IsNullOrWhiteSpace(request.Platform) ? null : request.Platform.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsEmailSent = false
        };

        // Attempt sending email notification; set flag before the single save
        var emailSent = await TrySendFeedbackEmailAsync(feedback, cancellationToken);
        feedback.IsEmailSent = emailSent;

        _db.Feedbacks.Add(feedback);
        await _db.SaveChangesAsync(cancellationToken); // Fix #12 — single DB round-trip

        _logger.LogInformation("Feedback {FeedbackId} recorded successfully (Rating: {Rating}, EmailSent: {EmailSent})",
            feedback.Id, feedback.Rating, feedback.IsEmailSent);

        return new FeedbackResponseDto(
            Success: true,
            Message: "Thank you for your feedback! Your experience helps us improve News Stand NG.",
            FeedbackId: feedback.Id
        );
    }

    public async Task<List<FeedbackItem>> GetRecentFeedbacksAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 200);

        // SQLite does not support native DateTimeOffset in ORDER BY clauses in EF Core
        if (_db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var items = await _db.Feedbacks
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return items
                .OrderByDescending(f => f.CreatedAt)
                .Take(effectiveLimit)
                .ToList();
        }

        return await _db.Feedbacks
            .AsNoTracking()
            .OrderByDescending(f => f.CreatedAt)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteFeedbackAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        var item = await _db.Feedbacks.FindAsync(new object[] { id }, cancellationToken);
        if (item is null) return false;

        _db.Feedbacks.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Feedback {FeedbackId} deleted by administrator.", id);
        return true;
    }

    private async Task<bool> TrySendFeedbackEmailAsync(FeedbackItem feedback, CancellationToken cancellationToken)
    {
        var recipientEmail = _config["Feedback:RecipientEmail"] ?? "feedback@nigeriannewsgrid.com";
        var smtpHost = _config["Feedback:Smtp:Host"];

        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation("[FeedbackService] SMTP host is not configured. Feedback recorded to DB and recipient is set to {RecipientEmail}.", recipientEmail);
            return false;
        }

        try
        {
            var port = int.TryParse(_config["Feedback:Smtp:Port"], out var p) ? p : 587;
            var enableSsl = !bool.TryParse(_config["Feedback:Smtp:EnableSsl"], out var ssl) || ssl;
            var username = _config["Feedback:Smtp:UserName"];
            var password = _config["Feedback:Smtp:Password"];
            var fromEmail = _config["Feedback:Smtp:FromEmail"] ?? "no-reply@nigeriannewsgrid.com";
            var fromName = _config["Feedback:Smtp:FromName"] ?? "News Stand NG";

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = $"[User Feedback - {feedback.Rating}⭐] {feedback.Category} - News Stand NG",
                IsBodyHtml = true,
                Body = BuildHtmlEmailBody(feedback)
            };

            message.To.Add(new MailAddress(recipientEmail));

            if (!string.IsNullOrWhiteSpace(feedback.UserEmail))
            {
                try
                {
                    message.ReplyToList.Add(new MailAddress(feedback.UserEmail, feedback.UserName ?? feedback.UserEmail));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set ReplyTo email: {UserEmail}", feedback.UserEmail);
                }
            }

            using var smtp = new SmtpClient(smtpHost, port)
            {
                EnableSsl = enableSsl
            };

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                smtp.Credentials = new NetworkCredential(username, password);
            }

            await smtp.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("[FeedbackService] Feedback email successfully sent to {RecipientEmail} for feedback {FeedbackId}", recipientEmail, feedback.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FeedbackService] Failed to send feedback email for feedback {FeedbackId}", feedback.Id);
            return false;
        }
    }

    private static string BuildHtmlEmailBody(FeedbackItem feedback)
    {
        var stars = new string('⭐', feedback.Rating);
        var submitter = string.IsNullOrWhiteSpace(feedback.UserName) ? "Anonymous User" : feedback.UserName;
        var submitterEmail = string.IsNullOrWhiteSpace(feedback.UserEmail) ? "Not provided" : feedback.UserEmail;
        var appVer = string.IsNullOrWhiteSpace(feedback.AppVersion) ? "Unknown" : feedback.AppVersion;
        var platform = string.IsNullOrWhiteSpace(feedback.Platform) ? "Unknown" : feedback.Platform;
        var dateFormatted = feedback.CreatedAt.ToString("MMMM dd, yyyy HH:mm:ss 'UTC'");

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f6faf7; margin: 0; padding: 24px; color: #1a1d20; }}
        .card {{ background-color: #ffffff; border-radius: 12px; border: 1px solid #e4ece6; max-width: 600px; margin: 0 auto; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.05); }}
        .header {{ background-color: #1B6B3A; color: #ffffff; padding: 20px 24px; }}
        .header h1 {{ margin: 0; font-size: 20px; font-weight: 700; }}
        .content {{ padding: 24px; }}
        .badge {{ display: inline-block; padding: 4px 10px; border-radius: 20px; font-size: 12px; font-weight: 600; background-color: #ebf3ff; color: #0066ff; margin-bottom: 16px; }}
        .rating {{ font-size: 24px; margin: 8px 0 16px 0; }}
        .message-box {{ background-color: #f8f9fa; border-left: 4px solid #1B6B3A; padding: 16px; border-radius: 4px; font-size: 15px; line-height: 1.6; white-space: pre-wrap; margin: 16px 0; }}
        .meta-table {{ width: 100%; border-collapse: collapse; font-size: 13px; margin-top: 20px; color: #6c757d; }}
        .meta-table td {{ padding: 6px 0; border-top: 1px solid #f0f0f0; }}
        .meta-table td.label {{ font-weight: 600; width: 130px; }}
        .footer {{ padding: 16px 24px; background-color: #f8f9fa; font-size: 12px; color: #adb5bd; text-align: center; border-top: 1px solid #e4ece6; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>
            <h1>New User Feedback Submission</h1>
        </div>
        <div class='content'>
            <span class='badge'>{WebUtility.HtmlEncode(feedback.Category)}</span>
            <div class='rating'>{stars} ({feedback.Rating}/5)</div>
            
            <h3 style='margin: 16px 0 8px 0; font-size: 14px; text-transform: uppercase; letter-spacing: 0.5px; color: #6c757d;'>Feedback Comments:</h3>
            <div class='message-box'>{WebUtility.HtmlEncode(feedback.Message)}</div>

            <table class='meta-table'>
                <tr>
                    <td class='label'>Submitted By:</td>
                    <td>{WebUtility.HtmlEncode(submitter)}</td>
                </tr>
                <tr>
                    <td class='label'>Email:</td>
                    <td>{WebUtility.HtmlEncode(submitterEmail)}</td>
                </tr>
                <tr>
                    <td class='label'>Platform:</td>
                    <td>{WebUtility.HtmlEncode(platform)}</td>
                </tr>
                <tr>
                    <td class='label'>App Version:</td>
                    <td>{WebUtility.HtmlEncode(appVer)}</td>
                </tr>
                <tr>
                    <td class='label'>Submitted At:</td>
                    <td>{dateFormatted}</td>
                </tr>
                <tr>
                    <td class='label'>Feedback ID:</td>
                    <td><code>{feedback.Id}</code></td>
                </tr>
            </table>
        </div>
        <div class='footer'>
            News Stand NG &bull; Automated User Experience Feedback Notification
        </div>
    </div>
</body>
</html>
";
    }
}