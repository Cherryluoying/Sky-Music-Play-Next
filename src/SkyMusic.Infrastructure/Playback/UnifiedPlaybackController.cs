// 模块：SkyMusic.Infrastructure 音频、MIDI 与乐谱统一播放控制器
using System.Diagnostics;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;
using SkyMusic.Core.Plugins;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.Infrastructure.Playback;

public sealed class UnifiedPlaybackController : IPlaybackController, IMidiPlaybackOutputControl, IAudioVolumeControl
{
    private const string MediaAlias = "skymusic_media";
    private readonly object _gate = new();
    private readonly object _nativeGate = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly PreviewPlaybackController _preview = new();
    private readonly IScorePlaybackController _score;
    private readonly FfmpegAudioPlayer? _audio;
    private readonly Timer _timer;
    private readonly IInstrumentPluginHost? _instrumentHost;
    private readonly IMciCommands _mci;
    private bool _nativeOpened;
    private bool _externalSequenceReady;
    private IMidiSequenceHost? SequenceHost => _instrumentHost as IMidiSequenceHost;
    private bool HasExternalInstrument => _useExternalInstrument &&
        _instrumentHost?.Snapshot.State == InstrumentHostState.Loaded && SequenceHost is not null;
    private PlaybackBackend _backend;
    private MusicTrack? _track;
    private PlaybackState _nativeState = PlaybackState.Stopped;
    private TimeSpan _nativeDuration;
    // 某些 Windows MCI sequencer 驱动无法返回 position，使用单调时钟补足进度。
    private TimeSpan _nativePosition;
    private TimeSpan _nativeClockBase;
    private long _nativeClockStart;
    private bool _nativeClockRunning;
    private bool _useExternalInstrument;
    private bool _disposed;

    public UnifiedPlaybackController(IScorePlaybackController score, string? ffmpegPath = null,
        IInstrumentPluginHost? instrumentHost = null)
        : this(score, ffmpegPath, instrumentHost, new MciCommandDispatcher())
    {
    }

    internal UnifiedPlaybackController(IScorePlaybackController score, string? ffmpegPath,
        IInstrumentPluginHost? instrumentHost, IMciCommands mci)
    {
        _score = score;
        _mci = mci;
        _instrumentHost = instrumentHost;
        _audio = string.IsNullOrWhiteSpace(ffmpegPath) ? null : new FfmpegAudioPlayer(ffmpegPath);
        _preview.SnapshotChanged += OnPreviewChanged;
        _score.Changed += OnScoreChanged;
        // 进度目标 60 FPS；MIDI 音频事件由宿主音频线程调度，与此刷新定时器无关。
        _timer = new Timer(_ => PublishTimedBackend(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1d / 60));
    }

    public event EventHandler<PlaybackSnapshot>? SnapshotChanged;

    public double Volume
    {
        get => _audio?.Volume ?? 1;
        set { if (_audio is not null) _audio.Volume = value; }
    }

    // 音源交接与播放/换曲共用操作锁；加载耗时不计入音乐时间线。
    public async ValueTask LoadInstrumentAsync(InstrumentPluginInfo plugin, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (SequenceHost is null || _backend != PlaybackBackend.Native ||
                _track?.Kind != MediaKind.Midi || _track.SourcePath is null)
                return;
            bool playing;
            TimeSpan position;
            lock (_nativeGate)
            {
                playing = _nativeState == PlaybackState.Playing;
                position = ReadNativeClockPosition();
                _nativePosition = position;
                _nativeClockRunning = false;
                _nativeState = PlaybackState.Paused;
            }
            // 先暂停旧插件；系统设备则必须成功关闭，不能只发送忽略错误的 stop。
            if (HasExternalInstrument)
                await SequenceHost.SetTransportAsync(false, position, cancellationToken).ConfigureAwait(false);
            lock (_nativeGate) CloseNativeMediaCore();
            _useExternalInstrument = true;
            _externalSequenceReady = false;
            PublishNativeTick();
            await _instrumentHost!.LoadAsync(plugin, cancellationToken).ConfigureAwait(false);
            await SequenceHost.LoadSequenceAsync(_track.SourcePath, cancellationToken).ConfigureAwait(false);
            await SequenceHost.SetTransportAsync(playing, position, cancellationToken).ConfigureAwait(false);
            _externalSequenceReady = true;
            lock (_nativeGate)
            {
                _nativeState = playing ? PlaybackState.Playing : PlaybackState.Paused;
                if (playing) StartNativeClock(position);
            }
            PublishNativeTick();
        }
        catch
        {
            // 激活失败时保持明确的暂停状态，不让视觉时间线继续走但没有声音。
            lock (_nativeGate)
            {
                _nativeState = PlaybackState.Paused;
                _nativeClockRunning = false;
            }
            // 失败后不自动恢复内置音源；插件未准备好时播放必须报错，不能静默走另一条路。
            _externalSequenceReady = false;
            // 命令取消也可能发生在宿主已开始播放之后，清理不能沿用已取消的令牌。
            PublishNativeTick();
            if (_useExternalInstrument && _instrumentHost is not null)
                await _instrumentHost.UnloadAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally { _operationGate.Release(); }
    }

    public PlaybackSnapshot Snapshot { get; private set; } =
        new(null, PlaybackState.Stopped, TimeSpan.Zero, TimeSpan.Zero);

    public bool UseExternalInstrument => _useExternalInstrument;

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
                // 时长包含速度变化和尾部休止，不使用游戏乐谱筛选后的音符估算。
                var data = await Task.Run(() => MidiSequenceReader.Read(track.SourcePath, cancellationToken), cancellationToken).ConfigureAwait(false);
                track = track with { Duration = data.Duration };
                _track = track;
                if (_useExternalInstrument)
                {
                    if (!HasExternalInstrument)
                        throw new InvalidOperationException("插件音源不可用，请重新加载音色。");
                    await SequenceHost!.LoadSequenceAsync(track.SourcePath, cancellationToken).ConfigureAwait(false);
                    _externalSequenceReady = true;
                    LoadNativeMedia(track, false);
                    await SequenceHost.SetTransportAsync(autoplay, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
                    lock (_nativeGate)
                    {
                        _nativeState = autoplay ? PlaybackState.Playing : PlaybackState.Paused;
                        if (autoplay) StartNativeClock(TimeSpan.Zero);
                    }
                    PublishNativeTick();
                }
                else
                {
                    _useExternalInstrument = false;
                    LoadNativeMedia(track, autoplay);
                }
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
                    if (_nativeState == PlaybackState.Playing) break;
                    if (_nativeDuration > TimeSpan.Zero && _nativePosition >= _nativeDuration)
                        _nativePosition = TimeSpan.Zero;
                    if (_useExternalInstrument)
                        await RequireSequenceHost().SetTransportAsync(true, _nativePosition, cancellationToken).ConfigureAwait(false);
                    lock (_nativeGate)
                    {
                        if (_nativeState != PlaybackState.Playing)
                        {
                            StartNativeClock(_nativePosition);
                        }
                        if (!_useExternalInstrument)
                        {
                            Send($"play {MediaAlias}");
                        }
                        _nativeState = PlaybackState.Playing;
                    }
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
                    lock (_nativeGate)
                    {
                        if (_nativeState == PlaybackState.Playing)
                        {
                            _nativePosition = ReadNativeClockPosition();
                        }
                        if (!_useExternalInstrument)
                        {
                            Send($"pause {MediaAlias}");
                        }
                        _nativeState = PlaybackState.Paused;
                        _nativeClockRunning = false;
                    }
                    if (HasExternalInstrument)
                        await SequenceHost!.SetTransportAsync(false, _nativePosition, cancellationToken).ConfigureAwait(false);
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
                    if (_useExternalInstrument)
                        await RequireSequenceHost().SetTransportAsync(_nativeState == PlaybackState.Playing, bounded, cancellationToken).ConfigureAwait(false);
                    lock (_nativeGate)
                    {
                        var milliseconds = Math.Max(0, (long)bounded.TotalMilliseconds);
                        if (!_useExternalInstrument)
                        {
                            Send($"seek {MediaAlias} to {milliseconds}");
                        }
                        _nativePosition = bounded;
                        if (_nativeState == PlaybackState.Playing)
                        {
                            StartNativeClock(bounded);
                            if (!_useExternalInstrument) Send($"play {MediaAlias}");
                        }
                        else
                        {
                            _nativeClockRunning = false;
                        }
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
        lock (_nativeGate)
        {
            _backend = PlaybackBackend.Native;
            CloseNativeMediaCore();
            // 插件模式完全不打开系统 sequencer，避免隐藏的第二个播放设备。
            if (!_useExternalInstrument)
            {
                var escapedPath = track.SourcePath!.Replace("\"", "\"\"");
                Send($"open \"{escapedPath}\" type sequencer alias {MediaAlias}");
                _nativeOpened = true;
                Send($"set {MediaAlias} time format milliseconds");
            }
            _nativeDuration = track.Duration;
            if (_nativeDuration <= TimeSpan.Zero && _nativeOpened)
            {
                _nativeDuration = TimeSpan.FromMilliseconds(QueryLong($"status {MediaAlias} length"));
            }

            _nativePosition = TimeSpan.Zero;
            _nativeClockBase = TimeSpan.Zero;
            _nativeState = autoplay ? PlaybackState.Playing : PlaybackState.Paused;
            _nativeClockRunning = autoplay;
            if (autoplay)
            {
                _nativeClockStart = Stopwatch.GetTimestamp();
            }
            Snapshot = new PlaybackSnapshot(track, _nativeState, TimeSpan.Zero, _nativeDuration);
            if (autoplay)
            {
                if (!_useExternalInstrument)
                {
                    Send($"play {MediaAlias}");
                }
            }
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
                if (HasExternalInstrument)
                    await SequenceHost!.SetTransportAsync(false, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
                _externalSequenceReady = false;
                CloseNativeMedia();
                _nativeState = PlaybackState.Stopped;
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

            TimeSpan position;
            lock (_nativeGate)
            {
                // MCI sequencer 在暂停/停止后经常返回 0，保留我们记录的暂停位置。
                if (!_nativeClockRunning && _nativeState != PlaybackState.Playing)
                {
                    position = _nativePosition;
                }
                else
                {
                    // MCI sequencer 的 status position 可能滞后或使用驱动私有单位，
                    // 播放期间只使用单调时钟，避免暂停时位置被回写到更早的位置。
                    position = ReadNativeClockPosition();
                }
                _nativePosition = position;
            }
            if (_nativeDuration > TimeSpan.Zero && position >= _nativeDuration)
            {
                position = _nativeDuration;
                _nativeState = PlaybackState.Stopped;
                _nativeClockRunning = false;
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

    private void Send(string command) => _mci.Execute(command);

    private long QueryLong(string command)
    {
        return long.TryParse(_mci.Execute(command), out var value) ? value : 0;
    }

    // 插件加载失败时不允许恢复过期时间线，也不回退到系统音源。
    private IMidiSequenceHost RequireSequenceHost() => HasExternalInstrument && _externalSequenceReady
        ? SequenceHost!
        : throw new InvalidOperationException("插件音源尚未准备好，请重新加载音色。");

    private void CloseNativeMedia()
    {
        lock (_nativeGate)
        {
            _nativeClockRunning = false;
            CloseNativeMediaCore();
        }
    }

    private void StartNativeClock(TimeSpan position)
    {
        _nativePosition = position;
        _nativeClockBase = position;
        _nativeClockStart = Stopwatch.GetTimestamp();
        _nativeClockRunning = true;
    }

    private TimeSpan ReadNativeClockPosition()
    {
        if (!_nativeClockRunning)
        {
            return _nativePosition;
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - _nativeClockStart;
        return _nativeClockBase + TimeSpan.FromSeconds(elapsedTicks / (double)Stopwatch.Frequency);
    }

    private void CloseNativeMediaCore()
    {
        if (!_nativeOpened) return;
        // close 本身停止播放并释放设备；失败时保留标记，阻止新音源启动并允许重试。
        try { Send($"close {MediaAlias}"); }
        catch (IOException closeError)
        {
            // 设备释放失败也先尝试静音，保留原始错误交给界面显示。
            try { Send($"pause {MediaAlias}"); }
            catch (IOException pauseError) { throw new AggregateException(closeError, pauseError); }
            throw;
        }
        _nativeOpened = false;
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
        try { CloseNativeMedia(); }
        finally { _mci.Dispose(); }
    }

    private enum PlaybackBackend
    {
        Preview,
        Audio,
        Native,
        Score
    }
}
