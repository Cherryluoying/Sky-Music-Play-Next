// 模块：SkyMusic.AudioEngine 原生接口 wasapi_audio_device
#pragma once

#include "audio_clock.h"
#include "audio_device.h"

namespace skymusic::audio
{
class WasapiAudioDevice final : public IAudioDevice
{
public:
    bool run(
        AudioGraph& graph,
        std::atomic<bool>& stopRequested,
        const std::function<void()>& onStarted,
        std::string& error) override;

    const AudioClock& clock() const noexcept;

private:
    AudioClock clock_;
};
}
