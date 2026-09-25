// 模块：SkyMusic.Core MIDI 输出路由
using SkyMusic.Core.Plugins;

namespace SkyMusic.Core.Services;

/// <summary>
/// 控制 MIDI 是否交给外部 VST3 乐器输出，避免与系统 MCI 音源叠音。
/// </summary>
public interface IMidiPlaybackOutputControl
{
    bool UseExternalInstrument { get; }
    // 关闭旧输出、加载音色、恢复位置必须作为同一次播放操作执行。
    ValueTask LoadInstrumentAsync(InstrumentPluginInfo plugin, CancellationToken cancellationToken = default);
}
