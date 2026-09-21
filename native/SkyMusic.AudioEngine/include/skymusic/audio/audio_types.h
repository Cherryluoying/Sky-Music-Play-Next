// 模块：SkyMusic.AudioEngine 原生接口 audio_types
#pragma once

#include <cstdint>

namespace skymusic::audio
{
enum class AudioProcessingMode
{
    Realtime,
    Offline
};

struct AudioStreamFormat
{
    std::uint32_t sampleRate {};
    std::uint16_t channelCount {};
    std::uint32_t maximumFrames {};
    AudioProcessingMode processingMode {AudioProcessingMode::Realtime};
};

struct AudioRenderContext
{
    std::uint64_t samplePosition {};
    double sampleRate {};
    double tempo {120.0};
    bool realtime {true};
};
}
