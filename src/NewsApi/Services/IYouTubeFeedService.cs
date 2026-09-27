namespace NewsApi.Services;

/// <summary>
/// Contract for syncing YouTube channel video stories and trending videos.
/// </summary>
public interface IYouTubeFeedService
{
    /// <summary>Syncs latest video stories from all registered YouTube channels.</summary>
    Task<int> SyncAllChannelsAsync(CancellationToken ct = default);

    /// <summary>Syncs latest video stories from a specific registered YouTube channel.</summary>
    Task<int> SyncChannelAsync(NewsApi.Models.VideoChannel channel, CancellationToken ct = default);

    /// <summary>Syncs trending Nigerian news YouTube videos.</summary>
    Task<int> SyncTrendingNewsAsync(CancellationToken ct = default);
}
