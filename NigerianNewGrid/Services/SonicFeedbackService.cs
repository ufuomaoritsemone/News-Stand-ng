using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace NigerianNewGrid.Services;

public class SonicFeedbackService : ISonicFeedbackService
{
    private const string SonicEnabledPreferenceKey = "sonic_feedback_enabled";

    public bool IsEnabled
    {
        get => Preferences.Get(SonicEnabledPreferenceKey, defaultValue: true);
        set => Preferences.Set(SonicEnabledPreferenceKey, value);
    }

    private static readonly TimeSpan MinChimeInterval = TimeSpan.FromSeconds(3);
    private static DateTime _lastChimeTimeUtc = DateTime.MinValue;
    private static readonly object _playerLock = new();
#if ANDROID
    private static Android.Media.MediaPlayer? _currentPlayer;
#endif

    public async Task PlayRefreshChimeAsync()
    {
        lock (_playerLock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastChimeTimeUtc < MinChimeInterval)
            {
                System.Diagnostics.Debug.WriteLine("[SonicFeedback] Debounced refresh chime (cooldown active).");
                return;
            }
            _lastChimeTimeUtc = now;
        }

        await PlaySoundAsync(SonicSound.NewspaperHornRefresh);
    }

    public async Task PlaySoundAsync(SonicSound sound)
    {
        if (!IsEnabled) return;

        // Perform subtle tactile haptic feedback in parallel
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Haptics unavailable on target hardware — ignore gracefully
        }

        var assetName = sound switch
        {
            SonicSound.NewspaperHornRefresh => "NewspaperHorn.mp3",
            _ => "NewspaperHorn.mp3"
        };

        try
        {
#if ANDROID
            await PlayAndroidAssetAsync(assetName);
#elif IOS || MACCATALYST
            await PlayAppleAssetAsync(assetName);
#elif WINDOWS
            await PlayWindowsAssetAsync(assetName);
#else
            await Task.CompletedTask;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SonicFeedback] Audio playback exception: {ex.Message}");
        }
    }

#if ANDROID
    private static async Task PlayAndroidAssetAsync(string assetName)
    {
        await Task.Run(() =>
        {
            lock (_playerLock)
            {
                try
                {
                    if (_currentPlayer != null)
                    {
                        try
                        {
                            if (_currentPlayer.IsPlaying)
                            {
                                _currentPlayer.Stop();
                            }
                            _currentPlayer.Reset();
                            _currentPlayer.Release();
                            _currentPlayer.Dispose();
                        }
                        catch { }
                        _currentPlayer = null;
                    }

                    var context = Android.App.Application.Context;
                    var afd = context.Assets?.OpenFd(assetName);
                    if (afd == null) return;

                    var player = new Android.Media.MediaPlayer();
                    player.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
                    player.Prepare();
                    player.Start();
                    _currentPlayer = player;

                    player.Completion += (_, _) =>
                    {
                        lock (_playerLock)
                        {
                            try
                            {
                                player.Release();
                                player.Dispose();
                            }
                            catch { }
                            if (_currentPlayer == player)
                            {
                                _currentPlayer = null;
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SonicFeedback] Android player failed: {ex.Message}");
                }
            }
        });
    }
#endif

#if IOS || MACCATALYST
    private static async Task PlayAppleAssetAsync(string assetName)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(assetName);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var data = Foundation.NSData.FromArray(ms.ToArray());
            var player = AVFoundation.AVAudioPlayer.FromData(data);
            if (player != null)
            {
                player.Play();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SonicFeedback] iOS/Mac player failed: {ex.Message}");
        }
    }
#endif

#if WINDOWS
    private static async Task PlayWindowsAssetAsync(string assetName)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(assetName);
            var randomAccessStream = stream.AsRandomAccessStream();
            var mediaPlayer = new Windows.Media.Playback.MediaPlayer
            {
                Source = Windows.Media.Core.MediaSource.CreateFromStream(randomAccessStream, "audio/mpeg")
            };
            mediaPlayer.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SonicFeedback] Windows player failed: {ex.Message}");
        }
    }
#endif
}
