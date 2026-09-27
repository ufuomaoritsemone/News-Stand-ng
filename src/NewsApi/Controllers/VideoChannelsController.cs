using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/video-channels")]
public class VideoChannelsController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly IYouTubeFeedService _ytService;

    public VideoChannelsController(NewsDbContext db, IYouTubeFeedService ytService)
    {
        _db = db;
        _ytService = ytService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var channels = await _db.VideoChannels
            .AsNoTracking()
            .OrderBy(c => c.ChannelName)
            .Select(c => ToDto(c))
            .ToListAsync();
        return Ok(channels);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var channel = await _db.VideoChannels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return channel is null ? NotFound() : Ok(ToDto(channel));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] VideoChannelInput input)
    {
        if (string.IsNullOrWhiteSpace(input.ChannelName))
            return BadRequest(new { message = "Channel name is required." });

        var channel = new VideoChannel
        {
            Id = Guid.NewGuid().ToString("N"),
            ChannelName = input.ChannelName.Trim(),
            YoutubeChannelId = input.YoutubeChannelId?.Trim() ?? string.Empty,
            ThumbnailUrl = input.ThumbnailUrl?.Trim() ?? string.Empty,
            ChannelUrl = input.ChannelUrl?.Trim() ?? string.Empty,
            Description = input.Description?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _db.VideoChannels.Add(channel);
        await _db.SaveChangesAsync();

        // Auto-sync latest videos from the newly added channel
        try
        {
            await _ytService.SyncChannelAsync(channel);
        }
        catch
        {
            // Non-fatal if sync fails immediately
        }

        var dto = ToDto(channel);
        return CreatedAtAction(nameof(GetById), new { id = channel.Id }, dto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var channel = await _db.VideoChannels.FirstOrDefaultAsync(c => c.Id == id);
        if (channel is not null)
        {
            _db.VideoChannels.Remove(channel);
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    private static VideoChannelDto ToDto(VideoChannel c) => new()
    {
        Id = c.Id,
        ChannelName = c.ChannelName,
        YoutubeChannelId = c.YoutubeChannelId,
        ThumbnailUrl = c.ThumbnailUrl,
        ChannelUrl = c.ChannelUrl,
        Description = c.Description,
        CreatedAt = c.CreatedAt
    };
}

public class VideoChannelInput
{
    [Required(ErrorMessage = "Channel name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Channel name must be between 2 and 150 characters.")]
    public string ChannelName { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "YoutubeChannelId cannot exceed 100 characters.")]
    public string? YoutubeChannelId { get; set; }

    [Url(ErrorMessage = "ThumbnailUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "ThumbnailUrl cannot exceed 500 characters.")]
    public string? ThumbnailUrl { get; set; }

    [Url(ErrorMessage = "ChannelUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "ChannelUrl cannot exceed 500 characters.")]
    public string? ChannelUrl { get; set; }

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }
}
