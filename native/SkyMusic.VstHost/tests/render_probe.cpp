// 模块：手动离线诊断，将与实时播放相同的 VST 处理链输出为浮点 PCM，便于频谱核对。
#include "host.h"
#include "protocol.h"
#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "skymusic/audio/offline_renderer.h"
#include <fstream>
#include <iostream>
#include <iterator>
#include <stdexcept>

namespace Steinberg { FUnknown* gStandardPluginContext = new Vst::HostApplication(); }
namespace skymusic
{
struct VstRenderProbe
{
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
    if (argc != 5)
    {
        std::cerr << "usage: RenderProbe plugin.vst3 sequence.json output.f32 seconds\n";
        return 2;
    }
    Steinberg::Vst::PluginContextFactory::instance().setPluginContext(Steinberg::gStandardPluginContext);
    try
    {
        std::ifstream input(argv[2]);
        const std::string json((std::istreambuf_iterator<char>(input)), {});
        skymusic::Request request;
        std::string error, name;
        if (!skymusic::parseRequest(json, request, error)) throw std::runtime_error(error);
        skymusic::VstHost host;
        if (!host.load(argv[1], name, error)) throw std::runtime_error(error);
        if (!skymusic::VstRenderProbe::run(host, std::move(request.events), argv[3], std::stoul(argv[4]), error))
            throw std::runtime_error(error);
        std::cout << "Rendered " << name << " at 48000 Hz, stereo float32\n";
        return 0;
    }
    catch (const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}
