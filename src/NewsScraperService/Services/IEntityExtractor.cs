namespace NewsScraperService.Services;

public interface IEntityExtractor
{
    Task<List<EntityMatch>> ExtractEntitiesAsync(
        string title,
        string? summary = null,
        string? content = null,
        CancellationToken cancellationToken = default);
}
