// 模块：媒体分类选择，页面筛选不改变原始曲库记录。
using SkyMusic.Core.Models;
namespace SkyMusic.App.ViewModels;

public static class MediaCategoryFilter
{
    public static IReadOnlyList<string> Labels { get; } = ["全部", "乐谱", "音乐", "MIDI"];
    public static bool Matches(MediaKind kind, int category) => category switch
    {
        1 => kind == MediaKind.Score,
        2 => kind == MediaKind.Audio,
        3 => kind == MediaKind.Midi,
        _ => true
    };
}
