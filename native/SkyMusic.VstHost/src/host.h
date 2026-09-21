// 模块：SkyMusic.VstHost 原生实现 host
#pragma once

#include "spsc_queue.h"

#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"
#include "skymusic/audio/audio_graph.h"
#include "skymusic/audio/rtaudio_device.h"

#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <thread>

namespace skymusic
{
enum class MidiCommandType
{
    NoteOn,
    NoteOff,
    AllNotesOff
};

struct MidiCommand
{
    MidiCommandType type {};
    int note {};
    int velocity {};
    int channel {};
};

class VstHost final : private audio::IAudioRenderNode
{
public:
    VstHost();
    ~VstHost();

    VstHost(const VstHost&) = delete;
    VstHost& operator=(const VstHost&) = delete;

    bool load(const std::string& path, std::string& pluginName, std::string& error);
    void unload() noexcept;
    bool enqueue(const MidiCommand& command, std::string& error) noexcept;
    bool isLoaded() const noexcept;

private:
    struct RenderState;

    void renderLoop() noexcept;
    void signalReady(std::string error = {});

    bool prepare(const audio::AudioStreamFormat& format, std::string& error) override;
    void render(
        const audio::AudioRenderContext& context,
        float* interleavedOutput,
        std::uint32_t frameCount,
        std::uint16_t channelCount) noexcept override;
    void release() noexcept override;

    VST3::Hosting::Module::Ptr module_;
    Steinberg::IPtr<Steinberg::Vst::PlugProvider> provider_;
    std::unique_ptr<RenderState> renderState_;
    SpscQueue<MidiCommand, 4096> commands_;
    audio::AudioGraph graph_;
    audio::RtAudioDevice audioDevice_;
    std::thread renderThread_;
    std::atomic<bool> stopRequested_ {false};
    std::atomic<bool> loaded_ {false};
    std::mutex readyMutex_;
    std::condition_variable readyCondition_;
    bool ready_ {};
    std::string startupError_;
};
}
