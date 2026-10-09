namespace NigerianNewGrid.Services;

/// <summary>
/// Composite persistence interface combining story storage, bookmarks, and read history.
/// Retained for backward compatibility across consumers while enabling narrower interface dependencies.
/// </summary>
public interface INewsPersistenceService : IStoryRepository, IBookmarkRepository, IReadHistoryRepository
{
}
