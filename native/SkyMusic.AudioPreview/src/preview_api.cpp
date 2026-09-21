// 模块：SkyMusic.AudioPreview 原生实现 preview_api
#include "skymusic/audio_preview/preview_api.h"

#include "media_foundation_decoder.h"
#include "skymusic/audio/audio_graph.h"
#include "skymusic/audio/rtaudio_device.h"
#include "skymusic/audio/sample_player_node.h"

#include <mfapi.h>

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

struct SkyMusicAudioPreview
{
    skymusic::audio::SamplePlayerNode player;
    skymusic::audio::AudioGraph graph;
    skymusic::audio::RtAudioDevice device {{0, 128, 2, true}};
    std::atomic<bool> stopRequested {false};
    std::thread audioThread;
    std::mutex stateMutex;
    std::condition_variable stateChanged;
    bool started {};
    bool finished {};
    std::string error;
};

uint32_t SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_abi_version(void)
{
    return SKYMUSIC_AUDIO_PREVIEW_ABI_VERSION;
}

// 创建后台音频设备并等待渲染线程就绪
SkyMusicAudioPreview* SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_create(void)
{
    if (FAILED(MFStartup(MF_VERSION, MFSTARTUP_LITE)))
        return nullptr;

    try
    {
        auto preview = std::make_unique<SkyMusicAudioPreview>();
        preview->graph.addNode(preview->player);
        auto* state = preview.get();
        preview->audioThread = std::thread([state] {
            const auto onStarted = [state] {
                {
                    std::scoped_lock lock(state->stateMutex);
                    state->started = true;
                }
                state->stateChanged.notify_all();
            };
            state->device.run(state->graph, state->stopRequested, onStarted, state->error);
            {
                std::scoped_lock lock(state->stateMutex);
                state->finished = true;
            }
            state->stateChanged.notify_all();
        });

        std::unique_lock lock(preview->stateMutex);
        preview->stateChanged.wait_for(lock, std::chrono::seconds(3), [&preview] {
            return preview->started || preview->finished;
        });
        if (!preview->started)
        {
            preview->stopRequested.store(true, std::memory_order_release);
            lock.unlock();
            if (preview->audioThread.joinable())
                preview->audioThread.join();
            MFShutdown();
            return nullptr;
        }
        return preview.release();
    }
    catch (...)
    {
        MFShutdown();
        return nullptr;
    }
}

void SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_destroy(SkyMusicAudioPreview* preview)
{
    if (!preview)
        return;
    preview->stopRequested.store(true, std::memory_order_release);
    if (preview->audioThread.joinable())
        preview->audioThread.join();
    delete preview;
    MFShutdown();
}

// 在调用线程解码采样并写入固定槽位
int32_t SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_load(
    SkyMusicAudioPreview* preview,
    uint32_t slot,
    const wchar_t* path)
{
    if (!preview || slot >= skymusic::audio::SamplePlayerNode::MaximumSamples)
        return -1;
    const auto sampleRate = preview->device.metrics().sampleRate;
    std::vector<float> samples;
    std::string error;
    return decodeAudioFile(path, sampleRate, samples, error) && preview->player.setSample(slot, std::move(samples))
        ? 0
        : -1;
}

int32_t SKYMUSIC_AUDIO_PREVIEW_CALL skymusic_audio_preview_trigger(
    SkyMusicAudioPreview* preview,
    uint32_t slot,
    float gain)
{
    return preview && preview->player.trigger(slot, gain) ? 0 : -1;
}
