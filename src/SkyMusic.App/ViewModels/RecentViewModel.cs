// 模块：SkyMusic.App 界面状态 RecentViewModel
namespace SkyMusic.App.ViewModels;

public sealed class RecentViewModel(PlaybackViewModel playback)
{
    public PlaybackViewModel Playback { get; } = playback;
}
