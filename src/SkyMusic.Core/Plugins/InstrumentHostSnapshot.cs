// 模块：SkyMusic.Core 插件领域 InstrumentHostSnapshot
namespace SkyMusic.Core.Plugins;

public sealed record InstrumentHostSnapshot(
    InstrumentHostState State,
    InstrumentPluginInfo? Plugin = null,
    string? Error = null);
