namespace NewsApi.Models;

public class StoryComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ArticleId { get; set; } = string.Empty;
    public string? ArticleTitle { get; set; }
    public string? ParentCommentId { get; set; }
    public string AuthorName { get; set; } = "Anonymous Reader";
    public string? AuthorAvatar { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Upvotes { get; set; } = 0;
    public int Downvotes { get; set; } = 0;
    public int ReportCount { get; set; } = 0;
    public bool IsPinned { get; set; } = false;
    public string Status { get; set; } = "Published"; // "Published", "Flagged", "Hidden"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class CommentVote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CommentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int VoteType { get; set; } = 1; // +1 or -1
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CommentReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CommentId { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = "Spam";
    public bool IsResolved { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
