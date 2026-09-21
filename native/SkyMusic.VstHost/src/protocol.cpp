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
