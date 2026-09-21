// 模块：SkyMusic.AudioEngine 原生接口 rubber_band_realtime_processor
#pragma once

#include <cstdint>
#include <memory>
#include <string>

namespace skymusic::audio
{
struct RubberBandRealtimeConfig
{
    std::uint32_t sampleRate {48'000};
    std::uint16_t channelCount {2};
    std::uint32_t maximumInputFrames {4'096};
    double timeRatio {1.0};
    double pitchScale {1.0};
};

class RubberBandRealtimeProcessor
{
public:
    RubberBandRealtimeProcessor();
    ~RubberBandRealtimeProcessor();

    RubberBandRealtimeProcessor(const RubberBandRealtimeProcessor&) = delete;
    RubberBandRealtimeProcessor& operator=(const RubberBandRealtimeProcessor&) = delete;
    RubberBandRealtimeProcessor(RubberBandRealtimeProcessor&&) noexcept;
    RubberBandRealtimeProcessor& operator=(RubberBandRealtimeProcessor&&) noexcept;

    bool prepare(const RubberBandRealtimeConfig& config, std::string& error);
    void reset() noexcept;
    bool setRatios(double timeRatio, double pitchScale, std::string& error) noexcept;

    std::uint32_t requiredInputFrames() const noexcept;
    std::int32_t availableOutputFrames() const noexcept;
    std::uint32_t preferredStartPadFrames() const noexcept;
    std::uint32_t startDelayFrames() const noexcept;

    bool processInterleaved(
        const float* input,
        std::uint32_t frameCount,
        bool final,
        std::string& error) noexcept;
    std::uint32_t retrieveInterleaved(float* output, std::uint32_t maximumFrames) noexcept;

private:
    struct State;
    std::unique_ptr<State> state_;
};
}
