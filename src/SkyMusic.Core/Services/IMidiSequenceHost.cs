// 模块：完整 MIDI 文件与原生乐器的时间线接口。
namespace SkyMusic.Core.Services;

public interface IMidiSequenceHost
{
    // 读取原始 MIDI，不经过用于游戏键位的乐谱转换。
    ValueTask LoadSequenceAsync(string path, CancellationToken cancellationToken = default);

    // 调用者提供统一播放时钟的位置，界面刷新不参与逐音符调度。
    ValueTask SetTransportAsync(bool playing, TimeSpan? position = null, CancellationToken cancellationToken = default);
}
