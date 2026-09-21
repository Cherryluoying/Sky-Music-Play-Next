// 模块：SkyMusic.Core 游戏乐谱领域 GameInstrumentLayer
namespace SkyMusic.Core.GameScores;

public enum GameNoteIcon
{
    Border,
    Circle,
    Line
}

public sealed record GameInstrumentLayer(
    Guid Id,
    string Name,
    int Volume = 100,
    string Pitch = "C",
    bool IsVisible = true,
    GameNoteIcon Icon = GameNoteIcon.Circle,
    string Alias = "",
    bool IsMuted = false,
    bool? ReverbOverride = null);
