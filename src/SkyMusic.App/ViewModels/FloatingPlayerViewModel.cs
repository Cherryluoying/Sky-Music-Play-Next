// 模块：悬浮演奏窗的单面板状态；复用曲库与播放接口，搜索不改变主页面的筛选。
using SkyMusic.App.Services;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class FloatingPlayerViewModel : ObservableObject, IDisposable
{
    private readonly IMediaLibraryStore _store;
    private readonly CoverImageService _covers;
    private readonly LibraryViewModel? _library;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IReadOnlyList<StoredMediaTrack> _records = [];
    private readonly Dictionary<string, TrackItemViewModel> _items = new(StringComparer.OrdinalIgnoreCase);
    private string _section = "playlist";
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string? _error;
    private int _sectionVersion;
    private int _categoryIndex;
    public IReadOnlyList<string> Categories => MediaCategoryFilter.Labels;
    public int CategoryIndex { get => _categoryIndex; set { if (SetProperty(ref _categoryIndex, value)) Filter(); } }

    public FloatingPlayerViewModel(IMediaLibraryStore store, CoverImageService covers, PlaybackViewModel playback, LibraryViewModel? library = null)
    {
        _store = store;
        _covers = covers;
        Playback = playback;
        _library = library;
        if (library is not null) library.Refreshed += OnMediaLibraryChanged;
        else Playback.MediaLibraryChanged += OnMediaLibraryChanged;
    }

    public PlaybackViewModel Playback { get; }
    // 悬浮窗使用虚拟化 ListBox；批量替换只通知一次，避免分类切换逐条重排窗口。
    public RangeObservableCollection<TrackItemViewModel> Tracks { get; } = [];
    public bool IsPlaylist => _section == "playlist";
    public bool IsImported => _section == "imported";
    public bool IsFavorites => _section == "favorites";
    public bool IsSearch => _section == "search";
    public bool IsSettings => _section == "settings";
    public bool ShowsTracks => !IsSettings;
    public bool IsEmpty => !IsLoading && Tracks.Count == 0 && ShowsTracks;
    public bool IsLoading { get => _isLoading; private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); } }
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) Filter(); }
    }

    // 所有入口只替换当前气泡内部内容，始终共用同一套播放控制。
    public async Task<bool> SelectSectionAsync(string section)
    {
        if (section is not ("playlist" or "imported" or "favorites" or "search" or "settings")) return false;
        var version = ++_sectionVersion;
        if (section == "search")
        {
            try { await Playback.PauseForTextInputAsync(); }
            catch (Exception ex) { Error = ex.Message; return false; }
        }
        if (_lifetime.IsCancellationRequested || version != _sectionVersion) return false;
        _section = section;
        _records = [];
        _items.Clear();
        foreach (var property in new[] { nameof(IsPlaylist), nameof(IsImported), nameof(IsFavorites), nameof(IsSearch), nameof(IsSettings), nameof(ShowsTracks) })
            OnPropertyChanged(property);
        Filter();
        if (ShowsTracks) await RefreshAsync();
        return !_lifetime.IsCancellationRequested && version == _sectionVersion;
    }

    public async Task RefreshAsync()
    {
        var entered = false;
        try
        {
            await _refreshGate.WaitAsync(_lifetime.Token);
            entered = true;
            IsLoading = true;
            Error = null;
            var section = _section;
            // SQLite 异步 API 的读取部分可能同步执行，整批查询移出界面线程。
            var records = await Task.Run(async () =>
            {
                await _store.InitializeAsync(_lifetime.Token);
                return section == "playlist"
                    ? await _store.GetPlaylistAsync(cancellationToken: _lifetime.Token)
                    : await _store.GetAllAsync(_lifetime.Token);
            }, _lifetime.Token);
            if (!_lifetime.IsCancellationRequested && section == _section)
            {
                _records = records;
                _items.Clear();
                Filter();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Error = $"曲库读取失败：{ex.Message}"; }
        finally
        {
            if (entered) { IsLoading = false; _refreshGate.Release(); }
        }
    }

    private void Filter()
    {
        var query = IsSearch ? SearchText.Trim() : string.Empty;
        var records = _records.Where(record => !IsFavorites || record.IsFavorite)
            .Where(record => !IsImported || ImportedMediaFilter.Matches(record.Track))
            .Where(record => MediaCategoryFilter.Matches(record.Track.Kind, CategoryIndex))
            .Where(record => string.IsNullOrEmpty(query) || Matches(record.Track, query))
            .DistinctBy(record => record.Track.Id, StringComparer.OrdinalIgnoreCase);
        var items = records.Select(GetItem).ToArray();
        Tracks.ReplaceRange(items);
        OnPropertyChanged(nameof(IsEmpty));
    }

    // 分类切换复用行状态和已读封面，避免反复创建命令与临时对象。
    private TrackItemViewModel GetItem(StoredMediaTrack record)
    {
        if (_items.TryGetValue(record.Track.Id, out var item)) return item;
        item = new TrackItemViewModel(record.Track, null,
            selected => Playback.PlayFromCollection(selected, Tracks), isFavorite: record.IsFavorite,
            loadCover: () => _covers.GetCover(record.Track.CoverSource));
        _items.Add(record.Track.Id, item);
        return item;
    }

    private static bool Matches(MusicTrack track, string query) =>
        track.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        track.Artist.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        track.Album.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        track.Author.Contains(query, StringComparison.OrdinalIgnoreCase);

    private async void OnMediaLibraryChanged(object? sender, EventArgs e)
    {
        if (!_lifetime.IsCancellationRequested && ShowsTracks) await RefreshAsync();
    }

    public void Dispose()
    {
        Playback.MediaLibraryChanged -= OnMediaLibraryChanged;
        if (_library is not null) _library.Refreshed -= OnMediaLibraryChanged;
        _lifetime.Cancel();
    }
}
