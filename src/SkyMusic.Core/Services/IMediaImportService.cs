// 模块：SkyMusic.Core 统一媒体导入契约
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Services;

public interface IMediaImportService
{
    IReadOnlySet<string> SupportedExtensions { get; }

    ValueTask<MusicTrack> ImportAsync(string sourcePath, CancellationToken cancellationToken = default);

    // 从现有源文件补读标签，不改变曲目 ID、歌单归属或歌词关联。
    ValueTask<MusicTrack> RefreshMetadataAsync(MusicTrack track, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(track);
}
