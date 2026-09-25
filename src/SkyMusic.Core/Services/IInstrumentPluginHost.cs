// 模块：SkyMusic.Core 桌面服务 IInstrumentPluginHost
using SkyMusic.Core.Plugins;

namespace SkyMusic.Core.Services;

public interface IInstrumentPluginHost : IAsyncDisposable
{
    InstrumentHostSnapshot Snapshot { get; }

    event Action<InstrumentHostSnapshot>? Changed;

    IReadOnlyList<InstrumentPluginInfo> DiscoverPlugins(IEnumerable<string> searchPaths);

    ValueTask LoadAsync(InstrumentPluginInfo plugin, CancellationToken cancellationToken = default);

    ValueTask UnloadAsync(CancellationToken cancellationToken = default);

    ValueTask NoteOnAsync(int note, byte velocity = 100, int channel = 0, CancellationToken cancellationToken = default);

    ValueTask NoteOffAsync(int note, byte velocity = 0, int channel = 0, CancellationToken cancellationToken = default);

    ValueTask AllNotesOffAsync(CancellationToken cancellationToken = default);

    // 打开当前 VST3 的原生编辑器窗口；不支持图形编辑器时返回 false。
    ValueTask<bool> OpenEditorAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(false);
}
