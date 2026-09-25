// 模块：标准 MIDI 时间线；事件顺序来自原文件，音频线程按采样位置执行。
#pragma once
#include <cstdint>
#include <vector>

namespace skymusic
{
struct SequenceEvent
{
    std::int64_t time {};
    int kind {}; // 0/1 音符开关，2 CC，3 弯音，4 通道压力，5 键压力，6 音色，7 速度。
    int data1 {};
    int data2 {};
    int channel {};
    int noteId {-1}; // 跨音轨同通道、同音高也有独立标识；-1 仅用于未配对的实时事件。
};

struct TransportPlan
{
    bool playing {};
    std::int64_t position {};
    std::size_t cursor {};
    std::vector<SequenceEvent> restore;
};

// 在控制线程重建拖动位置的控制器和延音状态，音频回调不扫描整首曲子。
TransportPlan prepareTransport(const std::vector<SequenceEvent>& events, bool playing, std::int64_t position);

// 返回音频块内的采样偏移；-1 表示事件属于未来的音频块。
inline int sequenceSampleOffset(std::int64_t microseconds, double firstFrame, double sampleRate, int frames)
{
    const double frame = microseconds * sampleRate / 1000000.0;
    if (frame >= firstFrame + frames) return -1;
    return frame <= firstFrame ? 0 : static_cast<int>(frame - firstFrame);
}
}
