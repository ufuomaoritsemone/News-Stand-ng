using Microsoft.Extensions.Logging;
using Microsoft.Maui.Accessibility;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;

namespace NigerianNewGrid.Services;

public class AudioBriefingCoordinator : IAudioBriefingCoordinator
{
    private readonly ITextToSpeechService _ttsService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AudioBriefingCoordinator> _logger;

    public bool IsSpeaking { get; private set; }
    public bool IsPaused { get; private set; }
    public string AudioButtonText { get; private set; } = "🎧";
    public string AudioButtonIcon { get; private set; } = "ic_headphones.png";
    public string AudioButtonHint { get; private set; } = "Play daily audio briefing";

    public event EventHandler? StateChanged;

    public AudioBriefingCoordinator(
        ITextToSpeechService ttsService,
        IAnalyticsService analyticsService,
        ILogger<AudioBriefingCoordinator> logger)
    {
        _ttsService = ttsService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task TogglePlayPauseAsync(IReadOnlyList<BriefingCategory> categories, string preferredLanguage)
    {
        // 1. If actively speaking -> Pause
        if (_ttsService.IsSpeaking && !_ttsService.IsPaused)
        {
            _ttsService.Pause();
            IsSpeaking = false;
            IsPaused = true;
            UpdateAudioButtonState();
            SemanticScreenReader.Announce("Daily briefing audio paused.");
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // 2. If paused -> Resume
        if (IsPaused || _ttsService.IsPaused)
        {
            IsSpeaking = true;
            IsPaused = false;
            UpdateAudioButtonState();
            SemanticScreenReader.Announce($"Resuming daily audio briefing in {preferredLanguage}…");
            StateChanged?.Invoke(this, EventArgs.Empty);

            var completed = await _ttsService.ResumeBriefingAsync(preferredLanguage);
            IsSpeaking = _ttsService.IsSpeaking;
            IsPaused = _ttsService.IsPaused;
            UpdateAudioButtonState();
            StateChanged?.Invoke(this, EventArgs.Empty);

            if (completed && !IsPaused)
            {
                _ = _analyticsService.TrackAsync("audio_listen_complete", null, $"Daily Audio Briefing ({preferredLanguage})", "Daily Briefing");
            }
            return;
        }

        // 3. Idle / Stopped -> Start Play from beginning
        var rawStories = categories
            .SelectMany(c => c.Top)
            .ToList();

        // Eliminate duplicate stories from competing news sources
        var topStories = StoryDeduplicationHelper.DeduplicateStories(
            rawStories,
            s => s.Title,
            s => s.Summary,
            s => s.Source)
            .Take(8)
            .ToList();

        if (topStories.Count == 0)
            return;

        IsSpeaking = true;
        IsPaused = false;
        UpdateAudioButtonState();
        SemanticScreenReader.Announce($"Playing daily audio briefing in {preferredLanguage}…");
        StateChanged?.Invoke(this, EventArgs.Empty);

        var firstStory = topStories.FirstOrDefault();
        _ = _analyticsService.TrackAsync("audio_listen_start", firstStory?.Id, $"Daily Audio Briefing ({preferredLanguage})", "Daily Briefing");

        var isFinished = await _ttsService.SpeakBriefingAsync(topStories, preferredLanguage);
        IsSpeaking = _ttsService.IsSpeaking;
        IsPaused = _ttsService.IsPaused;
        UpdateAudioButtonState();
        StateChanged?.Invoke(this, EventArgs.Empty);

        if (isFinished && !IsPaused)
        {
            _ = _analyticsService.TrackAsync("audio_listen_complete", firstStory?.Id, $"Daily Audio Briefing ({preferredLanguage})", "Daily Briefing");
        }
    }

    public void Cancel()
    {
        _ttsService.Cancel();
        IsSpeaking = false;
        IsPaused = false;
        UpdateAudioButtonState();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateAudioButtonState()
    {
        if (IsSpeaking)
        {
            AudioButtonText = "⏸";
            AudioButtonIcon = "ic_pause.png";
            AudioButtonHint = "Pause daily audio briefing";
        }
        else if (IsPaused)
        {
            AudioButtonText = "▶";
            AudioButtonIcon = "ic_play.png";
            AudioButtonHint = "Resume daily audio briefing";
        }
        else
        {
            AudioButtonText = "🎧";
            AudioButtonIcon = "ic_headphones.png";
            AudioButtonHint = "Play daily audio briefing";
        }
    }

    public void Dispose()
    {
        Cancel();
    }
}
