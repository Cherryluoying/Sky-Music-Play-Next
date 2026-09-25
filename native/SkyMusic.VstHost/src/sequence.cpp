// 模块：暂停/定位的 MIDI 状态恢复，保留踩踏板时已经松键的音符。
#include "sequence.h"
#include <array>
#include <map>
#include <algorithm>

namespace skymusic
{
TransportPlan prepareTransport(const std::vector<SequenceEvent>& events, bool playing, std::int64_t position)
{
    TransportPlan plan {playing, position};
    // 暂停只需要定位游标，不重建状态，避免大文件的状态扫描延迟静音。
    if (!playing)
    {
        plan.cursor = std::lower_bound(events.begin(), events.end(), position,
            [](const auto& e, auto time) { return e.time < time; }) - events.begin();
        return plan;
    }
    std::map<std::pair<int, int>, SequenceEvent> controls;
    std::array<std::array<std::vector<SequenceEvent>, 128>, 16> pressed;
    std::array<std::array<std::vector<SequenceEvent>, 128>, 16> held;
    std::array<bool, 16> sustain {};
    for (; plan.cursor < events.size() && events[plan.cursor].time < position; ++plan.cursor)
    {
        const auto& e = events[plan.cursor];
        if (e.kind == 0)
            pressed[e.channel][e.data1].push_back(e);
        else if (e.kind == 1)
        {
            auto& notes = pressed[e.channel][e.data1];
            const auto note = e.noteId < 0 ? notes.begin() : std::find_if(notes.begin(), notes.end(),
                [&](const auto& on) { return on.noteId == e.noteId; });
            if (note != notes.end())
            {
                if (sustain[e.channel]) held[e.channel][e.data1].push_back(*note);
                notes.erase(note);
            }
        }
        else
        {
            const auto key = e.kind == 2 ? e.data1 : 128 + e.kind * 128 + (e.kind == 5 ? e.data1 : 0);
            controls[{e.channel, key}] = e;
            if (e.kind == 2 && e.data1 == 64)
            {
                sustain[e.channel] = e.data2 >= 64;
                if (!sustain[e.channel]) for (auto& notes : held[e.channel]) notes.clear();
            }
            if (e.kind == 2 && (e.data1 == 120 || e.data1 == 123))
            {
                for (auto& notes : pressed[e.channel]) notes.clear();
                for (auto& notes : held[e.channel]) notes.clear();
            }
        }
    }
    if (playing)
    {
        for (const auto& entry : controls) plan.restore.push_back(entry.second);
        std::stable_sort(plan.restore.begin(), plan.restore.end(), [](const auto& a, const auto& b) { return a.time < b.time; });
        for (int channel = 0; channel < 16; ++channel)
            for (int pitch = 0; pitch < 128; ++pitch)
            {
                for (const auto& note : held[channel][pitch])
                {
                    plan.restore.push_back(note);
                    plan.restore.push_back({position, 1, pitch, 0, channel, note.noteId});
                }
                for (const auto& note : pressed[channel][pitch]) plan.restore.push_back(note);
            }
    }
    return plan;
}
}
