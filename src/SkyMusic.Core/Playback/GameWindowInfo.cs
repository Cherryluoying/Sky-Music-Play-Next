// 模块：SkyMusic.Core 播放领域 GameWindowInfo
namespace SkyMusic.Core.Playback;

public sealed record GameWindowInfo(
    long Handle,
    string Title,
    string ProcessName,
    int ProcessId)
{
    public string DisplayName => $"{Title} · {ProcessName}";
}
