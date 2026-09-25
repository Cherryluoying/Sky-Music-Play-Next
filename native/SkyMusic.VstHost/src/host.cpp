// 模块：SkyMusic.VstHost 原生实现 host
#include "host.h"

#include "public.sdk/source/vst/hosting/eventlist.h"
#include "public.sdk/source/vst/hosting/processdata.h"
#include "public.sdk/source/vst/hosting/parameterchanges.h"
#include "pluginterfaces/vst/ivstmidicontrollers.h"
#include "pluginterfaces/vst/ivstunits.h"
#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivstcomponent.h"

#include <algorithm>
#include <array>
#include <chrono>

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <objbase.h>

namespace Steinberg
{
extern FUnknown* gStandardPluginContext;
}

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
    Steinberg::Vst::EventList inputEvents {8192};
    Steinberg::Vst::ParameterChanges parameters {2096};
    Steinberg::Vst::ProcessContext processContext {};
    std::array<std::array<unsigned int, 128>, 16> activeNotes {};
    std::size_t cursor {};
    double sequenceFrame {};
    bool sequencePlaying {};
    bool hasSequence {};
};

namespace
{
void addEvent(
    Steinberg::Vst::EventList& events,
    const MidiCommand& command,
    std::array<std::array<unsigned int, 128>, 16>& activeNotes,
    int sampleOffset = 0)
{
    if (command.type == MidiCommandType::AllNotesOff)
    {
        for (int channel = 0; channel < 16; ++channel)
        {
            for (int note = 0; note < 128; ++note)
            {
                while (activeNotes[channel][note] > 0)
                    addEvent(events, {MidiCommandType::NoteOff, note, 0, channel}, activeNotes, sampleOffset);
            }
        }
        return;
    }

    Steinberg::Vst::Event event {};
    event.busIndex = 0;
    event.sampleOffset = sampleOffset;
    event.ppqPosition = 0;
    if (command.type == MidiCommandType::NoteOn)
    {
        event.type = Steinberg::Vst::Event::kNoteOnEvent;
        event.noteOn.channel = static_cast<Steinberg::int16>(command.channel);
        event.noteOn.pitch = static_cast<Steinberg::int16>(command.note);
        event.noteOn.velocity = static_cast<float>(command.velocity) / 127.0f;
        event.noteOn.noteId = command.noteId;
        ++activeNotes[command.channel][command.note];
    }
    else
    {
        event.type = Steinberg::Vst::Event::kNoteOffEvent;
        event.noteOff.channel = static_cast<Steinberg::int16>(command.channel);
        event.noteOff.pitch = static_cast<Steinberg::int16>(command.note);
        event.noteOff.velocity = static_cast<float>(command.velocity) / 127.0f;
        event.noteOff.noteId = command.noteId;
        if (activeNotes[command.channel][command.note] > 0)
            --activeNotes[command.channel][command.note];
    }
    events.addEvent(event);
}
}

namespace
{
// 编辑器向宿主回报参数编辑；未实现的自动化操作明确返回不支持。
class ComponentHandler final : public Steinberg::Vst::IComponentHandler
{
public:
    Steinberg::tresult PLUGIN_API beginEdit(Steinberg::Vst::ParamID) override { return Steinberg::kNotImplemented; }
    Steinberg::tresult PLUGIN_API performEdit(
        Steinberg::Vst::ParamID,
        Steinberg::Vst::ParamValue) override { return Steinberg::kNotImplemented; }
    Steinberg::tresult PLUGIN_API endEdit(Steinberg::Vst::ParamID) override { return Steinberg::kNotImplemented; }
    Steinberg::tresult PLUGIN_API restartComponent(Steinberg::int32) override { return Steinberg::kNotImplemented; }

    Steinberg::tresult PLUGIN_API queryInterface(const Steinberg::TUID, void**) override
    {
        return Steinberg::kNoInterface;
    }

    Steinberg::uint32 PLUGIN_API addRef() override { return 1000; }
    Steinberg::uint32 PLUGIN_API release() override { return 1000; }
};

ComponentHandler gComponentHandler;

}

// 编辑器窗口与视图在同一条消息线程上创建、缩放和释放。
struct VstHost::EditorState final : public Steinberg::IPlugFrame
{
    HWND window {};
    Steinberg::IPtr<Steinberg::IPlugView> view;
    bool attached {};
    bool resizing {};

    ~EditorState() { close(); }

    void close() noexcept
    {
        if (view)
        {
            view->setFrame(nullptr);
            if (attached)
                view->removed();
            attached = false;
            view.reset();
        }
        if (window)
            DestroyWindow(window);
        window = nullptr;
    }

    static LRESULT CALLBACK windowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (message == WM_NCCREATE)
        {
            auto* create = reinterpret_cast<CREATESTRUCTW*>(lParam);
            auto* state = static_cast<EditorState*>(create->lpCreateParams);
            state->window = hwnd;
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(state));
        }
        auto* state = reinterpret_cast<EditorState*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
        if (state && message == WM_CLOSE)
        {
            // 只关闭编辑器，音乐继续播放；下次打开重新创建插件视图。
            state->close();
            return 0;
        }
        if (state && message == WM_SIZE && state->attached && !state->resizing && wParam != SIZE_MINIMIZED)
        {
            Steinberg::ViewRect rect {0, 0, LOWORD(lParam), HIWORD(lParam)};
            state->view->onSize(&rect);
        }
        if (state && message == WM_NCDESTROY)
        {
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, 0);
            state->window = nullptr;
        }
        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    Steinberg::tresult PLUGIN_API resizeView(
        Steinberg::IPlugView* requestingView, Steinberg::ViewRect* newSize) override
    {
        if (!window || !newSize || requestingView != view.get() ||
            newSize->right <= newSize->left || newSize->bottom <= newSize->top)
            return Steinberg::kInvalidArgument;
        if (resizing)
            return Steinberg::kResultFalse;
        resizing = true;
        // 插件报告的是客户区尺寸，不能把标题栏高度扣掉导致底部被裁切。
        RECT rect {0, 0, newSize->right - newSize->left, newSize->bottom - newSize->top};
        AdjustWindowRectEx(&rect, static_cast<DWORD>(GetWindowLongPtrW(window, GWL_STYLE)), FALSE, 0);
        SetWindowPos(window, nullptr, 0, 0, rect.right - rect.left, rect.bottom - rect.top,
                     SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
        view->onSize(newSize);
        resizing = false;
        return Steinberg::kResultOk;
    }

    Steinberg::tresult PLUGIN_API queryInterface(const Steinberg::TUID iid, void** obj) override
    {
        if (!obj)
            return Steinberg::kInvalidArgument;
        *obj = nullptr;
        if (Steinberg::FUnknownPrivate::iidEqual(iid, Steinberg::IPlugFrame::iid) ||
            Steinberg::FUnknownPrivate::iidEqual(iid, Steinberg::FUnknown::iid))
        {
            *obj = this;
            addRef();
            return Steinberg::kResultTrue;
        }
        return Steinberg::kNoInterface;
    }

    Steinberg::uint32 PLUGIN_API addRef() override { return 1000; }
    Steinberg::uint32 PLUGIN_API release() override { return 1000; }

};

VstHost::VstHost()
    : transportAppliedEvent_(CreateEventW(nullptr, FALSE, FALSE, nullptr))
{
}

VstHost::~VstHost()
{
    unload();
    if (transportAppliedEvent_) CloseHandle(transportAppliedEvent_);
}

bool VstHost::openEditor(std::string& error)
{
    if (!loaded_.load(std::memory_order_acquire) || !provider_)
    {
        error = "no VST3 instrument is loaded";
        return false;
    }
    if (editor_ && editor_->window)
    {
        ShowWindow(editor_->window, SW_RESTORE);
        SetForegroundWindow(editor_->window);
        return true;
    }
    closeEditor();
    auto controller = provider_->getControllerPtr();
    if (!controller)
    {
        error = "VST3 plugin has no edit controller";
        return false;
    }
    auto editor = std::make_unique<EditorState>();
    editor->view = Steinberg::owned(controller->createView(Steinberg::Vst::ViewType::kEditor));
    if (!editor->view)
    {
        error = "VST3 controller returned no editor view";
        return false;
    }

    if (editor->view->isPlatformTypeSupported(Steinberg::kPlatformTypeHWND) != Steinberg::kResultTrue)
    {
        error = "VST3 editor does not support Windows HWND";
        return false;
    }

    auto instance = GetModuleHandleW(nullptr);
    constexpr wchar_t className[] = L"SkyMusicVst3EditorWindow";
    WNDCLASSW windowClass {};
    windowClass.hInstance = instance;
    windowClass.lpfnWndProc = EditorState::windowProc;
    windowClass.lpszClassName = className;
    windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassW(&windowClass);

    Steinberg::ViewRect viewRect {0, 0, 760, 520};
    if (editor->view->getSize(&viewRect) != Steinberg::kResultOk ||
        viewRect.right <= viewRect.left || viewRect.bottom <= viewRect.top)
    {
        error = "VST3 editor returned an invalid size";
        return false;
    }
    const DWORD style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX;
    RECT bounds {0, 0, viewRect.right - viewRect.left, viewRect.bottom - viewRect.top};
    AdjustWindowRectEx(&bounds, style, FALSE, 0);
    auto window = CreateWindowExW(
        0, className, L"VST3 插件设置", style,
        CW_USEDEFAULT, CW_USEDEFAULT, bounds.right - bounds.left, bounds.bottom - bounds.top,
        nullptr, nullptr, instance, editor.get());
    if (!window)
    {
        error = "VST3 editor window creation failed: " + std::to_string(GetLastError());
        return false;
    }
    editor->view->setFrame(editor.get());
    if (editor->view->attached(window, Steinberg::kPlatformTypeHWND) != Steinberg::kResultOk)
    {
        error = "VST3 editor attachment to HWND failed";
        return false;
    }
    editor->attached = true;
    editor_ = std::move(editor);
    ShowWindow(window, SW_SHOW);
    UpdateWindow(window);
    return true;
}

void VstHost::closeEditor() noexcept
{
    editor_.reset();
}

// 加载插件组件并完成总线与处理器初始化
bool VstHost::load(const std::string& path, std::string& pluginName, std::string& error)
{
    unload();
    module_ = VST3::Hosting::Module::create(path, error);
    if (!module_)
        return false;

    const auto factory = module_->getFactory();
    // 同时为工厂和组件提供标准宿主上下文。
    factory.setHostContext(Steinberg::gStandardPluginContext);
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

    // 初始化和 createView 必须运行在同一个持续泵送消息的主线程上。
    // 无编辑控制器的插件仍可发声，只在打开编辑器时报告对应错误。
    if (!provider_->initialize())
    {
        error = "VST3 plugin controller initialization failed";
        provider_ = nullptr;
        module_.reset();
        return false;
    }
    for (auto& channel : controllerMap_) channel.fill(Steinberg::Vst::kNoParamId);
    programSteps_.fill(127);
    if (auto controller = provider_->getControllerPtr())
    {
        controller->setComponentHandler(&gComponentHandler);
        Steinberg::FUnknownPtr<Steinberg::Vst::IMidiMapping> mapping(controller);
        if (mapping)
            for (int channel = 0; channel < 16; ++channel)
                for (int cc = 0; cc < 130; ++cc)
                {
                    Steinberg::Vst::ParamID id = Steinberg::Vst::kNoParamId;
                    if (mapping->getMidiControllerAssignment(0, channel, cc, id) == Steinberg::kResultOk)
                        controllerMap_[channel][cc] = id;
                }
        // VST3 的 Program Change 由 Unit 的节目参数表示，不能当作普通 CC130。
        Steinberg::FUnknownPtr<Steinberg::Vst::IUnitInfo> units(controller);
        if (units)
            for (int channel = 0; channel < 16; ++channel)
            {
                Steinberg::Vst::UnitID unit {};
                if (units->getUnitByBus(Steinberg::Vst::kEvent, Steinberg::Vst::kInput, 0, channel, unit) != Steinberg::kResultOk)
                    continue;
                for (int p = 0; p < controller->getParameterCount(); ++p)
                {
                    Steinberg::Vst::ParameterInfo info {};
                    if (controller->getParameterInfo(p, info) == Steinberg::kResultOk && info.unitId == unit &&
                        (info.flags & Steinberg::Vst::ParameterInfo::kIsProgramChange) && info.stepCount > 0)
                    {
                        controllerMap_[channel][130] = info.id;
                        programSteps_[channel] = info.stepCount;
                        break;
                    }
                }
            }
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
    closeEditor();
    loaded_.store(false, std::memory_order_release);
    stopRequested_.store(true, std::memory_order_release);
    if (renderThread_.joinable())
        renderThread_.join();
    graph_.clearNodes();
    MidiCommand discarded;
    while (commands_.pop(discarded))
    {
    }
    if (provider_)
        if (auto controller = provider_->getControllerPtr())
            controller->setComponentHandler(nullptr);
    provider_ = nullptr;
    module_.reset();
    sequence_.clear();
    transportPlans_.clear();
    appliedTransport_.store(nullptr, std::memory_order_release);
}

// 更换文件时先停止音频回调，时间线存储只在非实时线程分配和释放。
bool VstHost::setSequence(std::vector<SequenceEvent> events, std::string& error)
{
    if (!isLoaded()) { error = "no VST3 instrument is loaded"; return false; }
    stopRequested_.store(true, std::memory_order_release);
    if (renderThread_.joinable()) renderThread_.join();
    MidiCommand discarded;
    while (commands_.pop(discarded)) {}
    sequence_ = std::move(events);
    transportPlans_.clear();
    appliedTransport_.store(nullptr, std::memory_order_release);
    {
        std::scoped_lock lock(readyMutex_);
        ready_ = false;
        startupError_.clear();
    }
    stopRequested_.store(false, std::memory_order_release);
    renderThread_ = std::thread(&VstHost::renderLoop, this);
    std::unique_lock lock(readyMutex_);
    if (!readyCondition_.wait_for(lock, std::chrono::seconds(10), [this] { return ready_; }))
    { error = "MIDI audio startup timed out"; return false; }
    error = startupError_;
    return error.empty();
}

bool VstHost::setTransport(bool playing, std::int64_t position, std::string& error)
{
    if (!isLoaded()) { error = "no VST3 instrument is loaded"; return false; }
    auto plan = std::make_unique<TransportPlan>(prepareTransport(sequence_, playing, position));
    auto* pending = plan.get();
    transportPlans_.push_back(std::move(plan));
    if (!transportAppliedEvent_) { error = "MIDI transport event creation failed"; return false; }
    ResetEvent(transportAppliedEvent_);
    if (!enqueue({MidiCommandType::Transport, 0, 0, 0, pending}, error)) return false;
    // 回调确认以后再释放旧计划；回调不持有锁，也不负责析构动态对象。
    if (WaitForSingleObject(transportAppliedEvent_, 1000) == WAIT_OBJECT_0)
    {
        if (appliedTransport_.load(std::memory_order_acquire) == pending)
        {
            std::erase_if(transportPlans_, [pending](const auto& item) { return item.get() != pending; });
            return true;
        }
    }
    error = "MIDI transport audio acknowledgement timed out";
    return false;
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
    // 组件已在主线程初始化；音频线程不能触发 PlugProvider 的惰性初始化。
    auto componentOwner = provider_ ? provider_->getComponentPtr() : nullptr;
    auto* component = componentOwner.get();
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
    // 预分配控制器队列及其点缓存，普通 MIDI 密度下回调不分配参数对象。
    for (const auto& channel : controllerMap_)
        for (const auto id : channel)
            if (id != Steinberg::Vst::kNoParamId)
            {
                Steinberg::int32 index = 0;
                auto* queue = state->parameters.addParameterData(id, index);
                for (int sample = 0; sample < 256; ++sample) queue->addPoint(sample, 0, index);
            }
    state->parameters.clearQueue();
    state->processData.inputParameterChanges = &state->parameters;
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
    state.parameters.clearQueue();
    // MIDI CC/弯音通过 VST3 IMidiMapping 转换为参数点，音符使用原始通道和力度。
    auto sendSequenceEvent = [&](const SequenceEvent& event, int offset) {
        using namespace Steinberg::Vst;
        if (event.kind <= 1)
            addEvent(state.inputEvents,
                {event.kind == 0 ? MidiCommandType::NoteOn : MidiCommandType::NoteOff,
                 event.data1, event.data2, event.channel, nullptr, event.noteId}, state.activeNotes, offset);
        else if (event.kind == 7)
            state.processContext.tempo = 60000000.0 / event.data1;
        else if (event.kind == 5)
        {
            Event pressure {};
            pressure.type = Event::kPolyPressureEvent;
            pressure.sampleOffset = offset;
            pressure.polyPressure.channel = event.channel;
            pressure.polyPressure.pitch = event.data1;
            pressure.polyPressure.pressure = event.data2 / 127.0f;
            pressure.polyPressure.noteId = -1;
            state.inputEvents.addEvent(pressure);
        }
        else
        {
            // CC123 即使插件没有参数映射也要释放本通道的按键。
            if (event.kind == 2 && (event.data1 == 120 || event.data1 == 123))
                for (int note = 0; note < 128; ++note)
                    while (state.activeNotes[event.channel][note] > 0)
                        addEvent(state.inputEvents, {MidiCommandType::NoteOff, note, 0, event.channel}, state.activeNotes, offset);
            const int controller = event.kind == 2 ? event.data1 : event.kind == 3 ? kPitchBend :
                event.kind == 4 ? kAfterTouch : kCtrlProgramChange;
            const auto id = controllerMap_[event.channel][controller];
            if (id != kNoParamId)
            {
                Steinberg::int32 index = 0;
                auto* queue = state.parameters.addParameterData(id, index);
                const double value = event.kind == 2 ? event.data2 / 127.0 :
                    event.kind == 3 ? event.data1 / 16383.0 :
                    event.kind == 6 ? std::min(1.0, event.data1 / static_cast<double>(programSteps_[event.channel])) : event.data1 / 127.0;
                queue->addPoint(offset, value, index);
            }
        }
    };
    MidiCommand command;
    while (commands_.pop(command))
    {
        if (command.type == MidiCommandType::Transport)
        {
            const auto& plan = *command.transport;
            addEvent(state.inputEvents, {MidiCommandType::AllNotesOff}, state.activeNotes);
            for (int channel = 0; channel < 16; ++channel)
            {
                sendSequenceEvent({0, 2, 64, 0, channel}, 0);
                sendSequenceEvent({0, 2, 120, 0, channel}, 0);
                sendSequenceEvent({0, 2, 121, 0, channel}, 0);
            }
            state.hasSequence = true;
            state.cursor = plan.cursor;
            state.sequenceFrame = plan.position * state.processContext.sampleRate / 1000000.0;
            state.sequencePlaying = plan.playing;
            for (const auto& event : plan.restore) sendSequenceEvent(event, 0);
            appliedTransport_.store(command.transport, std::memory_order_release);
            SetEvent(transportAppliedEvent_);
        }
        else
            addEvent(state.inputEvents, command, state.activeNotes);
    }
    if (state.sequencePlaying)
    {
        const auto blockEnd = state.sequenceFrame + frameCount;
        while (state.cursor < sequence_.size())
        {
            const auto& event = sequence_[state.cursor];
            const auto offset = sequenceSampleOffset(event.time, state.sequenceFrame, state.processContext.sampleRate, frameCount);
            if (offset < 0) break;
            sendSequenceEvent(event, offset);
            ++state.cursor;
        }
        state.sequenceFrame = blockEnd;
    }

    state.processData.numSamples = static_cast<Steinberg::int32>(frameCount);
    state.processContext.continousTimeSamples = static_cast<Steinberg::int64>(context.samplePosition);
    state.processContext.projectTimeSamples = static_cast<Steinberg::int64>(state.sequenceFrame - (state.sequencePlaying ? frameCount : 0));
    if (state.sequencePlaying) state.processContext.state |= Steinberg::Vst::ProcessContext::kPlaying;
    else state.processContext.state &= ~Steinberg::Vst::ProcessContext::kPlaying;
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
                state.hasSequence && !state.sequencePlaying ? 0.0f :
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
