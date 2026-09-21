// 模块：SkyMusic.AudioEngine 原生接口 audio_render_node
#pragma once

#include "audio_types.h"

#include <cstdint>
#include <string>

namespace skymusic::audio
{
class IAudioRenderNode
{
public:
    virtual ~IAudioRenderNode() = default;

    virtual bool prepare(const AudioStreamFormat& format, std::string& error) = 0;
    virtual void render(
        const AudioRenderContext& context,
        float* interleavedOutput,
        std::uint32_t frameCount,
        std::uint16_t channelCount) noexcept = 0;
    virtual void release() noexcept = 0;
};
}
