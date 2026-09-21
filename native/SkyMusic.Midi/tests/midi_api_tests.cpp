// 模块：SkyMusic.Midi 原生测试 midi_api_tests
#include "skymusic/midi/midi_api.h"

#include <cstdlib>

namespace
{
void SKYMUSIC_MIDI_CALL receiveMidi(void*, double, const uint8_t*, uint32_t)
{
}
}

int main()
{
    if (skymusic_midi_abi_version() != SKYMUSIC_MIDI_ABI_VERSION)
        return EXIT_FAILURE;

    const auto inputCount = skymusic_midi_input_port_count();
    const auto outputCount = skymusic_midi_output_port_count();
    if (inputCount < 0 || outputCount < 0)
        return EXIT_FAILURE;
    if (skymusic_midi_input_port_name(static_cast<uint32_t>(inputCount), nullptr, 0) >= 0 ||
        skymusic_midi_output_port_name(static_cast<uint32_t>(outputCount), nullptr, 0) >= 0)
        return EXIT_FAILURE;

    auto* input = skymusic_midi_input_create(&receiveMidi, nullptr);
    auto* output = skymusic_midi_output_create();
    if ((inputCount > 0 && !input) || (outputCount > 0 && !output) ||
        skymusic_midi_input_open(nullptr, 0) >= 0 ||
        skymusic_midi_output_open(nullptr, 0) >= 0)
    {
        skymusic_midi_input_destroy(input);
        skymusic_midi_output_destroy(output);
        return EXIT_FAILURE;
    }

    const uint8_t noteOn[] {0x90, 60, 100};
    if ((input && skymusic_midi_input_open(input, static_cast<uint32_t>(inputCount)) >= 0) ||
        (output && skymusic_midi_output_open(output, static_cast<uint32_t>(outputCount)) >= 0) ||
        skymusic_midi_output_send(output, noteOn, sizeof(noteOn)) >= 0)
    {
        skymusic_midi_input_destroy(input);
        skymusic_midi_output_destroy(output);
        return EXIT_FAILURE;
    }

    skymusic_midi_input_close(input);
    skymusic_midi_output_close(output);
    skymusic_midi_input_destroy(input);
    skymusic_midi_output_destroy(output);
    return EXIT_SUCCESS;
}
