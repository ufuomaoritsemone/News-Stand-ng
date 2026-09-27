using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.Models;

/// <summary>
/// Groups briefing stories by publish date for CollectionView display.
/// Moved from MainPage.xaml.cs to follow Single Responsibility Principle (Fix #34).
/// </summary>
public class BriefingDateGroup
{
    public DateTime Date { get; set; }
    public string DateHeader { get; set; } = string.Empty;
    public string RelativeLabel { get; set; } = string.Empty;
    public List<BriefingItem> Stories { get; set; } = [];

    public static List<BriefingDateGroup> BuildDateGroups(List<BriefingItem> stories)
    {
        return stories
            .GroupBy(i => (i.PublishedAt?.ToLocalTime().Date) ?? DateTime.Today)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var date = g.Key;
                var (header, relative) = date switch
                {
                    _ when date == DateTime.Today             => ($"Today · {date:dddd, MMMM d, yyyy}", "Today"),
                    _ when date == DateTime.Today.AddDays(-1) => ($"Yesterday · {date:dddd, MMMM d, yyyy}", "Yesterday"),
                    _                                         => (date.ToString("dddd, MMMM d, yyyy"), date.ToString("MMM d"))
                };

                return new BriefingDateGroup
                {
                    Date          = date,
                    DateHeader    = header,
                    RelativeLabel = relative,
                    Stories       = g.ToList()
                };
            })
            .ToList();
    }
}
