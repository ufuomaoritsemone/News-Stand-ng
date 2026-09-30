using System.Threading.Tasks;

namespace NigerianNewGrid.Services;

public enum SonicSound
{
    NewspaperHornRefresh,
    Bookmark,
    CategorySelect,
    FeedbackSuccess
}

public interface ISonicFeedbackService
{
    Task PlayRefreshChimeAsync();
    Task PlaySoundAsync(SonicSound sound);
    bool IsEnabled { get; set; }
}
