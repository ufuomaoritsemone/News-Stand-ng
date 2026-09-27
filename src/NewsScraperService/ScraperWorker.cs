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
    private readonly IArticleEnricher _enricher;
    private readonly IHostApplicationLifetime _appLifetime;

    public ScraperWorker(
        ILogger<ScraperWorker> logger, 
        IHttpClientFactory httpFactory, 
        IConfiguration configuration,
        MlCategorizerEngine categorizer,
        ArticleContentExtractor extractor,
        UrlFrontierManager frontier,
        IArticleEnricher enricher,
        IHostApplicationLifetime appLifetime)
    {
        _logger        = logger;
        _httpFactory   = httpFactory;
        _configuration = configuration;
        _categorizer   = categorizer;
        _extractor     = extractor;
        _frontier      = frontier;
        _enricher      = enricher;
        _appLifetime   = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScraperWorker starting with Google News Sitemap discovery, JSON-LD/SmartReader extraction, and ML categorization...");

        var internalApiClient = _httpFactory.CreateClient();
        ConfigureApiKeyHeader(internalApiClient);

        var externalScraperClient = _httpFactory.CreateClient();

        while (!stoppingToken.IsCancellationRequested)
        {
            var scrapers = await GetScrapersAsync(internalApiClient, externalScraperClient, stoppingToken);
            _logger.LogInformation("ScraperWorker running cycle with {Count} sources (Sitemaps & RSS feeds)", scrapers.Count);

            var apiBaseUrl = GetApiBaseUrl();

            foreach (var s in scrapers)
            {
                try
                {
                    var items = (await s.ScrapeAsync(stoppingToken)).ToList();
                    _logger.LogInformation("{Source} returned {Count} discovered items", s.SourceName, items.Count);

                    if (!items.Any()) continue;

                    // Normalize & enrich items using IArticleEnricher (Fix #15, #16)
                    foreach (var it in items)
                    {
                        it.Title = ArticleContentExtractor.SanitizeText(_enricher.NormalizeTitle(it.Title));
                        it.Summary = ArticleContentExtractor.SanitizeText(it.Summary ?? string.Empty);
                        if (string.IsNullOrEmpty(it.Category))
                        {
                            it.Category = _categorizer.Categorize(it.Title, it.Summary);
                        }
                        it.Source = s.SourceName;

                        // Editorial and Opinion content classification
                        if (string.IsNullOrEmpty(it.ContentType) || it.ContentType == "News")
                        {
                            it.ContentType = OpinionDetector.Detect(it.Url, it.Title, it.Category, s.SourceName);
                        }
                    }

                    // Populate missing images using metadata/extractor fallback (external client, no X-Api-Key)
                    await _enricher.PopulateMissingImageUrlsAsync(externalScraperClient, items, s.SourceName, stoppingToken);

                    // Push items directly to NewsApi via REST Ingestion Endpoint (internal client with X-Api-Key)
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
                            i.AudioUrl,
                            i.Author,
                            i.ContentType
                        }).ToList()
                    };

                    var ingestUrl = $"{apiBaseUrl.TrimEnd('/')}/api/v1/articles/ingest";
                    var response = await internalApiClient.PostAsJsonAsync(ingestUrl, ingestPayload, stoppingToken);

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

            // Trigger YouTube video stories and trending YouTube sync (internal client with X-Api-Key)
            try
            {
                _logger.LogInformation("Triggering media syndication sync (YouTube channels and Trending videos)...");
                await internalApiClient.PostAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-stories/sync", null, stoppingToken);
                await internalApiClient.PostAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-stories/trending/sync", null, stoppingToken);
                _logger.LogInformation("Media syndication sync triggered successfully.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to trigger media syndication sync in ScraperWorker.");
            }

            var runOnce = _configuration.GetValue("Scraper:RunOnce", false) 
                || string.Equals(Environment.GetEnvironmentVariable("SCRAPER_RUN_ONCE"), "true", StringComparison.OrdinalIgnoreCase);

            if (runOnce)
            {
                _logger.LogInformation("ScraperWorker completed single run cycle (Cloud Run Job mode). Terminating application.");
                _appLifetime.StopApplication();
                return;
            }

            // Run every 15 minutes for continuous coverage
            var delayMinutes = _configuration.GetValue("Scraper:IntervalMinutes", 15);
            _logger.LogInformation("Scraping cycle completed. Next run in {Minutes} minutes.", delayMinutes);
            await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
        }
    }

    private void ConfigureApiKeyHeader(HttpClient client)
    {
        var apiKey = _configuration["ApiKey"] 
            ?? _configuration["NewsApi:ApiKey"] 
            ?? Environment.GetEnvironmentVariable("API_KEY");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }
    }

    private string GetApiBaseUrl()
    {
        return _configuration["ApiBaseUrl"] 
            ?? _configuration["NewsApi:BaseUrl"] 
            ?? "http://localhost:56193";
    }

    private async Task<List<IScraper>> GetScrapersAsync(HttpClient internalClient, HttpClient externalClient, CancellationToken cancellationToken)
    {
        var scrapers = new List<IScraper>();
        var apiBaseUrl = GetApiBaseUrl();

        try
        {
            var sources = await internalClient.GetFromJsonAsync<List<SourceDto>>($"{apiBaseUrl.TrimEnd('/')}/api/v1/sources", cancellationToken);
            if (sources != null && sources.Any())
            {
                foreach (var src in sources)
                {
                    if (!string.IsNullOrWhiteSpace(src.SitemapUrl))
                    {
                        scrapers.Add(new GenericSitemapScraper(externalClient, src.Id ?? src.Name, src.Name, src.SitemapUrl, src.RssUrl, _extractor, _frontier));
                    }
                    else if (!string.IsNullOrWhiteSpace(src.RssUrl))
                    {
                        scrapers.Add(new GenericRssScraper(externalClient, src.Id ?? src.Name, src.Name, src.RssUrl));
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
            scrapers.Add(new PunchSitemapScraper(externalClient, _extractor, _frontier));
            scrapers.Add(new VanguardSitemapScraper(externalClient, _extractor, _frontier));
            scrapers.Add(new PremiumTimesSitemapScraper(externalClient, _extractor, _frontier));
            scrapers.Add(new TheCableSitemapScraper(externalClient, _extractor, _frontier));
            scrapers.Add(new DailyPostSitemapScraper(externalClient, _extractor, _frontier));
            scrapers.Add(new DailyTrustSitemapScraper(externalClient, _extractor, _frontier));
            scrapers.Add(new NairametricsSitemapScraper(externalClient, _extractor, _frontier));
            scrapers.Add(new GuardianSitemapScraper(externalClient, _extractor, _frontier));
            scrapers.Add(new ChannelsTvSitemapScraper(externalClient, _extractor, _frontier));
            scrapers.Add(new BusinessDayScraper(externalClient));
            scrapers.Add(new LindaIkejiScraper(externalClient));
        }

        return scrapers;
    }
}
