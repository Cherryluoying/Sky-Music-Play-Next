// 模块：SkyMusic.VstHost 原生实现 protocol
#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include "sequence.h"

namespace skymusic
{
struct Request
{
    std::int64_t id {};
    std::string type;
    std::optional<std::string> path;
    std::optional<int> note;
    std::optional<int> velocity;
    std::optional<int> channel;
    std::vector<SequenceEvent> events;
    bool playing {};
    std::int64_t position {};
};

struct Response
{
    std::int64_t id {};
    bool ok {};
    std::optional<std::string> name;
    std::optional<std::string> error;
};

bool parseRequest(const std::string& line, Request& request, std::string& error);
std::string serializeResponse(const Response& response);
}
