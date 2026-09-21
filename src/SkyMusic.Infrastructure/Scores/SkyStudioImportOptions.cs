// 模块：SkyMusic.Infrastructure 通用模型 SkyStudioImportOptions
namespace SkyMusic.Infrastructure.Scores;

public sealed record SkyStudioImportOptions(
    int DefaultDurationMilliseconds = 80,
    int MinimumDurationMilliseconds = 10,
    bool ApplyPitchLevel = false);
