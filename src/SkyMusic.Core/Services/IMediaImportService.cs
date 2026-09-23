// 模块：SkyMusic.Core 统一媒体导入契约
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Services;

public interface IMediaImportService
{
    IReadOnlySet<string> SupportedExtensions { get; }

    ValueTask<MusicTrack> ImportAsync(string sourcePath, CancellationToken cancellationToken = default);
}
