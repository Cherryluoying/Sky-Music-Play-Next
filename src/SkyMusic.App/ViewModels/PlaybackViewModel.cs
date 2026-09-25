// 模块：SkyMusic.App 界面状态 PlaybackViewModel
using System.Collections.ObjectModel;
using Avalonia.Threading;
using SkyMusic.Core.Models;
using SkyMusic.Core.Lyrics;
using SkyMusic.Core.Plugins;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class PlaybackViewModel : ObservableObject, IDisposable
{
    private readonly IPlaybackController _player;
    private readonly SemaphoreSlim _playbackGate = new(1, 1);
    private readonly ILyricsProvider? _lyricsProvider;
    private TrackItemViewModel? _currentItem;
    private double _positionSeconds;
    private double _durationSeconds = 1;
    private bool _isPlaying;
    private bool _applyingSnapshot;
    private int _currentLyricIndex = -1;
    private bool _isDesktopLyricsVisible;
    private CancellationTokenSource? _lyricsRequest;
    private readonly IMediaLibraryStore? _libraryStore;
    private readonly IScorePlaybackController? _scoreController;
    private readonly IMidiVisualizationService? _midiVisualization;
    private readonly string _localLyricsDirectory;
    private bool _isQueuePopupOpen;
    private bool _isScoreSettingsOpen;
    private bool _isPlaybackLoading;
    private string? _playbackError;
    private CancellationTokenSource? _playbackRequest;
    private CancellationTokenSource? _seekRequest;
    private bool _isScrubbing;
    private readonly IInstrumentPluginHost? _instrumentPluginHost;
    private readonly IReadOnlyList<string> _vst3SearchPaths;
    private bool _isInstrumentPopupOpen;
    private InstrumentPluginInfo? _selectedInstrumentPlugin;
    private PlaybackSnapshot? _pendingSnapshot;
    private int _snapshotQueued;
    private bool _disposed;
    private readonly PlaybackQueueOrder _queueOrder = new();
    private double _volumePercent = 100;

    public bool CanAdjustVolume => CurrentItem?.Kind == MediaKind.Audio && _player is IAudioVolumeControl;
    public double VolumePercent
    {
        get => _volumePercent;
        set
        {
            var bounded = double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 100;
            if (SetProperty(ref _volumePercent, bounded) && _player is IAudioVolumeControl output)
                output.Volume = bounded / 100;
        }
    }

    public PlaybackViewModel(
        IPlaybackController player,
        ILyricsProvider? lyricsProvider = null,
        IMediaLibraryStore? libraryStore = null,
        IScorePlaybackController? scoreController = null,
        IMidiVisualizationService? midiVisualization = null,
        string? localLyricsDirectory = null,
        IInstrumentPluginHost? instrumentPluginHost = null,
        IReadOnlyList<string>? vst3SearchPaths = null)
    {
        _player = player;
        _lyricsProvider = lyricsProvider;
        _libraryStore = libraryStore;
        _scoreController = scoreController;
        _midiVisualization = midiVisualization;
        _instrumentPluginHost = instrumentPluginHost;
        _vst3SearchPaths = ResolveVst3SearchPaths(vst3SearchPaths);
        _localLyricsDirectory = Path.GetFullPath(localLyricsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SkyMusicPlay",
            "Library",
            "lyrics"));
        _player.SnapshotChanged += OnSnapshotChanged;

        TogglePlayCommand = new AsyncRelayCommand(
            _ => TogglePlayAsync(),
            _ => !IsPlaybackLoading,
            SetPlaybackError);
        PreviousCommand = new AsyncRelayCommand(
            _ => MoveTrackAsync(-1),
            _ => !IsPlaybackLoading,
            SetPlaybackError);
        NextCommand = new AsyncRelayCommand(
            _ => MoveTrackAsync(1),
            _ => !IsPlaybackLoading,
            SetPlaybackError);
        OpenPlayerCommand = new RelayCommand(_ =>
        {
            if (HasTrack)
            {
                OpenPlayerRequested?.Invoke(this, EventArgs.Empty);
            }
        });
        ToggleDesktopLyricsCommand = new RelayCommand(_ =>
            SetDesktopLyricsVisible(!IsDesktopLyricsVisible));
        ToggleFavoriteCommand = new AsyncRelayCommand(
            parameter => ToggleFavoriteAsync(parameter as TrackItemViewModel),
            parameter => parameter is TrackItemViewModel,
            _ => { });
        AdjustIntervalCommand = new AsyncRelayCommand(
            parameter => AdjustTimingAsync(int.TryParse(parameter?.ToString(), out var value) ? value : 0, 0),
            _ => _scoreController is not null,
            _ => { });
        AdjustReleaseDelayCommand = new AsyncRelayCommand(
            parameter => AdjustTimingAsync(0, int.TryParse(parameter?.ToString(), out var value) ? value : 0),
            _ => _scoreController is not null,
            _ => { });
        RefreshPlaybackWindowsCommand = new AsyncRelayCommand(
            _ => _scoreController?.RefreshWindowsAsync().AsTask() ?? Task.CompletedTask,
            _ => _scoreController is not null,
            _ => { });
        ToggleQueuePopupCommand = new RelayCommand(_ => IsQueuePopupOpen = !IsQueuePopupOpen);
        ToggleScoreSettingsCommand = new RelayCommand(_ => IsScoreSettingsOpen = !IsScoreSettingsOpen);
        ScanInstrumentPluginsCommand = new RelayCommand(_ => ScanInstrumentPlugins());
        LoadInstrumentPluginCommand = new AsyncRelayCommand(
            _ => LoadInstrumentPluginAsync(),
            _ => IsMidi && SelectedInstrumentPlugin is not null && _instrumentPluginHost is not null,
            SetInstrumentError);
        OpenInstrumentEditorCommand = new AsyncRelayCommand(
            _ => OpenInstrumentEditorAsync(),
            _ => IsMidi && _instrumentPluginHost?.Snapshot.State == InstrumentHostState.Loaded,
            SetInstrumentError);
        if (_scoreController is not null)
        {
            _scoreController.Changed += OnScoreControllerChanged;
        }
    }

    public event EventHandler? OpenPlayerRequested;

    public event Action<bool>? DesktopLyricsVisibilityChanged;

    public event EventHandler? MediaLibraryChanged;

    // 未配置路径时仍扫描 Windows 常见 VST3 目录，避免 MIDI 页面显示空插件列表。
    private static IReadOnlyList<string> ResolveVst3SearchPaths(IReadOnlyList<string>? configured)
    {
        if (configured is { Count: > 0 })
        {
            return configured;
        }

        var paths = new List<string>();
        var commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(commonFiles))
        {
            paths.Add(Path.Combine(commonFiles, "VST3"));
        }
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            paths.Add(Path.Combine(programFiles, "VSTPlugins"));
        }
        return paths;
    }

    public ObservableCollection<TrackItemViewModel> Queue { get; } = [];

    public ObservableCollection<TrackItemViewModel> RecentTracks { get; } = [];

    public ObservableCollection<LyricLineViewModel> Lyrics { get; } = [];

    // 使用不可变快照绑定钢琴窗，避免后台解析完成后集合实例不变而不触发重绘。
    private IReadOnlyList<MidiVisualNote> _midiNotes = [];

    public IReadOnlyList<MidiVisualNote> MidiNotes
    {
        get => _midiNotes;
        private set => SetProperty(ref _midiNotes, value);
    }

    public AsyncRelayCommand TogglePlayCommand { get; }

    public AsyncRelayCommand PreviousCommand { get; }

    public AsyncRelayCommand NextCommand { get; }

    public RelayCommand OpenPlayerCommand { get; }

    public RelayCommand ToggleDesktopLyricsCommand { get; }

    public AsyncRelayCommand ToggleFavoriteCommand { get; }

    public AsyncRelayCommand AdjustIntervalCommand { get; }

    public AsyncRelayCommand AdjustReleaseDelayCommand { get; }

    public AsyncRelayCommand RefreshPlaybackWindowsCommand { get; }

    public RelayCommand ToggleQueuePopupCommand { get; }

    public RelayCommand ToggleScoreSettingsCommand { get; }

    public RelayCommand ScanInstrumentPluginsCommand { get; }

    public AsyncRelayCommand LoadInstrumentPluginCommand { get; }

    public AsyncRelayCommand OpenInstrumentEditorCommand { get; }

    public bool IsMidi => CurrentItem?.Track.Kind == MediaKind.Midi;

    public ObservableCollection<InstrumentPluginInfo> InstrumentPlugins { get; } = [];

    public InstrumentPluginInfo? SelectedInstrumentPlugin
    {
        get => _selectedInstrumentPlugin;
        set
        {
            if (SetProperty(ref _selectedInstrumentPlugin, value))
            {
                LoadInstrumentPluginCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsInstrumentPopupOpen
    {
        get => _isInstrumentPopupOpen;
        set => SetProperty(ref _isInstrumentPopupOpen, value);
    }

    public bool ShowsLyrics => !IsMidi;

    public bool HasLyrics => Lyrics.Count > 0;

    public bool HasNoLyrics => ShowsLyrics && !HasLyrics;

    public bool IsScore => CurrentItem?.Track.Kind == MediaKind.Score;

    public bool IsPlaybackLoading
    {
        get => _isPlaybackLoading;
        private set
        {
            if (!SetProperty(ref _isPlaybackLoading, value))
            {
                return;
            }

            TogglePlayCommand.NotifyCanExecuteChanged();
            PreviousCommand.NotifyCanExecuteChanged();
            NextCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanSeek));
        }
    }

    public ScorePlaybackSnapshot? ScoreSnapshot => _scoreController?.Snapshot;

    public IReadOnlyList<PlaybackTarget> PlaybackTargets => _scoreController?.Targets ?? [];

    public IReadOnlyList<GameWindowInfo> PlaybackWindows => _scoreController?.Windows ?? [];

    public ScoreTimingSettings ScoreTiming => _scoreController?.Snapshot.Timing ?? ScoreTimingSettings.Default;

    public string IntervalAdjustmentText => $"{ScoreTiming.IntervalAdjustmentMilliseconds:+0;-0;0} ms";

    public string KeyReleaseDelayText => $"{ScoreTiming.KeyReleaseDelayMilliseconds:+0;-0;0} ms";

    public bool IsQueuePopupOpen
    {
        get => _isQueuePopupOpen;
        set => SetProperty(ref _isQueuePopupOpen, value);
    }

    public bool IsScoreSettingsOpen
    {
        get => _isScoreSettingsOpen;
        set => SetProperty(ref _isScoreSettingsOpen, value);
    }

    public string? PlaybackError
    {
        get => _playbackError;
        private set => SetProperty(ref _playbackError, value);
    }

    public string PlayerPageTitle => IsMidi ? "MIDI 钢琴窗" : "沉浸歌词";

    public PlaybackTarget? SelectedPlaybackTarget
    {
        get => _scoreController?.Targets.FirstOrDefault(target => target.Id == _scoreController.Snapshot.TargetId);
        set
        {
            if (value is not null && _scoreController is not null && value.Id != _scoreController.Snapshot.TargetId)
            {
                _ = _scoreController.SelectTargetAsync(value.Id);
            }
        }
    }

    public GameWindowInfo? SelectedPlaybackWindow
    {
        get => _scoreController?.SelectedWindow;
        set
        {
            if (value is not null && _scoreController?.SelectedWindow?.Handle != value.Handle)
            {
                _scoreController?.SelectWindow(value.Handle);
            }
        }
    }

    public TrackItemViewModel? CurrentItem
    {
        get => _currentItem;
        private set
        {
            if (!SetProperty(ref _currentItem, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CurrentTrack));
            OnPropertyChanged(nameof(HasTrack));
            OnPropertyChanged(nameof(CanSeek));
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(CurrentDesktopLyric));
            OnPropertyChanged(nameof(NextDesktopLyric));
            OnPropertyChanged(nameof(IsMidi));
            OnPropertyChanged(nameof(CanAdjustVolume));
            OnPropertyChanged(nameof(IsScore));
            OnPropertyChanged(nameof(ShowsLyrics));
            OnPropertyChanged(nameof(HasNoLyrics));
            OnPropertyChanged(nameof(PlayerPageTitle));
            LoadInstrumentPluginCommand.NotifyCanExecuteChanged();
            OpenInstrumentEditorCommand.NotifyCanExecuteChanged();
            LoadMidiVisualization(value);
        }
    }

    public MusicTrack? CurrentTrack => CurrentItem?.Track;

    public bool HasTrack => CurrentItem is not null;
    public bool CanSeek => HasTrack && !IsPlaybackLoading;

    // 拖动只改变 UI 预览值；实时快照不能覆盖指针选择的位置。
    public void BeginScrub()
    {
        if (!CanSeek) return;
        _isScrubbing = true;
        _seekRequest?.Cancel();
    }

    public void EndScrub(bool commit)
    {
        if (!_isScrubbing) return;
        _isScrubbing = false;
        if (commit && CanSeek) QueueSeek(TimeSpan.FromSeconds(PositionSeconds));
        else ApplySnapshot(_player.Snapshot);
    }

    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            value = double.IsFinite(value) ? Math.Clamp(value, 0, DurationSeconds) : 0;
            if (!SetProperty(ref _positionSeconds, value))
            {
                return;
            }

            OnPropertyChanged(nameof(PositionText));
            if (!_applyingSnapshot && !_isScrubbing)
            {
                QueueSeek(TimeSpan.FromSeconds(value));
            }
        }
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        private set
        {
            if (SetProperty(ref _durationSeconds, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(DurationText));
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
            }
        }
    }

    public int CurrentLyricIndex
    {
        get => _currentLyricIndex;
        private set
        {
            if (SetProperty(ref _currentLyricIndex, value))
            {
                OnPropertyChanged(nameof(CurrentDesktopLyric));
                OnPropertyChanged(nameof(NextDesktopLyric));
            }
        }
    }

    public bool IsDesktopLyricsVisible
    {
        get => _isDesktopLyricsVisible;
        private set => SetProperty(ref _isDesktopLyricsVisible, value);
    }

    public string CurrentDesktopLyric => CurrentLyricIndex >= 0 && CurrentLyricIndex < Lyrics.Count
        ? Lyrics[CurrentLyricIndex].Text
        : CurrentItem?.Title ?? "猫橘咪音乐";

    public string NextDesktopLyric => CurrentLyricIndex + 1 >= 0 && CurrentLyricIndex + 1 < Lyrics.Count
        ? Lyrics[CurrentLyricIndex + 1].Text
        : CurrentItem?.Artist ?? string.Empty;

    public string PlayGlyph => IsPlaying ? "\uE769" : "\uE768";

    public string PositionText => TimeSpan.FromSeconds(PositionSeconds).ToString(@"mm\:ss");

    public string DurationText => TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss");

    // 替换播放队列并保持当前曲目索引
    public void SetQueue(IEnumerable<TrackItemViewModel> tracks)
    {
        var incoming = tracks.ToArray();
        _queueOrder.Merge(incoming.Select(item => item.Track.Id));
        RebuildQueue(incoming);
    }

    // 只更新队列中的展示数据，保留用户安排的下一首和已经移除的曲目状态。
    private void RebuildQueue(IEnumerable<TrackItemViewModel> updated)
    {
        var items = Queue.Concat(updated).GroupBy(item => item.Track.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var currentId = CurrentItem?.Track.Id;
        Queue.Clear();
        foreach (var id in _queueOrder.Items)
        {
            if (items.TryGetValue(id, out var track))
            {
                track.IsSelected = string.Equals(id, currentId, StringComparison.OrdinalIgnoreCase);
                Queue.Add(track);
            }
        }

        var replacement = currentId is null
            ? null
            : Queue.FirstOrDefault(item => item.Track.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase));
        if (replacement is not null)
        {
            replacement.IsSelected = true;
            CurrentItem = replacement;
        }
        // 刷新曲库只更新队列，不应在每次启动时擅自选中第一首歌曲。
    }

    public void PlayNext(TrackItemViewModel item)
    {
        _queueOrder.AddNext(item.Track.Id);
        RebuildQueue([item]);
    }

    public void RemoveFromQueue(TrackItemViewModel item)
    {
        _queueOrder.Remove(item.Track.Id);
        Queue.Remove(item);
    }

    public void PlayTrack(TrackItemViewModel item, bool autoplay = true) =>
        _ = PlayTrackAsync(item, autoplay);

    // 双击和快速切歌共用一个请求入口：取消旧请求并串行切换后端，避免多个自动演奏会话互相等待。
    public async Task PlayTrackAsync(TrackItemViewModel item, bool autoplay = true)
    {
        ArgumentNullException.ThrowIfNull(item);
        var request = new CancellationTokenSource();
        var previousRequest = Interlocked.Exchange(ref _playbackRequest, request);
        previousRequest?.Cancel();
        IsPlaybackLoading = true;

        // 新曲目接管前取消旧曲目的待跳转请求，防止松手或排队的 Seek 作用到新曲目。
        _isScrubbing = false;
        _seekRequest?.Cancel();

        var entered = false;
        try
        {
            await _playbackGate.WaitAsync(request.Token);
            entered = true;
            PlaybackError = null;
            await LoadTrackAsync(item, autoplay, request.Token);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            // 新的播放请求已经接管，旧请求安静退出。
        }
        catch (Exception exception)
        {
            PlaybackError = exception.Message;
        }
        finally
        {
            if (entered)
            {
                _playbackGate.Release();
            }

            if (ReferenceEquals(Interlocked.CompareExchange(ref _playbackRequest, null, request), request))
            {
                IsPlaybackLoading = false;
            }
            request.Dispose();
        }
    }

    public void SetDesktopLyricsVisible(bool isVisible)
    {
        if (!SetProperty(ref _isDesktopLyricsVisible, isVisible, nameof(IsDesktopLyricsVisible)))
        {
            return;
        }

        DesktopLyricsVisibilityChanged?.Invoke(isVisible);
    }

    private async Task LoadTrackAsync(
        TrackItemViewModel item,
        bool autoplay,
        CancellationToken cancellationToken)
    {
        _queueOrder.Select(item.Track.Id);
        RebuildQueue([item]);
        foreach (var track in Queue)
        {
            track.IsSelected = ReferenceEquals(track, item);
        }

        CurrentItem = item;
        ReplaceLyrics(item.Track.Lyrics);

        CurrentLyricIndex = -1;

        // 键盘演奏依赖当前游戏窗口。每次开始前刷新一次，游戏晚于应用启动时也能自动识别。
        if (item.Track.Kind == MediaKind.Score && _scoreController is not null)
        {
            await _scoreController.RefreshWindowsAsync(cancellationToken);
            var target = SelectedPlaybackTarget;
            if (target?.Capabilities.HasFlag(PlaybackSinkCapabilities.ForegroundInput) == true &&
                _scoreController.SelectedWindow is null)
            {
                throw new InvalidOperationException(
                    "未找到目标游戏窗口，请先启动游戏，或在演奏设置中刷新并选择窗口。");
            }
        }

        await _player.LoadAsync(item.Track, autoplay, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        AddRecent(item);
        if (_libraryStore is not null)
        {
            _ = RecordPlayedAsync(item.Track);
        }
        LoadPreferredLyrics(item.Track);
    }

    // 导入 LRC/TXT，复制到媒体库并把关联路径持久化到当前曲目。
    public async Task ImportLocalLyricsAsync(string sourcePath)
    {
        if (CurrentItem is null || IsMidi)
        {
            return;
        }

        try
        {
            PlaybackError = null;
            var lines = await LrcLyricsParser.ParseFileAsync(sourcePath);
            if (lines.Count == 0)
            {
                throw new InvalidDataException("歌词文件中没有可显示的歌词。");
            }

            Directory.CreateDirectory(_localLyricsDirectory);
            var extension = Path.GetExtension(sourcePath).Equals(".lrc", StringComparison.OrdinalIgnoreCase)
                ? ".lrc"
                : ".txt";
            var targetPath = Path.Combine(_localLyricsDirectory, $"{CurrentItem.Track.Id}{extension}");
            if (!Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, true);
                await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, true);
                await source.CopyToAsync(target);
            }

            _lyricsRequest?.Cancel();
            var updatedTrack = CurrentItem.Track with
            {
                Lyrics = lines,
                LyricsSourcePath = targetPath
            };
            foreach (var item in Queue.Where(item => item.Track.Id == updatedTrack.Id))
            {
                item.UpdateTrack(updatedTrack);
            }
            CurrentItem.UpdateTrack(updatedTrack);
            ReplaceLyrics(lines);
            UpdateCurrentLyric(TimeSpan.FromSeconds(PositionSeconds), true);
            if (_libraryStore is not null)
            {
                await _libraryStore.UpsertAsync(updatedTrack, null);
            }
            MediaLibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            PlaybackError = $"歌词导入失败：{exception.Message}";
        }
    }

    public void SeekToLyric(LyricLineViewModel line) => PositionSeconds = line.Line.Timestamp.TotalSeconds;

    // MIDI 曲目进入播放页时异步读取音符，不阻塞队列切换。
    private async void LoadMidiVisualization(TrackItemViewModel? item)
    {
        MidiNotes = [];
        if (item?.Track.Kind != MediaKind.Midi || string.IsNullOrWhiteSpace(item.Track.SourcePath) ||
            _midiVisualization is null)
        {
            return;
        }

        var trackId = item.Track.Id;
        try
        {
            var notes = await _midiVisualization.LoadAsync(item.Track.SourcePath);
            if (CurrentItem?.Track.Id != trackId)
            {
                return;
            }

            // MIDI 解析在后台线程执行，集合绑定必须回到 Avalonia UI 线程。
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (CurrentItem?.Track.Id != trackId)
                {
                    return;
                }

                MidiNotes = notes.ToArray();
            });
        }
        catch (Exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 旧文件解析失败也必须被捕获，快速切歌不能让 async void 异常退出进程。
                if (CurrentItem?.Track.Id == trackId) MidiNotes = [];
            });
        }
    }

    // 本地歌词优先于内置歌词和云端歌词，异步结果会校验当前曲目。
    private async void LoadPreferredLyrics(MusicTrack track)
    {
        if (!string.IsNullOrWhiteSpace(track.LyricsSourcePath) && File.Exists(track.LyricsSourcePath))
        {
            try
            {
                var localLines = await LrcLyricsParser.ParseFileAsync(track.LyricsSourcePath);
                if (CurrentTrack?.Id == track.Id && localLines.Count > 0)
                {
                    ReplaceLyrics(localLines);
                    UpdateCurrentLyric(TimeSpan.FromSeconds(PositionSeconds), true);
                }
            }
            catch (IOException)
            {
                // 本地歌词损坏时继续使用曲目内置歌词。
            }
            return;
        }

        if (track.Lyrics.Count > 0 || _lyricsProvider is null)
        {
            return;
        }

        _lyricsRequest?.Cancel();
        _lyricsRequest?.Dispose();
        _lyricsRequest = new CancellationTokenSource();
        var cancellationToken = _lyricsRequest.Token;
        try
        {
            var result = await _lyricsProvider.GetLyricsAsync(
                new LyricsQuery(track.Title, track.Artist, track.Album, track.Duration),
                cancellationToken);
            if (result is null || result.Lines.Count == 0 || CurrentTrack?.Id != track.Id)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReplaceLyrics(result.Lines);
                UpdateCurrentLyric(TimeSpan.FromSeconds(PositionSeconds), true);
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ReplaceLyrics(IEnumerable<LyricLine> lines)
    {
        Lyrics.Clear();
        foreach (var line in lines)
        {
            Lyrics.Add(new LyricLineViewModel(line));
        }
        OnPropertyChanged(nameof(HasLyrics));
        OnPropertyChanged(nameof(HasNoLyrics));
    }

    private void AddRecent(TrackItemViewModel item)
    {
        var existing = RecentTracks.FirstOrDefault(track => track.Track.Id == item.Track.Id);
        if (existing is not null)
        {
            RecentTracks.Remove(existing);
        }

        RecentTracks.Insert(0, item);
    }

    // 悬浮搜索必须先暂停前台键盘演奏，再让搜索框获取输入焦点。
    public async Task PauseForTextInputAsync()
    {
        await _playbackGate.WaitAsync();
        try
        {
            if (IsScore && _player.Snapshot.State == PlaybackState.Playing)
                await _player.PauseAsync();
        }
        finally { _playbackGate.Release(); }
    }

    private async Task TogglePlayAsync()
    {
        try
        {
            PlaybackError = null;
            if (_player.Snapshot.State == PlaybackState.Playing)
            {
                await _player.PauseAsync();
            }
            else
            {
                await _player.PlayAsync();
            }
        }
        catch (Exception exception)
        {
            PlaybackError = exception.Message;
        }
    }

    private async Task ToggleFavoriteAsync(TrackItemViewModel? item)
    {
        if (item is null || _libraryStore is null)
        {
            return;
        }

        item.IsFavorite = !item.IsFavorite;
        await _libraryStore.UpsertAsync(item.Track, null);
        await _libraryStore.SetFavoriteAsync(item.Track.Id, item.IsFavorite);
        MediaLibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    // 扫描 VST3 音色，只在 MIDI 播放页的钢琴按钮中使用。
    private void ScanInstrumentPlugins()
    {
        if (_instrumentPluginHost is null || !IsMidi)
        {
            return;
        }

        InstrumentPlugins.Clear();
        foreach (var plugin in _instrumentPluginHost.DiscoverPlugins(_vst3SearchPaths))
        {
            InstrumentPlugins.Add(plugin);
        }

        SelectedInstrumentPlugin ??= InstrumentPlugins.FirstOrDefault();
    }

    private async Task LoadInstrumentPluginAsync()
    {
        if (!IsMidi || _instrumentPluginHost is null || SelectedInstrumentPlugin is null)
        {
            return;
        }

        if (_player is IMidiPlaybackOutputControl midiOutput)
        {
            // 输出切换由播放器串行完成，避免加载插件期间内置音源继续发声。
            await midiOutput.LoadInstrumentAsync(SelectedInstrumentPlugin);
        }
        OpenInstrumentEditorCommand.NotifyCanExecuteChanged();
    }

    // 打开 VST3 原生插件界面，参数调整由插件自身负责。
    private async Task OpenInstrumentEditorAsync()
    {
        if (!IsMidi || _instrumentPluginHost is null)
        {
            return;
        }

        if (!await _instrumentPluginHost.OpenEditorAsync())
        {
            PlaybackError = "当前 VST3 插件没有可用的图形编辑器";
        }
    }

    private void SetInstrumentError(Exception exception)
    {
        PlaybackError = exception.Message.Contains("no editor", StringComparison.OrdinalIgnoreCase)
            ? "当前 VST3 插件没有原生编辑器界面，只能使用插件默认音色。"
            : exception.Message;
    }


    private async Task RecordPlayedAsync(MusicTrack track)
    {
        if (_libraryStore is null)
        {
            return;
        }
        await _libraryStore.UpsertAsync(track, null);
        await _libraryStore.RecordPlayedAsync(track.Id);
        MediaLibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task AdjustTimingAsync(int intervalDelta, int releaseDelayDelta)
    {
        if (_scoreController is null)
        {
            return Task.CompletedTask;
        }

        var current = _scoreController.Snapshot.Timing;
        return _scoreController.SetTimingAsync(new ScoreTimingSettings(
            Math.Clamp(current.IntervalAdjustmentMilliseconds + intervalDelta, -200, 500),
            Math.Clamp(current.KeyReleaseDelayMilliseconds + releaseDelayDelta, -200, 1_000))).AsTask();
    }

    private void OnScoreControllerChanged(ScorePlaybackSnapshot snapshot) => Dispatcher.UIThread.Post(() =>
    {
        OnPropertyChanged(nameof(ScoreSnapshot));
        OnPropertyChanged(nameof(PlaybackTargets));
        OnPropertyChanged(nameof(PlaybackWindows));
        OnPropertyChanged(nameof(SelectedPlaybackTarget));
        OnPropertyChanged(nameof(SelectedPlaybackWindow));
        OnPropertyChanged(nameof(ScoreTiming));
        OnPropertyChanged(nameof(IntervalAdjustmentText));
        OnPropertyChanged(nameof(KeyReleaseDelayText));
    });

    private Task MoveTrackAsync(int offset)
    {
        if (Queue.Count == 0)
        {
            return Task.CompletedTask;
        }

        var id = _queueOrder.Adjacent(offset);
        var next = Queue.FirstOrDefault(item => string.Equals(item.Track.Id, id, StringComparison.OrdinalIgnoreCase));
        return next is null ? Task.CompletedTask : PlayTrackAsync(next, true);
    }

    // 拖动进度条时只保留最后一次跳转，避免高频 Seek 堵塞自动演奏控制器。
    private void QueueSeek(TimeSpan position)
    {
        if (_disposed || !CanSeek) return;
        PlaybackError = null;
        var request = new CancellationTokenSource();
        var previousRequest = Interlocked.Exchange(ref _seekRequest, request);
        previousRequest?.Cancel();
        _ = SeekAsync(position, request);
    }

    private async Task SeekAsync(TimeSpan position, CancellationTokenSource request)
    {
        var entered = false;
        try
        {
            await _playbackGate.WaitAsync(request.Token);
            entered = true;
            request.Token.ThrowIfCancellationRequested();
            // 解码器重定位可能同步重建输出，在工作线程执行以免松手时阻塞 UI。
            await Task.Run(async () => await _player.SeekAsync(position, request.Token), request.Token);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!request.IsCancellationRequested) PlaybackError = exception.Message;
        }
        finally
        {
            if (entered) _playbackGate.Release();
            if (ReferenceEquals(Interlocked.CompareExchange(ref _seekRequest, null, request), request) && !_disposed)
            {
                Interlocked.Exchange(ref _pendingSnapshot, null);
                ApplySnapshot(_player.Snapshot);
            }
            request.Dispose();
        }
    }

    private void SetPlaybackError(Exception exception)
    {
        PlaybackError = exception.Message;
    }

    // UI 繁忙时只保留最新快照，60 FPS 更新不能排队回放旧位置。
    private void OnSnapshotChanged(object? sender, PlaybackSnapshot snapshot)
    {
        if (_disposed) return;
        Interlocked.Exchange(ref _pendingSnapshot, snapshot);
        if (Interlocked.Exchange(ref _snapshotQueued, 1) != 0) return;
        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _snapshotQueued, 0);
            var latest = Interlocked.Exchange(ref _pendingSnapshot, null);
            if (!_disposed && latest is not null) ApplySnapshot(latest);
        });
    }

    // 把播放器快照同步到进度和播放状态
    private void ApplySnapshot(PlaybackSnapshot snapshot)
    {
        _applyingSnapshot = true;
        try
        {
            if (snapshot.Track is not null && CurrentTrack?.Id != snapshot.Track.Id)
            {
                // 快速切歌时丢弃旧后端快照，不能用旧快照反向触发再次加载歌曲。
                return;
            }

            DurationSeconds = snapshot.Duration.TotalSeconds;
            if (!_isScrubbing && _seekRequest is null)
            {
                PositionSeconds = snapshot.Position.TotalSeconds;
                UpdateCurrentLyric(snapshot.Position);
            }
            IsPlaying = snapshot.State == PlaybackState.Playing;
            PersistResolvedDuration(snapshot.Duration);
        }
        finally
        {
            _applyingSnapshot = false;
        }
    }

    // FFprobe 得到真实时长后立即更新列表项和数据库，避免旧记录下次仍显示 00:01。
    private void PersistResolvedDuration(TimeSpan duration)
    {
        if (CurrentItem is null || duration <= TimeSpan.FromSeconds(1) ||
            CurrentItem.Track.Duration > TimeSpan.FromSeconds(1))
        {
            return;
        }

        var updatedTrack = CurrentItem.Track with { Duration = duration };
        CurrentItem.UpdateTrack(updatedTrack);
        OnPropertyChanged(nameof(CurrentTrack));
        if (_libraryStore is not null)
        {
            _ = _libraryStore.UpsertAsync(updatedTrack, null);
        }
    }

    // 按播放位置选择当前歌词并更新高亮
    private void UpdateCurrentLyric(TimeSpan position, bool forceRefresh = false)
    {
        var index = -1;
        for (var i = 0; i < Lyrics.Count; i++)
        {
            if (Lyrics[i].Line.Timestamp > position)
            {
                break;
            }

            index = i;
        }

        if (!forceRefresh && index == CurrentLyricIndex)
        {
            return;
        }

        CurrentLyricIndex = index;
        for (var i = 0; i < Lyrics.Count; i++)
        {
            Lyrics[i].Distance = index < 0 ? 4 : Math.Abs(i - index);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _playbackRequest?.Cancel();
        _seekRequest?.Cancel();
        _lyricsRequest?.Cancel();
        _lyricsRequest?.Dispose();
        _player.SnapshotChanged -= OnSnapshotChanged;
        if (_scoreController is not null)
        {
            _scoreController.Changed -= OnScoreControllerChanged;
        }
    }
}
