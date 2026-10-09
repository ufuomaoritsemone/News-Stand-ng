using NewsApi.Models;

namespace NewsApi.Services;

public interface IArticleSeoBackfillService
{
    Task<BackfillSeoResultDto> BackfillArticleSeoAsync(int batchSize = 500, CancellationToken cancellationToken = default);
}
