using System.IO;
using NAudio.Wave;

namespace AthanDesktop;

/// <summary>
/// The sound the pre-prayer heads-up makes, if the user has asked for one.
///
/// Deliberately separate from <see cref="AudioPlayer"/>, which is the athan's
/// and is MP3-only by design. The reminder is often something the user has
/// chosen themselves — a hadith urging people to come early to the row, a
/// du'aa — and that arrives as whatever format it happens to be, so this reads
/// through NAudio's general reader instead.
///
/// Every failure here is silent. A reminder that cannot find its sound should
/// still show its window; a missing file is not worth a dialog when the point
/// is that a prayer is coming.
/// </summary>
public static class ReminderSound
{
    private static WaveOutEvent? _output;
    private static AudioFileReader? _reader;
    private static readonly object Lock = new();

    /// <summary>
    /// Plays the configured sound once. Does nothing when the feature is off,
    /// which is its default.
    /// </summary>
    public static void Play(Settings settings)
    {
        if (!settings.ReminderSoundEnabled) return;

        var path = settings.ReminderSoundPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            // No file chosen, or it has been moved or deleted since. The
            // system's own notification sound is the honest fallback.
            System.Media.SystemSounds.Exclamation.Play();
            return;
        }

        lock (Lock)
        {
            Stop();
            try
            {
                _reader = new AudioFileReader(path)
                {
                    // The heads-up's own slider, not the athan's.
                    Volume = Math.Clamp(settings.ReminderVolume / 100f, 0f, 1f),
                };
                _output = new WaveOutEvent();
                _output.Init(_reader);
                _output.PlaybackStopped += (_, _) => Stop();
                _output.Play();
            }
            catch
            {
                // An unreadable or unsupported file: fall back rather than
                // leave the reminder mute with no explanation.
                Stop();
                System.Media.SystemSounds.Exclamation.Play();
            }
        }
    }

    /// <summary>Called when the window closes, so a long file cannot outlive it.</summary>
    public static void Stop()
    {
        lock (Lock)
        {
            try { _output?.Stop(); } catch { }
            _output?.Dispose();
            _reader?.Dispose();
            _output = null;
            _reader = null;
        }
    }
}
