// 模块：SkyMusic.App 界面状态 SettingsViewModel
using System.Collections.ObjectModel;
using SkyMusic.Core.Execution;
using SkyMusic.Core.Services;
using SkyMusic.Core.Settings;

namespace SkyMusic.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsStore _store;
    private readonly IFfmpegService _ffmpeg;
    private AppSettings _settings;
    private string _defaultPlaybackTargetId;
    private bool _rememberLastTarget;
    private bool _floatingWindowEnabled;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private string _ffmpegPath;
    private string _pianoTransPath;
    private string _vst3SearchPaths;
    private int _audioBufferSize;
    private PerformanceModeOption _selectedPerformanceMode;
    private int _maximumConcurrency;
    private string _cloudServiceUrl;
    private int _networkTimeoutSeconds;
    private string _cacheDirectory;
    private string _scoreLibraryDirectory;
    private string _midiLibraryDirectory;
    private string _ffmpegStatus;
    private string _statusText = "设置会保存到当前 Windows 用户目录";

    public SettingsViewModel(
        IAppSettingsStore store,
        AppSettings settings,
        IFfmpegService ffmpeg,
        Action openKeyMapping)
    {
        _store = store;
        _settings = settings;
        _ffmpeg = ffmpeg;
        _defaultPlaybackTargetId = settings.General.DefaultPlaybackTargetId;
        _rememberLastTarget = settings.General.RememberLastTarget;
        _floatingWindowEnabled = settings.General.FloatingWindowEnabled;
        _ffmpegPath = settings.ExternalTools.FfmpegPath ?? string.Empty;
        _pianoTransPath = settings.ExternalTools.PianoTransPath ?? string.Empty;
        _vst3SearchPaths = string.Join(Environment.NewLine, settings.AudioPlugins.Vst3SearchPaths);
        _audioBufferSize = settings.AudioPlugins.BufferSize;
        _maximumConcurrency = settings.Performance.MaximumConcurrency;
        _cloudServiceUrl = settings.Network.CloudServiceUrl;
        _networkTimeoutSeconds = settings.Network.TimeoutSeconds;
        _cacheDirectory = settings.Storage.CacheDirectory ?? string.Empty;
        _scoreLibraryDirectory = settings.Storage.ScoreLibraryDirectory ?? MediaLibraryDirectories.Score(new StorageSettings());
        _midiLibraryDirectory = settings.Storage.MidiLibraryDirectory ?? MediaLibraryDirectories.Midi(new StorageSettings());

        PerformanceModes =
        [
            new PerformanceModeOption("自动", WorkloadExecutionMode.Automatic),
            new PerformanceModeOption("单线程", WorkloadExecutionMode.SingleThread),
            new PerformanceModeOption("受控并行", WorkloadExecutionMode.Parallel)
        ];
        _selectedPerformanceMode = PerformanceModes.First(item => item.Mode == settings.Performance.Mode);

        var located = ffmpeg.Locate(_ffmpegPath);
        _ffmpegStatus = located is null ? "未找到 FFmpeg" : $"已发现 {located}";
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), onError: SetError);
        DetectFfmpegCommand = new AsyncRelayCommand(_ => DetectFfmpegAsync(), onError: SetError);
        OpenKeyMappingCommand = new RelayCommand(_ => openKeyMapping());
    }

    public ObservableCollection<PerformanceModeOption> PerformanceModes { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DetectFfmpegCommand { get; }
    public RelayCommand OpenKeyMappingCommand { get; }

    public bool FloatingWindowEnabled
    {
        get => _floatingWindowEnabled;
        set
        {
            if (SetProperty(ref _floatingWindowEnabled, value)) _ = SaveFloatingPreferenceAsync();
        }
    }

    // 开关立即生效且单独持久化，不顺带保存其他尚未提交的设置。
    private async Task SaveFloatingPreferenceAsync()
    {
        await _saveGate.WaitAsync();
        try
        {
            var current = await _store.LoadAsync();
            await _store.SaveAsync(current with
            {
                General = current.General with { FloatingWindowEnabled = FloatingWindowEnabled }
            });
        }
        catch (Exception exception) { SetError(exception); }
        finally { _saveGate.Release(); }
    }

    public void ReportLinkError() => StatusText = "无法打开浏览器，请访问 https://github.com/Cherryluoying/Sky-Music-Play-Next";

    public string DefaultPlaybackTargetId
    {
        get => _defaultPlaybackTargetId;
        set => SetProperty(ref _defaultPlaybackTargetId, value);
    }

    public bool RememberLastTarget
    {
        get => _rememberLastTarget;
        set => SetProperty(ref _rememberLastTarget, value);
    }

    public string FfmpegPath
    {
        get => _ffmpegPath;
        set => SetProperty(ref _ffmpegPath, value);
    }

    public string PianoTransPath
    {
        get => _pianoTransPath;
        set => SetProperty(ref _pianoTransPath, value);
    }

    public string Vst3SearchPaths
    {
        get => _vst3SearchPaths;
        set => SetProperty(ref _vst3SearchPaths, value);
    }

    public int AudioBufferSize
    {
        get => _audioBufferSize;
        set => SetProperty(ref _audioBufferSize, value);
    }

    public PerformanceModeOption SelectedPerformanceMode
    {
        get => _selectedPerformanceMode;
        set => SetProperty(ref _selectedPerformanceMode, value);
    }

    public int MaximumConcurrency
    {
        get => _maximumConcurrency;
        set => SetProperty(ref _maximumConcurrency, value);
    }

    public string CloudServiceUrl
    {
        get => _cloudServiceUrl;
        set => SetProperty(ref _cloudServiceUrl, value);
    }

    public int NetworkTimeoutSeconds
    {
        get => _networkTimeoutSeconds;
        set => SetProperty(ref _networkTimeoutSeconds, value);
    }

    public string CacheDirectory
    {
        get => _cacheDirectory;
        set => SetProperty(ref _cacheDirectory, value);
    }

    public string ScoreLibraryDirectory
    {
        get => _scoreLibraryDirectory;
        set => SetProperty(ref _scoreLibraryDirectory, value);
    }

    public string MidiLibraryDirectory
    {
        get => _midiLibraryDirectory;
        set => SetProperty(ref _midiLibraryDirectory, value);
    }

    public void ReportDirectoryError(string message) => StatusText = $"目录操作失败：{message}";

    // 打开目录使用界面当前值；未填时解析为默认分类目录。
    public string GetLibraryDirectory(bool midi) => midi
        ? MediaLibraryDirectories.Midi(new StorageSettings { MidiLibraryDirectory = MidiLibraryDirectory })
        : MediaLibraryDirectories.Score(new StorageSettings { ScoreLibraryDirectory = ScoreLibraryDirectory });

    public string FfmpegStatus
    {
        get => _ffmpegStatus;
        private set => SetProperty(ref _ffmpegStatus, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    // 探测 FFmpeg 并更新可用状态
    private async Task DetectFfmpegAsync()
    {
        FfmpegStatus = "正在检测 FFmpeg";
        var result = await _ffmpeg.ProbeAsync(NullIfWhiteSpace(FfmpegPath));
        if (result.IsAvailable && string.IsNullOrWhiteSpace(FfmpegPath))
        {
            FfmpegPath = result.ExecutablePath ?? string.Empty;
        }

        FfmpegStatus = result.IsAvailable
            ? $"{result.Message} · {result.Source}\n{result.Version}"
            : result.Message;
    }

    // 校验并持久化播放与外部工具设置
    private async Task SaveAsync()
    {
        if (!Uri.TryCreate(CloudServiceUrl, UriKind.Absolute, out var cloudUri) ||
            cloudUri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("云服务地址必须是有效的 HTTP 或 HTTPS 地址");
        }

        var vst3Paths = Vst3SearchPaths
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var scoreDirectory = GetLibraryDirectory(false);
        var midiDirectory = GetLibraryDirectory(true);
        Directory.CreateDirectory(scoreDirectory);
        Directory.CreateDirectory(midiDirectory);
        _settings = new AppSettings
        {
            SchemaVersion = _settings.SchemaVersion,
            General = new GeneralSettings
            {
                DefaultPlaybackTargetId = string.IsNullOrWhiteSpace(DefaultPlaybackTargetId) ? "sky-15" : DefaultPlaybackTargetId.Trim(),
                RememberLastTarget = RememberLastTarget,
                FloatingWindowEnabled = FloatingWindowEnabled
            },
            ExternalTools = new ExternalToolSettings
            {
                FfmpegPath = NullIfWhiteSpace(FfmpegPath),
                PianoTransPath = NullIfWhiteSpace(PianoTransPath)
            },
            AudioPlugins = new AudioPluginSettings
            {
                Vst3SearchPaths = vst3Paths,
                BufferSize = Math.Clamp(AudioBufferSize, 64, 4096)
            },
            Performance = new PerformanceSettings
            {
                Mode = SelectedPerformanceMode.Mode,
                MaximumConcurrency = Math.Clamp(MaximumConcurrency, 1, Math.Max(1, Environment.ProcessorCount))
            },
            Network = new NetworkSettings
            {
                CloudServiceUrl = cloudUri.ToString(),
                TimeoutSeconds = Math.Clamp(NetworkTimeoutSeconds, 1, 120)
            },
            Storage = new StorageSettings
            {
                CacheDirectory = NullIfWhiteSpace(CacheDirectory),
                ScoreLibraryDirectory = scoreDirectory,
                MidiLibraryDirectory = midiDirectory
            }
        };

        await _saveGate.WaitAsync();
        try
        {
            _settings = _settings with { General = _settings.General with { FloatingWindowEnabled = FloatingWindowEnabled } };
            await _store.SaveAsync(_settings);
        }
        finally { _saveGate.Release(); }
        ScoreLibraryDirectory = scoreDirectory;
        MidiLibraryDirectory = midiDirectory;
        StatusText = "设置已保存，曲谱 / MIDI 目录立即用于后续导入；外部工具和网络配置下次启动生效";
    }

    private void SetError(Exception exception) => StatusText = exception.Message;

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
