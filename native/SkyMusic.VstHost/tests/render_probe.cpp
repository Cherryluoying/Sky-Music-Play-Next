// 模块：手动离线诊断，将与实时播放相同的 VST 处理链输出为浮点 PCM，便于频谱核对。
#include "host.h"
#include "protocol.h"
#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "skymusic/audio/offline_renderer.h"
#include <fstream>
#include <iostream>
#include <iterator>
#include <stdexcept>
#include <cmath>

namespace Steinberg { FUnknown* gStandardPluginContext = new Vst::HostApplication(); }
namespace skymusic
{
struct VstRenderProbe
{
    // 实际插件离线回归：文件发声 -> 暂停严格静音 -> 实时音符重新发声 -> 再次暂停。
    static void checkPauseAndLive(VstHost& host)
    {
        host.stopRequested_.store(true);
        if (host.renderThread_.joinable()) host.renderThread_.join();
        host.sequence_ = {{0, 0, 60, 100, 0, 1}, {3000000, 1, 60, 0, 0, 1}};
        std::string error;
        if (!host.graph_.prepare({48000, 2, 256, audio::AudioProcessingMode::Offline}, error))
            throw std::runtime_error(error);
        std::uint64_t position {};
        auto measure = [&] {
            double energy {};
            std::array<float, 512> samples {};
            for (int block = 0; block < 188; ++block)
            {
                host.graph_.render({position, 48000, 120, false}, samples.data(), 256, 2);
                position += 256;
                for (const auto sample : samples)
                {
                    if (!std::isfinite(sample)) throw std::runtime_error("nonfinite plugin output");
                    energy += sample * sample;
                }
            }
            return energy;
        };
        auto send = [&](MidiCommand command) {
            if (!host.enqueue(command, error)) throw std::runtime_error(error);
        };
        try
        {
            auto play = prepareTransport(host.sequence_, true, 0);
            send({MidiCommandType::Transport, 0, 0, 0, &play});
            const auto fileEnergy = measure();
            auto pause = prepareTransport(host.sequence_, false, 1000000);
            send({MidiCommandType::Transport, 0, 0, 0, &pause});
            const auto pausedEnergy = measure();
            send({MidiCommandType::ChannelMessage, 64, 127, 0, nullptr, -1, 2});
            send({MidiCommandType::ChannelMessage, 67, 100, 0, nullptr, -1, 0});
            const auto liveEnergy = measure();
            send({MidiCommandType::ChannelMessage, 67, 0, 0, nullptr, -1, 1});
            send({MidiCommandType::AllNotesOff});
            const auto stoppedEnergy = measure();
            send({MidiCommandType::Transport, 0, 0, 0, &play});
            const auto resumedEnergy = measure();
            send({MidiCommandType::Transport, 0, 0, 0, &pause});
            const auto finalPauseEnergy = measure();
            host.graph_.release();
            std::cout << "energy file=" << fileEnergy << " paused=" << pausedEnergy << " live=" << liveEnergy
                      << " stopped=" << stoppedEnergy << " resumed=" << resumedEnergy << " pausedAgain=" << finalPauseEnergy << '\n';
            if (fileEnergy <= 0.000001 || liveEnergy <= 0.000001 || resumedEnergy <= 0.000001 ||
                pausedEnergy != 0 || stoppedEnergy != 0 || finalPauseEnergy != 0)
                throw std::runtime_error("pause/live/resume audio regression failed");
        }
        catch (...) { host.graph_.release(); throw; }
    }

    // 仅在诊断进程使用；停止实时设备后，由离线时钟驱动同一 render 函数。
    static bool run(VstHost& host, std::vector<SequenceEvent> events, const char* output,
                    unsigned seconds, std::string& error)
    {
        host.stopRequested_.store(true);
        if (host.renderThread_.joinable()) host.renderThread_.join();
        host.sequence_ = std::move(events);
        auto plan = prepareTransport(host.sequence_, true, 0);
        if (!host.enqueue({MidiCommandType::Transport, 0, 0, 0, &plan}, error)) return false;
        std::ofstream pcm(output, std::ios::binary);
        if (!pcm) { error = "cannot create output"; return false; }
        audio::OfflineRenderer renderer;
        return renderer.render(host.graph_, {48000, 2, 256, audio::AudioProcessingMode::Offline},
            static_cast<std::uint64_t>(seconds) * 48000,
            [&](const float* samples, std::uint32_t frames, std::uint16_t channels) {
                pcm.write(reinterpret_cast<const char*>(samples), frames * channels * sizeof(float));
                return static_cast<bool>(pcm);
            }, error);
    }
};
}

int main(int argc, char** argv)
{
    const bool regression = argc == 3 && std::string(argv[2]) == "--regression";
    if (argc != 5 && !regression)
    {
        std::cerr << "usage: RenderProbe plugin.vst3 sequence.json output.f32 seconds\n";
        return 2;
    }
    Steinberg::Vst::PluginContextFactory::instance().setPluginContext(Steinberg::gStandardPluginContext);
    try
    {
        skymusic::Request request;
        std::string error, name;
        skymusic::VstHost host;
        if (!host.load(argv[1], name, error)) throw std::runtime_error(error);
        if (regression)
        {
            skymusic::VstRenderProbe::checkPauseAndLive(host);
            std::cout << "Pause/live/resume regression passed: " << name << '\n';
            return 0;
        }
        std::ifstream input(argv[2]);
        const std::string json((std::istreambuf_iterator<char>(input)), {});
        if (!skymusic::parseRequest(json, request, error)) throw std::runtime_error(error);
        if (!skymusic::VstRenderProbe::run(host, std::move(request.events), argv[3], std::stoul(argv[4]), error))
            throw std::runtime_error(error);
        std::cout << "Rendered " << name << " at 48000 Hz, stereo float32\n";
        return 0;
    }
    catch (const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}
