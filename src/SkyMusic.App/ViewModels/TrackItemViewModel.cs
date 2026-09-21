// 模块：SkyMusic.App 界面状态 TrackItemViewModel
using Avalonia.Media.Imaging;
using SkyMusic.Core.Models;

namespace SkyMusic.App.ViewModels;

public sealed class TrackItemViewModel : ObservableObject
{
    private bool _isSelected;

    public TrackItemViewModel(MusicTrack track, Bitmap? cover, Action<TrackItemViewModel> play)
    {
        Track = track;
        Cover = cover;
        PlayCommand = new RelayCommand(_ => play(this));
    }

    public MusicTrack Track { get; }

    public Bitmap? Cover { get; }

    public RelayCommand PlayCommand { get; }

    public string Title => Track.Title;

    public string Artist => Track.Artist;

    public string Album => Track.Album;

    public string DurationText => Track.Duration.ToString(@"mm\:ss");

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
