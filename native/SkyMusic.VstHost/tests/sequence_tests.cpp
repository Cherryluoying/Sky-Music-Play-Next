// 模块：原生 MIDI 定位和暂停回归，不需要声卡或第三方插件。
#include "sequence.h"
#include "protocol.h"
#include <stdexcept>
#include <iostream>
#include <algorithm>

static void check(bool condition, const char* message)
{
    if (!condition) throw std::runtime_error(message);
}

int main()
{
    using namespace skymusic;
    try
    {
        check(sequenceSampleOffset(0, 0, 48000, 480) == 0, "first note must start at the first sample");
        check(sequenceSampleOffset(5000, 0, 48000, 480) == 240, "arpeggio must keep its sub-frame offset");
        check(sequenceSampleOffset(10000, 0, 48000, 480) == -1, "exact block end belongs to the next block");
        check(sequenceSampleOffset(10000, 480, 48000, 480) == 0, "next block must not drop the boundary event");
        check(sequenceSampleOffset(1000000, 480, 48000, 480) == -1, "rests must not be collapsed");
        const std::vector<SequenceEvent> events {
            {0, 0, 60, 24, 2}, {5000, 2, 64, 127, 2}, {10000, 1, 60, 30, 2},
            {12000, 0, 60, 110, 2}, {20000, 1, 60, 0, 2}, {50000, 2, 64, 0, 2},
            {1000000, 0, 67, 90, 1}};
        const auto paused = prepareTransport(events, false, 15000);
        check(paused.restore.empty(), "pause must not replay notes");
        const auto resumed = prepareTransport(events, true, 15000);
        check(resumed.cursor == 4, "resume must retain the next event, including short notes");
        check(std::count_if(resumed.restore.begin(), resumed.restore.end(), [](auto e) {return e.kind == 0;}) == 2,
            "restore must include pressed and sustained repeated notes");
        check(resumed.restore.back().data2 == 110 && resumed.restore.back().channel == 2,
            "each repeated note keeps its velocity and channel");
        const auto silence = prepareTransport(events, true, 80000);
        check(std::none_of(silence.restore.begin(), silence.restore.end(), [](auto e) {return e.kind == 0;}),
            "seek into a rest must not replay earlier notes");
        check(prepareTransport(events, true, 12000).cursor == 3, "exact seek boundary must leave note-on pending");
        Request request;
        std::string error;
        const std::vector<SequenceEvent> sharedPitch {
            {0, 0, 81, 100, 11, 10}, {1000, 0, 81, 40, 11, 20}, {2000, 1, 81, 0, 11, 20}};
        const auto voices = prepareTransport(sharedPitch, true, 3000);
        check(voices.restore.size() == 1 && voices.restore[0].noteId == 10 && voices.restore[0].data2 == 100,
            "seek must release the matching track voice, not the first note of the same pitch");
        check(parseRequest(R"({"id":4,"type":"sequence","events":[{"time":0,"kind":0,"data1":81,"data2":80,"channel":11,"noteId":123}]})", request, error)
            && request.events[0].noteId == 123, "note identity must survive the host protocol");
        check(parseRequest(R"({"id":1,"type":"sequence","events":[{"time":0,"kind":3,"data1":16383,"data2":0,"channel":15}]})", request, error), "pitch bend full range must parse");
        check(!parseRequest(R"({"id":2,"type":"sequence","events":[{"time":0,"kind":0,"data1":128,"data2":80,"channel":0}]})", request, error), "invalid note must be rejected");
        check(!parseRequest(R"({"id":3,"type":"transport","playing":true,"position":-1})", request, error), "negative seek must be rejected");
        std::cout << "MIDI sequence regression checks passed\n";
        return 0;
    }
    catch (const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}
