// 模块：SkyMusic.AudioEngine 原生实现 audio_graph
#include "skymusic/audio/audio_graph.h"

#include <algorithm>
#include <cmath>
#include <stdexcept>

namespace skymusic::audio
{
void AudioGraph::addNode(IAudioRenderNode& node, float gain)
{
    if (prepared_)
        throw std::logic_error("audio graph cannot change while prepared");
    nodes_.push_back({&node, gain});
}

void AudioGraph::clearNodes() noexcept
{
    if (!prepared_)
        nodes_.clear();
}

// 在设备启动前准备所有节点与复用缓冲区
bool AudioGraph::prepare(const AudioStreamFormat& format, std::string& error)
{
    if (prepared_ || format.sampleRate == 0 || format.channelCount == 0 || format.maximumFrames == 0)
    {
        error = "audio graph format is invalid";
        return false;
    }

    format_ = format;
    scratchBuffer_.resize(static_cast<std::size_t>(format.maximumFrames) * format.channelCount);
    std::size_t preparedCount = 0;
    for (const auto& slot : nodes_)
    {
        if (!slot.node->prepare(format, error))
        {
            for (std::size_t index = 0; index < preparedCount; ++index)
                nodes_[index].node->release();
            scratchBuffer_.clear();
            return false;
        }
        ++preparedCount;
    }

    prepared_ = true;
    return true;
}

// 在实时回调中混合所有节点并避免动态分配
void AudioGraph::render(
    const AudioRenderContext& context,
    float* interleavedOutput,
    std::uint32_t frameCount,
    std::uint16_t channelCount) noexcept
{
    if (!prepared_ || !interleavedOutput || frameCount > format_.maximumFrames || channelCount != format_.channelCount)
        return;

    const auto sampleCount = static_cast<std::size_t>(frameCount) * channelCount;
    std::fill_n(interleavedOutput, sampleCount, 0.0f);
    for (const auto& slot : nodes_)
    {
        std::fill_n(scratchBuffer_.data(), sampleCount, 0.0f);
        slot.node->render(context, scratchBuffer_.data(), frameCount, channelCount);
        for (std::size_t sample = 0; sample < sampleCount; ++sample)
            interleavedOutput[sample] += scratchBuffer_[sample] * slot.gain;
    }
}

void AudioGraph::release() noexcept
{
    if (!prepared_)
        return;
    for (auto iterator = nodes_.rbegin(); iterator != nodes_.rend(); ++iterator)
        iterator->node->release();
    scratchBuffer_.clear();
    prepared_ = false;
}
}
