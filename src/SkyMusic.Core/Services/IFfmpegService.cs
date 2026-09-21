// 模块：SkyMusic.Core 桌面服务 IFfmpegService
using SkyMusic.Core.ExternalTools;

namespace SkyMusic.Core.Services;

public interface IFfmpegService
{
    string? Locate(string? configuredPath = null);

    ValueTask<ExternalToolStatus> ProbeAsync(
        string? configuredPath = null,
        CancellationToken cancellationToken = default);
}
