// 模块：SkyMusic.AudioEngine 原生实现 audio_clock
#include "skymusic/audio/audio_clock.h"

namespace skymusic::audio
{
void AudioClock::configure(double sampleRate) noexcept
{
    sampleRate_.store(sampleRate, std::memory_order_release);
}

void AudioClock::reset(std::uint64_t samplePosition) noexcept
{
    samplePosition_.store(samplePosition, std::memory_order_release);
}

AudioRenderContext AudioClock::context(bool realtime, double tempo) const noexcept
{
    return {
        samplePosition_.load(std::memory_order_acquire),
        sampleRate_.load(std::memory_order_acquire),
        tempo,
        realtime
    };
}

void AudioClock::advance(std::uint32_t frames) noexcept
{
    samplePosition_.fetch_add(frames, std::memory_order_release);
}

std::uint64_t AudioClock::samplePosition() const noexcept
{
    return samplePosition_.load(std::memory_order_acquire);
}

double AudioClock::sampleRate() const noexcept
{
    return sampleRate_.load(std::memory_order_acquire);
}
}
