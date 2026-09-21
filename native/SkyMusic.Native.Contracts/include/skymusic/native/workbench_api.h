// 模块：SkyMusic.Native.Contracts 原生接口 workbench_api
#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define SKYMUSIC_NATIVE_CALL __cdecl
#if defined(SKYMUSIC_NATIVE_EXPORTS)
#define SKYMUSIC_NATIVE_API __declspec(dllexport)
#else
#define SKYMUSIC_NATIVE_API __declspec(dllimport)
#endif
#else
#define SKYMUSIC_NATIVE_CALL
#define SKYMUSIC_NATIVE_API
#endif

#ifdef __cplusplus
extern "C"
{
#endif

enum
{
    SKYMUSIC_WORKBENCH_ABI_VERSION = 1
};

typedef struct SkyMusicWorkbenchHandle SkyMusicWorkbenchHandle;

typedef struct SkyMusicProjectNoteData
{
    uint64_t id_high;
    uint64_t id_low;
    int64_t start_tick;
    int64_t length_ticks;
    int32_t midi_note;
    int32_t velocity;
    int32_t channel;
    int32_t track_index;
} SkyMusicProjectNoteData;

typedef struct SkyMusicWorkbenchViewport
{
    double width;
    double height;
    double dpi_scale;
    double horizontal_offset;
    double vertical_offset;
    double pixels_per_tick;
    double row_height;
} SkyMusicWorkbenchViewport;

typedef void(SKYMUSIC_NATIVE_CALL* SkyMusicEditCommandCallback)(
    void* user_data,
    const char* command_json_utf8);

SKYMUSIC_NATIVE_API uint32_t SKYMUSIC_NATIVE_CALL skymusic_workbench_abi_version(void);

SKYMUSIC_NATIVE_API SkyMusicWorkbenchHandle* SKYMUSIC_NATIVE_CALL skymusic_workbench_create(
    void* parent_window,
    SkyMusicEditCommandCallback callback,
    void* user_data);

SKYMUSIC_NATIVE_API void SKYMUSIC_NATIVE_CALL skymusic_workbench_destroy(
    SkyMusicWorkbenchHandle* handle);

SKYMUSIC_NATIVE_API int32_t SKYMUSIC_NATIVE_CALL skymusic_workbench_set_viewport(
    SkyMusicWorkbenchHandle* handle,
    const SkyMusicWorkbenchViewport* viewport);

SKYMUSIC_NATIVE_API int32_t SKYMUSIC_NATIVE_CALL skymusic_workbench_set_notes(
    SkyMusicWorkbenchHandle* handle,
    const SkyMusicProjectNoteData* notes,
    uint32_t note_count);

SKYMUSIC_NATIVE_API int32_t SKYMUSIC_NATIVE_CALL skymusic_workbench_set_playhead(
    SkyMusicWorkbenchHandle* handle,
    int64_t tick);

#ifdef __cplusplus
}
#endif
