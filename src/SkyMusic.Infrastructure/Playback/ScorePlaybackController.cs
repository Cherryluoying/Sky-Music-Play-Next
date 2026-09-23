// 模块：SkyMusic.Infrastructure 播放领域 ScorePlaybackController
using SkyMusic.Core.Importing;
using SkyMusic.Core.Mapping;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Playback;

public sealed class ScorePlaybackController : IScorePlaybackController
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IScoreImportService _importer;
    private readonly IScoreTimelineCompiler _compiler;
    private readonly ITimelineScheduler _scheduler;
    private readonly IGameWindowService _windowService;
    private Dictionary<string, PlaybackTargetRegistration> _registrations;
    private readonly PlaybackEventMonitor? _monitor;

    private IPlaybackEventSink _sink;
    private IAutoPlaySession _session;
    private Score? _score;
    private CompiledTimeline? _timeline;
    private IReadOnlyList<string> _warnings = [];
    private string _targetId;
    private bool _disposed;
    private IReadOnlyList<GameWindowInfo> _windows = [];
    private ScoreTimingSettings _timing = ScoreTimingSettings.Default;

    public ScorePlaybackController(
        IScoreImportService importer,
        IScoreTimelineCompiler compiler,
        ITimelineScheduler scheduler,
        IGameWindowService windowService,
        IEnumerable<PlaybackTargetRegistration> targets,
        string defaultTargetId = "sky-15",
        PlaybackEventMonitor? monitor = null)
    {
        _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _monitor = monitor;
        ArgumentNullException.ThrowIfNull(targets);

        _registrations = targets.ToDictionary(item => item.Target.Id, StringComparer.OrdinalIgnoreCase);
        if (!_registrations.TryGetValue(defaultTargetId, out var registration))
        {
            throw new ArgumentException("Default playback target is not registered", nameof(defaultTargetId));
        }

        Targets = _registrations.Values.Select(item => item.Target).ToArray();
        _targetId = registration.Target.Id;
        _sink = CreateSink(registration);
        _session = CreateSession(_sink);
    }

    public IReadOnlyList<PlaybackTarget> Targets { get; private set; }

    public IReadOnlyList<GameWindowInfo> Windows => _windows;

    public GameWindowInfo? SelectedWindow { get; private set; }

    public ScorePlaybackSnapshot Snapshot => new(
        _score?.Title,
        _targetId,
        _score?.Notes.Count ?? 0,
        _session.Snapshot,
        _warnings,
        _timing);

    public event Action<ScorePlaybackSnapshot>? Changed;

    // 导入文件并用统一时间线装载当前乐谱
    public async ValueTask<ScoreImportResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var result = await _importer.ImportAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Score is null)
        {
            return result;
        }

        await LoadScoreCoreAsync(
            result.Score,
            result.Issues
                .Where(issue => issue.Severity == ScoreImportIssueSeverity.Warning)
                .Select(issue => issue.Message)
                .ToArray(),
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    public ValueTask LoadScoreAsync(Score score, CancellationToken cancellationToken = default)
    {
        // 工作区直接提交内存谱面 避免临时文件和二次解析
        ArgumentNullException.ThrowIfNull(score);
        return LoadScoreCoreAsync(score, [], cancellationToken);
    }

    private async ValueTask LoadScoreCoreAsync(
        Score score,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var timing = ScoreTimingSettings.Default;
        var timeline = _compiler.Compile(score, timing);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _session.LoadAsync(timeline, cancellationToken).ConfigureAwait(false);
            _score = score;
            _timeline = timeline;
            _timing = timing;
            _warnings = warnings;
        }
        finally
        {
            _gate.Release();
        }

        Publish();
    }

    // 切换播放目标并重新绑定对应事件输出器
    public async ValueTask SelectTargetAsync(string targetId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_registrations.TryGetValue(targetId, out var registration))
        {
            throw new ArgumentException("Playback target is not registered", nameof(targetId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.Equals(_targetId, registration.Target.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var oldSession = _session;
            var oldSink = _sink;
            await oldSession.StopAsync(cancellationToken).ConfigureAwait(false);

            var nextSink = CreateSink(registration);
            var nextSession = CreateSession(nextSink);
            if (_timeline is not null)
            {
                await nextSession.LoadAsync(_timeline, cancellationToken).ConfigureAwait(false);
            }

            _sink = nextSink;
            _session = nextSession;
            _targetId = registration.Target.Id;
            SelectPreferredWindow(registration.Target);

            oldSession.Changed -= OnSessionChanged;
            await oldSession.DisposeAsync().ConfigureAwait(false);
            (oldSink as IDisposable)?.Dispose();
        }
        finally
        {
            _gate.Release();
        }

        Publish();
    }

    public async ValueTask RefreshWindowsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var windows = await _windowService.GetAvailableWindowsAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _windows = windows;
            if (SelectedWindow is null || windows.All(window => window.Handle != SelectedWindow.Handle))
            {
                SelectPreferredWindow(_registrations[_targetId].Target);
            }
        }
        finally
        {
            _gate.Release();
        }

        Publish();
    }

    public void SelectWindow(long handle)
    {
        ThrowIfDisposed();
        SelectedWindow = _windows.FirstOrDefault(window => window.Handle == handle)
            ?? throw new ArgumentException("Game window is not available", nameof(handle));
        Publish();
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default) =>
        StartCoreAsync(cancellationToken);

    public ValueTask PauseAsync(CancellationToken cancellationToken = default) =>
        ExecuteSessionCommandAsync(session => session.PauseAsync(cancellationToken), cancellationToken);

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        ExecuteSessionCommandAsync(session => session.StopAsync(cancellationToken), cancellationToken);

    public ValueTask SeekAsync(long positionMicroseconds, CancellationToken cancellationToken = default) =>
        SeekCoreAsync(positionMicroseconds, cancellationToken);

    public ValueTask SetSpeedAsync(double speed, CancellationToken cancellationToken = default) =>
        SetSpeedCoreAsync(speed, cancellationToken);

    // 更新曲目专属间隔与释放延迟后重新编译时间线
    public async ValueTask SetTimingAsync(
        ScoreTimingSettings timing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timing);
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_timing == timing)
            {
                return;
            }

            if (_score is null || _timeline is null)
            {
                _timing = timing;
                Publish();
                return;
            }

            var previous = _session.Snapshot;
            var progress = previous.Progress;
            var nextTimeline = _compiler.Compile(_score, timing);

            // 重编译后按比例恢复位置，实时调整不会从头播放
            await _session.StopAsync(cancellationToken).ConfigureAwait(false);
            _timing = timing;
            _timeline = nextTimeline;
            await _session.LoadAsync(nextTimeline, cancellationToken).ConfigureAwait(false);

            var nextPosition = (long)(nextTimeline.DurationMicroseconds * progress);
            if (nextPosition > 0)
            {
                await _session.SeekAsync(nextPosition, cancellationToken).ConfigureAwait(false);
            }

            if (previous.State == AutoPlayState.Playing && nextPosition < nextTimeline.DurationMicroseconds)
            {
                await ActivateTargetAsync(cancellationToken).ConfigureAwait(false);
                await _session.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }

        Publish();
    }

    // 保存自定义键位后原地重建播放目标，保留当前乐谱和播放位置
    public async ValueTask ReloadCustomKeyMappingsAsync(
        IReadOnlyList<KeyMappingDefinition> mappings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        ThrowIfDisposed();
        var registrations = DefaultPlaybackTargets.Create(mappings)
            .ToDictionary(item => item.Target.Id, StringComparer.OrdinalIgnoreCase);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var oldSession = _session;
            var oldSink = _sink;
            var oldSnapshot = oldSession.Snapshot;
            await oldSession.StopAsync(cancellationToken).ConfigureAwait(false);

            _registrations = registrations;
            Targets = registrations.Values.Select(item => item.Target).ToArray();
            if (!registrations.ContainsKey(_targetId))
                _targetId = registrations.ContainsKey("sky-15") ? "sky-15" : registrations.Keys.First();

            var nextRegistration = registrations[_targetId];
            var nextSink = CreateSink(nextRegistration);
            var nextSession = CreateSession(nextSink);
            if (_timeline is not null)
            {
                await nextSession.LoadAsync(_timeline, cancellationToken).ConfigureAwait(false);
                if (oldSnapshot.PositionMicroseconds > 0)
                    await nextSession.SeekAsync(oldSnapshot.PositionMicroseconds, cancellationToken).ConfigureAwait(false);
            }

            _sink = nextSink;
            _session = nextSession;
            SelectPreferredWindow(nextRegistration.Target);
            oldSession.Changed -= OnSessionChanged;
            await oldSession.DisposeAsync().ConfigureAwait(false);
            (oldSink as IDisposable)?.Dispose();
        }
        finally
        {
            _gate.Release();
        }

        Publish();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Changed -= OnSessionChanged;
        await _session.DisposeAsync().ConfigureAwait(false);
        (_sink as IDisposable)?.Dispose();
        _gate.Dispose();
    }

    // 为当前输出目标创建独立播放会话
    private IAutoPlaySession CreateSession(IPlaybackEventSink sink)
    {
        var session = new AutoPlaySession(_scheduler, sink);
        session.Changed += OnSessionChanged;
        return session;
    }

    private IPlaybackEventSink CreateSink(PlaybackTargetRegistration registration)
    {
        var sink = registration.CreateSink();
        return _monitor is null ? sink : new ObservedPlaybackEventSink(sink, _monitor);
    }

    private void OnSessionChanged(AutoPlaySnapshot _) => Publish();

    private void Publish() => Changed?.Invoke(Snapshot);

    private async ValueTask StartCoreAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ActivateTargetAsync(cancellationToken).ConfigureAwait(false);
            await _session.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask SeekCoreAsync(long positionMicroseconds, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wasPlaying = _session.Snapshot.State == AutoPlayState.Playing;
            if (wasPlaying)
            {
                await _session.PauseAsync(cancellationToken).ConfigureAwait(false);
            }

            await _session.SeekAsync(positionMicroseconds, cancellationToken).ConfigureAwait(false);
            if (wasPlaying && positionMicroseconds < _session.Snapshot.DurationMicroseconds)
            {
                await ActivateTargetAsync(cancellationToken).ConfigureAwait(false);
                await _session.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask SetSpeedCoreAsync(double speed, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wasPlaying = _session.Snapshot.State == AutoPlayState.Playing;
            if (wasPlaying)
            {
                await _session.PauseAsync(cancellationToken).ConfigureAwait(false);
            }

            await _session.SetSpeedAsync(speed, cancellationToken).ConfigureAwait(false);
            if (wasPlaying)
            {
                await ActivateTargetAsync(cancellationToken).ConfigureAwait(false);
                await _session.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // 激活目标游戏窗口以保证扫描码发送到正确句柄
    private async ValueTask ActivateTargetAsync(CancellationToken cancellationToken)
    {
        var target = _registrations[_targetId].Target;
        if (!target.Capabilities.HasFlag(PlaybackSinkCapabilities.ForegroundInput))
        {
            return;
        }

        var window = SelectedWindow ?? throw new InvalidOperationException("请先选择游戏窗口");
        if (!await _windowService.ActivateAsync(window.Handle, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("无法激活游戏窗口，请刷新后重新选择");
        }
    }

    private void SelectPreferredWindow(PlaybackTarget target)
    {
        SelectedWindow = target.PreferredProcessNames is { Count: > 0 }
            ? _windows.FirstOrDefault(window => target.PreferredProcessNames.Contains(
                window.ProcessName,
                StringComparer.OrdinalIgnoreCase))
            : null;
    }

    private async ValueTask ExecuteSessionCommandAsync(
        Func<IAutoPlaySession, ValueTask> command,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await command(_session).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
