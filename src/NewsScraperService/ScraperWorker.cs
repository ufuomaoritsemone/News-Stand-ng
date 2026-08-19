using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewsScraperService.Models;
using NewsScraperService.Scrapers;
using NewsScraperService.Services;

namespace NewsScraperService;

public class ScraperWorker : BackgroundService
{
    private readonly ILogger<ScraperWorker> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly MlCategorizerEngine _categorizer;
    private readonly ArticleContentExtractor _extractor;
    private readonly UrlFrontierManager _frontier;

    public ScraperWorker(
        ILogger<ScraperWorker> logger, 
        IHttpClientFactory httpFactory, 
        IConfiguration configuration,
        MlCategorizerEngine categorizer,
        ArticleContentExtractor extractor,
        UrlFrontierManager frontier)
    {
        _logger = logger;
        _httpFactory = httpFactory;
        _configuration = configuration;
        _categorizer = categorizer;
        _extractor = extractor;
        _frontier = frontier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScraperWorker starting with Google News Sitemap discovery, JSON-LD/SmartReader extraction, and ML categorization...");

        var client = _httpFactory.CreateClient();

        while (!stoppingToken.IsCancellationRequested)
        {
            var scrapers = await GetScrapersAsync(client, stoppingToken);
            _logger.LogInformation("ScraperWorker running cycle with {Count} sources (Sitemaps & RSS feeds)", scrapers.Count);

            var apiBaseUrl = GetApiBaseUrl();

            foreach (var s in scrapers)
            {
                try
                {
                    var items = (await s.ScrapeAsync(stoppingToken)).ToList();
                    _logger.LogInformation("{Source} returned {Count} discovered items", s.SourceName, items.Count);

                    if (!items.Any()) continue;

                    // Normalize & enrich items
                    foreach (var it in items)
                    {
                        it.Title = ArticleContentExtractor.SanitizeText(NormalizeTitle(it.Title));
                        it.Summary = ArticleContentExtractor.SanitizeText(it.Summary ?? string.Empty);
                        if (string.IsNullOrEmpty(it.Category))
                        {
                            it.Category = _categorizer.Categorize(it.Title, it.Summary);
                        }
                        it.Source = s.SourceName;
                    }

                    // Populate missing images using metadata/extractor fallback
                    await PopulateMissingImageUrlsAsync(client, items, s.SourceName, stoppingToken);

                    // Push items directly to NewsApi via REST Ingestion Endpoint
                    var ingestPayload = new
                    {
                        Articles = items.Select(i => new
                        {
                            i.Title,
                            i.Summary,
                            i.Content,
                            i.Url,
                            i.ImageUrl,
                            i.Source,
                            i.Category,
                            i.PublishedAt,
                            i.AudioUrl
                        }).ToList()
                    };

                    var ingestUrl = $"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/ingest";
                    var response = await client.PostAsJsonAsync(ingestUrl, ingestPayload, stoppingToken);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully ingested articles from {Source} into NewsApi", s.SourceName);
                    }
                    else
                    {
                        _logger.LogWarning("NewsApi ingestion endpoint returned status {StatusCode} for {Source}", response.StatusCode, s.SourceName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scraping or ingesting for source {Source}", s.SourceName);
                }
            }

            // Trigger YouTube video stories and trending YouTube sync
            try
            {
                _logger.LogInformation("Triggering media syndication sync (YouTube channels and Trending videos)...");
                await client.PostAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-stories/sync", null, stoppingToken);
                await client.PostAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-stories/trending/sync", null, stoppingToken);
                _logger.LogInformation("Media syndication sync triggered successfully.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to trigger media syndication sync in ScraperWorker.");
            }

            // Run every 15 minutes for comprehensive coverage
            var delayMinutes = _configuration.GetValue("Scraper:IntervalMinutes", 15);
            _logger.LogInformation("Scraping cycle completed. Next run in {Minutes} minutes.", delayMinutes);
            await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
        }
    }

    private string GetApiBaseUrl()
    {
        return _configuration["ApiBaseUrl"] 
            ?? _configuration["NewsApi:BaseUrl"] 
            ?? "http://localhost:56193";
    }

    private async Task<List<IScraper>> GetScrapersAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var scrapers = new List<IScraper>();
        var apiBaseUrl = GetApiBaseUrl();

        try
        {
            var sources = await client.GetFromJsonAsync<List<SourceDto>>($"{apiBaseUrl.TrimEnd('/')}/api/v1/sources", cancellationToken);
            if (sources != null && sources.Any())
            {
                foreach (var src in sources)
                {
                    if (!string.IsNullOrWhiteSpace(src.SitemapUrl))
                    {
                        scrapers.Add(new GenericSitemapScraper(client, src.Id ?? src.Name, src.Name, src.SitemapUrl, src.RssUrl, _extractor, _frontier));
                    }
                    else if (!string.IsNullOrWhiteSpace(src.RssUrl))
                    {
                        scrapers.Add(new GenericRssScraper(client, src.Id ?? src.Name, src.Name, src.RssUrl));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Unable to fetch dynamic sources from NewsApi: {Message}. Falling back to default sitemap and RSS scrapers.", ex.Message);
        }

        if (!scrapers.Any())
        {
            // Default verified high-yield Nigerian news sources (Google News Sitemaps & RSS)
            scrapers.Add(new PunchSitemapScraper(client, _extractor, _frontier));
            scrapers.Add(new VanguardSitemapScraper(client, _extractor, _frontier));
            scrapers.Add(new PremiumTimesSitemapScraper(client, _extractor, _frontier));
            scrapers.Add(new TheCableSitemapScraper(client, _extractor, _frontier));
            scrapers.Add(new DailyPostSitemapScraper(client, _extractor, _frontier));
            scrapers.Add(new DailyTrustSitemapScraper(client, _extractor, _frontier));
            scrapers.Add(new GuardianScraper(client));
        }

        return scrapers;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(title, "\"|'|\\s+", " ").Trim();
    }

    private static bool IsGenericImage(string? url)
    {
        if (string.IsNullOrEmpty(url)) return true;
        return url.Contains("logo", StringComparison.OrdinalIgnoreCase) 
            || url.Contains("default", StringComparison.OrdinalIgnoreCase)
            || url.Contains("fallback", StringComparison.OrdinalIgnoreCase)
            || url.Contains("header", StringComparison.OrdinalIgnoreCase)
            || url.Contains("icon", StringComparison.OrdinalIgnoreCase)
            || url.Contains("avatar", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PopulateMissingImageUrlsAsync(HttpClient client, List<ArticleModel> items, string sourceName, CancellationToken cancellationToken)
    {
        var itemsWithoutImages = items.Where(it => IsGenericImage(it.ImageUrl) && !string.IsNullOrEmpty(it.Url)).ToList();
        if (!itemsWithoutImages.Any()) return;

        _logger.LogInformation("Resolving missing images and rich metadata for {Count} articles from {Source}...", itemsWithoutImages.Count, sourceName);

        using var semaphore = new SemaphoreSlim(4);
        var tasks = itemsWithoutImages.Select(async it =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var extracted = await _extractor.ExtractFromUrlAsync(client, it.Url!, sourceName, cancellationToken);
                if (extracted != null)
                {
                    if (!string.IsNullOrEmpty(extracted.ImageUrl) && !IsGenericImage(extracted.ImageUrl))
                    {
                        it.ImageUrl = extracted.ImageUrl;
                    }
                    if (string.IsNullOrEmpty(it.Content) && !string.IsNullOrEmpty(extracted.Content))
                    {
                        it.Content = extracted.Content;
                    }
                    if (string.IsNullOrEmpty(it.Summary) && !string.IsNullOrEmpty(extracted.Summary))
                    {
                        it.Summary = extracted.Summary;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resolve metadata for article '{Title}'", it.Title);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private class SourceDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? RssUrl { get; set; }
        public string? SitemapUrl { get; set; }
        public string? ScraperType { get; set; }
    }
}
