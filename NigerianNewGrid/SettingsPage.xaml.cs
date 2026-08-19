using System.ComponentModel;
using NigerianNewGrid.Services;

namespace NigerianNewGrid;

public partial class SettingsPage : ContentPage
{
    private readonly INotificationService _notificationService;
    private readonly Dictionary<string, (Label checkLabel, Label textLabel)> _langRows;
    private bool _isInitializing = true;

    private static readonly string[] PresetSuggestions =
    [
        "Naira",
        "Tinubu",
        "EFCC",
        "Fuel Price",
        "Super Eagles",
        "Tech/Startups",
        "CBN"
    ];

    public SettingsPage(INotificationService notificationService)
    {
        InitializeComponent();
        _notificationService = notificationService;

        // Map language names to their XAML named controls
        _langRows = new()
        {
            ["English"] = (CheckEnglish, LblEnglish),
            ["Yoruba"] = (CheckYoruba, LblYoruba),
            ["Igbo"] = (CheckIgbo, LblIgbo),
            ["Hausa"] = (CheckHausa, LblHausa),
        };

        Appearing += (_, _) =>
        {
            UpdateLanguageChecks();
            LoadNotificationSettings();
        };
    }

    private void UpdateLanguageChecks()
    {
        var current = Preferences.Get("preferred_language", "English");
        CurrentLanguageLabel.Text = $"Currently: {current}";

        foreach (var (lang, (check, lbl)) in _langRows)
        {
            var isSelected = string.Equals(lang, current, StringComparison.OrdinalIgnoreCase);
            check.IsVisible = isSelected;
            lbl.FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private void OnLanguageTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string lang) return;

        Preferences.Set("preferred_language", lang);
        Preferences.Set("has_seen_onboarding", true);
        UpdateLanguageChecks();
    }

    // ──────────────────────────────────────────────────────────
    // Notification & Keyword Alerts Management
    // ──────────────────────────────────────────────────────────

    private void LoadNotificationSettings()
    {
        _isInitializing = true;

        SwitchMorningBriefing.IsToggled = NotificationPreferences.MorningBriefingEnabled;
        MorningTimeContainer.IsVisible = NotificationPreferences.MorningBriefingEnabled;
        TimePickerMorning.Time = NotificationPreferences.MorningBriefingTime;

        SwitchKeywordAlerts.IsToggled = NotificationPreferences.KeywordAlertsEnabled;
        KeywordAlertsContainer.IsVisible = NotificationPreferences.KeywordAlertsEnabled;

        RenderActiveKeywordChips();
        RenderPresetSuggestions();

        _isInitializing = false;
    }

    private async void OnMorningBriefingToggled(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing) return;

        NotificationPreferences.MorningBriefingEnabled = e.Value;
        MorningTimeContainer.IsVisible = e.Value;

        if (e.Value)
        {
            var granted = await _notificationService.RequestPermissionAsync();
            if (granted)
            {
                _notificationService.ScheduleDailyMorningBriefing(NotificationPreferences.MorningBriefingTime);
            }
        }
        else
        {
            _notificationService.CancelDailyMorningBriefing();
        }
    }

    private void OnMorningTimeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isInitializing || e.PropertyName != nameof(TimePicker.Time)) return;

        if (TimePickerMorning.Time.HasValue)
        {
            var time = TimePickerMorning.Time.Value;
            NotificationPreferences.MorningBriefingTime = time;
            if (NotificationPreferences.MorningBriefingEnabled)
            {
                _notificationService.ScheduleDailyMorningBriefing(time);
            }
        }
    }

    private async void OnKeywordAlertsToggled(object? sender, ToggledEventArgs e)
    {
        if (_isInitializing) return;

        NotificationPreferences.KeywordAlertsEnabled = e.Value;
        KeywordAlertsContainer.IsVisible = e.Value;

        if (e.Value)
        {
            await _notificationService.RequestPermissionAsync();
        }
    }

    private void RenderActiveKeywordChips()
    {
        ActiveKeywordsFlexLayout.Children.Clear();
        var keywords = NotificationPreferences.MonitoredKeywords;

        LblEmptyKeywords.IsVisible = keywords.Count == 0;

        foreach (var keyword in keywords)
        {
            var chip = CreateActiveKeywordChip(keyword);
            ActiveKeywordsFlexLayout.Children.Add(chip);
        }
    }

    private View CreateActiveKeywordChip(string keyword)
    {
        var border = new Border
        {
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 0, 8, 8),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
            StrokeThickness = 1,
            Stroke = GetBrush("AccentBlueBrush", Color.FromArgb("#0066FF")),
            Background = GetBrush("AccentBlueLightBrush", Color.FromArgb("#EBF3FF"))
        };

        var stack = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center
        };

        var label = new Label
        {
            Text = keyword,
            FontFamily = "LegacySansBook",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetColor("AccentBlue", Color.FromArgb("#0066FF")),
            VerticalOptions = LayoutOptions.Center
        };

        var deleteBtn = new Label
        {
            Text = "✕",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetColor("Gray500", Color.FromArgb("#6C757D")),
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(4, 0, 0, 0)
        };

        var tapRecognizer = new TapGestureRecognizer();
        tapRecognizer.Tapped += (_, _) =>
        {
            NotificationPreferences.RemoveKeyword(keyword);
            RenderActiveKeywordChips();
            RenderPresetSuggestions();
        };

        deleteBtn.GestureRecognizers.Add(tapRecognizer);

        stack.Children.Add(label);
        stack.Children.Add(deleteBtn);
        border.Content = stack;

        return border;
    }

    private void RenderPresetSuggestions()
    {
        PresetSuggestionsFlexLayout.Children.Clear();
        var activeKeywords = NotificationPreferences.MonitoredKeywords;

        var availablePresets = PresetSuggestions
            .Where(p => !activeKeywords.Any(k => string.Equals(k, p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (availablePresets.Count == 0)
            return;

        foreach (var preset in availablePresets)
        {
            var btn = new Border
            {
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 6, 6),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(14) },
                StrokeThickness = 1,
                Stroke = GetBrush("Gray200Brush", Color.FromArgb("#D5E2D8")),
                Background = GetBrush("WhiteBrush", Colors.White)
            };

            var label = new Label
            {
                Text = $"+ {preset}",
                FontFamily = "LegacySansBook",
                FontSize = 12,
                TextColor = GetColor("Gray700Brush", Color.FromArgb("#334155")),
                VerticalOptions = LayoutOptions.Center
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                NotificationPreferences.AddKeyword(preset);
                RenderActiveKeywordChips();
                RenderPresetSuggestions();
            };

            btn.GestureRecognizers.Add(tap);
            btn.Content = label;

            PresetSuggestionsFlexLayout.Children.Add(btn);
        }
    }

    private void OnAddKeywordClicked(object? sender, EventArgs e)
    {
        var input = TxtNewKeyword.Text?.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        if (NotificationPreferences.AddKeyword(input))
        {
            TxtNewKeyword.Text = string.Empty;
            RenderActiveKeywordChips();
            RenderPresetSuggestions();
        }
    }

    private static Color GetColor(string resourceKey, Color fallbackColor)
    {
        if (Application.Current?.Resources != null &&
            Application.Current.Resources.TryGetValue(resourceKey, out var res))
        {
            if (res is Color color) return color;
            if (res is SolidColorBrush brush) return brush.Color;
        }

        return fallbackColor;
    }

    private static Brush GetBrush(string resourceKey, Color fallbackColor)
    {
        if (Application.Current?.Resources != null &&
            Application.Current.Resources.TryGetValue(resourceKey, out var res) &&
            res is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallbackColor);
    }
}
