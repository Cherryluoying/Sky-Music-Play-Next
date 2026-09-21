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
    private string _ffmpegPath;
    private string _pianoTransPath;
    private string _vst3SearchPaths;
    private int _audioBufferSize;
    private PerformanceModeOption _selectedPerformanceMode;
    private int _maximumConcurrency;
    private string _cloudServiceUrl;
    private int _networkTimeoutSeconds;
    private string _cacheDirectory;
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
        _ffmpegPath = settings.ExternalTools.FfmpegPath ?? string.Empty;
        _pianoTransPath = settings.ExternalTools.PianoTransPath ?? string.Empty;
        _vst3SearchPaths = string.Join(Environment.NewLine, settings.AudioPlugins.Vst3SearchPaths);
        _audioBufferSize = settings.AudioPlugins.BufferSize;
        _maximumConcurrency = settings.Performance.MaximumConcurrency;
        _cloudServiceUrl = settings.Network.CloudServiceUrl;
        _networkTimeoutSeconds = settings.Network.TimeoutSeconds;
        _cacheDirectory = settings.Storage.CacheDirectory ?? string.Empty;

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
        _settings = new AppSettings
        {
            SchemaVersion = _settings.SchemaVersion,
            General = new GeneralSettings
            {
                DefaultPlaybackTargetId = string.IsNullOrWhiteSpace(DefaultPlaybackTargetId) ? "sky-15" : DefaultPlaybackTargetId.Trim(),
                RememberLastTarget = RememberLastTarget
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
                CacheDirectory = NullIfWhiteSpace(CacheDirectory)
            }
        };

        await _store.SaveAsync(_settings);
        StatusText = "设置已保存，外部工具和网络配置将在下次启动时完全生效";
    }

    private void SetError(Exception exception) => StatusText = exception.Message;

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
