// 模块：SkyMusic.Core 统一媒体导入契约
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Services;

public interface IMediaImportService
{
    IReadOnlySet<string> SupportedExtensions { get; }

    ValueTask<MusicTrack> ImportAsync(string sourcePath, CancellationToken cancellationToken = default);

    // 扫描现有目录时直接索引源文件，避免在原目录再次生成哈希副本。
    ValueTask<MusicTrack> ImportInPlaceAsync(string sourcePath, string? libraryRoot = null,
        CancellationToken cancellationToken = default) => ImportAsync(sourcePath, cancellationToken);

    // 从现有源文件补读标签，不改变曲目 ID、歌单归属或歌词关联。
    ValueTask<MusicTrack> RefreshMetadataAsync(MusicTrack track, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(track);
}
