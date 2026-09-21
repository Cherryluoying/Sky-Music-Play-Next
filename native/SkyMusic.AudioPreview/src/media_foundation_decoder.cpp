// 模块：SkyMusic.AudioPreview 原生实现 media_foundation_decoder
#include "media_foundation_decoder.h"

#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <wrl/client.h>

#include <memory>

using Microsoft::WRL::ComPtr;

bool decodeAudioFile(
    const wchar_t* path,
    std::uint32_t sampleRate,
    std::vector<float>& samples,
    std::string& error)
{
    if (!path || sampleRate == 0)
    {
        error = "invalid sample path or rate";
        return false;
    }

    const auto comResult = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    const bool uninitialize = SUCCEEDED(comResult);
    const auto comScope = std::unique_ptr<void, void (*)(void*)>(
        uninitialize ? reinterpret_cast<void*>(1) : nullptr,
        [](void* value) { if (value) CoUninitialize(); });

    ComPtr<IMFSourceReader> reader;
    if (FAILED(MFCreateSourceReaderFromURL(path, nullptr, &reader)))
    {
        error = "audio sample could not be opened";
        return false;
    }

    reader->SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, FALSE);
    reader->SetStreamSelection(MF_SOURCE_READER_FIRST_AUDIO_STREAM, TRUE);

    ComPtr<IMFMediaType> mediaType;
    if (FAILED(MFCreateMediaType(&mediaType)) ||
        FAILED(mediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio)) ||
        FAILED(mediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_Float)) ||
        FAILED(mediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, 2)) ||
        FAILED(mediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, sampleRate)) ||
        FAILED(mediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 32)) ||
        FAILED(mediaType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, 8)) ||
        FAILED(mediaType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, sampleRate * 8)) ||
        FAILED(reader->SetCurrentMediaType(MF_SOURCE_READER_FIRST_AUDIO_STREAM, nullptr, mediaType.Get())))
    {
        error = "audio sample format could not be configured";
        return false;
    }

    samples.clear();
    while (true)
    {
        DWORD flags = 0;
        ComPtr<IMFSample> sample;
        const auto result = reader->ReadSample(
            MF_SOURCE_READER_FIRST_AUDIO_STREAM,
            0,
            nullptr,
            &flags,
            nullptr,
            &sample);
        if (FAILED(result))
        {
            error = "audio sample could not be decoded";
            return false;
        }
        if ((flags & MF_SOURCE_READERF_ENDOFSTREAM) != 0)
            break;
        if (!sample)
            continue;

        ComPtr<IMFMediaBuffer> buffer;
        BYTE* data = nullptr;
        DWORD length = 0;
        if (FAILED(sample->ConvertToContiguousBuffer(&buffer)) ||
            FAILED(buffer->Lock(&data, nullptr, &length)))
        {
            error = "decoded audio buffer could not be read";
            return false;
        }
        const auto* begin = reinterpret_cast<const float*>(data);
        samples.insert(samples.end(), begin, begin + (length / sizeof(float)));
        buffer->Unlock();
    }

    return !samples.empty() && samples.size() % 2 == 0;
}
