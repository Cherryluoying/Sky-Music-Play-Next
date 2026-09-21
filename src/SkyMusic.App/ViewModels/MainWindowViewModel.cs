// 模块：SkyMusic.App 界面状态 MainWindowViewModel
using System.Collections.ObjectModel;
using SkyMusic.App.Models;
using SkyMusic.App.Services;
using SkyMusic.Core.Services;
using SkyMusic.Core.Settings;
using SkyMusic.Infrastructure.Input;

namespace SkyMusic.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<AppPage, object> _pages;
    private readonly Stack<AppPage> _backStack = [];
    private readonly Stack<AppPage> _forwardStack = [];
    private readonly TasksViewModel _tasks;
    private readonly MidiStudioViewModel _midiStudio;
    private readonly ScoreEditorViewModel _scoreEditor;
    private readonly TranscriptionViewModel _transcription;
    private readonly MacroRunnerViewModel _macroRunner;
    private AppPage _currentPage = AppPage.Discover;
    private object _currentPageViewModel;
    private bool _isPlayerVisible;

    public IScorePlaybackController ScorePlaybackController { get; }

    public MainWindowViewModel(
        IMusicCatalog catalog,
        IPlaybackController player,
        CoverImageService coverImages,
        IScorePlaybackController scorePlaybackController,
        IPlaybackEventMonitor playbackMonitor,
        IKeyMappingStore keyMappingStore,
        IReadOnlyList<SkyMusic.Core.Mapping.KeyMappingDefinition> keyMappings,
        ITranscriptionAdapter transcriptionAdapter,
        IMacroScriptImporter macroImporter,
        IMacroPlaybackSession macroSession,
        IGameWindowService gameWindowService,
        IInstrumentPluginHost instrumentPluginHost,
        IAppSettingsStore appSettingsStore,
        AppSettings appSettings,
        IFfmpegService ffmpegService,
        ILyricsProvider? lyricsProvider = null)
    {
        ScorePlaybackController = scorePlaybackController;
        Playback = new PlaybackViewModel(player, lyricsProvider);
        _tasks = new TasksViewModel(scorePlaybackController);
        _midiStudio = new MidiStudioViewModel(
            new MidiInputCapture(),
            playbackMonitor,
            instrumentPluginHost,
            appSettings.AudioPlugins.Vst3SearchPaths);
        _scoreEditor = new ScoreEditorViewModel(new SkyMusic.Infrastructure.Scores.ScoreImportService());
        _transcription = new TranscriptionViewModel(transcriptionAdapter, async midiPath =>
        {
            await _tasks.ImportFileAsync(midiPath);
            NavigateTo(AppPage.Tasks, true);
        });
        _macroRunner = new MacroRunnerViewModel(macroImporter, macroSession, gameWindowService);
        var settings = new SettingsViewModel(
            appSettingsStore,
            appSettings,
            ffmpegService,
            () => NavigateTo(AppPage.KeyMapping, true));

        var discover = new DiscoverViewModel(catalog, coverImages, Playback);
        _pages = new Dictionary<AppPage, object>
        {
            [AppPage.Discover] = discover,
            [AppPage.Library] = new LibraryViewModel(Playback),
            [AppPage.Favorites] = new FavoriteViewModel(),
            [AppPage.Recent] = new RecentViewModel(Playback),
            [AppPage.Tasks] = _tasks,
            [AppPage.MidiStudio] = _midiStudio,
            [AppPage.ScoreEditor] = _scoreEditor,
            [AppPage.KeyMapping] = new KeyMappingViewModel(keyMappingStore, keyMappings),
            [AppPage.Transcription] = _transcription,
            [AppPage.MacroRunner] = _macroRunner,
            [AppPage.Settings] = settings
        };
        _currentPageViewModel = discover;

        PrimaryNavigationItems =
        [
            new NavigationItemViewModel("发现音乐", "\uE80F", AppPage.Discover),
            new NavigationItemViewModel("乐谱库", "\uE8D6", AppPage.Library),
            new NavigationItemViewModel("演奏任务", "\uE714", AppPage.Tasks),
            new NavigationItemViewModel("MIDI 工作台", "\uE9D9", AppPage.MidiStudio),
            new NavigationItemViewModel("谱面编辑", "\uE70F", AppPage.ScoreEditor),
            new NavigationItemViewModel("音频转 MIDI", "\uE8D4", AppPage.Transcription),
            new NavigationItemViewModel("宏脚本", "\uE756", AppPage.MacroRunner)
        ];
        MusicNavigationItems =
        [
            new NavigationItemViewModel("我喜欢的音乐", "\uEB51", AppPage.Favorites),
            new NavigationItemViewModel("最近播放", "\uE81C", AppPage.Recent)
        ];
        SettingsNavigationItem = new NavigationItemViewModel("设置", "\uE713", AppPage.Settings);

        NavigateCommand = new RelayCommand(parameter =>
        {
            if (parameter is NavigationItemViewModel item)
            {
                NavigateTo(item.Page, true);
            }
        });
        BackCommand = new RelayCommand(_ => GoBack());
        ForwardCommand = new RelayCommand(_ => GoForward());
        OpenWorkbenchCommand = new RelayCommand(_ => OpenWorkbenchRequested?.Invoke(this, EventArgs.Empty));
        ClosePlayerCommand = new RelayCommand(_ => IsPlayerVisible = false);

        Playback.OpenPlayerRequested += OnOpenPlayerRequested;
        UpdateNavigationSelection();
    }

    public ObservableCollection<NavigationItemViewModel> PrimaryNavigationItems { get; }

    public ObservableCollection<NavigationItemViewModel> MusicNavigationItems { get; }

    public NavigationItemViewModel SettingsNavigationItem { get; }

    public PlaybackViewModel Playback { get; }

    public RelayCommand NavigateCommand { get; }

    public RelayCommand BackCommand { get; }

    public RelayCommand ForwardCommand { get; }

    public RelayCommand OpenWorkbenchCommand { get; }

    public RelayCommand ClosePlayerCommand { get; }

    public event EventHandler? OpenWorkbenchRequested;

    public object CurrentPageViewModel
    {
        get => _currentPageViewModel;
        private set => SetProperty(ref _currentPageViewModel, value);
    }

    public bool IsPlayerVisible
    {
        get => _isPlayerVisible;
        private set
        {
            if (SetProperty(ref _isPlayerVisible, value))
            {
                OnPropertyChanged(nameof(IsStandardShellVisible));
            }
        }
    }

    public bool IsStandardShellVisible => !IsPlayerVisible;

    // 切换内容页并按需记录前进后退历史
    private void NavigateTo(AppPage page, bool recordHistory)
    {
        IsPlayerVisible = false;
        if (page == _currentPage)
        {
            return;
        }

        if (recordHistory)
        {
            _backStack.Push(_currentPage);
            _forwardStack.Clear();
        }

        _currentPage = page;
        CurrentPageViewModel = _pages[page];
        UpdateNavigationSelection();
    }

    private void GoBack()
    {
        if (IsPlayerVisible)
        {
            IsPlayerVisible = false;
            return;
        }

        if (_backStack.Count == 0)
        {
            return;
        }

        _forwardStack.Push(_currentPage);
        NavigateTo(_backStack.Pop(), false);
    }

    private void GoForward()
    {
        if (IsPlayerVisible || _forwardStack.Count == 0)
        {
            return;
        }

        _backStack.Push(_currentPage);
        NavigateTo(_forwardStack.Pop(), false);
    }

    // 保持侧栏选中项与当前页面一致
    private void UpdateNavigationSelection()
    {
        foreach (var item in PrimaryNavigationItems.Concat(MusicNavigationItems).Append(SettingsNavigationItem))
        {
            item.IsSelected = item.Page == _currentPage;
        }
    }

    private void OnOpenPlayerRequested(object? sender, EventArgs e)
        => IsPlayerVisible = true;

    public void Dispose()
    {
        Playback.OpenPlayerRequested -= OnOpenPlayerRequested;
        _tasks.Dispose();
        _midiStudio.Dispose();
        _transcription.Dispose();
        _macroRunner.Dispose();
        Playback.Dispose();
    }
}
