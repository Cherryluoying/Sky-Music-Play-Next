// 模块：SkyMusic.AudioEngine 原生接口 audio_clock
#pragma once

#include "audio_types.h"

#include <atomic>
#include <cstdint>

namespace skymusic::audio
{
class AudioClock
{
public:
    void configure(double sampleRate) noexcept;
    void reset(std::uint64_t samplePosition = 0) noexcept;
    AudioRenderContext context(bool realtime, double tempo = 120.0) const noexcept;
    void advance(std::uint32_t frames) noexcept;

    std::uint64_t samplePosition() const noexcept;
    double sampleRate() const noexcept;

private:
    std::atomic<std::uint64_t> samplePosition_ {0};
    std::atomic<double> sampleRate_ {0.0};
};
}
