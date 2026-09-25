// 模块：播放器软件音量，不改变 Windows 系统或其它应用的音量。
namespace SkyMusic.Core.Services;

public interface IAudioVolumeControl
{
    double Volume { get; set; }
}
