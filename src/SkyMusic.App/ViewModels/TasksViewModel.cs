// 模块：SkyMusic.App 界面状态 TasksViewModel
using Avalonia.Threading;
using SkyMusic.Core.Importing;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class TasksViewModel : ObservableObject, IDisposable
{
    private readonly IScorePlaybackController _controller;
    private PlaybackTarget _selectedTarget;
    private GameWindowInfo? _selectedWindow;
    private string _scoreTitle = "尚未导入乐谱";
    private string _statusText = "请选择 Sky Studio TXT/JSON 或 MIDI 文件";
    private string _timeText = "00:00 / 00:00";
    private double _progress;
    private double _speed = 1;
    private int _noteCount;
    private bool _hasScore;
    private bool _isPlaying;
    private int _intervalAdjustment;
    private int _keyReleaseDelay;
    private CancellationTokenSource? _seekDebounce;

    public TasksViewModel(IScorePlaybackController controller)
    {
        _controller = controller;
        _selectedTarget = Targets.First(target => target.Id == controller.Snapshot.TargetId);

        StartPauseCommand = new AsyncRelayCommand(
            _ => TogglePlaybackAsync(),
            _ => HasScore,
            SetError);
        StopCommand = new AsyncRelayCommand(
            _ => _controller.StopAsync().AsTask(),
            _ => HasScore,
            SetError);
        SpeedDownCommand = new AsyncRelayCommand(
            _ => ChangeSpeedAsync(-0.1),
            _ => Speed > 0.5,
            SetError);
        SpeedUpCommand = new AsyncRelayCommand(
            _ => ChangeSpeedAsync(0.1),
            _ => Speed < 2,
            SetError);
        RefreshWindowsCommand = new AsyncRelayCommand(
            _ => RefreshWindowsAsync(),
            onError: SetError);
        IntervalDownCommand = new AsyncRelayCommand(
            _ => ChangeTimingAsync(-10, 0),
            _ => IntervalAdjustment > -200,
            SetError);
        IntervalUpCommand = new AsyncRelayCommand(
            _ => ChangeTimingAsync(10, 0),
            _ => IntervalAdjustment < 500,
            SetError);
        ReleaseDelayDownCommand = new AsyncRelayCommand(
            _ => ChangeTimingAsync(0, -10),
            _ => KeyReleaseDelay > -200,
            SetError);
        ReleaseDelayUpCommand = new AsyncRelayCommand(
            _ => ChangeTimingAsync(0, 10),
            _ => KeyReleaseDelay < 1_000,
            SetError);

        _controller.Changed += OnControllerChanged;
        ApplySnapshot(controller.Snapshot);
        RefreshWindowsCommand.Execute(null);
    }

    public string Title => "演奏任务";

    public IReadOnlyList<PlaybackTarget> Targets => _controller.Targets;

    public AsyncRelayCommand StartPauseCommand { get; }

    public AsyncRelayCommand StopCommand { get; }

    public AsyncRelayCommand SpeedDownCommand { get; }

    public AsyncRelayCommand SpeedUpCommand { get; }

    public AsyncRelayCommand RefreshWindowsCommand { get; }

    public AsyncRelayCommand IntervalDownCommand { get; }

    public AsyncRelayCommand IntervalUpCommand { get; }

    public AsyncRelayCommand ReleaseDelayDownCommand { get; }

    public AsyncRelayCommand ReleaseDelayUpCommand { get; }

    public PlaybackTarget SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (value is null || !SetProperty(ref _selectedTarget, value))
            {
                return;
            }

            _ = SelectTargetAsync(value.Id);
            OnPropertyChanged(nameof(TargetDescription));
            OnPropertyChanged(nameof(RequiresWindow));
        }
    }

    public string TargetDescription => SelectedTarget.Description;

    public bool RequiresWindow => SelectedTarget.Capabilities.HasFlag(PlaybackSinkCapabilities.ForegroundInput);

    public IReadOnlyList<GameWindowInfo> Windows => _controller.Windows;

    public GameWindowInfo? SelectedWindow
    {
        get => _selectedWindow;
        set
        {
            if (value is null || !SetProperty(ref _selectedWindow, value))
            {
                return;
            }

            try
            {
                _controller.SelectWindow(value.Handle);
            }
            catch (Exception exception)
            {
                SetError(exception);
            }
        }
    }

    public string ScoreTitle
    {
        get => _scoreTitle;
        private set => SetProperty(ref _scoreTitle, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (!HasScore || !SetProperty(ref _progress, Math.Clamp(value, 0, 100)))
            {
                return;
            }

            _seekDebounce?.Cancel();
            _seekDebounce?.Dispose();
            _seekDebounce = new CancellationTokenSource();
            _ = SeekAsync(_progress, _seekDebounce.Token);
        }
    }

    public double Speed
    {
        get => _speed;
        private set
        {
            if (SetProperty(ref _speed, value))
            {
                OnPropertyChanged(nameof(SpeedText));
            }
        }
    }

    public string SpeedText => $"{Speed:0.0}x";

    public int IntervalAdjustment
    {
        get => _intervalAdjustment;
        private set
        {
            if (SetProperty(ref _intervalAdjustment, value))
            {
                OnPropertyChanged(nameof(IntervalAdjustmentText));
            }
        }
    }

    public string IntervalAdjustmentText => FormatAdjustment(IntervalAdjustment);

    public int KeyReleaseDelay
    {
        get => _keyReleaseDelay;
        private set
        {
            if (SetProperty(ref _keyReleaseDelay, value))
            {
                OnPropertyChanged(nameof(KeyReleaseDelayText));
            }
        }
    }

    public string KeyReleaseDelayText => FormatAdjustment(KeyReleaseDelay);

    public int NoteCount
    {
        get => _noteCount;
        private set => SetProperty(ref _noteCount, value);
    }

    public bool HasScore
    {
        get => _hasScore;
        private set
        {
            if (SetProperty(ref _hasScore, value))
            {
                NotifyCommands();
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayGlyph));
                OnPropertyChanged(nameof(PlayLabel));
            }
        }
    }

    public string PlayGlyph => IsPlaying ? "\uE769" : "\uE768";

    public string PlayLabel => IsPlaying ? "暂停" : "开始";

    // 导入任务文件并同步可用播放目标
    public async Task ImportFileAsync(string filePath)
    {
        StatusText = "正在解析乐谱";
        var result = await _controller.LoadAsync(filePath);
        if (!result.IsSuccess)
        {
            StatusText = string.Join(" · ", result.Issues
                .Where(issue => issue.Severity == ScoreImportIssueSeverity.Error)
                .Select(issue => issue.Message));
        }
    }

    public void SetError(Exception exception) => StatusText = exception.Message;

    public void Dispose()
    {
        _controller.Changed -= OnControllerChanged;
        _seekDebounce?.Cancel();
        _seekDebounce?.Dispose();
    }

    // 根据当前会话状态执行播放或暂停
    private async Task TogglePlaybackAsync()
    {
        if (IsPlaying)
        {
            await _controller.PauseAsync();
        }
        else
        {
            // 游戏窗口可能是在打开任务页之后才启动；开始前再刷新一次目标句柄。
            if (RequiresWindow && _controller.SelectedWindow is null)
            {
                await RefreshWindowsAsync();
            }

            if (RequiresWindow && _controller.SelectedWindow is null)
            {
                StatusText = "没有找到目标游戏窗口，请先启动游戏并点击右侧刷新按钮后选择窗口";
                return;
            }

            await _controller.StartAsync();
        }
    }

    private async Task ChangeSpeedAsync(double delta)
    {
        var next = Math.Round(Math.Clamp(Speed + delta, 0.5, 2), 1);
        await _controller.SetSpeedAsync(next);
    }

    // 实时调整当前曲目的按键间隔与释放延迟
    private Task ChangeTimingAsync(int intervalDelta, int releaseDelayDelta)
    {
        var current = _controller.Snapshot.Timing;
        var timing = new ScoreTimingSettings(
            Math.Clamp(current.IntervalAdjustmentMilliseconds + intervalDelta, -200, 500),
            Math.Clamp(current.KeyReleaseDelayMilliseconds + releaseDelayDelta, -200, 1_000));
        return _controller.SetTimingAsync(timing).AsTask();
    }

    private async Task SelectTargetAsync(string targetId)
    {
        try
        {
            await _controller.SelectTargetAsync(targetId);
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var currentTarget = Targets.First(target => target.Id == _controller.Snapshot.TargetId);
                _selectedTarget = currentTarget;
                OnPropertyChanged(nameof(SelectedTarget));
                OnPropertyChanged(nameof(TargetDescription));
                SetError(exception);
            });
        }
    }

    private async Task RefreshWindowsAsync()
    {
        await _controller.RefreshWindowsAsync();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(Windows));
            _selectedWindow = _controller.SelectedWindow;
            OnPropertyChanged(nameof(SelectedWindow));
        });
    }

    private async Task SeekAsync(double progress, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(100, cancellationToken);
            var duration = _controller.Snapshot.Session.DurationMicroseconds;
            await _controller.SeekAsync((long)(duration * progress / 100), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetError(exception));
        }
    }

    private void OnControllerChanged(ScorePlaybackSnapshot snapshot) =>
        Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));

    // 将后端播放快照投影为界面状态
    private void ApplySnapshot(ScorePlaybackSnapshot snapshot)
    {
        ScoreTitle = snapshot.ScoreTitle ?? "尚未导入乐谱";
        NoteCount = snapshot.NoteCount;
        HasScore = snapshot.ScoreTitle is not null;
        IsPlaying = snapshot.Session.State == AutoPlayState.Playing;
        Speed = snapshot.Session.Speed;
        IntervalAdjustment = snapshot.Timing.IntervalAdjustmentMilliseconds;
        KeyReleaseDelay = snapshot.Timing.KeyReleaseDelayMilliseconds;
        OnPropertyChanged(nameof(Targets));
        var activeTarget = Targets.FirstOrDefault(target => target.Id == snapshot.TargetId);
        if (activeTarget is not null && !Equals(_selectedTarget, activeTarget))
        {
            _selectedTarget = activeTarget;
            OnPropertyChanged(nameof(SelectedTarget));
            OnPropertyChanged(nameof(TargetDescription));
            OnPropertyChanged(nameof(RequiresWindow));
        }
        OnPropertyChanged(nameof(Windows));
        if (!Equals(_selectedWindow, _controller.SelectedWindow))
        {
            _selectedWindow = _controller.SelectedWindow;
            OnPropertyChanged(nameof(SelectedWindow));
        }

        var progress = snapshot.Session.Progress * 100;
        if (Math.Abs(_progress - progress) >= 0.01)
        {
            _progress = progress;
            OnPropertyChanged(nameof(Progress));
        }

        TimeText = $"{FormatTime(snapshot.Session.PositionMicroseconds)} / {FormatTime(snapshot.Session.DurationMicroseconds)}";
        StatusText = snapshot.Session.State switch
        {
            AutoPlayState.Ready => snapshot.Warnings.Count == 0 ? "乐谱已就绪" : string.Join(" · ", snapshot.Warnings),
            AutoPlayState.Playing => "正在自动演奏",
            AutoPlayState.Paused => "已暂停",
            AutoPlayState.Completed => "演奏完成",
            AutoPlayState.Faulted => snapshot.Session.Error ?? "演奏失败",
            _ => "请选择 Sky Studio TXT/JSON 或 MIDI 文件"
        };
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        StartPauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        SpeedDownCommand.NotifyCanExecuteChanged();
        SpeedUpCommand.NotifyCanExecuteChanged();
        IntervalDownCommand.NotifyCanExecuteChanged();
        IntervalUpCommand.NotifyCanExecuteChanged();
        ReleaseDelayDownCommand.NotifyCanExecuteChanged();
        ReleaseDelayUpCommand.NotifyCanExecuteChanged();
    }

    private static string FormatAdjustment(int value) => $"{value:+0;-0;0} ms";

    private static string FormatTime(long microseconds) =>
        TimeSpan.FromTicks(microseconds * 10).ToString(microseconds >= 3_600_000_000 ? @"hh\:mm\:ss" : @"mm\:ss");
}
