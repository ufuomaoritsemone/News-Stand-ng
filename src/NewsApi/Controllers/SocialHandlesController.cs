using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/social-handles")]
public class SocialHandlesController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly SocialFeedService _socialService;

    public SocialHandlesController(NewsDbContext db, SocialFeedService socialService)
    {
        _db = db;
        _socialService = socialService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var handles = await _db.SocialHandles
            .AsNoTracking()
            .OrderBy(h => h.Category)
            .ThenBy(h => h.DisplayName)
            .ToListAsync();
        return Ok(handles);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var handle = await _db.SocialHandles.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
        return handle is null ? NotFound() : Ok(handle);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] SocialHandleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Handle))
            return BadRequest(new { message = "Twitter/X handle is required." });

        var handle = new SocialHandle
        {
            Id = Guid.NewGuid().ToString("N"),
            Handle = input.Handle.Trim(),
            DisplayName = input.DisplayName?.Trim() ?? input.Handle.Trim(),
            ProfileUrl = input.ProfileUrl?.Trim() ?? $"https://x.com/{input.Handle.TrimStart('@')}",
            AvatarUrl = input.AvatarUrl?.Trim() ?? string.Empty,
            Bio = input.Bio?.Trim() ?? string.Empty,
            Category = input.Category?.Trim() ?? "Media",
            CreatedAt = DateTime.UtcNow
        };

        _db.SocialHandles.Add(handle);
        await _db.SaveChangesAsync();

        // Auto-sync posts for the new handle
        try
        {
            await _socialService.SyncHandlePostsAsync(handle);
        }
        catch
        {
            // Non-fatal if sync fails immediately
        }

        return CreatedAtAction(nameof(GetById), new { id = handle.Id }, handle);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var handle = await _db.SocialHandles.FirstOrDefaultAsync(h => h.Id == id);
        if (handle is not null)
        {
            _db.SocialHandles.Remove(handle);
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }
}

public class SocialHandleInput
{
    public string Handle { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ProfileUrl { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Category { get; set; }
}
