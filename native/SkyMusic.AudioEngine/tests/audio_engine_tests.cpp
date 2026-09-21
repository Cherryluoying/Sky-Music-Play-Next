// 模块：SkyMusic.AudioEngine 原生测试 audio_engine_tests
#include "skymusic/audio/audio_clock.h"
#include "skymusic/audio/audio_graph.h"
#include "skymusic/audio/offline_renderer.h"
#include "skymusic/audio/rtaudio_device.h"
#include "skymusic/audio/rubber_band_realtime_processor.h"
#include "skymusic/audio/sample_player_node.h"

#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <string>
#include <vector>

namespace
{
class ConstantNode final : public skymusic::audio::IAudioRenderNode
{
public:
    ConstantNode(float value, bool& sawOffline)
        : value_(value), sawOffline_(sawOffline)
    {
    }

    bool prepare(const skymusic::audio::AudioStreamFormat& format, std::string&) override
    {
        sawOffline_ = format.processingMode == skymusic::audio::AudioProcessingMode::Offline;
        return true;
    }

    void render(
        const skymusic::audio::AudioRenderContext& context,
        float* output,
        std::uint32_t frames,
        std::uint16_t channels) noexcept override
    {
        sawOffline_ = sawOffline_ && !context.realtime;
        for (std::size_t index = 0; index < static_cast<std::size_t>(frames) * channels; ++index)
            output[index] = value_;
    }

    void release() noexcept override
    {
    }

private:
    float value_;
    bool& sawOffline_;
};

bool near(float left, float right)
{
    return std::abs(left - right) < 0.0001f;
}
}

int main()
{
    using namespace skymusic::audio;

    AudioClock clock;
    clock.configure(48'000);
    clock.advance(256);
    if (clock.samplePosition() != 256 || clock.context(true).sampleRate != 48'000)
        return EXIT_FAILURE;

    RtAudioDevice device;
    const auto deviceMetrics = device.metrics();
    if (deviceMetrics.sampleRate != 0 || deviceMetrics.bufferFrames != 0 ||
        deviceMetrics.latencyFrames != 0 || deviceMetrics.underflowCount != 0)
        return EXIT_FAILURE;

    std::string error;
    RubberBandRealtimeProcessor stretcher;
    RubberBandRealtimeConfig stretchConfig;
    stretchConfig.maximumInputFrames = 4'096;
    if (!stretcher.prepare(stretchConfig, error) ||
        !stretcher.setRatios(1.1, 1.0, error) ||
        stretcher.preferredStartPadFrames() == 0 ||
        stretcher.startDelayFrames() == 0)
        return EXIT_FAILURE;

    std::vector<float> stretchInput(
        static_cast<std::size_t>(stretchConfig.maximumInputFrames) * stretchConfig.channelCount);
    std::vector<float> stretchOutput(
        static_cast<std::size_t>(stretchConfig.maximumInputFrames) * stretchConfig.channelCount);
    std::uint64_t stretchedFrames = 0;
    bool heardSignal = false;
    const auto drainOutput = [&] {
        while (stretcher.availableOutputFrames() > 0)
        {
            const auto frames = stretcher.retrieveInterleaved(
                stretchOutput.data(), stretchConfig.maximumInputFrames);
            if (frames == 0)
                return false;
            stretchedFrames += frames;
            for (std::size_t index = 0;
                 index < static_cast<std::size_t>(frames) * stretchConfig.channelCount;
                 ++index)
            {
                if (!std::isfinite(stretchOutput[index]))
                    return false;
                heardSignal = heardSignal || std::abs(stretchOutput[index]) > 0.0001f;
            }
        }
        return true;
    };

    auto padFrames = stretcher.preferredStartPadFrames();
    while (padFrames > 0)
    {
        const auto frames = std::min(padFrames, stretchConfig.maximumInputFrames);
        std::fill(stretchInput.begin(), stretchInput.end(), 0.0f);
        if (!stretcher.processInterleaved(stretchInput.data(), frames, false, error) || !drainOutput())
            return EXIT_FAILURE;
        padFrames -= frames;
    }

    constexpr std::uint32_t sourceFrames = 8'192;
    std::uint32_t sourceOffset = 0;
    while (sourceOffset < sourceFrames)
    {
        const auto requested = stretcher.requiredInputFrames();
        if (requested == 0 || requested > stretchConfig.maximumInputFrames)
            return EXIT_FAILURE;
        const auto frames = std::min(requested, sourceFrames - sourceOffset);
        for (std::uint32_t frame = 0; frame < frames; ++frame)
        {
            const auto sample = static_cast<float>(std::sin((sourceOffset + frame) * 0.05));
            for (std::uint16_t channel = 0; channel < stretchConfig.channelCount; ++channel)
                stretchInput[static_cast<std::size_t>(frame) * stretchConfig.channelCount + channel] = sample;
        }
        sourceOffset += frames;
        if (!stretcher.processInterleaved(
                stretchInput.data(), frames, sourceOffset == sourceFrames, error) ||
            !drainOutput())
            return EXIT_FAILURE;
    }
    if (stretchedFrames == 0 || !heardSignal)
        return EXIT_FAILURE;

    SamplePlayerNode samplePlayer;
    AudioStreamFormat sampleFormat {48'000, 2, 8};
    if (!samplePlayer.setSample(0, std::vector<float>(16, 0.25f)) ||
        !samplePlayer.prepare(sampleFormat, error) ||
        !samplePlayer.trigger(0, 1.0f))
        return EXIT_FAILURE;
    std::vector<float> sampleOutput(8);
    samplePlayer.render({}, sampleOutput.data(), 4, 2);
    if (!std::all_of(sampleOutput.begin(), sampleOutput.end(), [](float sample) { return near(sample, 0.25f); }))
        return EXIT_FAILURE;
    if (!samplePlayer.trigger(0, 1.0f))
        return EXIT_FAILURE;
    std::fill(sampleOutput.begin(), sampleOutput.end(), 0.0f);
    samplePlayer.render({}, sampleOutput.data(), 4, 2);
    if (!std::all_of(sampleOutput.begin(), sampleOutput.end(), [](float sample) { return near(sample, 0.5f); }))
        return EXIT_FAILURE;
    samplePlayer.release();

    bool firstOffline = false;
    bool secondOffline = false;
    ConstantNode first(0.25f, firstOffline);
    ConstantNode second(0.5f, secondOffline);
    AudioGraph graph;
    graph.addNode(first);
    graph.addNode(second, 0.5f);
    OfflineRenderer renderer;
    AudioStreamFormat format {48'000, 2, 64};
    std::uint64_t renderedFrames = 0;
    const auto rendered = renderer.render(graph, format, 150,
        [&](const float* samples, std::uint32_t frames, std::uint16_t channels) {
            renderedFrames += frames;
            for (std::size_t index = 0; index < static_cast<std::size_t>(frames) * channels; ++index)
            {
                if (!near(samples[index], 0.5f))
                    return false;
            }
            return true;
        }, error);

    return rendered && error.empty() && renderedFrames == 150 && firstOffline && secondOffline
        ? EXIT_SUCCESS
        : EXIT_FAILURE;
}
