// 模块：已导入内容判定；复用现有本地来源路径，排除在线目录项与无文件的演示记录。
using SkyMusic.Core.Models;

namespace SkyMusic.App.Services;

public static class ImportedMediaFilter
{
    // 不逐行访问磁盘，文件暂时离线时仍保留曲库记录，避免大曲库分类卡顿。
    public static bool Matches(MusicTrack track) =>
        track.Kind is MediaKind.Audio or MediaKind.Score or MediaKind.Midi &&
        !string.IsNullOrWhiteSpace(track.SourcePath) &&
        (!Uri.TryCreate(track.SourcePath, UriKind.Absolute, out var uri) || uri.IsFile);
}
