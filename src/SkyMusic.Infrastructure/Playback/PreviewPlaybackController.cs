// 模块：SkyMusic.Infrastructure 播放领域 PreviewPlaybackController
using System.Diagnostics;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Playback;

public sealed class PreviewPlaybackController : IPlaybackController
{
    private readonly object _gate = new();
    private readonly Stopwatch _clock = new();
    private readonly Timer _timer;
    private TimeSpan _basePosition;
    private PlaybackSnapshot _snapshot = new(null, PlaybackState.Stopped, TimeSpan.Zero, TimeSpan.Zero);

    public PreviewPlaybackController()
    {
        _timer = new Timer(_ => PublishTick(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
    }

    public event EventHandler<PlaybackSnapshot>? SnapshotChanged;

    public PlaybackSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public void Load(MusicTrack track, bool autoplay = false)
    {
        lock (_gate)
        {
            _clock.Reset();
            _basePosition = TimeSpan.Zero;
            _snapshot = new PlaybackSnapshot(
                track,
                autoplay ? PlaybackState.Playing : PlaybackState.Paused,
                TimeSpan.Zero,
                track.Duration);
            if (autoplay)
            {
                _clock.Start();
            }
        }

        Publish();
    }

    public void Play()
    {
        lock (_gate)
        {
            if (_snapshot.Track is null || _snapshot.State == PlaybackState.Playing)
            {
                return;
            }

            if (_snapshot.Position >= _snapshot.Duration)
            {
                _basePosition = TimeSpan.Zero;
            }

            _clock.Restart();
            _snapshot = _snapshot with { State = PlaybackState.Playing };
        }

        Publish();
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_snapshot.State != PlaybackState.Playing)
            {
                return;
            }

            _basePosition = CurrentPosition();
            _clock.Reset();
            _snapshot = _snapshot with { State = PlaybackState.Paused, Position = _basePosition };
        }

        Publish();
    }

    public void Seek(TimeSpan position)
    {
        lock (_gate)
        {
            var duration = _snapshot.Duration;
            _basePosition = position < TimeSpan.Zero
                ? TimeSpan.Zero
                : position > duration ? duration : position;
            if (_snapshot.State == PlaybackState.Playing)
            {
                _clock.Restart();
            }

            _snapshot = _snapshot with { Position = _basePosition };
        }

        Publish();
    }

    private void PublishTick()
    {
        PlaybackSnapshot? changed = null;
        lock (_gate)
        {
            if (_snapshot.State != PlaybackState.Playing)
            {
                return;
            }

            var position = CurrentPosition();
            if (position >= _snapshot.Duration)
            {
                position = _snapshot.Duration;
                _basePosition = position;
                _clock.Reset();
                _snapshot = _snapshot with { State = PlaybackState.Stopped, Position = position };
            }
            else
            {
                _snapshot = _snapshot with { Position = position };
            }

            changed = _snapshot;
        }

        SnapshotChanged?.Invoke(this, changed);
    }

    private TimeSpan CurrentPosition() => _basePosition + _clock.Elapsed;

    private void Publish() => SnapshotChanged?.Invoke(this, Snapshot);

    public void Dispose() => _timer.Dispose();
}
