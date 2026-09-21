// 模块：SkyMusic.VstHost 原生实现 host
#include "host.h"

#include "public.sdk/source/vst/hosting/eventlist.h"
#include "public.sdk/source/vst/hosting/processdata.h"
#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivstcomponent.h"

#include <algorithm>
#include <array>
#include <chrono>

namespace skymusic
{
struct VstHost::RenderState
{
    explicit RenderState(Steinberg::Vst::IComponent* value)
        : component(value), processor(value)
    {
    }

    Steinberg::Vst::IComponent* component {};
    Steinberg::FUnknownPtr<Steinberg::Vst::IAudioProcessor> processor;
    Steinberg::Vst::HostProcessData processData;
    Steinberg::Vst::EventList inputEvents;
    Steinberg::Vst::ProcessContext processContext {};
    std::array<std::array<bool, 128>, 16> activeNotes {};
};

namespace
{
void addEvent(
    Steinberg::Vst::EventList& events,
    const MidiCommand& command,
    std::array<std::array<bool, 128>, 16>& activeNotes)
{
    if (command.type == MidiCommandType::AllNotesOff)
    {
        for (int channel = 0; channel < 16; ++channel)
        {
            for (int note = 0; note < 128; ++note)
            {
                if (!activeNotes[channel][note])
                    continue;
                addEvent(events, {MidiCommandType::NoteOff, note, 0, channel}, activeNotes);
            }
        }
        return;
    }

    Steinberg::Vst::Event event {};
    event.busIndex = 0;
    event.sampleOffset = 0;
    event.ppqPosition = 0;
    if (command.type == MidiCommandType::NoteOn)
    {
        event.type = Steinberg::Vst::Event::kNoteOnEvent;
        event.noteOn.channel = static_cast<Steinberg::int16>(command.channel);
        event.noteOn.pitch = static_cast<Steinberg::int16>(command.note);
        event.noteOn.velocity = static_cast<float>(command.velocity) / 127.0f;
        event.noteOn.noteId = -1;
        activeNotes[command.channel][command.note] = true;
    }
    else
    {
        event.type = Steinberg::Vst::Event::kNoteOffEvent;
        event.noteOff.channel = static_cast<Steinberg::int16>(command.channel);
        event.noteOff.pitch = static_cast<Steinberg::int16>(command.note);
        event.noteOff.velocity = static_cast<float>(command.velocity) / 127.0f;
        event.noteOff.noteId = -1;
        activeNotes[command.channel][command.note] = false;
    }
    events.addEvent(event);
}
}

VstHost::VstHost() = default;

VstHost::~VstHost()
{
    unload();
}

// 加载插件组件并完成总线与处理器初始化
bool VstHost::load(const std::string& path, std::string& pluginName, std::string& error)
{
    unload();
    module_ = VST3::Hosting::Module::create(path, error);
    if (!module_)
        return false;

    const auto factory = module_->getFactory();
    for (const auto& classInfo : factory.classInfos())
    {
        if (classInfo.category() != kVstAudioEffectClass ||
            classInfo.subCategoriesString().find("Instrument") == std::string::npos)
            continue;
        provider_ = Steinberg::owned(new Steinberg::Vst::PlugProvider(factory, classInfo, true));
        pluginName = classInfo.name();
        break;
    }
    if (!provider_)
    {
        error = "module contains no VST3 instrument";
        module_.reset();
        return false;
    }

    {
        std::scoped_lock lock(readyMutex_);
        ready_ = false;
        startupError_.clear();
    }
    graph_.addNode(*this);
    stopRequested_.store(false, std::memory_order_release);
    renderThread_ = std::thread(&VstHost::renderLoop, this);

    std::unique_lock lock(readyMutex_);
    if (!readyCondition_.wait_for(lock, std::chrono::seconds(10), [this] { return ready_; }))
    {
        error = "audio host startup timed out";
        lock.unlock();
        unload();
        return false;
    }
    if (!startupError_.empty())
    {
        error = startupError_;
        lock.unlock();
        unload();
        return false;
    }

    loaded_.store(true, std::memory_order_release);
    return true;
}

void VstHost::unload() noexcept
{
    loaded_.store(false, std::memory_order_release);
    stopRequested_.store(true, std::memory_order_release);
    if (renderThread_.joinable())
        renderThread_.join();
    graph_.clearNodes();
    MidiCommand discarded;
    while (commands_.pop(discarded))
    {
    }
    provider_ = nullptr;
    module_.reset();
}

// 将控制线程 MIDI 事件写入无锁队列
bool VstHost::enqueue(const MidiCommand& command, std::string& error) noexcept
{
    if (!loaded_.load(std::memory_order_acquire))
    {
        error = "no VST3 instrument is loaded";
        return false;
    }
    if (command.note < 0 || command.note > 127 || command.velocity < 0 || command.velocity > 127 ||
        command.channel < 0 || command.channel > 15)
    {
        error = "MIDI value is outside its valid range";
        return false;
    }
    if (!commands_.push(command))
    {
        error = "real-time MIDI queue is full";
        return false;
    }
    return true;
}

bool VstHost::isLoaded() const noexcept
{
    return loaded_.load(std::memory_order_acquire);
}

// 在宿主音频线程持续处理插件输出
void VstHost::renderLoop() noexcept
{
    std::string error;
    try
    {
        if (!audioDevice_.run(graph_, stopRequested_, [this] { signalReady(); }, error))
            signalReady(error);
    }
    catch (...)
    {
        signalReady("unexpected native audio host failure");
    }
}

bool VstHost::prepare(const audio::AudioStreamFormat& format, std::string& error)
{
    auto* component = provider_ ? provider_->getComponent() : nullptr;
    auto state = component ? std::make_unique<RenderState>(component) : nullptr;
    if (!state || !state->processor)
    {
        error = "VST3 component has no audio processor";
        return false;
    }

    component->activateBus(Steinberg::Vst::kAudio, Steinberg::Vst::kOutput, 0, true);
    if (component->getBusCount(Steinberg::Vst::kEvent, Steinberg::Vst::kInput) > 0)
        component->activateBus(Steinberg::Vst::kEvent, Steinberg::Vst::kInput, 0, true);

    Steinberg::Vst::ProcessSetup setup {
        format.processingMode == audio::AudioProcessingMode::Realtime
            ? Steinberg::Vst::kRealtime
            : Steinberg::Vst::kOffline,
        Steinberg::Vst::kSample32,
        static_cast<Steinberg::int32>(format.maximumFrames),
        static_cast<double>(format.sampleRate)
    };
    if (state->processor->setupProcessing(setup) != Steinberg::kResultOk ||
        component->setActive(true) != Steinberg::kResultOk ||
        state->processor->setProcessing(true) != Steinberg::kResultOk)
    {
        component->setActive(false);
        error = "VST3 processor could not enter the requested processing mode";
        return false;
    }

    if (!state->processData.prepare(*component, static_cast<Steinberg::int32>(format.maximumFrames),
                                    Steinberg::Vst::kSample32))
    {
        state->processor->setProcessing(false);
        component->setActive(false);
        error = "VST3 audio buffers could not be prepared";
        return false;
    }
    state->processData.inputEvents = &state->inputEvents;
    state->processData.processContext = &state->processContext;
    state->processContext.sampleRate = format.sampleRate;
    state->processContext.tempo = 120;
    state->processContext.state = Steinberg::Vst::ProcessContext::kTempoValid |
                                  Steinberg::Vst::ProcessContext::kContTimeValid |
                                  Steinberg::Vst::ProcessContext::kPlaying;
    renderState_ = std::move(state);
    return true;
}

void VstHost::render(
    const audio::AudioRenderContext& context,
    float* interleavedOutput,
    std::uint32_t frameCount,
    std::uint16_t channelCount) noexcept
{
    if (!renderState_)
        return;

    auto& state = *renderState_;
    state.inputEvents.clear();
    MidiCommand command;
    while (commands_.pop(command))
        addEvent(state.inputEvents, command, state.activeNotes);

    state.processData.numSamples = static_cast<Steinberg::int32>(frameCount);
    state.processContext.continousTimeSamples = static_cast<Steinberg::int64>(context.samplePosition);
    state.processContext.tempo = context.tempo;
    for (Steinberg::int32 bus = 0; bus < state.processData.numOutputs; ++bus)
    {
        for (Steinberg::int32 channel = 0; channel < state.processData.outputs[bus].numChannels; ++channel)
            std::fill_n(state.processData.outputs[bus].channelBuffers32[channel], frameCount, 0.0f);
    }

    if (state.processor->process(state.processData) != Steinberg::kResultOk ||
        state.processData.numOutputs == 0 || state.processData.outputs[0].numChannels == 0)
        return;

    const auto pluginChannels = state.processData.outputs[0].numChannels;
    for (std::uint32_t frame = 0; frame < frameCount; ++frame)
    {
        for (std::uint16_t channel = 0; channel < channelCount; ++channel)
        {
            const auto sourceChannel = std::min<int>(channel, pluginChannels - 1);
            interleavedOutput[static_cast<std::size_t>(frame) * channelCount + channel] =
                state.processData.outputs[0].channelBuffers32[sourceChannel][frame];
        }
    }
}

void VstHost::release() noexcept
{
    if (!renderState_)
        return;
    renderState_->processor->setProcessing(false);
    renderState_->component->setActive(false);
    renderState_.reset();
}

void VstHost::signalReady(std::string error)
{
    {
        std::scoped_lock lock(readyMutex_);
        if (ready_)
            return;
        startupError_ = std::move(error);
        ready_ = true;
    }
    readyCondition_.notify_all();
}
}
