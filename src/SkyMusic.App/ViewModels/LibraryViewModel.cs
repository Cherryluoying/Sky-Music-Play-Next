// 模块：SkyMusic.App 界面状态 LibraryViewModel
namespace SkyMusic.App.ViewModels;

public sealed class LibraryViewModel(PlaybackViewModel playback)
{
    public PlaybackViewModel Playback { get; } = playback;
}
