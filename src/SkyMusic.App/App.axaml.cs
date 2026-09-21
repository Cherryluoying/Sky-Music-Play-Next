// 模块：SkyMusic.App 通用模型 App.axaml
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SkyMusic.App.Services;
using SkyMusic.App.ViewModels;
using SkyMusic.App.Views;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;
using SkyMusic.Core.Settings;
using SkyMusic.Infrastructure.Automation;
using SkyMusic.Infrastructure.Catalog;
using SkyMusic.Infrastructure.Input;
using SkyMusic.Infrastructure.Lyrics;
using SkyMusic.Infrastructure.Media;
using SkyMusic.Infrastructure.Playback;
using SkyMusic.Infrastructure.Plugins;
using SkyMusic.Infrastructure.Scores;
using SkyMusic.Infrastructure.Settings;
using SkyMusic.Infrastructure.Transcription;
using System.Text.Json;

namespace SkyMusic.App;

public sealed partial class App : Application
{
    private PreviewPlaybackController? _playbackController;
    private CoverImageService? _coverImages;
    private MainWindowViewModel? _mainViewModel;
    private IScorePlaybackController? _scorePlaybackController;
    private HttpClient? _cloudClient;
    private IKeyMappingStore? _keyMappingStore;
    private IAppSettingsStore? _appSettingsStore;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    // 组装服务依赖并创建主窗口或独立工作区
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _playbackController = new PreviewPlaybackController();
            _coverImages = new CoverImageService();
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SkyMusicPlay");
            _appSettingsStore = new JsonAppSettingsStore(Path.Combine(appData, "settings.json"));
            AppSettings appSettings;
            try
            {
                appSettings = _appSettingsStore.LoadAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is IOException or JsonException or ArgumentException)
            {
                // 设置损坏不能阻断应用启动
                appSettings = new AppSettings();
            }

            var cloudUrl = Environment.GetEnvironmentVariable("SKYMUSIC_CLOUD_URL");
            cloudUrl = string.IsNullOrWhiteSpace(cloudUrl) ? appSettings.Network.CloudServiceUrl : cloudUrl;
            if (!Uri.TryCreate(cloudUrl, UriKind.Absolute, out var cloudUri))
            {
                cloudUri = new Uri("http://127.0.0.1:8787/");
            }
            _cloudClient = new HttpClient
            {
                BaseAddress = cloudUri,
                Timeout = TimeSpan.FromSeconds(Math.Clamp(appSettings.Network.TimeoutSeconds, 1, 120))
            };
            _keyMappingStore = new JsonKeyMappingStore(Path.Combine(appData, "key-mappings.json"));
            IReadOnlyList<SkyMusic.Core.Mapping.KeyMappingDefinition> keyMappings;
            try
            {
                keyMappings = _keyMappingStore.LoadAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is IOException or JsonException or ArgumentException)
            {
                // 配置损坏不能阻断应用启动
                keyMappings = [];
            }
            var playbackMonitor = new PlaybackEventMonitor();
            var windowService = new WindowsGameWindowService();
            var pianoTransDirectory = PianoTransLocator.LocatePackage(
                AppContext.BaseDirectory,
                appSettings.ExternalTools.PianoTransPath);
            var ffmpegService = new FfmpegService(AppContext.BaseDirectory, pianoTransDirectory);
            _scorePlaybackController = new ScorePlaybackController(
                new ScoreImportService(),
                new ScoreTimelineCompiler(),
                new HighPrecisionTimelineScheduler(),
                windowService,
                DefaultPlaybackTargets.Create(keyMappings),
                monitor: playbackMonitor);
            if (_scorePlaybackController.Targets.Any(target => target.Id == appSettings.General.DefaultPlaybackTargetId))
            {
                _scorePlaybackController.SelectTargetAsync(appSettings.General.DefaultPlaybackTargetId)
                    .AsTask().GetAwaiter().GetResult();
            }
            _mainViewModel = new MainWindowViewModel(
                new DemoMusicCatalog(),
                _playbackController,
                _coverImages,
                _scorePlaybackController,
                playbackMonitor,
                _keyMappingStore,
                keyMappings,
                new PianoTransAdapter(new PianoTransOptions(pianoTransDirectory)),
                new JsonMacroScriptImporter(),
                new MacroPlaybackSession(new WindowsMacroInputSink()),
                windowService,
                new Vst3ProcessHost(new Vst3ProcessHostOptions(Path.Combine(
                    AppContext.BaseDirectory,
                    "SkyMusic.VstHost.exe"))),
                _appSettingsStore,
                appSettings,
                ffmpegService,
                new CloudLyricsProvider(
                    _cloudClient,
                    Path.Combine(appSettings.Storage.CacheDirectory ?? appData, "lyrics")));
            desktop.MainWindow = desktop.Args?.Contains("--workbench", StringComparer.OrdinalIgnoreCase) == true
                ? new WorkbenchWindow
                {
                    DataContext = new WorkbenchWindowViewModel(playbackController: _scorePlaybackController)
                }
                : new MainWindow
                {
                    DataContext = _mainViewModel
                };
            desktop.Exit += (_, _) =>
            {
                PersistLastTarget();
                _mainViewModel.Dispose();
                _playbackController.Dispose();
                _coverImages.Dispose();
                _scorePlaybackController.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _cloudClient.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    // 退出前保存最近使用的播放目标和窗口句柄
    private void PersistLastTarget()
    {
        if (_appSettingsStore is null || _scorePlaybackController is null)
        {
            return;
        }

        try
        {
            var settings = _appSettingsStore.LoadAsync().AsTask().GetAwaiter().GetResult();
            if (!settings.General.RememberLastTarget)
            {
                return;
            }

            var updated = settings with
            {
                General = settings.General with
                {
                    DefaultPlaybackTargetId = _scorePlaybackController.Snapshot.TargetId
                }
            };
            _appSettingsStore.SaveAsync(updated).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // 退出阶段不因设置写入失败阻塞进程
        }
    }
}
