// 模块：SkyMusic.Core 桌面服务 IInstrumentPreviewService
using SkyMusic.Core.GameScores;

namespace SkyMusic.Core.Services;

public interface IInstrumentPreviewService : IDisposable
{
    ValueTask PreloadAsync(
        GameScoreProfile profile,
        string instrumentId,
        CancellationToken cancellationToken = default);

    ValueTask PlayAsync(
        GameScoreProfile profile,
        string instrumentId,
        int noteIndex,
        int volume,
        CancellationToken cancellationToken = default);
}
