// 模块：SkyMusic.AudioEngine 原生实现 offline_renderer
#include "skymusic/audio/offline_renderer.h"

#include <algorithm>
#include <vector>

namespace skymusic::audio
{
// 使用离线时钟分块渲染完整音频图
bool OfflineRenderer::render(
    AudioGraph& graph,
    const AudioStreamFormat& format,
    std::uint64_t totalFrames,
    const OfflineBlockSink& sink,
    std::string& error)
{
    auto offlineFormat = format;
    offlineFormat.processingMode = AudioProcessingMode::Offline;
    if (!sink || !graph.prepare(offlineFormat, error))
        return false;

    AudioClock clock;
    clock.configure(offlineFormat.sampleRate);
    std::vector<float> buffer(static_cast<std::size_t>(offlineFormat.maximumFrames) * offlineFormat.channelCount);
    std::uint64_t remaining = totalFrames;
    while (remaining > 0)
    {
        const auto frames = static_cast<std::uint32_t>(std::min<std::uint64_t>(remaining, offlineFormat.maximumFrames));
        graph.render(clock.context(false), buffer.data(), frames, offlineFormat.channelCount);
        if (!sink(buffer.data(), frames, offlineFormat.channelCount))
        {
            graph.release();
            error = "offline audio sink stopped rendering";
            return false;
        }
        clock.advance(frames);
        remaining -= frames;
    }

    graph.release();
    return true;
}
}
