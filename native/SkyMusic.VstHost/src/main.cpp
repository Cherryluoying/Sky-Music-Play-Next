// 模块：SkyMusic.VstHost 原生实现 main
#include "host.h"
#include "protocol.h"

#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "pluginterfaces/base/funknown.h"

#include <iostream>
#include <algorithm>

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

namespace Steinberg
{
FUnknown* gStandardPluginContext = new Vst::HostApplication();
}

namespace
{
// 分派隔离宿主协议命令并返回结构化结果
skymusic::Response handleRequest(skymusic::VstHost& host, const skymusic::Request& request, bool& running)
{
    std::string error;
    if (request.type == "sequence")
        return host.setSequence(request.events, error)
            ? skymusic::Response {request.id, true} : skymusic::Response {request.id, false, {}, error};
    if (request.type == "transport")
        return host.setTransport(request.playing, request.position, error)
            ? skymusic::Response {request.id, true} : skymusic::Response {request.id, false, {}, error};
    if (request.type == "load")
    {
        if (!request.path)
            return {request.id, false, {}, "load requires path"};
        std::string name;
        return host.load(*request.path, name, error)
            ? skymusic::Response {request.id, true, name, {}}
            : skymusic::Response {request.id, false, {}, error};
    }
    if (request.type == "unload")
    {
        host.unload();
        return {request.id, true};
    }
    if (request.type == "shutdown")
    {
        host.unload();
        running = false;
        return {request.id, true};
    }
    if (request.type == "allNotesOff")
    {
        const skymusic::MidiCommand command {skymusic::MidiCommandType::AllNotesOff, 0, 0, 0};
        return host.enqueue(command, error)
            ? skymusic::Response {request.id, true}
            : skymusic::Response {request.id, false, {}, error};
    }
    if (request.type == "openEditor")
        return host.openEditor(error)
            ? skymusic::Response {request.id, true}
            : skymusic::Response {request.id, false, {}, error};
    if (request.type == "noteOn" || request.type == "noteOff")
    {
        if (!request.note || !request.velocity || !request.channel)
            return {request.id, false, {}, "MIDI command requires note, velocity and channel"};
        const skymusic::MidiCommand command {
            request.type == "noteOn" ? skymusic::MidiCommandType::NoteOn : skymusic::MidiCommandType::NoteOff,
            *request.note,
            *request.velocity,
            *request.channel
        };
        return host.enqueue(command, error)
            ? skymusic::Response {request.id, true}
            : skymusic::Response {request.id, false, {}, error};
    }
    return {request.id, false, {}, "unknown command type"};
}
}

int main()
{
    // VST3 编辑器需要宿主上下文；没有上下文时部分插件会创建音频处理器，
    // 但拒绝创建原生编辑器视图。使用 SDK 提供的标准宿主对象贯穿整个进程。
    Steinberg::Vst::PluginContextFactory::instance().setPluginContext(
        Steinberg::gStandardPluginContext);
    skymusic::VstHost host;
    bool running = true;
    std::string pending;
    // 控制线程同时承担插件窗口的消息泵。stdin 是匿名管道，不能直接用阻塞
    // getline，否则原生 VST 编辑器会创建成功但无法重绘或响应关闭。
    while (running)
    {
        MSG message {};
        while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
        {
            if (message.message == WM_QUIT)
            {
                running = false;
                break;
            }
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        if (!running)
            break;

        // 不使用会预读后续命令的 std::cin；完整时间线可以分段到达，
        // 缺少换行时继续处理窗口消息，不在半条 JSON 上阻塞界面。
        auto newline = pending.find('\n');
        if (newline == std::string::npos)
        {
            DWORD available = 0;
            const auto input = GetStdHandle(STD_INPUT_HANDLE);
            if (!PeekNamedPipe(input, nullptr, 0, nullptr, &available, nullptr)) break;
            if (available == 0) { Sleep(1); continue; }
            char bytes[65536];
            DWORD read = 0;
            if (!ReadFile(input, bytes, std::min<DWORD>(available, sizeof(bytes)), &read, nullptr) || read == 0) break;
            pending.append(bytes, read);
            newline = pending.find('\n');
            if (newline == std::string::npos) continue;
        }
        const auto line = pending.substr(0, newline);
        pending.erase(0, newline + 1);

        // 控制线程只解析协议并投递事件
        skymusic::Request request;
        std::string error;
        skymusic::Response response;
        if (!skymusic::parseRequest(line, request, error))
            response = {0, false, {}, error};
        else
            response = handleRequest(host, request, running);
        std::cout << skymusic::serializeResponse(response) << '\n' << std::flush;
    }
    host.unload();
    Steinberg::Vst::PluginContextFactory::instance().setPluginContext(nullptr);
    return 0;
}
