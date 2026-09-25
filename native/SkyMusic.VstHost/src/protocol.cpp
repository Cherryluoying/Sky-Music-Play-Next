// 模块：SkyMusic.VstHost 原生实现 protocol
#include "protocol.h"

#include <rapidjson/document.h>
#include <rapidjson/stringbuffer.h>
#include <rapidjson/writer.h>

namespace skymusic
{
namespace
{
std::optional<int> readInt(const rapidjson::Value& value, const char* name)
{
    const auto member = value.FindMember(name);
    return member != value.MemberEnd() && member->value.IsInt()
        ? std::optional<int>(member->value.GetInt())
        : std::nullopt;
}
}

// 将单行 JSON 请求解析为宿主命令
bool parseRequest(const std::string& line, Request& request, std::string& error)
{
    rapidjson::Document document;
    if (document.Parse(line.data(), line.size()).HasParseError() || !document.IsObject())
    {
        error = "request is not valid JSON";
        return false;
    }

    const auto id = document.FindMember("id");
    const auto type = document.FindMember("type");
    if (id == document.MemberEnd() || !id->value.IsInt64() ||
        type == document.MemberEnd() || !type->value.IsString())
    {
        error = "request requires integer id and string type";
        return false;
    }

    request = {};
    request.id = id->value.GetInt64();
    request.type.assign(type->value.GetString(), type->value.GetStringLength());
    if (const auto path = document.FindMember("path");
        path != document.MemberEnd() && path->value.IsString())
    {
        request.path = std::string(path->value.GetString(), path->value.GetStringLength());
    }

    request.note = readInt(document, "note");
    request.velocity = readInt(document, "velocity");
    request.channel = readInt(document, "channel");
    if (request.type == "sequence")
    {
        const auto events = document.FindMember("events");
        if (events == document.MemberEnd() || !events->value.IsArray())
        {
            error = "sequence requires events";
            return false;
        }
        for (const auto& e : events->value.GetArray())
        {
            if (!e.IsObject() || !e.HasMember("time") || !e["time"].IsInt64())
            { error = "invalid MIDI event time"; return false; }
            const auto kind = readInt(e, "kind"), data1 = readInt(e, "data1"), data2 = readInt(e, "data2"), channel = readInt(e, "channel");
            if (!kind || !data1 || !data2 || !channel || *kind < 0 || *kind > 7 ||
                *channel < 0 || *channel > 15 || *data1 < 0 || *data2 < 0 || *data2 > 127 ||
                (*kind == 3 ? *data1 > 16383 : *kind == 7 ? *data1 == 0 : *data1 > 127) ||
                e["time"].GetInt64() < 0 || (!request.events.empty() && e["time"].GetInt64() < request.events.back().time))
            { error = "invalid or unordered MIDI event"; return false; }
            const auto noteId = readInt(e, "noteId");
            if ((e.HasMember("noteId") && !noteId) || (noteId && *noteId < -1))
            { error = "invalid MIDI note identity"; return false; }
            request.events.push_back({e["time"].GetInt64(), *kind, *data1, *data2, *channel, noteId.value_or(-1)});
        }
    }
    if (request.type == "transport")
    {
        if (!document.HasMember("playing") || !document["playing"].IsBool() ||
            !document.HasMember("position") || !document["position"].IsInt64() || document["position"].GetInt64() < 0)
        { error = "transport requires playing and nonnegative position"; return false; }
        request.playing = document["playing"].GetBool();
        request.position = document["position"].GetInt64();
    }
    return true;
}

// 将宿主执行结果序列化为单行 JSON
std::string serializeResponse(const Response& response)
{
    rapidjson::StringBuffer buffer;
    rapidjson::Writer<rapidjson::StringBuffer> writer(buffer);
    writer.StartObject();
    writer.Key("id");
    writer.Int64(response.id);
    writer.Key("ok");
    writer.Bool(response.ok);
    if (response.name)
    {
        writer.Key("name");
        writer.String(response.name->data(), static_cast<rapidjson::SizeType>(response.name->size()));
    }
    if (response.error)
    {
        writer.Key("error");
        writer.String(response.error->data(), static_cast<rapidjson::SizeType>(response.error->size()));
    }
    writer.EndObject();
    return {buffer.GetString(), buffer.GetSize()};
}
}
