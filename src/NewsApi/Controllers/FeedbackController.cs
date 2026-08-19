using Microsoft.AspNetCore.Mvc;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(IFeedbackService feedbackService, ILogger<FeedbackController> logger)
    {
        _feedbackService = feedbackService;
        _logger = logger;
    }

    /// <summary>
    /// Submits user experience feedback and triggers email dispatch.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new FeedbackResponseDto(false, "Feedback request cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length < 3)
        {
            return BadRequest(new FeedbackResponseDto(false, "Please provide a feedback message with at least 3 characters."));
        }

        var result = await _feedbackService.SubmitFeedbackAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns recent user feedbacks (administrative/audit view).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecentFeedbacks([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var list = await _feedbackService.GetRecentFeedbacksAsync(limit, cancellationToken);
        return Ok(list);
    }
}