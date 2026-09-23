// 模块：SkyMusic.App 最近播放页面状态
namespace SkyMusic.App.ViewModels;

public sealed class RecentViewModel(LibraryViewModel library)
{
    public LibraryViewModel Library { get; } = library;
}
