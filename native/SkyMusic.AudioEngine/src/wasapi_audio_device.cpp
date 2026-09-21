// 模块：SkyMusic.AudioEngine 原生实现 wasapi_audio_device
#include "skymusic/audio/wasapi_audio_device.h"

#include <audioclient.h>
#include <avrt.h>
#include <ksmedia.h>
#include <mmdeviceapi.h>
#include <wrl/client.h>

#include <memory>

using Microsoft::WRL::ComPtr;

namespace skymusic::audio
{
namespace
{
bool isFloatFormat(const WAVEFORMATEX* format)
{
    if (format->wFormatTag == WAVE_FORMAT_IEEE_FLOAT && format->wBitsPerSample == 32)
        return true;
    if (format->wFormatTag != WAVE_FORMAT_EXTENSIBLE || format->cbSize < 22)
        return false;
    const auto* extensible = reinterpret_cast<const WAVEFORMATEXTENSIBLE*>(format);
    return extensible->SubFormat == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT && format->wBitsPerSample == 32;
}
}

bool WasapiAudioDevice::run(
    AudioGraph& graph,
    std::atomic<bool>& stopRequested,
    const std::function<void()>& onStarted,
    std::string& error)
{
    const auto comResult = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(comResult))
    {
        error = "COM audio initialization failed";
        return false;
    }
    const auto uninitializeCom = std::unique_ptr<void, void (*)(void*)>(reinterpret_cast<void*>(1), [](void*) {
        CoUninitialize();
    });

    ComPtr<IMMDeviceEnumerator> enumerator;
    ComPtr<IMMDevice> device;
    ComPtr<IAudioClient> audioClient;
    ComPtr<IAudioRenderClient> renderClient;
    if (FAILED(CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&enumerator))) ||
        FAILED(enumerator->GetDefaultAudioEndpoint(eRender, eConsole, &device)) ||
        FAILED(device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, &audioClient)))
    {
        error = "default Windows audio output is unavailable";
        return false;
    }

    WAVEFORMATEX* mixFormat = nullptr;
    if (FAILED(audioClient->GetMixFormat(&mixFormat)) || !mixFormat)
    {
        error = "Windows audio mix format is unavailable";
        return false;
    }
    const auto releaseFormat = std::unique_ptr<WAVEFORMATEX, decltype(&CoTaskMemFree)>(mixFormat, CoTaskMemFree);
    if (!isFloatFormat(mixFormat))
    {
        error = "Windows audio output must use 32-bit float shared mode";
        return false;
    }

    const DWORD streamFlags = AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST;
    if (FAILED(audioClient->Initialize(AUDCLNT_SHAREMODE_SHARED, streamFlags, 0, 0, mixFormat, nullptr)))
    {
        error = "Windows shared audio stream could not be initialized";
        return false;
    }

    UINT32 bufferFrames = 0;
    if (FAILED(audioClient->GetBufferSize(&bufferFrames)) ||
        FAILED(audioClient->GetService(IID_PPV_ARGS(&renderClient))))
    {
        error = "Windows audio render buffer is unavailable";
        return false;
    }

    const HANDLE bufferEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    if (!bufferEvent || FAILED(audioClient->SetEventHandle(bufferEvent)))
    {
        if (bufferEvent)
            CloseHandle(bufferEvent);
        error = "Windows audio event could not be created";
        return false;
    }
    const auto closeEvent = std::unique_ptr<void, decltype(&CloseHandle)>(bufferEvent, CloseHandle);

    const AudioStreamFormat format {
        mixFormat->nSamplesPerSec,
        mixFormat->nChannels,
        bufferFrames
    };
    if (!graph.prepare(format, error))
        return false;
    const auto releaseGraph = std::unique_ptr<void, std::function<void(void*)>>(reinterpret_cast<void*>(1), [&graph](void*) {
        graph.release();
    });

    BYTE* initialData = nullptr;
    if (SUCCEEDED(renderClient->GetBuffer(bufferFrames, &initialData)))
        renderClient->ReleaseBuffer(bufferFrames, AUDCLNT_BUFFERFLAGS_SILENT);
    if (FAILED(audioClient->Start()))
    {
        error = "Windows audio stream could not be started";
        return false;
    }

    clock_.configure(format.sampleRate);
    clock_.reset();
    onStarted();
    DWORD taskIndex = 0;
    const HANDLE mmcss = AvSetMmThreadCharacteristicsW(L"Pro Audio", &taskIndex);

    // 实时线程只推进设备时钟并渲染音频图
    while (!stopRequested.load(std::memory_order_acquire))
    {
        if (WaitForSingleObject(bufferEvent, 100) != WAIT_OBJECT_0)
            continue;
        UINT32 padding = 0;
        if (FAILED(audioClient->GetCurrentPadding(&padding)) || padding >= bufferFrames)
            continue;
        const UINT32 frames = bufferFrames - padding;
        BYTE* output = nullptr;
        if (FAILED(renderClient->GetBuffer(frames, &output)))
            continue;

        graph.render(clock_.context(true), reinterpret_cast<float*>(output), frames, format.channelCount);
        renderClient->ReleaseBuffer(frames, 0);
        clock_.advance(frames);
    }

    audioClient->Stop();
    if (mmcss)
        AvRevertMmThreadCharacteristics(mmcss);
    return true;
}

const AudioClock& WasapiAudioDevice::clock() const noexcept
{
    return clock_;
}
}
