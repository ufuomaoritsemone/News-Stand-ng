namespace NewsScraperService.Scrapers;

public interface IScraper
{
    string SourceId { get; }
    string SourceName { get; }
    Task<IEnumerable<NewsScraperService.Models.ArticleModel>> ScrapeAsync(CancellationToken cancellationToken = default);
}
