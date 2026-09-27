namespace NewsApi.Services;

/// <summary>
/// Contract for syncing social posts from monitored handles.
/// </summary>
public interface ISocialFeedService
{
    /// <summary>Syncs latest posts for all monitored social handles.</summary>
    Task<int> SyncAllHandlesAsync(CancellationToken ct = default);

    /// <summary>Syncs latest posts for a specific monitored social handle.</summary>
    Task<int> SyncHandlePostsAsync(NewsApi.Models.SocialHandle handle, CancellationToken ct = default);
}
