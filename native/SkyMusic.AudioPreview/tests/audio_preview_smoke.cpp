// 模块：SkyMusic.AudioPreview 原生测试 audio_preview_smoke
#include "skymusic/audio_preview/preview_api.h"

#include <chrono>
#include <thread>

int wmain(int argumentCount, wchar_t** arguments)
{
    if (argumentCount != 2 || skymusic_audio_preview_abi_version() != SKYMUSIC_AUDIO_PREVIEW_ABI_VERSION)
        return 1;

    auto* preview = skymusic_audio_preview_create();
    if (!preview)
        return 2;
    const auto loaded = skymusic_audio_preview_load(preview, 0, arguments[1]);
    const auto triggered = loaded == 0 ? skymusic_audio_preview_trigger(preview, 0, 0.65f) : -1;
    std::this_thread::sleep_for(std::chrono::milliseconds(300));
    skymusic_audio_preview_destroy(preview);
    return loaded == 0 && triggered == 0 ? 0 : 3;
}
