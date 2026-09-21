// 模块：SkyMusic.AudioPreview 原生实现 media_foundation_decoder
#pragma once

#include <cstdint>
#include <string>
#include <vector>

bool decodeAudioFile(
    const wchar_t* path,
    std::uint32_t sampleRate,
    std::vector<float>& samples,
    std::string& error);
