// 模块：SkyMusic.AudioEngine 原生接口 audio_device
#pragma once

#include "audio_graph.h"

#include <atomic>
#include <functional>
#include <string>

namespace skymusic::audio
{
class IAudioDevice
{
public:
    virtual ~IAudioDevice() = default;

    virtual bool run(
        AudioGraph& graph,
        std::atomic<bool>& stopRequested,
        const std::function<void()>& onStarted,
        std::string& error) = 0;
};
}
