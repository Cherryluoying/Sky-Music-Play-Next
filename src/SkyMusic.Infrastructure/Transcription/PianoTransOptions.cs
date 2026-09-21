// 模块：SkyMusic.Infrastructure 扒谱领域 PianoTransOptions
namespace SkyMusic.Infrastructure.Transcription;

public sealed record PianoTransOptions(
    string PackageDirectory,
    string ExecutableName = "PianoTrans.exe",
    TimeSpan? Timeout = null)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromMinutes(30);
}
