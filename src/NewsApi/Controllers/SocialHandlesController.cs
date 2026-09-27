using System.ComponentModel.DataAnnotations;
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
    private readonly ISocialFeedService _socialService;

    public SocialHandlesController(NewsDbContext db, ISocialFeedService socialService)
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
            .Select(h => ToDto(h))
            .ToListAsync();
        return Ok(handles);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var handle = await _db.SocialHandles.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
        return handle is null ? NotFound() : Ok(ToDto(handle));
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

        var dto = ToDto(handle);
        return CreatedAtAction(nameof(GetById), new { id = handle.Id }, dto);
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

    private static SocialHandleDto ToDto(SocialHandle h) => new()
    {
        Id = h.Id,
        Handle = h.Handle,
        DisplayName = h.DisplayName,
        ProfileUrl = h.ProfileUrl,
        AvatarUrl = h.AvatarUrl,
        Bio = h.Bio,
        Category = h.Category,
        CreatedAt = h.CreatedAt
    };
}

public class SocialHandleInput
{
    [Required(ErrorMessage = "Twitter/X handle is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Handle must be between 1 and 100 characters.")]
    public string Handle { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "DisplayName cannot exceed 150 characters.")]
    public string? DisplayName { get; set; }

    [Url(ErrorMessage = "ProfileUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "ProfileUrl cannot exceed 500 characters.")]
    public string? ProfileUrl { get; set; }

    [Url(ErrorMessage = "AvatarUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "AvatarUrl cannot exceed 500 characters.")]
    public string? AvatarUrl { get; set; }

    [StringLength(500, ErrorMessage = "Bio cannot exceed 500 characters.")]
    public string? Bio { get; set; }

    [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
    public string? Category { get; set; }
}
