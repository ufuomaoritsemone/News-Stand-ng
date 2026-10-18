using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NigerianNewsGrid.Client.Models;

namespace NewsApi.Services;

/// <summary>
/// Default implementation of <see cref="IArticleIngestionService"/>.
/// Encapsulates batch deduplication, topic entity resolution, IndexNow enqueueing,
/// ML.NET category feedback persistence, sponsored campaign lifecycle, and DTO mapping helpers.
/// </summary>
public class ArticleIngestionService : IArticleIngestionService
{
    private readonly NewsDbContext _db;
    private readonly ISlugService _slugService;
    private readonly IBriefingService _briefingService;
    private readonly ILogger _logger;
    private readonly ICategorizerTrainingService? _categorizerTrainingService;
    private readonly IIndexNowService? _indexNowService;

    public ArticleIngestionService(
        NewsDbContext db,
        ISlugService slugService,
        IBriefingService briefingService,
        ILogger<ArticleIngestionService> logger,
        ICategorizerTrainingService? categorizerTrainingService = null,
        IIndexNowService? indexNowService = null)
        : this(db, slugService, briefingService, (ILogger)logger, categorizerTrainingService, indexNowService)
    {
    }

    public ArticleIngestionService(
        NewsDbContext db,
        ISlugService slugService,
        IBriefingService briefingService,
        ILogger logger,
        ICategorizerTrainingService? categorizerTrainingService = null,
        IIndexNowService? indexNowService = null)
    {
        _db = db;
        _slugService = slugService;
        _briefingService = briefingService;
        _logger = logger;
        _categorizerTrainingService = categorizerTrainingService;
        _indexNowService = indexNowService;
    }

    public async Task<IngestArticlesResponse> IngestArticlesAsync(
        IngestArticlesRequest request,
        CancellationToken cancellationToken = default)
    {
        var incomingList = request.Articles
            .Where(i => !string.IsNullOrWhiteSpace(i.Title))
            .ToList();

        if (incomingList.Count == 0)
            return new IngestArticlesResponse { IngestedCount = 0, SkippedDuplicateCount = 0 };

        // Batch duplicate check — O(1) HashSet lookups instead of N DB round-trips
        var incomingUrls = incomingList
            .Select(a => a.Url?.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList();

        var existingUrls = incomingUrls.Count > 0
            ? (await _db.Articles
                .AsNoTracking()
                .Where(a => a.Url != null && incomingUrls.Contains(a.Url))
                .Select(a => a.Url!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var incomingTitles = incomingList
            .Select(a => a.Title.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var incomingTitlesLower = incomingTitles
            .Select(t => t.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingTitles = incomingTitles.Count > 0
            ? (await _db.Articles
                .AsNoTracking()
                .Where(a => incomingTitles.Contains(a.Title) || incomingTitlesLower.Contains(a.Title.ToLower()))
                .Select(a => a.Title)
                .ToListAsync(cancellationToken))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int ingested = 0, skipped = 0;
        var ingestedSlugs = new List<string>();
        var topicCache = new Dictionary<string, Topic>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in incomingList)
        {
            var titleNorm = item.Title.Trim().ToLowerInvariant();
            var urlNorm   = item.Url?.Trim();

            bool isDuplicate = (!string.IsNullOrEmpty(urlNorm) && existingUrls.Contains(urlNorm)) ||
                               existingTitles.Contains(titleNorm);

            if (isDuplicate)
            {
                skipped++;
                continue;
            }

            var articleId = Guid.NewGuid().ToString("N");
            var title = item.Title.Trim();
            var slug = _slugService.GenerateArticleSlug(title, articleId);
            var excerpt = _slugService.GenerateExcerpt(item.Content, item.Summary, 300);

            _db.Articles.Add(new Article
            {
                Id          = articleId,
                Title       = title,
                Slug        = slug,
                Excerpt     = excerpt,
                Summary     = item.Summary,
                Content     = item.Content,
                Url         = item.Url,
                ImageUrl    = item.ImageUrl,
                Source      = item.Source ?? "Scraped News",
                Category    = item.Category ?? "General",
                PublishedAt = item.PublishedAt ?? DateTime.UtcNow,
                AudioUrl    = item.AudioUrl,
                Author      = item.Author,
                ContentType = string.IsNullOrWhiteSpace(item.ContentType) ? "News" : item.ContentType
            });

            if (item.Topics != null && item.Topics.Count > 0)
            {
                var assignedTopicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in item.Topics)
                {
                    if (string.IsNullOrWhiteSpace(t.Slug)) continue;
                    var topicSlug = t.Slug.Trim().ToLowerInvariant();

                    if (!topicCache.TryGetValue(topicSlug, out var topic))
                    {
                        topic = _db.Topics.Local.FirstOrDefault(x => x.Slug == topicSlug)
                            ?? await _db.Topics.FirstOrDefaultAsync(x => x.Slug == topicSlug, cancellationToken);

                        if (topic == null)
                        {
                            Enum.TryParse<TopicType>(t.Type, true, out var parsedType);
                            topic = new Topic
                            {
                                Id          = Guid.NewGuid().ToString("N"),
                                Name        = string.IsNullOrWhiteSpace(t.Name) ? topicSlug : t.Name.Trim(),
                                Slug        = topicSlug,
                                Type        = parsedType,
                                Description = t.Description,
                                CreatedAt   = DateTime.UtcNow
                            };
                            _db.Topics.Add(topic);
                        }

                        topicCache[topicSlug] = topic;
                    }

                    if (assignedTopicIds.Add(topic.Id))
                    {
                        _db.ArticleTopics.Add(new ArticleTopic
                        {
                            ArticleId  = articleId,
                            TopicId    = topic.Id,
                            Confidence = t.Confidence
                        });
                    }
                }
            }

            if (!string.IsNullOrEmpty(urlNorm)) existingUrls.Add(urlNorm);
            existingTitles.Add(titleNorm);
            ingestedSlugs.Add(slug);
            ingested++;
        }

        if (ingested > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _briefingService.InvalidateBriefingCache();

            if (_indexNowService != null && ingestedSlugs.Count > 0)
            {
                var host = _indexNowService.GetConfiguration().Host;
                var urls = ingestedSlugs.Select(s => $"https://{host}/news/{s}");
                _ = _indexNowService.EnqueueUrlsAsync(urls, CancellationToken.None);
            }
        }

        _logger.LogInformation(
            "Ingested {IngestedCount} articles, skipped {SkippedCount} duplicates.",
            ingested, skipped);

        return new IngestArticlesResponse
        {
            IngestedCount         = ingested,
            SkippedDuplicateCount = skipped
        };
    }

    public async Task<Article?> UpdateCategoryAsync(
        string id,
        string newCategory,
        CancellationToken cancellationToken = default)
    {
        var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article == null) return null;

        var oldCategory   = article.Category ?? "General";
        article.Category  = newCategory.Trim();
        article.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await SaveCorrectionFeedbackAsync(article, oldCategory, cancellationToken);
        _briefingService.InvalidateBriefingCache();

        _logger.LogInformation(
            "Updated category for article '{ArticleId}' from '{Old}' to '{New}'.",
            id, oldCategory, article.Category);

        return article;
    }

    public async Task<List<ArticleDeltaDto>> GetArticleDeltasAsync(
        DateTime? sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var cutoff = sinceUtc ?? DateTime.UtcNow.AddDays(-14);

        return await _db.Articles
            .AsNoTracking()
            .Where(a => a.UpdatedAt != null && a.UpdatedAt > cutoff)
            .OrderBy(a => a.UpdatedAt)
            .Take(500)
            .Select(a => new ArticleDeltaDto
            {
                Id        = a.Id,
                Category  = a.Category ?? "General",
                UpdatedAt = a.UpdatedAt!.Value
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ArticleDto>> GetSponsoredArticlesAsync(
        string? platform = null,
        string? zone = null,
        CancellationToken ct = default)
    {
        var query = _db.Articles
            .AsNoTracking()
            .Where(a => a.IsSponsored);

        if (!string.IsNullOrWhiteSpace(platform) && !platform.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(a => a.TargetPlatform == "All" || a.TargetPlatform == platform);
        }

        if (!string.IsNullOrWhiteSpace(zone))
        {
            query = query.Where(a => a.PlacementZone == zone);
        }

        return await query
            .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
            .Select(a => new ArticleDto
            {
                Id                = a.Id,
                Title             = a.Title,
                Summary           = a.Summary,
                Content           = a.Content,
                Url               = a.Url,
                ImageUrl          = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
                Source            = string.IsNullOrWhiteSpace(a.Source) ? $"{a.SponsorName} Press Release" : a.Source,
                Category          = string.IsNullOrWhiteSpace(a.Category) ? "Business" : a.Category,
                PublishedAt       = a.PublishedAt,
                IsSponsored       = a.IsSponsored,
                SponsorName       = a.SponsorName,
                SponsorUrl        = a.SponsorUrl,
                CampaignExpiresAt = a.CampaignExpiresAt,
                IsPinned          = a.IsPinned,
                TargetPosition    = a.TargetPosition,
                PriorityWeight    = a.PriorityWeight,
                PlacementZone     = a.PlacementZone ?? "MainPage_InFeed",
                TargetPlatform    = a.TargetPlatform ?? "All",
                ImpressionCount   = a.ImpressionCount,
                ClickCount        = a.ClickCount
            })
            .ToListAsync(ct);
    }

    public async Task<(Article? Article, string? ValidationError)> CreateSponsoredArticleAsync(
        CreateSponsoredArticleRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.SponsorName))
        {
            return (null, "Title and SponsorName are required.");
        }

        if (request.TargetPosition.HasValue)
        {
            var pos = request.TargetPosition.Value;
            if (!((pos >= 1 && pos <= 3) || (pos >= 5 && pos <= 30)))
            {
                return (null, "TargetPosition must be either a Special Placement (1, 2, or 3) or an In-Feed Placement between 5 and 30.");
            }
        }

        var articleId = Guid.NewGuid().ToString("N");
        var title = request.Title.Trim();
        var slug = _slugService.GenerateArticleSlug(title, articleId);
        var excerpt = _slugService.GenerateExcerpt(request.Content, request.Summary, 300);

        var placementZone = !string.IsNullOrWhiteSpace(request.PlacementZone) ? request.PlacementZone.Trim() : "MainPage_InFeed";
        var targetPlatform = !string.IsNullOrWhiteSpace(request.TargetPlatform) ? request.TargetPlatform.Trim() : "All";

        var article = new Article
        {
            Id                = articleId,
            Title             = title,
            Slug              = slug,
            Excerpt           = excerpt,
            Summary           = request.Summary?.Trim(),
            Content           = request.Content?.Trim(),
            Url               = !string.IsNullOrWhiteSpace(request.Url) ? request.Url.Trim() : (!string.IsNullOrWhiteSpace(request.SponsorUrl) ? request.SponsorUrl.Trim() : null),
            ImageUrl          = request.ImageUrl?.Trim(),
            Source            = $"{request.SponsorName.Trim()} (Sponsored)",
            Category          = !string.IsNullOrWhiteSpace(request.Category) ? request.Category.Trim() : "Business",
            PublishedAt       = DateTime.UtcNow,
            IsSponsored       = true,
            SponsorName       = request.SponsorName.Trim(),
            SponsorUrl        = request.SponsorUrl?.Trim(),
            CampaignExpiresAt = request.CampaignExpiresAt,
            IsPinned          = request.IsPinned,
            TargetPosition    = request.TargetPosition,
            PriorityWeight    = request.PriorityWeight > 0 ? request.PriorityWeight : 1,
            PlacementZone     = placementZone,
            TargetPlatform    = targetPlatform,
            ImpressionCount   = 0,
            ClickCount        = 0
        };

        _db.Articles.Add(article);
        await _db.SaveChangesAsync(ct);
        _briefingService.InvalidateBriefingCache();

        _logger.LogInformation(
            "Created new sponsored campaign '{Title}' for sponsor '{Sponsor}' in zone '{Zone}' on platform '{Platform}'.",
            article.Title, article.SponsorName, article.PlacementZone, article.TargetPlatform);

        return (article, null);
    }

    public async Task<(Article? Article, bool NotFound, string? ValidationError)> UpdateSponsoredArticleAsync(
        string id,
        UpdateSponsoredArticleRequest request,
        CancellationToken ct = default)
    {
        var article = await _db.Articles.FindAsync([id], ct);
        if (article == null)
        {
            return (null, NotFound: true, null);
        }

        if (!string.IsNullOrWhiteSpace(request.Title)) article.Title = request.Title.Trim();
        if (request.Summary != null) article.Summary = request.Summary.Trim();
        if (request.Content != null) article.Content = request.Content.Trim();
        if (request.Url != null) article.Url = request.Url.Trim();
        if (request.ImageUrl != null) article.ImageUrl = request.ImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.SponsorName)) article.SponsorName = request.SponsorName.Trim();
        if (request.SponsorUrl != null) article.SponsorUrl = request.SponsorUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.Category)) article.Category = request.Category.Trim();
        if (request.CampaignExpiresAt.HasValue) article.CampaignExpiresAt = request.CampaignExpiresAt.Value;
        if (request.IsPinned.HasValue) article.IsPinned = request.IsPinned.Value;
        if (request.IsSponsored.HasValue) article.IsSponsored = request.IsSponsored.Value;
        if (!string.IsNullOrWhiteSpace(request.PlacementZone)) article.PlacementZone = request.PlacementZone.Trim();
        if (!string.IsNullOrWhiteSpace(request.TargetPlatform)) article.TargetPlatform = request.TargetPlatform.Trim();

        if (request.TargetPosition.HasValue)
        {
            var pos = request.TargetPosition.Value;
            if (!((pos >= 1 && pos <= 3) || (pos >= 5 && pos <= 30)))
            {
                return (null, NotFound: false, "TargetPosition must be either a Special Placement (1, 2, or 3) or an In-Feed Placement between 5 and 30.");
            }
            article.TargetPosition = pos;
        }

        if (request.PriorityWeight.HasValue && request.PriorityWeight.Value > 0)
        {
            article.PriorityWeight = request.PriorityWeight.Value;
        }

        await _db.SaveChangesAsync(ct);
        _briefingService.InvalidateBriefingCache();

        _logger.LogInformation("Updated sponsored campaign '{Id}'.", id);
        return (article, NotFound: false, null);
    }

    public async Task<bool> DeleteSponsoredArticleAsync(string id, CancellationToken ct = default)
    {
        var article = await _db.Articles.FindAsync([id], ct);
        if (article == null) return false;

        _db.Articles.Remove(article);
        await _db.SaveChangesAsync(ct);
        _briefingService.InvalidateBriefingCache();

        _logger.LogInformation("Deleted sponsored campaign '{Id}'.", id);
        return true;
    }

    public async Task<bool> TrackImpressionAsync(string id, CancellationToken ct = default)
    {
        var updated = await _db.Articles
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ImpressionCount, a => a.ImpressionCount + 1), ct);
        return updated > 0;
    }

    public async Task<bool> TrackClickAsync(string id, CancellationToken ct = default)
    {
        var updated = await _db.Articles
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ClickCount, a => a.ClickCount + 1), ct);
        return updated > 0;
    }

    public async Task<string?> RenderShareCardHtmlAsync(string id, CancellationToken ct = default)
    {
        var a = await _db.Articles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (a == null) return null;

        var imageUrl    = CategoryImageMap.Resolve(a.ImageUrl, a.Category);
        var title       = System.Net.WebUtility.HtmlEncode(a.Title ?? "News Stand NG Story");
        var source      = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(a.Source) ? "News Stand NG" : a.Source);
        var summary     = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(a.Summary) ? $"Read the latest breaking story from {source} on News Stand NG." : a.Summary);
        var targetUrl   = a.Url ?? "https://play.google.com/store/apps/details?id=com.nigeriannewsgrid.app";
        var appStoreUrl = "https://play.google.com/store/apps/details?id=com.nigeriannewsgrid.app";
        var deepLinkUri = $"nigeriannewsgrid://article/{id}";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>{title} — News Stand NG</title>

    <!-- 𝕏 (Twitter) Card Metadata -->
    <meta name=""twitter:card"" content=""summary_large_image"">
    <meta name=""twitter:site"" content=""@NewsStandNG"">
    <meta name=""twitter:creator"" content=""@NewsStandNG"">
    <meta name=""twitter:title"" content=""{title}"">
    <meta name=""twitter:description"" content=""Via {source} • {summary}"">
    <meta name=""twitter:image"" content=""{imageUrl}"">
    <meta name=""twitter:image:alt"" content=""{title}"">

    <!-- Open Graph Metadata -->
    <meta property=""og:type"" content=""article"">
    <meta property=""og:site_name"" content=""News Stand NG"">
    <meta property=""og:title"" content=""{title}"">
    <meta property=""og:description"" content=""Via {source} • {summary}"">
    <meta property=""og:image"" content=""{imageUrl}"">
    <meta property=""og:url"" content=""{targetUrl}"">

    <style>
        :root {{
            --bg: #F8FAFC;
            --card-bg: #FFFFFF;
            --text-primary: #0F172A;
            --text-secondary: #64748B;
            --accent: #2563EB;
            --accent-hover: #1D4ED8;
            --border: #E2E8F0;
        }}
        @media (prefers-color-scheme: dark) {{
            :root {{
                --bg: #090D16;
                --card-bg: #131B2E;
                --text-primary: #F8FAFC;
                --text-secondary: #94A3B8;
                --border: #1E293B;
            }}
        }}
        * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }}
        body {{ background-color: var(--bg); color: var(--text-primary); display: flex; justify-content: center; align-items: center; min-height: 100vh; padding: 20px; }}
        .card {{ background: var(--card-bg); border: 1px solid var(--border); border-radius: 20px; max-width: 600px; width: 100%; overflow: hidden; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.1); }}
        .image-container {{ position: relative; width: 100%; height: 280px; background: #E2E8F0; }}
        .image-container img {{ width: 100%; height: 100%; object-fit: cover; }}
        .source-badge {{ position: absolute; top: 16px; left: 16px; background: rgba(15, 23, 42, 0.85); color: #FFF; padding: 6px 12px; border-radius: 999px; font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; backdrop-filter: blur(8px); }}
        .content {{ padding: 24px; }}
        .headline {{ font-size: 20px; line-height: 1.4; font-weight: 700; margin-bottom: 12px; }}
        .summary {{ font-size: 14px; line-height: 1.6; color: var(--text-secondary); margin-bottom: 24px; }}
        .actions {{ display: flex; flex-direction: column; gap: 12px; }}
        .btn {{ display: flex; align-items: center; justify-content: center; gap: 8px; padding: 14px 20px; border-radius: 12px; font-size: 15px; font-weight: 600; text-decoration: none; transition: transform 0.15s ease, background 0.15s ease; cursor: pointer; text-align: center; }}
        .btn:active {{ transform: scale(0.98); }}
        .btn-primary {{ background: var(--accent); color: #FFF; }}
        .btn-primary:hover {{ background: var(--accent-hover); }}
        .btn-secondary {{ background: transparent; border: 1.5px solid var(--border); color: var(--text-primary); }}
        .footer-note {{ text-align: center; margin-top: 18px; font-size: 12px; color: var(--text-secondary); }}
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""image-container"">
            <img src=""{imageUrl}"" alt=""{title}"" onerror=""this.style.display='none'"">
            <span class=""source-badge"">{source}</span>
        </div>
        <div class=""content"">
            <h1 class=""headline"">{title}</h1>
            <p class=""summary"">{summary}</p>
            <div class=""actions"">
                <a href=""{deepLinkUri}"" class=""btn btn-primary"">📲 Open in News Stand NG</a>
                <a href=""{appStoreUrl}"" class=""btn btn-secondary"">⭐ Download App on Google Play</a>
                <a href=""{targetUrl}"" target=""_blank"" rel=""noopener noreferrer"" class=""btn btn-secondary"">🌐 Read Full Story at {source}</a>
            </div>
            <p class=""footer-note"">Powered by News Stand NG — Nigeria's Premier Smart News Aggregator</p>
        </div>
    </div>
</body>
</html>";
    }

    private async Task SaveCorrectionFeedbackAsync(
        Article article, string oldCategory, CancellationToken ct)
    {
        try
        {
            _db.CategoryCorrections.Add(new CategoryCorrection
            {
                ArticleId   = article.Id,
                Title       = article.Title,
                Summary     = article.Summary,
                OldCategory = oldCategory,
                NewCategory = article.Category ?? "General",
                Source      = article.Source,
                Url         = article.Url,
                CreatedAt   = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Persisted category correction for article {ArticleId}.", article.Id);

            _categorizerTrainingService?.CheckAndTriggerAutoRetrain();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save category correction for article {ArticleId}.", article.Id);
        }
    }

    // ── Shared DTO Mapping Helpers ───────────────────────────────────────────

    public static ArticleDto MapToArticleDto(
        Article a,
        ISlugService slugService,
        bool includeContent,
        string? resolvedAudioUrl = null,
        int commentCount = 0,
        ArticleEnrichment? enr = null)
    {
        return new ArticleDto
        {
            Id                = a.Id,
            Title             = a.Title,
            Slug              = !string.IsNullOrEmpty(a.Slug) ? a.Slug : slugService.GenerateArticleSlug(a.Title, a.Id),
            Excerpt           = a.Excerpt ?? slugService.GenerateExcerpt(a.Content, a.Summary, 300),
            SeoTitle          = a.SeoTitle,
            SeoDescription    = a.SeoDescription,
            Summary           = a.Summary,
            Content           = includeContent ? a.Content : null,
            Url               = a.Url,
            ImageUrl          = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
            Source            = string.IsNullOrWhiteSpace(a.Source)   ? "General News" : a.Source,
            Category          = string.IsNullOrWhiteSpace(a.Category) ? "General"      : a.Category,
            PublishedAt       = a.PublishedAt,
            AudioUrl          = resolvedAudioUrl ?? a.AudioUrl,
            Author            = a.Author,
            ContentType       = string.IsNullOrWhiteSpace(a.ContentType) ? "News" : a.ContentType,
            CommentCount      = commentCount,
            IsSponsored       = a.IsSponsored,
            SponsorName       = a.SponsorName,
            SponsorUrl        = a.SponsorUrl,
            CampaignExpiresAt = a.CampaignExpiresAt,
            IsPinned          = a.IsPinned,
            ImpressionCount   = a.ImpressionCount,
            ClickCount        = a.ClickCount,
            PlacementZone     = a.PlacementZone ?? "MainPage_InFeed",
            TargetPlatform    = a.TargetPlatform ?? "All",
            HasAiSummary      = enr != null,
            AiSummaryEngine   = enr?.Engine,
            AiSummary         = enr?.FullSummary?.ExecutiveOverview ?? enr?.KeyTakeaways.FirstOrDefault(),
            Enrichment        = MapToEnrichmentDto(enr)
        };
    }

    public static ArticleEnrichmentDto? MapToEnrichmentDto(ArticleEnrichment? enr)
    {
        if (enr == null) return null;

        return new ArticleEnrichmentDto
        {
            KeyTakeaways  = enr.KeyTakeaways,
            TimelineFacts = enr.TimelineFacts,
            Faqs          = enr.Faqs,
            FullSummary   = MapToFullSummaryDto(enr.FullSummary),
            Engine        = enr.Engine,
            UpdatedAt     = enr.UpdatedAt ?? enr.CreatedAt
        };
    }

    public static FullSummaryDto? MapToFullSummaryDto(FullSummaryPayload? summary)
    {
        if (summary == null) return null;

        return new FullSummaryDto
        {
            ExecutiveOverview     = summary.ExecutiveOverview,
            ContextAndBackground  = summary.ContextAndBackground,
            KeyFiguresAndEntities = summary.KeyFiguresAndEntities,
            OutlookOrNextSteps    = summary.OutlookOrNextSteps,
            ReadingTimeSeconds    = summary.ReadingTimeSeconds
        };
    }
}
