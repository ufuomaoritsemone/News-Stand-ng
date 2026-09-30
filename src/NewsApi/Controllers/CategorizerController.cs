using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/categorizer")]
public class CategorizerController : ControllerBase
{
    private readonly ICategorizerTrainingService _trainingService;
    private readonly NewsDbContext _db;

    public CategorizerController(
        ICategorizerTrainingService trainingService,
        NewsDbContext db)
    {
        _trainingService = trainingService;
        _db              = db;
    }

    /// <summary>
    /// Returns the operational health, accuracy metrics, and correction stats of the ML Categorizer.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = _trainingService.GetStatus();
        return Ok(status);
    }

    /// <summary>
    /// Returns the historical human category corrections recorded from the Admin Dashboard.
    /// </summary>
    [HttpGet("corrections")]
    public async Task<IActionResult> GetCorrections(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var clampedLimit = Math.Clamp(limit, 1, 500);
        var corrections = await _db.CategoryCorrections
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(clampedLimit)
            .Select(c => new CategorizerCorrectionDto(
                c.Id,
                c.ArticleId,
                c.Title,
                c.Summary,
                c.OldCategory,
                c.NewCategory,
                c.Source,
                c.Url,
                c.CreatedAt
            ))
            .ToListAsync(ct);

        return Ok(new
        {
            total = await _db.CategoryCorrections.CountAsync(ct),
            items = corrections
        });
    }

    /// <summary>
    /// Triggers an immediate re-training of the ML.NET multiclass classification model
    /// utilizing the pristine seed dataset merged with 5x reinforced human corrections.
    /// </summary>
    [HttpPost("retrain")]
    public async Task<IActionResult> RetrainModel(CancellationToken ct = default)
    {
        var result = await _trainingService.TrainAsync(ct);
        if (!result.Success && result.Message.Contains("already"))
        {
            return Conflict(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Evaluates sample news text against the active ML model to test prediction accuracy and confidence scores.
    /// </summary>
    [HttpPost("predict")]
    public IActionResult PredictSample([FromBody] PredictSampleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Title))
        {
            return BadRequest(new { message = "Title is required for category prediction." });
        }

        var prediction = _trainingService.Predict(request.Title, request.Summary);
        return Ok(prediction);
    }

    /// <summary>
    /// Streams the latest compiled categorizer_model.zip binary for distribution to worker nodes.
    /// </summary>
    [HttpGet("model")]
    public IActionResult DownloadModel()
    {
        var bytes = _trainingService.GetModelBytes();
        if (bytes == null || bytes.Length == 0)
        {
            return NotFound(new { message = "No trained model binary exists on the server." });
        }

        return File(bytes, "application/octet-stream", "categorizer_model.zip");
    }
}

public class PredictSampleRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
}
