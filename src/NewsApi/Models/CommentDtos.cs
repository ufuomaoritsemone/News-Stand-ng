namespace NewsApi.Models;

public record CreateCommentRequest(
    string AuthorName,
    string Content,
    string? ParentCommentId = null,
    string? AuthorId = null,
    string? AuthorAvatar = null
);

public record VoteCommentRequest(
    string UserId,
    int VoteType // +1, -1, or 0
);

public record ReportCommentRequest(
    string? ReportedBy,
    string Reason
);

public class CommentDto
{
    public string Id { get; set; } = string.Empty;
    public string ArticleId { get; set; } = string.Empty;
    public string? ParentCommentId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public int UserVote { get; set; } // +1, -1, or 0
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<CommentDto> Replies { get; set; } = [];
}

public class ForumTrendingStoryDto
{
    public string ArticleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int CommentCount { get; set; }
    public string? LatestCommentAuthor { get; set; }
    public string? LatestCommentSnippet { get; set; }
    public DateTime? LatestCommentAt { get; set; }
}
