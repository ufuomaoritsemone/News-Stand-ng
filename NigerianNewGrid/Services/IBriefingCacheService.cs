using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public interface IBriefingCacheService
{
    Task<List<BriefingCategory>> GetCachedBriefingAsync(int topPerCategory = 0);
    Task SaveBriefingAsync(List<BriefingCategory> categories);
    List<BriefingCategory> GetFallbackSampleBriefing(string language);
}
