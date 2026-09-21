// 模块：SkyMusic.AudioEngine 原生接口 offline_renderer
#pragma once

#include "audio_clock.h"
#include "audio_graph.h"

#include <cstdint>
#include <functional>
#include <string>

namespace skymusic::audio
{
using OfflineBlockSink = std::function<bool(const float*, std::uint32_t, std::uint16_t)>;

class OfflineRenderer
{
public:
    bool render(
        AudioGraph& graph,
        const AudioStreamFormat& format,
        std::uint64_t totalFrames,
        const OfflineBlockSink& sink,
        std::string& error);
};
}
