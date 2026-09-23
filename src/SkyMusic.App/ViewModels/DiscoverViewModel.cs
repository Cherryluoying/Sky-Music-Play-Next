// 模块：SkyMusic.App 界面状态 DiscoverViewModel
using System.Collections.ObjectModel;
using SkyMusic.App.Services;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class DiscoverViewModel : ObservableObject
{
    private readonly IMusicCatalog _catalog;
    private readonly CoverImageService _coverImages;
    private readonly PlaybackViewModel _playback;
    private readonly IMediaLibraryStore? _mediaLibrary;
    private readonly List<TrackItemViewModel> _allTracks = [];
    private string _searchText = string.Empty;
    private TrackItemViewModel? _bannerTrack;
    private bool _isLoading = true;
    private string? _errorMessage;

    public DiscoverViewModel(
        IMusicCatalog catalog,
        CoverImageService coverImages,
        PlaybackViewModel playback,
        IMediaLibraryStore? mediaLibrary = null)
    {
        _catalog = catalog;
        _coverImages = coverImages;
        _playback = playback;
        _mediaLibrary = mediaLibrary;
        _ = InitializeAsync();
    }

    public ObservableCollection<TrackItemViewModel> RecommendedTracks { get; } = [];

    public ObservableCollection<TrackItemViewModel> PopularTracks { get; } = [];

    public ObservableCollection<TrackItemViewModel> LatestTracks { get; } = [];

    // 首页只读展示播放器的最近播放队列，不改变播放器核心状态。
    public PlaybackViewModel Playback => _playback;

    public IReadOnlyList<CategoryItemViewModel> Categories { get; } =
    [
        new("原神乐谱", "\uE7FC"),
        new("光遇乐谱", "\uE753"),
        new("钢琴 88 键", "\uE92E"),
        new("MIDI", "\uE8D6"),
        new("最新上传", "\uE895")
    ];

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

    public TrackItemViewModel? BannerTrack
    {
        get => _bannerTrack;
        private set => SetProperty(ref _bannerTrack, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    private async Task InitializeAsync()
    {
        try
        {
            var tracks = await _catalog.GetFeaturedAsync();
            foreach (var track in tracks)
            {
                var item = new TrackItemViewModel(
                    track,
                    _coverImages.GetCover(track.CoverSource),
                    selected => _playback.PlayTrack(selected),
                    favorite => _playback.ToggleFavoriteCommand.Execute(favorite));
                _allTracks.Add(item);
                if (_mediaLibrary is not null)
                {
                    await _mediaLibrary.UpsertAsync(track, null);
                }
            }

            _playback.SetQueue(_allTracks);
            ApplyFilter();
        }
        catch (Exception exception)
        {
            ErrorMessage = $"内容加载失败：{exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _allTracks
            : _allTracks.Where(item =>
                    item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Artist.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Album.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        BannerTrack = filtered.FirstOrDefault();
        Replace(RecommendedTracks, filtered.Take(5));
        Replace(PopularTracks, filtered.OrderByDescending(item => item.Track.Duration).Take(5));
        Replace(LatestTracks, filtered.AsEnumerable().Reverse().Take(5));
    }

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
}
