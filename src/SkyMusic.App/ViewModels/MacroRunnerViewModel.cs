// 模块：SkyMusic.App 界面状态 MacroRunnerViewModel
using Avalonia.Threading;
using SkyMusic.Core.Automation;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class MacroRunnerViewModel : ObservableObject, IDisposable
{
    private readonly IMacroScriptImporter _importer;
    private readonly IMacroPlaybackSession _session;
    private readonly IGameWindowService _windowService;
    private IReadOnlyList<GameWindowInfo> _windows = [];
    private GameWindowInfo? _selectedWindow;
    private string _scriptName = "尚未导入宏脚本";
    private string _statusText = "请选择原版 key/type/delay JSON 宏脚本";
    private int _eventCount;
    private double _speed = 1;
    private bool _hasScript;
    private bool _isPlaying;

    public MacroRunnerViewModel(
        IMacroScriptImporter importer,
        IMacroPlaybackSession session,
        IGameWindowService windowService)
    {
        _importer = importer;
        _session = session;
        _windowService = windowService;
        RefreshWindowsCommand = new AsyncRelayCommand(_ => RefreshWindowsAsync(), onError: SetError);
        StartPauseCommand = new AsyncRelayCommand(_ => TogglePlaybackAsync(), _ => HasScript && SelectedWindow is not null, SetError);
        StopCommand = new AsyncRelayCommand(_ => _session.StopAsync().AsTask(), _ => HasScript, SetError);
        SpeedDownCommand = new AsyncRelayCommand(_ => ChangeSpeedAsync(-0.1), _ => Speed > 0.5, SetError);
        SpeedUpCommand = new AsyncRelayCommand(_ => ChangeSpeedAsync(0.1), _ => Speed < 2, SetError);
        _session.Changed += OnSessionChanged;
        RefreshWindowsCommand.Execute(null);
    }

    public string Title => "宏脚本";
    public AsyncRelayCommand RefreshWindowsCommand { get; }
    public AsyncRelayCommand StartPauseCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand SpeedDownCommand { get; }
    public AsyncRelayCommand SpeedUpCommand { get; }
    public IReadOnlyList<GameWindowInfo> Windows => _windows;

    public GameWindowInfo? SelectedWindow
    {
        get => _selectedWindow;
        set
        {
            if (SetProperty(ref _selectedWindow, value))
            {
                StartPauseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ScriptName
    {
        get => _scriptName;
        private set => SetProperty(ref _scriptName, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public int EventCount
    {
        get => _eventCount;
        private set => SetProperty(ref _eventCount, value);
    }

    public bool HasScript
    {
        get => _hasScript;
        private set
        {
            if (SetProperty(ref _hasScript, value))
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
                OnPropertyChanged(nameof(PlayLabel));
            }
        }
    }

    public string PlayLabel => IsPlaying ? "暂停" : "开始";

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

    // 导入安全宏脚本并准备播放会话
    public async Task ImportAsync(string filePath)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
        var result = await _importer.ImportAsync(stream, Path.GetFileName(filePath));
        if (!result.IsSuccess || result.Script is null)
        {
            HasScript = false;
            StatusText = string.Join(" · ", result.Errors);
            return;
        }

        await _session.LoadAsync(result.Script);
        ScriptName = result.Script.Name;
        EventCount = result.Script.Events.Count;
        HasScript = true;
        StatusText = "宏脚本已就绪，开始前请确认目标窗口";
    }

    public void SetError(Exception exception) => StatusText = exception.Message;

    public void Dispose()
    {
        _session.Changed -= OnSessionChanged;
        _session.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task RefreshWindowsAsync()
    {
        _windows = await _windowService.GetAvailableWindowsAsync();
        OnPropertyChanged(nameof(Windows));
        SelectedWindow = _windows.FirstOrDefault();
    }

    // 根据会话状态开始或暂停宏播放
    private async Task TogglePlaybackAsync()
    {
        if (IsPlaying)
        {
            await _session.PauseAsync();
            return;
        }

        var window = SelectedWindow ?? throw new InvalidOperationException("请先选择目标窗口");
        if (!await _windowService.ActivateAsync(window.Handle))
        {
            throw new InvalidOperationException("无法激活目标窗口，请刷新后重试");
        }
        await _session.StartAsync();
    }

    private async Task ChangeSpeedAsync(double delta)
    {
        await _session.SetSpeedAsync(Math.Round(Math.Clamp(Speed + delta, 0.5, 2), 1));
    }

    private void OnSessionChanged(AutoPlaySnapshot snapshot) =>
        Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = snapshot.State == AutoPlayState.Playing;
            Speed = snapshot.Speed;
            StatusText = snapshot.State switch
            {
                AutoPlayState.Playing => "宏脚本正在执行",
                AutoPlayState.Paused => "宏脚本已暂停，按键已释放",
                AutoPlayState.Completed => "宏脚本执行完成",
                AutoPlayState.Faulted => snapshot.Error ?? "宏脚本执行失败",
                _ => StatusText
            };
            NotifyCommands();
        });

    private void NotifyCommands()
    {
        StartPauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        SpeedDownCommand.NotifyCanExecuteChanged();
        SpeedUpCommand.NotifyCanExecuteChanged();
    }
}
