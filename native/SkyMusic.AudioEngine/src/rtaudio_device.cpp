// 模块：SkyMusic.AudioEngine 原生实现 rtaudio_device
#include "skymusic/audio/rtaudio_device.h"

#include "RtAudio.h"

#include <algorithm>
#include <chrono>
#include <thread>

namespace skymusic::audio
{
struct RtAudioDevice::CallbackState
{
    AudioGraph* graph {};
    AudioClock* clock {};
    std::atomic<bool>* stopRequested {};
    std::atomic<std::uint64_t>* underflowCount {};
    std::uint16_t channelCount {};
};

RtAudioDevice::RtAudioDevice(RtAudioDeviceOptions options)
    : options_(options)
{
}

// 以单一 WASAPI 设备时钟运行音频图
bool RtAudioDevice::run(
    AudioGraph& graph,
    std::atomic<bool>& stopRequested,
    const std::function<void()>& onStarted,
    std::string& error)
{
    sampleRate_.store(0, std::memory_order_release);
    bufferFrames_.store(0, std::memory_order_release);
    latencyFrames_.store(0, std::memory_order_release);
    underflowCount_.store(0, std::memory_order_release);

    std::atomic<bool> deviceFailed {false};
    RtAudio audio(RtAudio::WINDOWS_WASAPI,
        [&deviceFailed](RtAudioErrorType type, const std::string&) {
            if (type != RTAUDIO_WARNING)
                deviceFailed.store(true, std::memory_order_release);
        });
    audio.showWarnings(false);

    const auto deviceId = audio.getDefaultOutputDevice();
    const auto deviceInfo = audio.getDeviceInfo(deviceId);
    if (deviceInfo.outputChannels == 0)
    {
        error = "default audio output is unavailable";
        return false;
    }

    const auto requestedChannels = std::max<std::uint16_t>(options_.outputChannels, 1);
    const auto channelCount = static_cast<std::uint16_t>(
        std::min<unsigned int>(requestedChannels, deviceInfo.outputChannels));
    const auto requestedSampleRate = options_.preferredSampleRate != 0
        ? options_.preferredSampleRate
        : deviceInfo.preferredSampleRate;
    if (requestedSampleRate == 0)
    {
        error = "default audio output has no supported sample rate";
        return false;
    }

    RtAudio::StreamParameters outputParameters;
    outputParameters.deviceId = deviceId;
    outputParameters.nChannels = channelCount;
    outputParameters.firstChannel = 0;

    auto bufferFrames = std::max(options_.preferredBufferFrames, 32u);
    RtAudio::StreamOptions streamOptions;
    streamOptions.streamName = "SkyMusicPlay AudioGraph";
    if (options_.minimizeLatency)
        streamOptions.flags |= RTAUDIO_MINIMIZE_LATENCY;

    CallbackState callbackState {
        &graph,
        &clock_,
        &stopRequested,
        &underflowCount_,
        channelCount
    };
    const auto openResult = audio.openStream(
        &outputParameters,
        nullptr,
        RTAUDIO_FLOAT32,
        requestedSampleRate,
        &bufferFrames,
        &RtAudioDevice::renderCallback,
        &callbackState,
        &streamOptions);
    if (openResult != RTAUDIO_NO_ERROR)
    {
        error = audio.getErrorText();
        return false;
    }

    const auto actualSampleRate = audio.getStreamSampleRate();
    const AudioStreamFormat format {
        actualSampleRate,
        channelCount,
        bufferFrames,
        AudioProcessingMode::Realtime
    };
    if (!graph.prepare(format, error))
    {
        audio.closeStream();
        return false;
    }

    clock_.configure(actualSampleRate);
    clock_.reset();
    sampleRate_.store(actualSampleRate, std::memory_order_release);
    bufferFrames_.store(bufferFrames, std::memory_order_release);
    const auto streamLatency = audio.getStreamLatency();
    latencyFrames_.store(
        streamLatency > 0 ? static_cast<std::uint32_t>(streamLatency) : 0,
        std::memory_order_release);
    const auto startResult = audio.startStream();
    if (startResult != RTAUDIO_NO_ERROR)
    {
        graph.release();
        audio.closeStream();
        error = audio.getErrorText();
        return false;
    }

    onStarted();
    while (!stopRequested.load(std::memory_order_acquire) && audio.isStreamRunning())
        std::this_thread::sleep_for(std::chrono::milliseconds(5));

    if (audio.isStreamRunning())
        audio.abortStream();
    audio.closeStream();
    graph.release();

    if (deviceFailed.load(std::memory_order_acquire) &&
        !stopRequested.load(std::memory_order_acquire))
    {
        error = "audio output stopped after a device failure";
        return false;
    }
    return true;
}

const AudioClock& RtAudioDevice::clock() const noexcept
{
    return clock_;
}

RtAudioDeviceMetrics RtAudioDevice::metrics() const noexcept
{
    return {
        sampleRate_.load(std::memory_order_acquire),
        bufferFrames_.load(std::memory_order_acquire),
        latencyFrames_.load(std::memory_order_acquire),
        underflowCount_.load(std::memory_order_acquire)
    };
}

// 在设备回调内渲染固定帧块并记录欠载
int RtAudioDevice::renderCallback(
    void* outputBuffer,
    void*,
    unsigned int frameCount,
    double,
    unsigned int status,
    void* userData) noexcept
{
    auto* state = static_cast<CallbackState*>(userData);
    if (!state || !outputBuffer)
        return 2;
    if (state->stopRequested->load(std::memory_order_acquire))
        return 2;

    if ((status & RTAUDIO_OUTPUT_UNDERFLOW) != 0)
        state->underflowCount->fetch_add(1, std::memory_order_relaxed);

    // 实时回调只渲染音频图并推进设备时钟
    state->graph->render(
        state->clock->context(true),
        static_cast<float*>(outputBuffer),
        frameCount,
        state->channelCount);
    state->clock->advance(frameCount);
    return 0;
}
}
