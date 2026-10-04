// 模块：SkyMusic.Core 桌面服务 IInstrumentPluginHost
using SkyMusic.Core.Plugins;
using SkyMusic.Core.Midi;

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

    // 兼容仅支持音符的宿主；VST3 宿主覆盖此方法传递踏板、弯音等控制器。
    ValueTask SendMessageAsync(MidiChannelMessage message, CancellationToken cancellationToken = default)
    {
        if (!message.IsValid) throw new ArgumentOutOfRangeException(nameof(message));
        return message.Kind switch
        {
            MidiChannelMessageKind.NoteOn => NoteOnAsync(message.Data1, (byte)message.Data2, message.Channel, cancellationToken),
            MidiChannelMessageKind.NoteOff => NoteOffAsync(message.Data1, (byte)message.Data2, message.Channel, cancellationToken),
            _ => ValueTask.FromException(new NotSupportedException("宿主不支持 MIDI 控制器消息"))
        };
    }

    // 打开当前 VST3 的原生编辑器窗口；不支持图形编辑器时返回 false。
    ValueTask<bool> OpenEditorAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(false);
}
