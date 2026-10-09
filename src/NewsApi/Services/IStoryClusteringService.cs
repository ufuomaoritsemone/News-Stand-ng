using NewsApi.Models;

namespace NewsApi.Services;

public interface IStoryClusteringService
{
    Task<int> RunClusteringAsync(CancellationToken cancellationToken = default);
    Task<List<Article>> FindRelatedArticlesAsync(string articleId, int limit = 6, CancellationToken cancellationToken = default);
}
