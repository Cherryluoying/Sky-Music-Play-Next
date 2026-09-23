// 模块：SkyMusic.Infrastructure 输入设备 DefaultKeyMappings
using SkyMusic.Core.Mapping;

namespace SkyMusic.Infrastructure.Input;

public static class DefaultKeyMappings
{
    public static IReadOnlyList<KeyMappingDefinition> Create()
        =>
        [
            ToDefinition("builtin:sky-15", "内置 · 光遇 15 键", LegacySky15KeyProfile.Create(), ["Sky", "Sky-Windows", "SkyClient"]),
            ToDefinition("builtin:genshin-21", "内置 · 原神 21 键", Genshin21KeyProfile.Create(), ["YuanShen", "GenshinImpact", "Genshin"])
        ];

    private static KeyMappingDefinition ToDefinition(
        string id,
        string name,
        KeyboardMappingProfile profile,
        IReadOnlyList<string> processNames)
        => new(
            id,
            name,
            profile.Bindings
                .OrderBy(pair => pair.Key)
                .Select(pair => new KeyMappingEntry(pair.Key, pair.Value.ScanCode, pair.Value.IsExtended))
                .ToArray(),
            processNames);
}
