// 模块：SkyMusic.AudioEngine 原生接口 sample_player_node
#pragma once

#include "audio_render_node.h"

#include <array>
#include <atomic>
#include <cstdint>
#include <memory>
#include <vector>

namespace skymusic::audio
{
class SamplePlayerNode final : public IAudioRenderNode
{
public:
    static constexpr std::uint32_t MaximumSamples = 1024;
    static constexpr std::uint32_t MaximumVoices = 64;

    bool setSample(std::uint32_t slot, std::vector<float> interleavedStereo);
    bool trigger(std::uint32_t slot, float gain) noexcept;

    bool prepare(const AudioStreamFormat& format, std::string& error) override;
    void render(
        const AudioRenderContext& context,
        float* interleavedOutput,
        std::uint32_t frameCount,
        std::uint16_t channelCount) noexcept override;
    void release() noexcept override;

private:
    struct SampleData
    {
        std::vector<float> samples;
    };

    struct Voice
    {
        std::shared_ptr<const SampleData> sample;
        std::uint64_t frame {};
        float gain {1.0f};
    };

    void startVoice(std::uint32_t slot) noexcept;

    std::array<std::shared_ptr<const SampleData>, MaximumSamples> samples_ {};
    std::array<std::atomic<std::uint64_t>, MaximumSamples> triggerVersions_ {};
    std::array<std::uint64_t, MaximumSamples> consumedVersions_ {};
    std::array<std::atomic<float>, MaximumSamples> gains_ {};
    std::array<Voice, MaximumVoices> voices_ {};
    std::uint32_t nextVoice_ {};
    bool prepared_ {};
};
}
