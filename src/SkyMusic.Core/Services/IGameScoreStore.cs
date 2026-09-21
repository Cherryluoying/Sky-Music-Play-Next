// 模块：SkyMusic.Core 桌面服务 IGameScoreStore
using SkyMusic.Core.GameScores;

namespace SkyMusic.Core.Services;

public interface IGameScoreStore
{
    ValueTask<GameScoreDocument> LoadAsync(Stream source, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(
        GameScoreDocument document,
        Stream destination,
        CancellationToken cancellationToken = default);

    ValueTask SaveLegacyAsync(
        GameScoreDocument document,
        Stream destination,
        CancellationToken cancellationToken = default);
}
