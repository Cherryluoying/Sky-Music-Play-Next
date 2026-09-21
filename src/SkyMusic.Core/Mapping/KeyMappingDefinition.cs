// 模块：SkyMusic.Core 键位映射 KeyMappingDefinition
namespace SkyMusic.Core.Mapping;

public sealed record KeyMappingDefinition(
    string Id,
    string Name,
    IReadOnlyList<KeyMappingEntry> Entries,
    IReadOnlyList<string>? WindowProcessNames = null);

public sealed record KeyMappingEntry(int MidiNote, ushort ScanCode, bool IsExtended = false);
