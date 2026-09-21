// 模块：SkyMusic.Midi 原生接口 midi_api
#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define SKYMUSIC_MIDI_CALL __cdecl
#if defined(SKYMUSIC_MIDI_EXPORTS)
#define SKYMUSIC_MIDI_API __declspec(dllexport)
#else
#define SKYMUSIC_MIDI_API __declspec(dllimport)
#endif
#else
#define SKYMUSIC_MIDI_CALL
#define SKYMUSIC_MIDI_API
#endif

#ifdef __cplusplus
extern "C"
{
#endif

enum
{
    SKYMUSIC_MIDI_ABI_VERSION = 1
};

typedef struct SkyMusicMidiInput SkyMusicMidiInput;
typedef struct SkyMusicMidiOutput SkyMusicMidiOutput;

typedef void(SKYMUSIC_MIDI_CALL* SkyMusicMidiMessageCallback)(
    void* user_data,
    double delta_seconds,
    const uint8_t* data,
    uint32_t size);

SKYMUSIC_MIDI_API uint32_t SKYMUSIC_MIDI_CALL skymusic_midi_abi_version(void);
SKYMUSIC_MIDI_API int32_t SKYMUSIC_MIDI_CALL skymusic_midi_input_port_count(void);
SKYMUSIC_MIDI_API int32_t SKYMUSIC_MIDI_CALL skymusic_midi_output_port_count(void);
SKYMUSIC_MIDI_API int32_t SKYMUSIC_MIDI_CALL skymusic_midi_input_port_name(
    uint32_t index, char* destination, uint32_t capacity);
SKYMUSIC_MIDI_API int32_t SKYMUSIC_MIDI_CALL skymusic_midi_output_port_name(
    uint32_t index, char* destination, uint32_t capacity);

SKYMUSIC_MIDI_API SkyMusicMidiInput* SKYMUSIC_MIDI_CALL skymusic_midi_input_create(
    SkyMusicMidiMessageCallback callback, void* user_data);
SKYMUSIC_MIDI_API int32_t SKYMUSIC_MIDI_CALL skymusic_midi_input_open(
    SkyMusicMidiInput* input, uint32_t port_index);
SKYMUSIC_MIDI_API void SKYMUSIC_MIDI_CALL skymusic_midi_input_close(SkyMusicMidiInput* input);
SKYMUSIC_MIDI_API void SKYMUSIC_MIDI_CALL skymusic_midi_input_destroy(SkyMusicMidiInput* input);

SKYMUSIC_MIDI_API SkyMusicMidiOutput* SKYMUSIC_MIDI_CALL skymusic_midi_output_create(void);
SKYMUSIC_MIDI_API int32_t SKYMUSIC_MIDI_CALL skymusic_midi_output_open(
    SkyMusicMidiOutput* output, uint32_t port_index);
SKYMUSIC_MIDI_API int32_t SKYMUSIC_MIDI_CALL skymusic_midi_output_send(
    SkyMusicMidiOutput* output, const uint8_t* data, uint32_t size);
SKYMUSIC_MIDI_API void SKYMUSIC_MIDI_CALL skymusic_midi_output_close(SkyMusicMidiOutput* output);
SKYMUSIC_MIDI_API void SKYMUSIC_MIDI_CALL skymusic_midi_output_destroy(SkyMusicMidiOutput* output);

#ifdef __cplusplus
}
#endif
