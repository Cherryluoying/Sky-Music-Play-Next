// 模块：SkyMusic.Infrastructure 通用模型 MidiImportOptions
namespace SkyMusic.Infrastructure.Scores;

public sealed record MidiImportOptions(
    int MinimumNote = 21,
    int MaximumNote = 108,
    bool ExcludePercussionChannel = true);
