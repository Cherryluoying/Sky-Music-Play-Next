// 模块：SkyMusic.App 本地歌单、喜欢与播放历史状态
using System.Collections.ObjectModel;
using SkyMusic.App.Services;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class LibraryViewModel : ObservableObject, IDisposable
{
    private readonly IMediaLibraryStore _store;
    private readonly IMediaImportService _importer;
    private readonly CoverImageService _covers;
    private readonly PlaybackViewModel _playback;
    private readonly List<StoredMediaTrack> _records = [];
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string? _statusText;

    public LibraryViewModel(
        IMediaLibraryStore store,
        IMediaImportService importer,
        CoverImageService covers,
        PlaybackViewModel playback)
    {
        _store = store;
        _importer = importer;
        _covers = covers;
        _playback = playback;
        _playback.MediaLibraryChanged += OnMediaLibraryChanged;
        _ = RefreshAsync();
    }

    public ObservableCollection<TrackItemViewModel> Tracks { get; } = [];
    public ObservableCollection<TrackItemViewModel> FavoriteTracks { get; } = [];
    public ObservableCollection<TrackItemViewModel> RecentTracks { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public IReadOnlySet<string> SupportedExtensions => _importer.SupportedExtensions;

    // 多文件导入逐个隔离错误，成功项目立即写入 SQLite 并刷新统一队列。
    public async Task ImportFilesAsync(IEnumerable<string> paths)
    {
        IsLoading = true;
        var imported = 0;
        var errors = new List<string>();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var track = await _importer.ImportAsync(path);
                await _store.UpsertAsync(track);
                imported++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
            {
                errors.Add($"{Path.GetFileName(path)}：{exception.Message}");
            }
        }

        await RefreshAsync();
        StatusText = errors.Count == 0
            ? $"已导入 {imported} 个媒体文件"
            : $"成功 {imported} 个，失败 {errors.Count} 个 · {string.Join(" · ", errors.Take(2))}";
        IsLoading = false;
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        await _store.InitializeAsync();
        _records.Clear();
        _records.AddRange(await _store.GetPlaylistAsync());
        ApplyFilter();
        IsLoading = false;
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _records
            : _records.Where(record => Matches(record.Track, query)).ToList();
        Replace(Tracks, filtered.Select((record, index) => CreateItem(record, index + 1)));
        Replace(FavoriteTracks, filtered.Where(record => record.IsFavorite).Select((record, index) => CreateItem(record, index + 1)));
        Replace(RecentTracks, filtered
            .Where(record => record.LastPlayedAt is not null)
            .OrderByDescending(record => record.LastPlayedAt)
            .Select((record, index) => CreateItem(record, index + 1)));

        // 统一队列按曲目 ID 去重，搜索和喜欢只是同一曲目的不同投影。
        _playback.SetQueue(_records
            .GroupBy(record => record.Track.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateItem(group.First())));
    }

    private TrackItemViewModel CreateItem(StoredMediaTrack record, int index = 0) => new(
        record.Track,
        _covers.GetCover(record.Track.CoverSource),
        item => _playback.PlayTrack(item),
        item => _ = ToggleFavoriteAsync(item),
        record.IsFavorite,
        index);

    private async Task ToggleFavoriteAsync(TrackItemViewModel item)
    {
        item.IsFavorite = !item.IsFavorite;
        await _store.SetFavoriteAsync(item.Track.Id, item.IsFavorite);
        var index = _records.FindIndex(record => record.Track.Id == item.Track.Id);
        if (index >= 0)
        {
            _records[index] = _records[index] with { IsFavorite = item.IsFavorite };
        }
        ApplyFilter();
    }

    private static bool Matches(MusicTrack track, string query) =>
        track.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        track.Artist.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        track.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        track.Album.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static void Replace(
        ObservableCollection<TrackItemViewModel> target,
        IEnumerable<TrackItemViewModel> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void OnMediaLibraryChanged(object? sender, EventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(async () => await RefreshAsync());

    public void Dispose() => _playback.MediaLibraryChanged -= OnMediaLibraryChanged;
}
