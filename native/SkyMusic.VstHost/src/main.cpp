// 模块：SkyMusic.VstHost 原生实现 main
#include "host.h"
#include "protocol.h"

#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "pluginterfaces/base/funknown.h"

#include <iostream>

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
    skymusic::VstHost host;
    bool running = true;
    std::string line;
    while (running && std::getline(std::cin, line))
    {
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
    return 0;
}
