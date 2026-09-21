// 模块：SkyMusic.AudioEngine 原生实现 sample_player_node
#include "skymusic/audio/sample_player_node.h"

#include <algorithm>
#include <cmath>

namespace skymusic::audio
{
// 在非实时线程原子替换采样数据
bool SamplePlayerNode::setSample(std::uint32_t slot, std::vector<float> interleavedStereo)
{
    if (slot >= MaximumSamples || interleavedStereo.size() < 2 || interleavedStereo.size() % 2 != 0)
        return false;

    auto sample = std::make_shared<SampleData>();
    sample->samples = std::move(interleavedStereo);
    std::atomic_store_explicit(
        &samples_[slot],
        std::shared_ptr<const SampleData>(std::move(sample)),
        std::memory_order_release);
    return true;
}

bool SamplePlayerNode::trigger(std::uint32_t slot, float gain) noexcept
{
    if (slot >= MaximumSamples || !std::atomic_load_explicit(&samples_[slot], std::memory_order_acquire))
        return false;

    gains_[slot].store(std::clamp(gain, 0.0f, 1.5f), std::memory_order_relaxed);
    triggerVersions_[slot].fetch_add(1, std::memory_order_release);
    return true;
}

bool SamplePlayerNode::prepare(const AudioStreamFormat& format, std::string& error)
{
    if (format.channelCount == 0 || format.channelCount > 2)
    {
        error = "sample player supports mono or stereo output";
        return false;
    }
    prepared_ = true;
    return true;
}

// 在固定复音池中混合所有活动采样
void SamplePlayerNode::render(
    const AudioRenderContext&,
    float* interleavedOutput,
    std::uint32_t frameCount,
    std::uint16_t channelCount) noexcept
{
    if (!prepared_ || !interleavedOutput)
        return;

    // 触发计数让音频线程无锁接收按键事件
    for (std::uint32_t slot = 0; slot < MaximumSamples; ++slot)
    {
        const auto version = triggerVersions_[slot].load(std::memory_order_acquire);
        if (version == consumedVersions_[slot])
            continue;
        consumedVersions_[slot] = version;
        startVoice(slot);
    }

    for (auto& voice : voices_)
    {
        if (!voice.sample)
            continue;
        const auto totalFrames = voice.sample->samples.size() / 2;
        for (std::uint32_t frame = 0; frame < frameCount && voice.frame < totalFrames; ++frame, ++voice.frame)
        {
            const auto source = static_cast<std::size_t>(voice.frame) * 2;
            const auto destination = static_cast<std::size_t>(frame) * channelCount;
            if (channelCount == 1)
            {
                interleavedOutput[destination] +=
                    (voice.sample->samples[source] + voice.sample->samples[source + 1]) * 0.5f * voice.gain;
            }
            else
            {
                interleavedOutput[destination] += voice.sample->samples[source] * voice.gain;
                interleavedOutput[destination + 1] += voice.sample->samples[source + 1] * voice.gain;
            }
        }
        if (voice.frame >= totalFrames)
            voice = {};
    }
}

void SamplePlayerNode::release() noexcept
{
    for (auto& voice : voices_)
        voice = {};
    prepared_ = false;
}

void SamplePlayerNode::startVoice(std::uint32_t slot) noexcept
{
    auto sample = std::atomic_load_explicit(&samples_[slot], std::memory_order_acquire);
    if (!sample)
        return;
    voices_[nextVoice_++ % MaximumVoices] = {
        std::move(sample),
        0,
        gains_[slot].load(std::memory_order_relaxed)
    };
}
}
