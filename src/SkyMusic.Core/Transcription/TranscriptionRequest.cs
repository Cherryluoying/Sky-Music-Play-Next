// 模块：SkyMusic.Core 扒谱领域 TranscriptionRequest
namespace SkyMusic.Core.Transcription;

public sealed record TranscriptionRequest(string SourcePath, TranscriptionMode Mode = TranscriptionMode.Automatic);
