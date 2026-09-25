// 模块：SkyMusic.VstHost 原生实现 host
#pragma once

#include "spsc_queue.h"
#include "sequence.h"

#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"
#include "pluginterfaces/gui/iplugview.h"
#include "skymusic/audio/audio_graph.h"
#include "skymusic/audio/rtaudio_device.h"

#include <atomic>
#include <array>
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
    AllNotesOff,
    Transport
};

struct MidiCommand
{
    MidiCommandType type {};
    int note {};
    int velocity {};
    int channel {};
    const TransportPlan* transport {};
    int noteId {-1};
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
    bool openEditor(std::string& error);
    bool isLoaded() const noexcept;
    bool setSequence(std::vector<SequenceEvent> events, std::string& error);
    bool setTransport(bool playing, std::int64_t position, std::string& error);

private:
    // 离线诊断复用实际 render 路径，不连接声卡、不改动用户正在运行的插件。
    friend struct VstRenderProbe;
    struct RenderState;
    struct EditorState;

    void renderLoop() noexcept;
    void signalReady(std::string error = {});

    bool prepare(const audio::AudioStreamFormat& format, std::string& error) override;
    void render(
        const audio::AudioRenderContext& context,
        float* interleavedOutput,
        std::uint32_t frameCount,
        std::uint16_t channelCount) noexcept override;
    void release() noexcept override;
    void closeEditor() noexcept;

    VST3::Hosting::Module::Ptr module_;
    Steinberg::IPtr<Steinberg::Vst::PlugProvider> provider_;
    std::unique_ptr<RenderState> renderState_;
    SpscQueue<MidiCommand, 4096> commands_;
    audio::AudioGraph graph_;
    audio::RtAudioDevice audioDevice_;
    std::thread renderThread_;
    // 插件控制器、原生窗口和生命周期操作都由宿主主线程持有。
    std::unique_ptr<EditorState> editor_;
    std::atomic<bool> stopRequested_ {false};
    std::atomic<bool> loaded_ {false};
    std::mutex readyMutex_;
    std::condition_variable readyCondition_;
    bool ready_ {};
    std::string startupError_;
    std::vector<SequenceEvent> sequence_;
    std::vector<std::unique_ptr<TransportPlan>> transportPlans_;
    std::atomic<const TransportPlan*> appliedTransport_ {nullptr};
    void* transportAppliedEvent_ {};
    // MIDI 参数映射在 UI 线程读取，在音频回调中只查表。
    std::array<std::array<Steinberg::Vst::ParamID, 131>, 16> controllerMap_ {};
    std::array<int, 16> programSteps_ {};
};
}
