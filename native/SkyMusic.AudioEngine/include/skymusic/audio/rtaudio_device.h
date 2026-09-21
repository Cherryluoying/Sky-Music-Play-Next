// 模块：SkyMusic.AudioEngine 原生接口 rtaudio_device
#pragma once

#include "audio_clock.h"
#include "audio_device.h"

#include <atomic>
#include <cstdint>
#include <string>

namespace skymusic::audio
{
struct RtAudioDeviceOptions
{
    std::uint32_t preferredSampleRate {};
    std::uint32_t preferredBufferFrames {256};
    std::uint16_t outputChannels {2};
    bool minimizeLatency {true};
};

struct RtAudioDeviceMetrics
{
    std::uint32_t sampleRate {};
    std::uint32_t bufferFrames {};
    std::uint32_t latencyFrames {};
    std::uint64_t underflowCount {};
};

class RtAudioDevice final : public IAudioDevice
{
public:
    explicit RtAudioDevice(RtAudioDeviceOptions options = {});

    bool run(
        AudioGraph& graph,
        std::atomic<bool>& stopRequested,
        const std::function<void()>& onStarted,
        std::string& error) override;

    const AudioClock& clock() const noexcept;
    RtAudioDeviceMetrics metrics() const noexcept;

private:
    struct CallbackState;

    static int renderCallback(
        void* outputBuffer,
        void* inputBuffer,
        unsigned int frameCount,
        double streamTime,
        unsigned int status,
        void* userData) noexcept;

    RtAudioDeviceOptions options_;
    AudioClock clock_;
    std::atomic<std::uint32_t> sampleRate_ {0};
    std::atomic<std::uint32_t> bufferFrames_ {0};
    std::atomic<std::uint32_t> latencyFrames_ {0};
    std::atomic<std::uint64_t> underflowCount_ {0};
};
}
