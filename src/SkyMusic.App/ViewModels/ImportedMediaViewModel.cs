// 模块：已导入内容页；读取整个本地媒体库，独立筛选，不改动歌单成员和播放队列。
using SkyMusic.App.Services;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class ImportedMediaViewModel : ObservableObject, IDisposable
{
    private readonly IMediaLibraryStore _store;
    private readonly CoverImageService _covers;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<string, TrackItemViewModel> _items = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<StoredMediaTrack> _records = [];
    private int _refreshVersion;
    private int _categoryIndex;
    private string _searchText = "";
    private bool _isLoading;
    private string? _error;

    public ImportedMediaViewModel(IMediaLibraryStore store, CoverImageService covers, LibraryViewModel library)
    {
        _store = store;
        _covers = covers;
        Library = library;
        library.Refreshed += OnLibraryRefreshed;
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        _ = RefreshAsync();
    }

    public LibraryViewModel Library { get; }
    public PlaybackViewModel Playback => Library.Playback;
    public RangeObservableCollection<TrackItemViewModel> Tracks { get; } = [];
    public IReadOnlyList<string> Categories => MediaCategoryFilter.Labels;
    public int CategoryIndex { get => _categoryIndex; set { if (SetProperty(ref _categoryIndex, value)) Filter(); } }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) Filter(); } }
    public bool IsLoading { get => _isLoading; private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); } }
    public bool IsEmpty => !IsLoading && Tracks.Count == 0;
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }
    public string CountText => $"当前显示 {Tracks.Count} 项 · 已导入 {_records.Count} 项";
    public AsyncRelayCommand RefreshCommand { get; }

    // SQLite 读取移到后台，旧查询不覆盖新结果；分类仅在内存中筛选并复用行对象。
    public async Task RefreshAsync()
    {
        if (_lifetime.IsCancellationRequested) return;
        var version = ++_refreshVersion;
        IsLoading = true;
        Error = null;
        try
        {
            var records = await Task.Run(async () =>
            {
                await _store.InitializeAsync(_lifetime.Token);
                return await _store.GetAllAsync(_lifetime.Token);
            }, _lifetime.Token);
            if (_lifetime.IsCancellationRequested || version != _refreshVersion) return;
            _records = records.Where(record => ImportedMediaFilter.Matches(record.Track))
                .DistinctBy(record => record.Track.Id, StringComparer.OrdinalIgnoreCase).ToArray();
            _items.Clear();
            Filter();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (version == _refreshVersion) Error = $"读取已导入内容失败：{ex.Message}"; }
        finally { if (version == _refreshVersion) IsLoading = false; }
    }

    private void Filter()
    {
        var query = SearchText.Trim();
        var records = _records.Select((record, index) => (Record: record, Index: index + 1))
            .Where(entry => MediaCategoryFilter.Matches(entry.Record.Track.Kind, CategoryIndex))
            .Where(entry => query.Length == 0 || new[] { entry.Record.Track.Title, entry.Record.Track.Artist, entry.Record.Track.Album, entry.Record.Track.Author }
                .Any(text => text.Contains(query, StringComparison.OrdinalIgnoreCase)));
        Tracks.ReplaceRange(records.Select(entry =>
        {
            var record = entry.Record;
            if (_items.TryGetValue(record.Track.Id, out var cached)) return cached;
            var item = new TrackItemViewModel(record.Track, null,
                selected => Playback.PlayFromCollection(selected, Tracks),
                selected => Playback.ToggleFavoriteCommand.Execute(selected), record.IsFavorite, entry.Index,
                playNext: selected => Playback.PlayNext(selected), loadCover: () => _covers.GetCover(record.Track.CoverSource));
            _items.Add(record.Track.Id, item);
            return item;
        }).ToArray());
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async void OnLibraryRefreshed(object? sender, EventArgs e) => await RefreshAsync();
    public void Dispose() { Library.Refreshed -= OnLibraryRefreshed; _lifetime.Cancel(); }
}
