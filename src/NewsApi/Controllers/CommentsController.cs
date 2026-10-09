using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Controllers;

[ApiController]
public class CommentsController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(NewsDbContext db, ILogger<CommentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the discussion thread for a given article, including nested replies and user vote state.
    /// </summary>
    [HttpGet("api/v1/articles/{articleId}/comments")]
    public async Task<IActionResult> GetArticleComments(
        [FromRoute] string articleId,
        [FromQuery] string? userId = null,
        [FromQuery] string sort = "top",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(articleId))
        {
            return BadRequest(new { error = "Article ID is required." });
        }

        // Fetch published comments for this article
        var comments = await _db.StoryComments
            .AsNoTracking()
            .Where(c => c.ArticleId == articleId && c.Status != "Hidden")
            .ToListAsync(ct);

        // Fetch user's votes if userId is provided
        Dictionary<string, int> userVotes = [];
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var commentIds = comments.Select(c => c.Id).ToList();
            userVotes = await _db.CommentVotes
                .AsNoTracking()
                .Where(v => v.UserId == userId && commentIds.Contains(v.CommentId))
                .ToDictionaryAsync(v => v.CommentId, v => v.VoteType, ct);
        }

        // Map to DTOs
        var commentDtos = comments.Select(c => new CommentDto
        {
            Id = c.Id,
            ArticleId = c.ArticleId,
            ParentCommentId = c.ParentCommentId,
            AuthorName = c.AuthorName,
            AuthorAvatar = c.AuthorAvatar,
            AuthorId = c.AuthorId,
            Content = c.Content,
            Upvotes = c.Upvotes,
            Downvotes = c.Downvotes,
            UserVote = userVotes.TryGetValue(c.Id, out var uv) ? uv : 0,
            IsPinned = c.IsPinned,
            CreatedAt = c.CreatedAt,
            Replies = []
        }).ToList();

        // Build hierarchical tree (Top-level + Nested replies)
        var lookup = commentDtos.ToDictionary(c => c.Id);
        List<CommentDto> rootComments = [];

        foreach (var c in commentDtos)
        {
            if (!string.IsNullOrEmpty(c.ParentCommentId) && lookup.TryGetValue(c.ParentCommentId, out var parent))
            {
                parent.Replies.Add(c);
            }
            else
            {
                rootComments.Add(c);
            }
        }

        // Sort root comments
        rootComments = sort.Equals("newest", StringComparison.OrdinalIgnoreCase)
            ? rootComments.OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.CreatedAt).ToList()
            : rootComments.OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.Upvotes - c.Downvotes).ThenByDescending(c => c.CreatedAt).ToList();

        // Sort replies chronologically
        foreach (var root in rootComments)
        {
            root.Replies = root.Replies.OrderBy(r => r.CreatedAt).ToList();
        }

        return Ok(new
        {
            articleId,
            totalCount = comments.Count,
            comments = rootComments
        });
    }

    /// <summary>
    /// Posts a new comment or reply to an existing comment on an article.
    /// </summary>
    [HttpPost("api/v1/articles/{articleId}/comments")]
    public async Task<IActionResult> PostComment(
        [FromRoute] string articleId,
        [FromBody] CreateCommentRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Invalid comment payload." });
        }

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length < 2)
        {
            return BadRequest(new { error = "Comment cannot be empty." });
        }

        if (content.Length > 1500)
        {
            return BadRequest(new { error = "Comment exceeds maximum limit of 1500 characters." });
        }

        var authorName = string.IsNullOrWhiteSpace(request.AuthorName) ? "Anonymous Reader" : request.AuthorName.Trim();
        if (authorName.Length > 50)
        {
            authorName = authorName[..50];
        }

        // Verify article exists or fetch title
        var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == articleId, ct);
        var articleTitle = article?.Title;

        var comment = new StoryComment
        {
            Id = Guid.NewGuid().ToString(),
            ArticleId = articleId,
            ArticleTitle = articleTitle,
            ParentCommentId = string.IsNullOrWhiteSpace(request.ParentCommentId) ? null : request.ParentCommentId.Trim(),
            AuthorName = authorName,
            AuthorAvatar = request.AuthorAvatar,
            AuthorId = string.IsNullOrWhiteSpace(request.AuthorId) ? Guid.NewGuid().ToString() : request.AuthorId.Trim(),
            Content = content,
            Status = "Published",
            CreatedAt = DateTime.UtcNow
        };

        _db.StoryComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("New comment {CommentId} posted on article {ArticleId} by {Author}", comment.Id, articleId, authorName);

        var dto = new CommentDto
        {
            Id = comment.Id,
            ArticleId = comment.ArticleId,
            ParentCommentId = comment.ParentCommentId,
            AuthorName = comment.AuthorName,
            AuthorAvatar = comment.AuthorAvatar,
            AuthorId = comment.AuthorId,
            Content = comment.Content,
            Upvotes = 0,
            Downvotes = 0,
            UserVote = 0,
            IsPinned = false,
            CreatedAt = comment.CreatedAt,
            Replies = []
        };

        return Created($"/api/v1/articles/{articleId}/comments/{comment.Id}", dto);
    }

    /// <summary>
    /// Upvotes or downvotes a comment (+1, -1, or 0 to clear).
    /// </summary>
    [HttpPost("api/v1/comments/{id}/vote")]
    public async Task<IActionResult> VoteComment(
        [FromRoute] string id,
        [FromBody] VoteCommentRequest request,
        CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new { error = "User ID is required to cast a vote." });
        }

        var comment = await _db.StoryComments.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (comment == null || comment.Status == "Hidden")
        {
            return NotFound(new { error = "Comment not found." });
        }

        var existingVote = await _db.CommentVotes
            .FirstOrDefaultAsync(v => v.CommentId == id && v.UserId == request.UserId, ct);

        int effectiveUserVote = 0;

        if (existingVote != null)
        {
            if (existingVote.VoteType == request.VoteType || request.VoteType == 0)
            {
                // Toggled off / removed
                _db.CommentVotes.Remove(existingVote);
                effectiveUserVote = 0;
            }
            else
            {
                // Switched vote
                existingVote.VoteType = request.VoteType;
                existingVote.CreatedAt = DateTime.UtcNow;
                effectiveUserVote = request.VoteType;
            }
        }
        else if (request.VoteType != 0)
        {
            // New vote
            _db.CommentVotes.Add(new CommentVote
            {
                Id = Guid.NewGuid().ToString(),
                CommentId = id,
                UserId = request.UserId,
                VoteType = request.VoteType > 0 ? 1 : -1,
                CreatedAt = DateTime.UtcNow
            });
            effectiveUserVote = request.VoteType > 0 ? 1 : -1;
        }

        await _db.SaveChangesAsync(ct);

        // Recalculate tally
        var upvotes = await _db.CommentVotes.CountAsync(v => v.CommentId == id && v.VoteType > 0, ct);
        var downvotes = await _db.CommentVotes.CountAsync(v => v.CommentId == id && v.VoteType < 0, ct);

        comment.Upvotes = upvotes;
        comment.Downvotes = downvotes;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            commentId = id,
            upvotes,
            downvotes,
            userVote = effectiveUserVote
        });
    }

    /// <summary>
    /// Reports an offensive or spam comment for moderation review.
    /// </summary>
    [HttpPost("api/v1/comments/{id}/report")]
    public async Task<IActionResult> ReportComment(
        [FromRoute] string id,
        [FromBody] ReportCommentRequest request,
        CancellationToken ct = default)
    {
        var comment = await _db.StoryComments.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (comment == null)
        {
            return NotFound(new { error = "Comment not found." });
        }

        comment.ReportCount++;
        if (comment.ReportCount >= 3 && comment.Status == "Published")
        {
            comment.Status = "Flagged";
        }

        _db.CommentReports.Add(new CommentReport
        {
            Id = Guid.NewGuid().ToString(),
            CommentId = id,
            ReportedBy = request?.ReportedBy ?? "Anonymous",
            Reason = request?.Reason ?? "Inappropriate content",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Thank you. Your report has been submitted to moderators." });
    }

    /// <summary>
    /// Returns the hottest debated stories for the Community Forum view, ranked by active discussion count.
    /// </summary>
    [HttpGet("api/v1/forum/trending")]
    public async Task<IActionResult> GetTrendingDiscussions(
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        // Group comments by ArticleId and count discussions
        var groupedStats = await _db.StoryComments
            .AsNoTracking()
            .Where(c => c.Status != "Hidden")
            .GroupBy(c => c.ArticleId)
            .Select(g => new
            {
                ArticleId = g.Key,
                CommentCount = g.Count(),
                LatestCommentAt = g.Max(c => c.CreatedAt)
            })
            .OrderByDescending(x => x.CommentCount)
            .ThenByDescending(x => x.LatestCommentAt)
            .Take(limit)
            .ToListAsync(ct);

        var articleIds = groupedStats.Select(s => s.ArticleId).ToList();

        var articles = await _db.Articles
            .AsNoTracking()
            .Where(a => articleIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        // Fetch latest comment snippet for each
        var latestComments = await _db.StoryComments
            .AsNoTracking()
            .Where(c => articleIds.Contains(c.ArticleId) && c.Status != "Hidden")
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        var latestCommentByArticle = latestComments
            .GroupBy(c => c.ArticleId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<ForumTrendingStoryDto>();

        foreach (var stat in groupedStats)
        {
            if (!articles.TryGetValue(stat.ArticleId, out var art)) continue;

            latestCommentByArticle.TryGetValue(stat.ArticleId, out var latestComment);

            result.Add(new ForumTrendingStoryDto
            {
                ArticleId = art.Id,
                Title = art.Title,
                Category = art.Category ?? "General",
                Source = art.Source ?? "Newsstand",
                ImageUrl = art.ImageUrl,
                PublishedAt = art.PublishedAt,
                CommentCount = stat.CommentCount,
                LatestCommentAuthor = latestComment?.AuthorName,
                LatestCommentSnippet = latestComment?.Content != null && latestComment.Content.Length > 100
                    ? $"{latestComment.Content[..97]}..."
                    : latestComment?.Content,
                LatestCommentAt = latestComment?.CreatedAt
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Administrative endpoint: lists flagged comments requiring review.
    /// </summary>
    [HttpGet("api/v1/comments/flagged")]
    public async Task<IActionResult> GetFlaggedComments(CancellationToken ct = default)
    {
        var flagged = await _db.StoryComments
            .AsNoTracking()
            .Where(c => c.Status == "Flagged" || c.ReportCount > 0)
            .OrderByDescending(c => c.ReportCount)
            .ThenByDescending(c => c.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return Ok(flagged);
    }

    /// <summary>
    /// Administrative endpoint: update comment status (Publish, Flag, Hide) or Pin comment.
    /// </summary>
    [HttpDelete("api/v1/comments/{id}")]
    public async Task<IActionResult> DeleteComment(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        var comment = await _db.StoryComments.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (comment == null)
        {
            return NotFound(new { error = "Comment not found." });
        }

        comment.Status = "Hidden";
        comment.Content = "[This comment was removed by a moderator]";
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Comment hidden successfully." });
    }
}
