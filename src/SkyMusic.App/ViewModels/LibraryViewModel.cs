// 模块：SkyMusic.App 本地歌单、喜欢与播放历史状态
using System.Collections.ObjectModel;
using SkyMusic.App.Services;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;
using SkyMusic.Core.Settings;
using SkyMusic.Infrastructure.Catalog;

namespace SkyMusic.App.ViewModels;

public sealed class LibraryViewModel : ObservableObject, IDisposable
{
    private readonly IMediaLibraryStore _store;
    private readonly IMediaImportService _importer;
    private readonly CoverImageService _covers;
    private readonly PlaybackViewModel _playback;
    private readonly List<StoredMediaTrack> _records = [];
    private readonly HashSet<string> _metadataChecked = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly IAppSettingsStore? _settingsStore;
    private readonly SemaphoreSlim _importGate = new(1, 1);
    private bool _isImporting;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string? _statusText;
    private int _categoryIndex;
    private int _favoriteCategoryIndex;
    public IReadOnlyList<string> Categories => MediaCategoryFilter.Labels;
    public PlaybackViewModel Playback => _playback;
    public int CategoryIndex { get => _categoryIndex; set { if (SetProperty(ref _categoryIndex, value)) ApplyFilter(); } }
    public int FavoriteCategoryIndex { get => _favoriteCategoryIndex; set { if (SetProperty(ref _favoriteCategoryIndex, value)) ApplyFilter(); } }

    public LibraryViewModel(
        IMediaLibraryStore store,
        IMediaImportService importer,
        CoverImageService covers,
        PlaybackViewModel playback,
        IAppSettingsStore? settingsStore = null)
    {
        _store = store;
        _importer = importer;
        _covers = covers;
        _playback = playback;
        _settingsStore = settingsStore;
        ScanDirectoriesCommand = new AsyncRelayCommand(_ => ScanConfiguredDirectoriesAsync(),
            _ => !IsImporting, exception => StatusText = exception.Message);
        _playback.MediaLibraryChanged += OnMediaLibraryChanged;
        _ = InitializeAsync();
    }

    public ObservableCollection<TrackItemViewModel> Tracks { get; } = [];
    // 导入、目录扫描、收藏与元数据更新后，通知其他本地媒体视图刷新。
    public event EventHandler? Refreshed;
    public ObservableCollection<TrackItemViewModel> FavoriteTracks { get; } = [];
    public ObservableCollection<TrackItemViewModel> RecentTracks { get; } = [];
    public AsyncRelayCommand ScanDirectoriesCommand { get; }
    public bool IsImporting
    {
        get => _isImporting;
        private set
        {
            if (SetProperty(ref _isImporting, value)) ScanDirectoriesCommand.NotifyCanExecuteChanged();
        }
    }

    // 启动先显示数据库中的歌单，再索引已配置目录；发现文件不依赖手工逐首导入。
    private async Task InitializeAsync()
    {
        await RefreshAsync();
        if (_settingsStore is not null) await ScanConfiguredDirectoriesAsync();
    }

    public async Task ScanConfiguredDirectoriesAsync()
    {
        try
        {
            var settings = _settingsStore is null ? new StorageSettings()
                : (await _settingsStore.LoadAsync(_lifetime.Token)).Storage;
            var root = MediaLibraryDirectories.Root(settings);
            var directories = new List<string> { root };
            if (!string.IsNullOrWhiteSpace(settings.ScoreLibraryDirectory)) directories.Add(MediaLibraryDirectories.Score(settings));
            if (!string.IsNullOrWhiteSpace(settings.MidiLibraryDirectory)) directories.Add(MediaLibraryDirectories.Midi(settings));
            await ImportDirectoriesAsync(directories);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { StatusText = $"扫描曲库失败：{exception.Message}"; }
    }

    // 切换数据库前等待当前导入结束，避免同一批文件跨库写入。
    public async Task SaveStorageSettingsAsync(string directory, Func<Task> persistSettings)
    {
        await _importGate.WaitAsync(_lifetime.Token);
        try
        {
            await _refreshGate.WaitAsync(_lifetime.Token);
            try
            {
                await Task.Run(() => _store.ChangeDirectoryAsync(directory, persistSettings, _lifetime.Token));
                _metadataChecked.Clear();
            }
            finally { _refreshGate.Release(); }
        }
        finally { _importGate.Release(); }
    }

    public async Task ImportDirectoriesAsync(IEnumerable<string> paths)
    {
        var entered = false;
        try
        {
            await _importGate.WaitAsync(_lifetime.Token);
            entered = true;
            IsImporting = true;
            StatusText = "正在查找目录中的乐谱、音乐和 MIDI…";
            var progress = new Progress<string>(message => { if (IsImporting) StatusText = message; });
            var scanner = new MediaDirectoryScanner(_importer, _store);
            var result = await Task.Run(() => scanner.ScanAsync(paths, progress, _lifetime.Token), _lifetime.Token);
            foreach (var record in await _store.GetAllAsync(_lifetime.Token)) _metadataChecked.Add(record.Track.Id);
            await RefreshAsync();
            StatusText = result.Summary;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { StatusText = $"目录扫描失败：{exception.Message}"; }
        finally
        {
            if (entered) { IsImporting = false; _importGate.Release(); }
        }
    }

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
    public void ReportImportError(string message) => StatusText = $"导入失败：{message}";

    // 多文件导入逐个隔离错误，成功项目立即写入 SQLite 并刷新统一队列。
    public async Task ImportFilesAsync(IEnumerable<string> paths)
    {
        var entered = false;
        var imported = 0;
        var errors = new List<string>();
        try
        {
            await _importGate.WaitAsync(_lifetime.Token);
            entered = true;
            IsImporting = true;
            foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var track = await Task.Run(async () => await _importer.ImportAsync(path, _lifetime.Token), _lifetime.Token);
                    await _store.UpsertAsync(track, cancellationToken: _lifetime.Token);
                    _metadataChecked.Add(track.Id);
                    imported++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception)
                {
                    errors.Add($"{Path.GetFileName(path)}：{exception.Message}");
                }
            }

            await RefreshAsync();
            StatusText = errors.Count == 0
                ? $"已导入 {imported} 个媒体文件"
                : $"成功 {imported} 个，失败 {errors.Count} 个 · {string.Join(" · ", errors.Take(2))}";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { StatusText = $"媒体导入失败：{exception.Message}"; }
        finally
        {
            if (entered) { IsImporting = false; _importGate.Release(); }
        }
    }

    public async Task RefreshAsync()
    {
        var entered = false;
        try
        {
            await _refreshGate.WaitAsync(_lifetime.Token);
            entered = true;
            IsLoading = true;
            await _store.InitializeAsync(_lifetime.Token);
            _records.Clear();
            _records.AddRange(await _store.GetPlaylistAsync(cancellationToken: _lifetime.Token));
            ApplyFilter();
            // 先展示已有曲库，再异步补读旧版本缺失的标签；本次启动每首只扫描一次。
            foreach (var record in await _store.GetAllAsync(_lifetime.Token))
            {
                if (record.Track.Kind != MediaKind.Audio || _metadataChecked.Contains(record.Track.Id)) continue;
                var updated = await _importer.RefreshMetadataAsync(record.Track, _lifetime.Token);
                if (updated != record.Track) await _store.UpdateMetadataAsync(updated, _lifetime.Token);
                _metadataChecked.Add(record.Track.Id);
            }
            // 重读数据库，以免补读期间用户刚修改的收藏或歌词被旧快照覆盖。
            _records.Clear();
            _records.AddRange(await _store.GetPlaylistAsync(cancellationToken: _lifetime.Token));
            ApplyFilter();
            Refreshed?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            StatusText = $"曲库信息更新失败：{exception.Message}";
        }
        finally
        {
            if (entered) { IsLoading = false; _refreshGate.Release(); }
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _records
            : _records.Where(record => Matches(record.Track, query)).ToList();
        Replace(Tracks, filtered.Where(record => MediaCategoryFilter.Matches(record.Track.Kind, CategoryIndex)).Select((record, index) => CreateItem(record, index + 1, Tracks)));
        Replace(FavoriteTracks, filtered.Where(record => record.IsFavorite && MediaCategoryFilter.Matches(record.Track.Kind, FavoriteCategoryIndex)).Select((record, index) => CreateItem(record, index + 1, FavoriteTracks)));
        Replace(RecentTracks, filtered
            .Where(record => record.LastPlayedAt is not null)
            .OrderByDescending(record => record.LastPlayedAt)
            .Select((record, index) => CreateItem(record, index + 1)));

        // 统一队列按曲目 ID 去重，搜索和喜欢只是同一曲目的不同投影。
        _playback.SetQueue(_records
            .GroupBy(record => record.Track.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateItem(group.First())));
    }

    private TrackItemViewModel CreateItem(StoredMediaTrack record, int index = 0, IEnumerable<TrackItemViewModel>? context = null) => new(
        record.Track,
        _covers.GetCover(record.Track.CoverSource),
        item => { if (context is null) _playback.PlayTrack(item); else _playback.PlayFromCollection(item, context); },
        item => _playback.ToggleFavoriteCommand.Execute(item),
        record.IsFavorite,
        index,
        item => _playback.PlayNext(item));

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

    public void Dispose()
    {
        _playback.MediaLibraryChanged -= OnMediaLibraryChanged;
        _lifetime.Cancel();
    }
}
