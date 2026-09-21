// 模块：SkyMusic.Core 扒谱领域 TranscriptionResult
namespace SkyMusic.Core.Transcription;

public sealed record TranscriptionResult(string MidiPath, TimeSpan Elapsed, string DiagnosticOutput);
