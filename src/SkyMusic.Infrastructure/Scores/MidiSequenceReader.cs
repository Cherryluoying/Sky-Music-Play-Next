// 模块：标准 MIDI 事件读取，保留顺序、通道、力度和表情控制。
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace SkyMusic.Infrastructure.Scores;

// Kind 与原生 sequence.h 保持一致；时间统一为微秒，转换时使用完整速度图。
public sealed record MidiSequenceEvent(long Time, int Kind, int Data1, int Data2, int Channel, int NoteId = -1);
public sealed record MidiSequenceData(IReadOnlyList<MidiSequenceEvent> Events, TimeSpan Duration);

public static class MidiSequenceReader
{
    public static MidiSequenceData Read(string path, CancellationToken cancellationToken = default)
    {
        var file = MidiFile.Read(path);
        var tempo = file.GetTempoMap();
        var events = new List<MidiSequenceEvent>();
        long end = 0;
        var nextNoteId = 0;
        foreach (var track in file.GetTrackChunks())
        {
            // 不同音轨可能复用同一通道、同一音高；先在原音轨配对，避免合并后松错声部。
            var pressed = new Dictionary<(int Channel, int Pitch), Queue<int>>();
            foreach (var timed in track.GetTimedEvents())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var time = TimeConverter.ConvertTo<MetricTimeSpan>(timed.Time, tempo).TotalMicroseconds;
                end = Math.Max(end, time);
                var channel = timed.Event is ChannelEvent channelEvent ? (int)channelEvent.Channel : 0;
                var item = timed.Event switch
                {
                    NoteOnEvent n => new MidiSequenceEvent(time, n.Velocity == 0 ? 1 : 0, n.NoteNumber, n.Velocity, channel),
                    NoteOffEvent n => new MidiSequenceEvent(time, 1, n.NoteNumber, n.Velocity, channel),
                    ControlChangeEvent c => new MidiSequenceEvent(time, 2, c.ControlNumber, c.ControlValue, channel),
                    PitchBendEvent b => new MidiSequenceEvent(time, 3, b.PitchValue, 0, channel),
                    ChannelAftertouchEvent a => new MidiSequenceEvent(time, 4, a.AftertouchValue, 0, channel),
                    NoteAftertouchEvent a => new MidiSequenceEvent(time, 5, a.NoteNumber, a.AftertouchValue, channel),
                    ProgramChangeEvent p => new MidiSequenceEvent(time, 6, p.ProgramNumber, 0, channel),
                    SetTempoEvent t => new MidiSequenceEvent(time, 7, checked((int)t.MicrosecondsPerQuarterNote), 0, 0),
                    _ => null
                };
                if (item?.Kind is 0 or 1)
                {
                    var key = (channel, item.Data1);
                    if (!pressed.TryGetValue(key, out var ids)) pressed[key] = ids = new Queue<int>();
                    if (item.Kind == 0)
                    {
                        item = item with { NoteId = nextNoteId++ };
                        ids.Enqueue(item.NoteId);
                    }
                    else if (ids.TryDequeue(out var id)) item = item with { NoteId = id };
                }
                if (item is not null)
                    events.Add(item);
            }
        }
        // 文件结束时释放踏板和声音，兼容未写最后一次松踏板的录制文件。
        for (var channel = 0; channel < 16; channel++)
        {
            events.Add(new MidiSequenceEvent(end, 2, 64, 0, channel));
            events.Add(new MidiSequenceEvent(end, 2, 123, 0, channel));
        }
        var ordered = events.OrderBy(e => e.Time).ToList();
        // 多轨同通道在相同时刻交接同音高：先释放上一音，再触发新音。
        // 只移动较早开始的音符的松键，零时长音符仍保持自己的 On → Off 顺序。
        var starts = ordered.Where(e => e.Kind == 0).ToDictionary(e => e.NoteId, e => e.Time);
        var firstOn = new Dictionary<(int Channel, int Pitch), int>();
        long groupTime = -1;
        for (var i = 0; i < ordered.Count; i++)
        {
            var e = ordered[i];
            if (e.Time != groupTime) { firstOn.Clear(); groupTime = e.Time; }
            var key = (e.Channel, e.Data1);
            if (e.Kind == 0) firstOn.TryAdd(key, i);
            else if (e.Kind == 1 && starts.TryGetValue(e.NoteId, out var start) && start < e.Time &&
                     firstOn.TryGetValue(key, out var before))
            {
                ordered.RemoveAt(i);
                ordered.Insert(before, e);
                foreach (var existing in firstOn.Keys.ToArray())
                    if (firstOn[existing] >= before) firstOn[existing]++;
            }
        }
        return new MidiSequenceData(ordered, TimeSpan.FromTicks(end * 10));
    }
}
