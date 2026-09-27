using Microsoft.Maui.Controls;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.TemplateSelectors;

/// <summary>
/// Selects between standard story card and native AdMob ad card templates.
/// Ensures native AdMob ads blend seamlessly into the feed with matching dimensions.
/// </summary>
public class StoryItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? StoryTemplate { get; set; }
    public DataTemplate? NativeAdTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is BriefingItem { IsAdMobPlaceholder: true } && NativeAdTemplate != null)
        {
            return NativeAdTemplate;
        }

        return StoryTemplate ?? new DataTemplate();
    }
}
