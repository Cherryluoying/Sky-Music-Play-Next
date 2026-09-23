// 模块：SkyMusic.App 我喜欢的音乐页面状态
namespace SkyMusic.App.ViewModels;

public sealed class FavoriteViewModel(LibraryViewModel library)
{
    public string Title => "我喜欢的音乐";

    public LibraryViewModel Library { get; } = library;
}
