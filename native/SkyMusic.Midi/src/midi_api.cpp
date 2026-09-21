// 模块：SkyMusic.Midi 原生实现 midi_api
#include "skymusic/midi/midi_api.h"

#include "RtMidi.h"

#include <algorithm>
#include <cstring>
#include <memory>
#include <string>
#include <vector>

struct SkyMusicMidiInput
{
    std::unique_ptr<RtMidiIn> device;
    SkyMusicMidiMessageCallback callback {};
    void* userData {};
};

struct SkyMusicMidiOutput
{
    std::unique_ptr<RtMidiOut> device;
};

namespace
{
template<typename Device>
int32_t portCount()
{
    try
    {
        Device device;
        return static_cast<int32_t>(device.getPortCount());
    }
    catch (...)
    {
        return -1;
    }
}

template<typename Device>
int32_t portName(uint32_t index, char* destination, uint32_t capacity)
{
    try
    {
        Device device;
        if (index >= device.getPortCount())
            return -1;
        const auto name = device.getPortName(index);
        const auto required = static_cast<uint32_t>(name.size() + 1);
        if (destination && capacity > 0)
        {
            const auto copyLength = std::min<uint32_t>(static_cast<uint32_t>(name.size()), capacity - 1);
            std::memcpy(destination, name.data(), copyLength);
            destination[copyLength] = '\0';
        }
        return static_cast<int32_t>(required);
    }
    catch (...)
    {
        return -1;
    }
}

// 将 RtMidi 回调转发为稳定的 C ABI 消息
void midiInputCallback(double deltaSeconds, std::vector<unsigned char>* message, void* userData)
{
    auto* input = static_cast<SkyMusicMidiInput*>(userData);
    if (!input || !input->callback || !message || message->empty())
        return;

    // 驱动线程只转发原始 MIDI 字节
    input->callback(input->userData, deltaSeconds, message->data(), static_cast<uint32_t>(message->size()));
}
}

uint32_t SKYMUSIC_MIDI_CALL skymusic_midi_abi_version(void)
{
    return SKYMUSIC_MIDI_ABI_VERSION;
}

int32_t SKYMUSIC_MIDI_CALL skymusic_midi_input_port_count(void)
{
    return portCount<RtMidiIn>();
}

int32_t SKYMUSIC_MIDI_CALL skymusic_midi_output_port_count(void)
{
    return portCount<RtMidiOut>();
}

int32_t SKYMUSIC_MIDI_CALL skymusic_midi_input_port_name(uint32_t index, char* destination, uint32_t capacity)
{
    return portName<RtMidiIn>(index, destination, capacity);
}

int32_t SKYMUSIC_MIDI_CALL skymusic_midi_output_port_name(uint32_t index, char* destination, uint32_t capacity)
{
    return portName<RtMidiOut>(index, destination, capacity);
}

SkyMusicMidiInput* SKYMUSIC_MIDI_CALL skymusic_midi_input_create(
    SkyMusicMidiMessageCallback callback,
    void* userData)
{
    try
    {
        auto input = std::make_unique<SkyMusicMidiInput>();
        input->device = std::make_unique<RtMidiIn>(RtMidi::Api::WINDOWS_MM, "SkyMusicPlay Input");
        input->callback = callback;
        input->userData = userData;
        input->device->ignoreTypes(false, false, false);
        input->device->setCallback(midiInputCallback, input.get());
        return input.release();
    }
    catch (...)
    {
        return nullptr;
    }
}

// 打开指定输入端口并启用音符回调
int32_t SKYMUSIC_MIDI_CALL skymusic_midi_input_open(SkyMusicMidiInput* input, uint32_t portIndex)
{
    if (!input || !input->device)
        return -1;
    try
    {
        if (input->device->isPortOpen())
            input->device->closePort();
        if (portIndex >= input->device->getPortCount())
            return -1;
        input->device->openPort(portIndex, "SkyMusicPlay Input");
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

void SKYMUSIC_MIDI_CALL skymusic_midi_input_close(SkyMusicMidiInput* input)
{
    if (input && input->device && input->device->isPortOpen())
        input->device->closePort();
}

void SKYMUSIC_MIDI_CALL skymusic_midi_input_destroy(SkyMusicMidiInput* input)
{
    if (!input)
        return;
    if (input->device)
    {
        input->device->cancelCallback();
        if (input->device->isPortOpen())
            input->device->closePort();
    }
    delete input;
}

SkyMusicMidiOutput* SKYMUSIC_MIDI_CALL skymusic_midi_output_create(void)
{
    try
    {
        auto output = std::make_unique<SkyMusicMidiOutput>();
        output->device = std::make_unique<RtMidiOut>(RtMidi::Api::WINDOWS_MM, "SkyMusicPlay Output");
        return output.release();
    }
    catch (...)
    {
        return nullptr;
    }
}

// 打开指定输出端口供播放目标发送 MIDI
int32_t SKYMUSIC_MIDI_CALL skymusic_midi_output_open(SkyMusicMidiOutput* output, uint32_t portIndex)
{
    if (!output || !output->device)
        return -1;
    try
    {
        if (output->device->isPortOpen())
            output->device->closePort();
        if (portIndex >= output->device->getPortCount())
            return -1;
        output->device->openPort(portIndex, "SkyMusicPlay Output");
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

int32_t SKYMUSIC_MIDI_CALL skymusic_midi_output_send(
    SkyMusicMidiOutput* output,
    const uint8_t* data,
    uint32_t size)
{
    if (!output || !output->device || !output->device->isPortOpen() || !data || size == 0)
        return -1;
    try
    {
        output->device->sendMessage(data, size);
        return 0;
    }
    catch (...)
    {
        return -1;
    }
}

void SKYMUSIC_MIDI_CALL skymusic_midi_output_close(SkyMusicMidiOutput* output)
{
    if (output && output->device && output->device->isPortOpen())
        output->device->closePort();
}

void SKYMUSIC_MIDI_CALL skymusic_midi_output_destroy(SkyMusicMidiOutput* output)
{
    if (!output)
        return;
    if (output->device && output->device->isPortOpen())
        output->device->closePort();
    delete output;
}
