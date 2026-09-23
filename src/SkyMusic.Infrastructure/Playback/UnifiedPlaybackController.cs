// 模块：SkyMusic.Infrastructure 音频、MIDI 与乐谱统一播放控制器
using System.Runtime.InteropServices;
using System.Text;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Playback;

public sealed class UnifiedPlaybackController : IPlaybackController
{
    private const string MediaAlias = "skymusic_media";
    private readonly object _gate = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly PreviewPlaybackController _preview = new();
    private readonly IScorePlaybackController _score;
    private readonly FfmpegAudioPlayer? _audio;
    private readonly Timer _timer;
    private PlaybackBackend _backend;
    private MusicTrack? _track;
    private PlaybackState _nativeState = PlaybackState.Stopped;
    private TimeSpan _nativeDuration;
    private bool _disposed;

    public UnifiedPlaybackController(IScorePlaybackController score, string? ffmpegPath = null)
    {
        _score = score;
        _audio = string.IsNullOrWhiteSpace(ffmpegPath) ? null : new FfmpegAudioPlayer(ffmpegPath);
        _preview.SnapshotChanged += OnPreviewChanged;
        _score.Changed += OnScoreChanged;
        _timer = new Timer(_ => PublishTimedBackend(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    public event EventHandler<PlaybackSnapshot>? SnapshotChanged;

    public PlaybackSnapshot Snapshot { get; private set; } =
        new(null, PlaybackState.Stopped, TimeSpan.Zero, TimeSpan.Zero);

    // 根据媒体类型选择真实音频/MIDI 播放、自动演奏或演示计时后端。
    public async ValueTask LoadAsync(
        MusicTrack track,
        bool autoplay = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(track);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopActiveBackendAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _track = track;

            if (track.Kind == MediaKind.Score && !string.IsNullOrWhiteSpace(track.SourcePath))
            {
                _backend = PlaybackBackend.Score;
                var result = await _score.LoadAsync(track.SourcePath, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    throw new InvalidDataException(string.Join(" · ", result.Issues.Select(issue => issue.Message)));
                }

                if (autoplay)
                {
                    await _score.StartAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    PublishScore(_score.Snapshot);
                }
                return;
            }

            if (track.Kind == MediaKind.Audio && !string.IsNullOrWhiteSpace(track.SourcePath))
            {
                if (_audio is null)
                {
                    throw new InvalidOperationException("未找到 FFmpeg，请在设置中配置 FFmpeg 后再播放音频。");
                }
                _backend = PlaybackBackend.Audio;
                _audio.Load(track.SourcePath, track.Duration, autoplay);
                PublishAudioTick();
                return;
            }

            if (track.Kind == MediaKind.Midi &&
                !string.IsNullOrWhiteSpace(track.SourcePath) &&
                OperatingSystem.IsWindows())
            {
                LoadNativeMedia(track, autoplay);
                return;
            }

            _backend = PlaybackBackend.Preview;
            await _preview.LoadAsync(track, autoplay, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask PlayAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            switch (_backend)
            {
                case PlaybackBackend.Score:
                    await _score.StartAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case PlaybackBackend.Audio:
                    _audio!.Play();
                    PublishAudioTick();
                    break;
                case PlaybackBackend.Native:
                    Send($"play {MediaAlias}");
                    _nativeState = PlaybackState.Playing;
                    PublishNativeTick();
                    break;
                default:
                    await _preview.PlayAsync(cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            switch (_backend)
            {
                case PlaybackBackend.Score:
                    await _score.PauseAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case PlaybackBackend.Audio:
                    _audio!.Pause();
                    PublishAudioTick();
                    break;
                case PlaybackBackend.Native:
                    Send($"pause {MediaAlias}");
                    _nativeState = PlaybackState.Paused;
                    PublishNativeTick();
                    break;
                default:
                    await _preview.PauseAsync(cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bounded = position < TimeSpan.Zero
                ? TimeSpan.Zero
                : position > Snapshot.Duration ? Snapshot.Duration : position;
            switch (_backend)
            {
                case PlaybackBackend.Score:
                    await _score.SeekAsync(bounded.Ticks / 10, cancellationToken).ConfigureAwait(false);
                    break;
                case PlaybackBackend.Audio:
                    _audio!.Seek(bounded);
                    PublishAudioTick();
                    break;
                case PlaybackBackend.Native:
                    var milliseconds = Math.Max(0, (long)bounded.TotalMilliseconds);
                    Send($"seek {MediaAlias} to {milliseconds}");
                    if (_nativeState == PlaybackState.Playing)
                    {
                        Send($"play {MediaAlias}");
                    }
                    PublishNativeTick();
                    break;
                default:
                    await _preview.SeekAsync(bounded, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void LoadNativeMedia(MusicTrack track, bool autoplay)
    {
        _backend = PlaybackBackend.Native;
        CloseNativeMedia();
        var mediaType = track.Kind == MediaKind.Midi
            ? "sequencer"
            : Path.GetExtension(track.SourcePath!).Equals(".wav", StringComparison.OrdinalIgnoreCase)
                ? "waveaudio"
                : "mpegvideo";
        var escapedPath = track.SourcePath!.Replace("\"", "\"\"");
        Send($"open \"{escapedPath}\" type {mediaType} alias {MediaAlias}");
        Send($"set {MediaAlias} time format milliseconds");
        _nativeDuration = TimeSpan.FromMilliseconds(QueryLong($"status {MediaAlias} length"));
        if (_nativeDuration <= TimeSpan.Zero)
        {
            _nativeDuration = track.Duration;
        }

        _nativeState = autoplay ? PlaybackState.Playing : PlaybackState.Paused;
        Snapshot = new PlaybackSnapshot(track, _nativeState, TimeSpan.Zero, _nativeDuration);
        if (autoplay)
        {
            Send($"play {MediaAlias}");
        }
        SnapshotChanged?.Invoke(this, Snapshot);
    }

    private async ValueTask StopActiveBackendAsync(CancellationToken cancellationToken)
    {
        switch (_backend)
        {
            case PlaybackBackend.Score:
                await _score.StopAsync(cancellationToken).ConfigureAwait(false);
                break;
            case PlaybackBackend.Audio:
                _audio?.Stop();
                break;
            case PlaybackBackend.Native:
                CloseNativeMedia();
                break;
            case PlaybackBackend.Preview:
                await _preview.PauseAsync(cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private void PublishNativeTick()
    {
        lock (_gate)
        {
            if (_disposed || _backend != PlaybackBackend.Native || _track is null)
            {
                return;
            }

            var position = TimeSpan.FromMilliseconds(QueryLong($"status {MediaAlias} position", false));
            if (_nativeDuration > TimeSpan.Zero && position >= _nativeDuration)
            {
                position = _nativeDuration;
                _nativeState = PlaybackState.Stopped;
            }

            Snapshot = new PlaybackSnapshot(_track, _nativeState, position, _nativeDuration);
        }

        SnapshotChanged?.Invoke(this, Snapshot);
    }

    private void PublishAudioTick()
    {
        lock (_gate)
        {
            if (_disposed || _backend != PlaybackBackend.Audio || _track is null || _audio is null)
            {
                return;
            }

            _audio.CompleteIfNeeded();
            Snapshot = new PlaybackSnapshot(
                _track,
                _audio.State,
                _audio.Position,
                _audio.Duration > TimeSpan.Zero ? _audio.Duration : _track.Duration);
        }

        SnapshotChanged?.Invoke(this, Snapshot);
    }

    private void PublishTimedBackend()
    {
        if (_backend == PlaybackBackend.Audio)
        {
            PublishAudioTick();
        }
        else if (_backend == PlaybackBackend.Native)
        {
            PublishNativeTick();
        }
    }

    private void OnPreviewChanged(object? sender, PlaybackSnapshot snapshot)
    {
        if (_backend != PlaybackBackend.Preview)
        {
            return;
        }
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }

    private void OnScoreChanged(ScorePlaybackSnapshot snapshot)
    {
        if (_backend == PlaybackBackend.Score)
        {
            PublishScore(snapshot);
        }
    }

    private void PublishScore(ScorePlaybackSnapshot snapshot)
    {
        var state = snapshot.Session.State switch
        {
            AutoPlayState.Playing => PlaybackState.Playing,
            AutoPlayState.Paused or AutoPlayState.Ready => PlaybackState.Paused,
            _ => PlaybackState.Stopped
        };
        Snapshot = new PlaybackSnapshot(
            _track,
            state,
            TimeSpan.FromTicks(snapshot.Session.PositionMicroseconds * 10),
            TimeSpan.FromTicks(snapshot.Session.DurationMicroseconds * 10));
        SnapshotChanged?.Invoke(this, Snapshot);
    }

    private static void Send(string command, bool throwOnError = true)
    {
        var error = MciSendString(command, null, 0, IntPtr.Zero);
        if (error != 0 && throwOnError)
        {
            var message = new StringBuilder(256);
            MciGetErrorString(error, message, message.Capacity);
            throw new IOException($"媒体播放失败：{message}");
        }
    }

    private static long QueryLong(string command, bool throwOnError = true)
    {
        var buffer = new StringBuilder(64);
        var error = MciSendString(command, buffer, buffer.Capacity, IntPtr.Zero);
        if (error != 0)
        {
            if (throwOnError)
            {
                var message = new StringBuilder(256);
                MciGetErrorString(error, message, message.Capacity);
                throw new IOException($"媒体状态读取失败：{message}");
            }
            return 0;
        }
        return long.TryParse(buffer.ToString(), out var value) ? value : 0;
    }

    private static void CloseNativeMedia()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        Send($"stop {MediaAlias}", false);
        Send($"close {MediaAlias}", false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _timer.Dispose();
        _preview.SnapshotChanged -= OnPreviewChanged;
        _score.Changed -= OnScoreChanged;
        _preview.Dispose();
        _audio?.Dispose();
        CloseNativeMedia();
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
    private static extern int MciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr callback);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciGetErrorStringW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MciGetErrorString(int errorCode, StringBuilder errorText, int errorTextSize);

    private enum PlaybackBackend
    {
        Preview,
        Audio,
        Native,
        Score
    }
}
