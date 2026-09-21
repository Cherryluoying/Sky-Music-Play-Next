// 模块：SkyMusic.Core 桌面服务 ITranscriptionAdapter
using SkyMusic.Core.Transcription;

namespace SkyMusic.Core.Services;

public interface ITranscriptionAdapter
{
    string Name { get; }

    bool IsAvailable { get; }

    Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        IProgress<TranscriptionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
