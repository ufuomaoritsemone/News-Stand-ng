using System.Diagnostics;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;

namespace NigerianNewGrid;

public partial class OnboardingPage : ContentPage
{
    private readonly INotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;

    private string _selectedLanguage = "English";
    private readonly HashSet<string> _selectedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Naira",
        "Tinubu",
        "EFCC"
    };

    private static readonly string[] AvailableTopics =
    [
        "Naira",
        "Tinubu",
        "EFCC",
        "Fuel Price",
        "Super Eagles",
        "Tech/Startups",
        "CBN",
        "Politics",
        "Business",
        "International"
    ];

    private readonly Dictionary<string, (Border card, Label check)> _langCards;
    private readonly IBackgroundSyncService _backgroundSyncService;

    public OnboardingPage(INotificationService notificationService, IServiceProvider serviceProvider, IBackgroundSyncService backgroundSyncService)
    {
        InitializeComponent();
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;
        _backgroundSyncService = backgroundSyncService;

        _langCards = new()
        {
            ["English"] = (CardLangEnglish, CheckLangEnglish),
            ["Yoruba"]  = (CardLangYoruba, CheckLangYoruba),
            ["Igbo"]    = (CardLangIgbo, CheckLangIgbo),
            ["Hausa"]   = (CardLangHausa, CheckLangHausa)
        };

        RenderTopicChips();
        UpdateLanguageSelection();
    }

    private void UpdateLanguageSelection()
    {
        var accentBrush = (Brush)Application.Current!.Resources["AccentBlueBrush"];
        var defaultBorderBrush = (Brush)Application.Current.Resources["Gray100Brush"];

        foreach (var (lang, (card, check)) in _langCards)
        {
            var isSelected = string.Equals(lang, _selectedLanguage, StringComparison.OrdinalIgnoreCase);
            check.IsVisible = isSelected;
            card.Stroke = isSelected ? accentBrush : defaultBorderBrush;
            card.StrokeThickness = isSelected ? 1.5 : 1.0;
        }
    }

    private void OnLanguageCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string lang) return;
        _selectedLanguage = lang;
        UpdateLanguageSelection();
    }

    private void OnMorningToggled(object? sender, ToggledEventArgs e)
    {
        MorningTimeContainer.IsVisible = e.Value;
    }

    private void OnKeywordAlertsToggled(object? sender, ToggledEventArgs e)
    {
        TopicsSection.IsVisible = e.Value;
    }

    private void RenderTopicChips()
    {
        FlexTopics.Children.Clear();

        var accentBlue = (Color)Application.Current!.Resources["AccentBlue"];
        var accentBlueLight = (Color)Application.Current.Resources["AccentBlueLight"];
        var gray100 = (Color)Application.Current.Resources["Gray100"];
        var gray700 = (Color)Application.Current.Resources["Gray900"];

        foreach (var topic in AvailableTopics)
        {
            var isSelected = _selectedKeywords.Contains(topic);

            var chip = new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                StrokeThickness = 1,
                Stroke = isSelected ? new SolidColorBrush(accentBlue) : new SolidColorBrush(gray100),
                BackgroundColor = isSelected ? accentBlueLight : Colors.Transparent,
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 0, 8, 8)
            };

            var label = new Label
            {
                Text = topic,
                FontFamily = "LegacySansBook",
                FontSize = 13,
                FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isSelected ? accentBlue : gray700,
                VerticalOptions = LayoutOptions.Center
            };

            chip.Content = label;

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (_selectedKeywords.Contains(topic))
                {
                    _selectedKeywords.Remove(topic);
                }
                else
                {
                    _selectedKeywords.Add(topic);
                }
                RenderTopicChips();
            };

            chip.GestureRecognizers.Add(tap);
            FlexTopics.Children.Add(chip);
        }
    }

    private async void OnGetStartedClicked(object? sender, EventArgs e)
    {
        try
        {
            BtnGetStarted.IsEnabled = false;
            BtnGetStarted.Text = "Setting up...";

            // 1. Language Preference
            Preferences.Set(AppPreferenceKeys.PreferredLanguage, _selectedLanguage);

            // 2. Notification Preferences & System Permission Request
            var wantsNotifications = SwitchMorningBriefing.IsToggled ||
                                     SwitchAudioBriefings.IsToggled ||
                                     SwitchKeywordAlerts.IsToggled;

            if (wantsNotifications)
            {
                // Prompts user with native Android 13+ / iOS permission dialog
                var granted = await _notificationService.RequestPermissionAsync();
                Debug.WriteLine($"[Onboarding] Notification permission granted: {granted}");
            }

            var morningTime = TimePickerMorning.Time ?? new TimeSpan(7, 30, 0);

            NotificationPreferences.MorningBriefingEnabled = SwitchMorningBriefing.IsToggled;
            NotificationPreferences.MorningBriefingTime = morningTime;
            NotificationPreferences.AudioBriefingsEnabled = SwitchAudioBriefings.IsToggled;
            NotificationPreferences.KeywordAlertsEnabled = SwitchKeywordAlerts.IsToggled;
            NotificationPreferences.MonitoredKeywords = _selectedKeywords.ToList();

            // Schedule alarms if enabled
            if (SwitchMorningBriefing.IsToggled)
            {
                _notificationService.ScheduleDailyMorningBriefing(morningTime);
            }
            else
            {
                _notificationService.CancelDailyMorningBriefing();
            }

            if (SwitchAudioBriefings.IsToggled)
            {
                _notificationService.ScheduleAudioBriefings();
            }
            else
            {
                _notificationService.CancelAudioBriefings();
            }

            if (SwitchKeywordAlerts.IsToggled)
            {
                _backgroundSyncService.ScheduleNewsSync(immediate: false);
            }
            else
            {
                _backgroundSyncService.CancelNewsSync();
            }

            // 3. Mark Onboarding as Completed
            Preferences.Set(AppPreferenceKeys.HasSeenOnboarding, true);
            Preferences.Set(AppPreferenceKeys.DailyReminderEnabled, SwitchMorningBriefing.IsToggled);

            SemanticScreenReader.Announce($"Welcome to News Stand NG! Setup complete.");

            // 4. Transition root to AppShell
            var shell = _serviceProvider.GetRequiredService<AppShell>();
            if (Application.Current?.Windows.Count > 0)
            {
                Application.Current.Windows[0].Page = shell;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Onboarding] Error completing setup: {ex.Message}");
            BtnGetStarted.IsEnabled = true;
            BtnGetStarted.Text = "Get Started";
        }
    }
}
