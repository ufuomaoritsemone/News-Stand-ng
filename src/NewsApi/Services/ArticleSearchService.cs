using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using Npgsql;

namespace NewsApi.Services;

/// <summary>
/// High-performance full-text search service utilizing:
/// - SQLite: FTS5 virtual table with BM25 relevance ranking (weighting Title > Summary > Content).
/// - PostgreSQL: tsvector generated column with GIN index and ts_rank.
/// - Fallback: Graceful LINQ-based filtering if FTS infrastructure is unavailable.
/// </summary>
public class ArticleSearchService : IArticleSearchService
{
    private readonly NewsDbContext _db;
    private readonly ILogger<ArticleSearchService> _logger;

    public ArticleSearchService(NewsDbContext db, ILogger<ArticleSearchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ArticleDto>> SearchArticlesAsync(
        ArticleSearchParams searchParams,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchParams.Search))
        {
            return [];
        }

        var isSqlite = _db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        var isPostgres = _db.Database.ProviderName?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;

        List<Article> rawArticles;
        try
        {
            if (isSqlite)
            {
                rawArticles = await ExecuteSqliteFtsSearchAsync(searchParams, cancellationToken);
            }
            else if (isPostgres)
            {
                rawArticles = await ExecutePostgresTsSearchAsync(searchParams, cancellationToken);
            }
            else
            {
                rawArticles = await ExecuteFallbackSearchAsync(searchParams, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Full-Text Search failed for query '{Query}'. Executing resilient fallback search.", searchParams.Search);
            rawArticles = await ExecuteFallbackSearchAsync(searchParams, cancellationToken);
        }

        return await MapToArticleDtosAsync(rawArticles, cancellationToken);
    }

    /// <summary>
    /// Executes SQLite FTS5 search with BM25 ranking (Title weight=5.0, Summary weight=2.0, Content weight=1.0).
    /// </summary>
    private async Task<List<Article>> ExecuteSqliteFtsSearchAsync(
        ArticleSearchParams searchParams,
        CancellationToken cancellationToken)
    {
        var ftsQuery = FormatFts5Query(searchParams.Search);
        if (string.IsNullOrWhiteSpace(ftsQuery))
        {
            return await ExecuteFallbackSearchAsync(searchParams, cancellationToken);
        }

        var pageIndex = Math.Max(1, searchParams.Page);
        var size = Math.Clamp(searchParams.PageSize, 1, 200);
        var offset = (pageIndex - 1) * size;

        string? categoryFilter = null;
        if (!string.IsNullOrWhiteSpace(searchParams.Category) && !searchParams.Category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            categoryFilter = searchParams.Category.Trim().ToLowerInvariant();
        }

        string? sourceFilter = null;
        if (!string.IsNullOrWhiteSpace(searchParams.Source) && !searchParams.Source.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            sourceFilter = searchParams.Source.Trim().ToLowerInvariant();
        }

        string? contentTypeFilter = null;
        if (!string.IsNullOrWhiteSpace(searchParams.ContentType))
        {
            contentTypeFilter = searchParams.ContentType.Trim().ToLowerInvariant();
        }

        string? cutoffFilter = null;
        if (searchParams.TodayOnly)
        {
            cutoffFilter = DateTime.UtcNow.Date.ToString("yyyy-MM-dd HH:mm:ss");
        }
        else if (searchParams.Days.HasValue && searchParams.Days.Value > 0)
        {
            cutoffFilter = DateTime.UtcNow.AddDays(-searchParams.Days.Value).ToString("yyyy-MM-dd HH:mm:ss");
        }

        var sql = """
            SELECT a."Id", a."Title", a."Summary", a."Content", a."Url", a."ImageUrl", a."Source",
                   a."Category", a."PublishedAt", a."AudioUrl", a."Author", a."ContentType", a."IsSponsored", a."SponsorName",
                   a."SponsorUrl", a."CampaignExpiresAt", a."IsPinned", a."TargetPosition", a."PriorityWeight", a."ImpressionCount", a."ClickCount",
                   a."UpdatedAt"
            FROM "Articles" a
            INNER JOIN "Articles_fts" fts ON a."Id" = fts."Id"
            WHERE "Articles_fts" MATCH @ftsQuery
              AND (@category IS NULL OR lower(a."Category") = @category)
              AND (@source IS NULL OR lower(a."Source") LIKE '%' || @source || '%')
              AND (@contentType IS NULL OR lower(a."ContentType") = @contentType)
              AND (@cutoff IS NULL OR a."PublishedAt" >= @cutoff)
            ORDER BY bm25("Articles_fts", 0.0, 10.0, 5.0, 1.0)
            LIMIT @limit OFFSET @offset;
            """;

        var parameters = new object[]
        {
            new SqliteParameter("@ftsQuery", ftsQuery),
            new SqliteParameter("@category", (object?)categoryFilter ?? DBNull.Value),
            new SqliteParameter("@source", (object?)sourceFilter ?? DBNull.Value),
            new SqliteParameter("@contentType", (object?)contentTypeFilter ?? DBNull.Value),
            new SqliteParameter("@cutoff", (object?)cutoffFilter ?? DBNull.Value),
            new SqliteParameter("@limit", size),
            new SqliteParameter("@offset", offset)
        };

        return await _db.Articles
            .FromSqlRaw(sql, parameters)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Executes PostgreSQL full-text search with tsvector and ts_rank.
    /// </summary>
    private async Task<List<Article>> ExecutePostgresTsSearchAsync(
        ArticleSearchParams searchParams,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, searchParams.Page);
        var size = Math.Clamp(searchParams.PageSize, 1, 200);
        var offset = (pageIndex - 1) * size;

        string? categoryFilter = null;
        if (!string.IsNullOrWhiteSpace(searchParams.Category) && !searchParams.Category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            categoryFilter = searchParams.Category.Trim().ToLowerInvariant();
        }

        string? sourceFilter = null;
        if (!string.IsNullOrWhiteSpace(searchParams.Source) && !searchParams.Source.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            sourceFilter = searchParams.Source.Trim().ToLowerInvariant();
        }

        string? contentTypeFilter = null;
        if (!string.IsNullOrWhiteSpace(searchParams.ContentType))
        {
            contentTypeFilter = searchParams.ContentType.Trim().ToLowerInvariant();
        }

        DateTime? cutoffFilter = null;
        if (searchParams.TodayOnly)
        {
            cutoffFilter = DateTime.UtcNow.Date;
        }
        else if (searchParams.Days.HasValue && searchParams.Days.Value > 0)
        {
            cutoffFilter = DateTime.UtcNow.AddDays(-searchParams.Days.Value);
        }

        var sql = """
            SELECT a."Id", a."Title", a."Summary", a."Content", a."Url", a."ImageUrl", a."Source",
                   a."Category", a."PublishedAt", a."AudioUrl", a."Author", a."ContentType", a."IsSponsored", a."SponsorName",
                   a."SponsorUrl", a."CampaignExpiresAt", a."IsPinned", a."TargetPosition", a."PriorityWeight", a."ImpressionCount", a."ClickCount",
                   a."UpdatedAt"
            FROM "Articles" a
            WHERE a."SearchVector" @@ websearch_to_tsquery('english', @query)
              AND (@category IS NULL OR lower(a."Category") = @category)
              AND (@source IS NULL OR lower(a."Source") LIKE '%' || @source || '%')
              AND (@contentType IS NULL OR lower(a."ContentType") = @contentType)
              AND (@cutoff IS NULL OR a."PublishedAt" >= @cutoff)
            ORDER BY ts_rank(a."SearchVector", websearch_to_tsquery('english', @query)) DESC,
                     a."PublishedAt" DESC
            LIMIT @limit OFFSET @offset;
            """;

        var parameters = new object[]
        {
            new NpgsqlParameter("@query", searchParams.Search.Trim()),
            new NpgsqlParameter("@category", (object?)categoryFilter ?? DBNull.Value),
            new NpgsqlParameter("@source", (object?)sourceFilter ?? DBNull.Value),
            new NpgsqlParameter("@contentType", (object?)contentTypeFilter ?? DBNull.Value),
            new NpgsqlParameter("@cutoff", (object?)cutoffFilter ?? DBNull.Value),
            new NpgsqlParameter("@limit", size),
            new NpgsqlParameter("@offset", offset)
        };

        return await _db.Articles
            .FromSqlRaw(sql, parameters)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Resilient fallback search using indexed columns and LINQ expressions.
    /// </summary>
    private async Task<List<Article>> ExecuteFallbackSearchAsync(
        ArticleSearchParams searchParams,
        CancellationToken cancellationToken)
    {
        var query = _db.Articles.AsNoTracking();

        if (searchParams.TodayOnly)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(a => a.PublishedAt != null && a.PublishedAt >= today);
        }
        else if (searchParams.Days.HasValue && searchParams.Days.Value > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-searchParams.Days.Value);
            query = query.Where(a => a.PublishedAt != null && a.PublishedAt >= cutoff);
        }

        if (!string.IsNullOrWhiteSpace(searchParams.Source) && !searchParams.Source.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var s = searchParams.Source.Trim().ToLowerInvariant();
            query = query.Where(a => a.Source != null && a.Source.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(searchParams.Category) && !searchParams.Category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var c = searchParams.Category.Trim().ToLowerInvariant();
            query = query.Where(a => a.Category != null && a.Category.ToLower() == c);
        }

        if (!string.IsNullOrWhiteSpace(searchParams.ContentType))
        {
            var ct = searchParams.ContentType.Trim().ToLowerInvariant();
            query = query.Where(a => a.ContentType != null && a.ContentType.ToLower() == ct);
        }

        var term = searchParams.Search.Trim().ToLowerInvariant();
        query = query.Where(a =>
            a.Title.ToLower().Contains(term) ||
            (a.Summary != null && a.Summary.ToLower().Contains(term)) ||
            (a.Content != null && a.Content.ToLower().Contains(term)));

        var pageIndex = Math.Max(1, searchParams.Page);
        var size = Math.Clamp(searchParams.PageSize, 1, 200);

        return await query
            .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
            .Skip((pageIndex - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Formats user search input for SQLite FTS5 query with prefix matching.
    /// Example: "Tinubu economy" -> "\"Tinubu\"* \"economy\"*"
    /// </summary>
    public static string FormatFts5Query(string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return string.Empty;

        // Match alphanumeric and accented word characters
        var tokens = Regex.Matches(search, @"[\w\u00C0-\u017F]+")
            .Select(m => m.Value.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        if (tokens.Count == 0) return string.Empty;

        // Quote each token and append wildcard '*' for prefix/typeahead search
        var formatted = tokens.Select(t => $"\"{t.Replace("\"", "")}\"*");
        return string.Join(" ", formatted);
    }

    private async Task<List<ArticleDto>> MapToArticleDtosAsync(
        List<Article> rawArticles,
        CancellationToken cancellationToken)
    {
        var pageIds = rawArticles.Select(a => a.Id).ToList();
        var audioMap = pageIds.Count > 0
            ? (await _db.AudioAssets.AsNoTracking()
                .Where(x => x.ArticleId != null && pageIds.Contains(x.ArticleId))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.ArticleId!)
                .ToDictionary(g => g.Key, g => g.First().Url)
            : new Dictionary<string, string>();

        return rawArticles.Select(a => new ArticleDto
        {
            Id = a.Id,
            Title = a.Title,
            Summary = a.Summary,
            Content = a.Content,
            Url = a.Url,
            ImageUrl = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
            Source = string.IsNullOrWhiteSpace(a.Source) ? "General News" : a.Source,
            Category = string.IsNullOrWhiteSpace(a.Category) ? "General" : a.Category,
            PublishedAt = a.PublishedAt ?? DateTime.UtcNow,
            AudioUrl = audioMap.GetValueOrDefault(a.Id) ?? a.AudioUrl,
            Author = a.Author,
            ContentType = string.IsNullOrWhiteSpace(a.ContentType) ? "News" : a.ContentType,
            IsSponsored = a.IsSponsored,
            SponsorName = a.SponsorName,
            SponsorUrl = a.SponsorUrl,
            CampaignExpiresAt = a.CampaignExpiresAt,
            IsPinned = a.IsPinned,
            TargetPosition = a.TargetPosition,
            PriorityWeight = a.PriorityWeight,
            ImpressionCount = a.ImpressionCount,
            ClickCount = a.ClickCount
        }).ToList();
    }
}
