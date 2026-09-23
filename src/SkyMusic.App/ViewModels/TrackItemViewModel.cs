// 模块：SkyMusic.App 界面状态 TrackItemViewModel
using Avalonia.Media.Imaging;
using SkyMusic.Core.Models;

namespace SkyMusic.App.ViewModels;

public sealed class TrackItemViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isFavorite;

    public TrackItemViewModel(
        MusicTrack track,
        Bitmap? cover,
        Action<TrackItemViewModel> play,
        Action<TrackItemViewModel>? toggleFavorite = null,
        bool isFavorite = false,
        int displayIndex = 0)
    {
        Track = track;
        Cover = cover;
        PlayCommand = new RelayCommand(_ => play(this));
        ToggleFavoriteCommand = new RelayCommand(_ => toggleFavorite?.Invoke(this));
        _isFavorite = isFavorite;
        DisplayIndex = displayIndex;
    }

    public MusicTrack Track { get; private set; }

    public Bitmap? Cover { get; }

    public RelayCommand PlayCommand { get; }

    public RelayCommand ToggleFavoriteCommand { get; }

    public string Title => Track.Title;

    public string Artist => Track.Artist;

    public string Album => Track.Album;

    public string Author => string.IsNullOrWhiteSpace(Track.Author) ? Artist : Track.Author;

    public MediaKind Kind => Track.Kind;

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteGlyph));
            }
        }
    }

    public string FavoriteGlyph => IsFavorite ? "\uEB52" : "\uEB51";

    public int DisplayIndex { get; }

    public string DisplayIndexText => DisplayIndex > 0 ? DisplayIndex.ToString() : "";

    public string DurationText => Track.Duration.ToString(@"mm\:ss");

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    // 本地歌词关联等元数据更新后，保持列表项和播放队列对象不变。
    public void UpdateTrack(MusicTrack track)
    {
        Track = track;
        OnPropertyChanged(nameof(Track));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Artist));
        OnPropertyChanged(nameof(Album));
        OnPropertyChanged(nameof(Author));
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(DurationText));
    }
}
