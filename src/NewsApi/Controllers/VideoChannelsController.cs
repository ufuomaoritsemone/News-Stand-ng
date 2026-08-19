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
    private readonly YouTubeFeedService _ytService;

    public VideoChannelsController(NewsDbContext db, YouTubeFeedService ytService)
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
            .ToListAsync();
        return Ok(channels);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var channel = await _db.VideoChannels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return channel is null ? NotFound() : Ok(channel);
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

        return CreatedAtAction(nameof(GetById), new { id = channel.Id }, channel);
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
}

public class VideoChannelInput
{
    public string ChannelName { get; set; } = string.Empty;
    public string? YoutubeChannelId { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ChannelUrl { get; set; }
    public string? Description { get; set; }
}
