// 模块：悬浮演奏窗的单面板状态；复用曲库与播放接口，搜索不改变主页面的筛选。
using System.Collections.ObjectModel;
using SkyMusic.App.Services;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class FloatingPlayerViewModel : ObservableObject, IDisposable
{
    private readonly IMediaLibraryStore _store;
    private readonly CoverImageService _covers;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IReadOnlyList<StoredMediaTrack> _records = [];
    private string _section = "playlist";
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string? _error;
    private int _sectionVersion;

    public FloatingPlayerViewModel(IMediaLibraryStore store, CoverImageService covers, PlaybackViewModel playback)
    {
        _store = store;
        _covers = covers;
        Playback = playback;
    }

    public PlaybackViewModel Playback { get; }
    public ObservableCollection<TrackItemViewModel> Tracks { get; } = [];
    public bool IsPlaylist => _section == "playlist";
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

    // 四个入口只替换当前气泡内部内容，始终共用同一套播放控制。
    public async Task<bool> SelectSectionAsync(string section)
    {
        if (section is not ("playlist" or "favorites" or "search" or "settings")) return false;
        var version = ++_sectionVersion;
        if (section == "search")
        {
            try { await Playback.PauseForTextInputAsync(); }
            catch (Exception ex) { Error = ex.Message; return false; }
        }
        if (_lifetime.IsCancellationRequested || version != _sectionVersion) return false;
        _section = section;
        foreach (var property in new[] { nameof(IsPlaylist), nameof(IsFavorites), nameof(IsSearch), nameof(IsSettings), nameof(ShowsTracks) })
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
            await _store.InitializeAsync(_lifetime.Token);
            var section = _section;
            var records = IsPlaylist
                ? await _store.GetPlaylistAsync(cancellationToken: _lifetime.Token)
                : await _store.GetAllAsync(_lifetime.Token);
            if (!_lifetime.IsCancellationRequested && section == _section) { _records = records; Filter(); }
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
            .Where(record => string.IsNullOrEmpty(query) || new[] { record.Track.Title, record.Track.Artist, record.Track.Album, record.Track.Author }
                .Any(text => text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            .GroupBy(record => record.Track.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First());
        Tracks.Clear();
        foreach (var record in records)
            Tracks.Add(new TrackItemViewModel(record.Track, _covers.GetCover(record.Track.CoverSource),
                item => Playback.PlayTrack(item), isFavorite: record.IsFavorite));
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Dispose() => _lifetime.Cancel();
}
