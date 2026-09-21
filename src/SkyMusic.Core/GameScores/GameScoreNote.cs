// 模块：SkyMusic.Core 游戏乐谱领域 GameScoreNote
namespace SkyMusic.Core.GameScores;

public sealed record GameScoreNote(int KeyIndex, ulong LayerMask)
{
    public bool HasLayer(int layer) => (LayerMask & (1UL << layer)) != 0;
}
