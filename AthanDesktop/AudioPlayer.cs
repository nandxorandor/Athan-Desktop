using System.IO;
using NAudio.Wave;

namespace AthanDesktop;

/// <summary>
/// Plays one athan at a time, from an embedded resource or a file the user
/// picked. NAudio rather than WPF's MediaPlayer because MediaPlayer can only
/// open a URI - it cannot play a stream, and the recordings live inside the exe
/// with no file to point at.
/// </summary>
public class AudioPlayer : IDisposable
{
    private WaveOutEvent? _output;
    private Mp3FileReader? _reader;
    private Stream? _source;
    private readonly object _lock = new();

    /// <summary>Raised on the thread pool when playback ends by itself.</summary>
    public event Action? Finished;

    public bool IsPlaying
    {
        get { lock (_lock) return _output?.PlaybackState == PlaybackState.Playing; }
    }

    /// <summary>Starts <paramref name="stream"/>, replacing anything already playing.</summary>
    public void Play(Stream stream, int volumePercent)
    {
        lock (_lock)
        {
            StopLocked();
            try
            {
                _source = stream;
                _reader = new Mp3FileReader(stream);
                _output = new WaveOutEvent { Volume = Math.Clamp(volumePercent / 100f, 0f, 1f) };
                _output.Init(_reader);
                _output.PlaybackStopped += OnStopped;
                _output.Play();
            }
            catch
            {
                // A corrupt or unreadable file must not take the app down; the
                // prayer time itself has already been announced by the window.
                StopLocked();
            }
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        // Fires for a natural end and for an explicit Stop alike. Stop() detaches
        // this handler first, so reaching here means the recording ran out.
        Finished?.Invoke();
    }

    public void Stop()
    {
        lock (_lock) StopLocked();
    }

    private void StopLocked()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnStopped;
            try { _output.Stop(); } catch { /* already gone */ }
            _output.Dispose();
            _output = null;
        }
        _reader?.Dispose();
        _reader = null;
        _source?.Dispose();
        _source = null;
    }

    /// <summary>Live volume change, so the settings slider is audible as it moves.</summary>
    public void SetVolume(int percent)
    {
        lock (_lock)
        {
            if (_output is not null) _output.Volume = Math.Clamp(percent / 100f, 0f, 1f);
        }
    }

    public void Dispose() => Stop();
}
