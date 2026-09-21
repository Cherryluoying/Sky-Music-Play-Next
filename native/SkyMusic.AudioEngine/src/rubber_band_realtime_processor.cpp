// 模块：SkyMusic.AudioEngine 原生实现 rubber_band_realtime_processor
#include "skymusic/audio/rubber_band_realtime_processor.h"

#include "rubberband/RubberBandStretcher.h"

#include <algorithm>
#include <cmath>
#include <limits>
#include <vector>

namespace skymusic::audio
{
struct RubberBandRealtimeProcessor::State
{
    RubberBandRealtimeConfig config;
    std::unique_ptr<RubberBand::RubberBandStretcher> stretcher;
    std::vector<std::vector<float>> inputChannels;
    std::vector<std::vector<float>> outputChannels;
    std::vector<const float*> inputPointers;
    std::vector<float*> outputPointers;
};

namespace
{
std::uint32_t clampFrameCount(std::size_t value) noexcept
{
    return value > std::numeric_limits<std::uint32_t>::max()
        ? std::numeric_limits<std::uint32_t>::max()
        : static_cast<std::uint32_t>(value);
}
}

RubberBandRealtimeProcessor::RubberBandRealtimeProcessor() = default;
RubberBandRealtimeProcessor::~RubberBandRealtimeProcessor() = default;
RubberBandRealtimeProcessor::RubberBandRealtimeProcessor(RubberBandRealtimeProcessor&&) noexcept = default;
RubberBandRealtimeProcessor& RubberBandRealtimeProcessor::operator=(RubberBandRealtimeProcessor&&) noexcept = default;

// 预分配 Rubber Band 状态与声道缓冲区
bool RubberBandRealtimeProcessor::prepare(const RubberBandRealtimeConfig& config, std::string& error)
{
    if (config.sampleRate < 8'000 || config.sampleRate > 192'000 ||
        config.channelCount == 0 || config.maximumInputFrames == 0 ||
        !std::isfinite(config.timeRatio) || config.timeRatio <= 0.0 ||
        !std::isfinite(config.pitchScale) || config.pitchScale <= 0.0)
    {
        error = "Rubber Band configuration is invalid";
        return false;
    }

    try
    {
        auto next = std::make_unique<State>();
        next->config = config;
        const auto options = RubberBand::RubberBandStretcher::OptionProcessRealTime |
                             RubberBand::RubberBandStretcher::OptionEngineFaster |
                             RubberBand::RubberBandStretcher::OptionThreadingNever |
                             RubberBand::RubberBandStretcher::OptionWindowShort;
        next->stretcher = std::make_unique<RubberBand::RubberBandStretcher>(
            config.sampleRate,
            config.channelCount,
            options,
            config.timeRatio,
            config.pitchScale);
        next->stretcher->setMaxProcessSize(config.maximumInputFrames);
        next->inputChannels.assign(config.channelCount, std::vector<float>(config.maximumInputFrames));
        next->outputChannels.assign(config.channelCount, std::vector<float>(config.maximumInputFrames));
        next->inputPointers.resize(config.channelCount);
        next->outputPointers.resize(config.channelCount);
        for (std::uint16_t channel = 0; channel < config.channelCount; ++channel)
        {
            next->inputPointers[channel] = next->inputChannels[channel].data();
            next->outputPointers[channel] = next->outputChannels[channel].data();
        }
        state_ = std::move(next);
        return true;
    }
    catch (...)
    {
        state_.reset();
        error = "Rubber Band realtime processor could not be prepared";
        return false;
    }
}

void RubberBandRealtimeProcessor::reset() noexcept
{
    if (state_ && state_->stretcher)
        state_->stretcher->reset();
}

bool RubberBandRealtimeProcessor::setRatios(
    double timeRatio,
    double pitchScale,
    std::string& error) noexcept
{
    if (!state_ || !std::isfinite(timeRatio) || timeRatio <= 0.0 ||
        !std::isfinite(pitchScale) || pitchScale <= 0.0)
    {
        error = "Rubber Band ratios are invalid";
        return false;
    }

    try
    {
        // 与 process 在同一实时线程调用
        state_->stretcher->setTimeRatio(timeRatio);
        state_->stretcher->setPitchScale(pitchScale);
        state_->config.timeRatio = timeRatio;
        state_->config.pitchScale = pitchScale;
        return true;
    }
    catch (...)
    {
        error = "Rubber Band ratios could not be changed";
        return false;
    }
}

std::uint32_t RubberBandRealtimeProcessor::requiredInputFrames() const noexcept
{
    return state_ && state_->stretcher
        ? clampFrameCount(state_->stretcher->getSamplesRequired())
        : 0;
}

std::int32_t RubberBandRealtimeProcessor::availableOutputFrames() const noexcept
{
    return state_ && state_->stretcher ? state_->stretcher->available() : 0;
}

std::uint32_t RubberBandRealtimeProcessor::preferredStartPadFrames() const noexcept
{
    return state_ && state_->stretcher
        ? clampFrameCount(state_->stretcher->getPreferredStartPad())
        : 0;
}

std::uint32_t RubberBandRealtimeProcessor::startDelayFrames() const noexcept
{
    return state_ && state_->stretcher
        ? clampFrameCount(state_->stretcher->getStartDelay())
        : 0;
}

// 将交错输入转换为 Rubber Band 声道块
bool RubberBandRealtimeProcessor::processInterleaved(
    const float* input,
    std::uint32_t frameCount,
    bool final,
    std::string& error) noexcept
{
    if (!state_ || frameCount > state_->config.maximumInputFrames ||
        (frameCount > 0 && !input) || (final && frameCount == 0))
    {
        error = "Rubber Band input block is invalid";
        return false;
    }

    try
    {
        const auto channels = state_->config.channelCount;
        for (std::uint32_t frame = 0; frame < frameCount; ++frame)
        {
            for (std::uint16_t channel = 0; channel < channels; ++channel)
            {
                state_->inputChannels[channel][frame] =
                    input[static_cast<std::size_t>(frame) * channels + channel];
            }
        }
        state_->stretcher->process(
            frameCount > 0 ? state_->inputPointers.data() : nullptr,
            frameCount,
            final);
        return true;
    }
    catch (...)
    {
        error = "Rubber Band could not process the input block";
        return false;
    }
}

std::uint32_t RubberBandRealtimeProcessor::retrieveInterleaved(
    float* output,
    std::uint32_t maximumFrames) noexcept
{
    if (!state_ || !output || maximumFrames == 0)
        return 0;

    const auto available = state_->stretcher->available();
    if (available <= 0)
        return 0;
    const auto request = std::min({
        maximumFrames,
        state_->config.maximumInputFrames,
        static_cast<std::uint32_t>(available)
    });
    try
    {
        const auto retrieved = clampFrameCount(
            state_->stretcher->retrieve(state_->outputPointers.data(), request));
        const auto channels = state_->config.channelCount;
        for (std::uint32_t frame = 0; frame < retrieved; ++frame)
        {
            for (std::uint16_t channel = 0; channel < channels; ++channel)
            {
                output[static_cast<std::size_t>(frame) * channels + channel] =
                    state_->outputChannels[channel][frame];
            }
        }
        return retrieved;
    }
    catch (...)
    {
        return 0;
    }
}
}
