// 模块：SkyMusic.AudioEngine 原生接口 audio_graph
#pragma once

#include "audio_render_node.h"

#include <cstdint>
#include <string>
#include <vector>

namespace skymusic::audio
{
class AudioGraph
{
public:
    void addNode(IAudioRenderNode& node, float gain = 1.0f);
    void clearNodes() noexcept;

    bool prepare(const AudioStreamFormat& format, std::string& error);
    void render(
        const AudioRenderContext& context,
        float* interleavedOutput,
        std::uint32_t frameCount,
        std::uint16_t channelCount) noexcept;
    void release() noexcept;

private:
    struct NodeSlot
    {
        IAudioRenderNode* node {};
        float gain {1.0f};
    };

    std::vector<NodeSlot> nodes_;
    std::vector<float> scratchBuffer_;
    AudioStreamFormat format_ {};
    bool prepared_ {};
};
}
