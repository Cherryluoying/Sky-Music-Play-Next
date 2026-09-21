// 模块：SkyMusic.AudioPreview 原生接口 preview_api
#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define SKYMUSIC_AUDIO_PREVIEW_CALL __cdecl
#if defined(SKYMUSIC_AUDIO_PREVIEW_EXPORTS)
#define SKYMUSIC_AUDIO_PREVIEW_API __declspec(dllexport)
#else
#define SKYMUSIC_AUDIO_PREVIEW_API __declspec(dllimport)
#endif
#else
#define SKYMUSIC_AUDIO_PREVIEW_CALL
#define SKYMUSIC_AUDIO_PREVIEW_API
#endif

#ifdef __cplusplus
extern "C"
{
#endif

enum
{
    SKYMUSIC_AUDIO_PREVIEW_ABI_VERSION = 1
};

typedef struct SkyMusicAudioPreview SkyMusicAudioPreview;

SKYMUSIC_AUDIO_PREVIEW_API uint32_t SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_abi_version(void);
SKYMUSIC_AUDIO_PREVIEW_API SkyMusicAudioPreview* SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_create(void);
SKYMUSIC_AUDIO_PREVIEW_API void SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_destroy(
    SkyMusicAudioPreview* preview);
SKYMUSIC_AUDIO_PREVIEW_API int32_t SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_load(
    SkyMusicAudioPreview* preview,
    uint32_t slot,
    const wchar_t* path);
SKYMUSIC_AUDIO_PREVIEW_API int32_t SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_trigger(
    SkyMusicAudioPreview* preview,
    uint32_t slot,
    float gain);

#ifdef __cplusplus
}
#endif
