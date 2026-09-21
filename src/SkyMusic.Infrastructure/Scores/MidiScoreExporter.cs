// 模块：SkyMusic.Infrastructure 通用模型 MidiScoreExporter
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using SkyMusic.Core.Models;

namespace SkyMusic.Infrastructure.Scores;

public sealed class MidiScoreExporter
{
    private const short TicksPerQuarterNote = 480;
    private const long DefaultMicrosecondsPerQuarterNote = 500_000;

    // 将统一乐谱写为包含速度信息的标准 MIDI 文件
    public void Export(Score score, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(destination);

        var microsecondsPerQuarterNote = ResolveTempo(score);
        var groups = score.Notes.GroupBy(note => note.Track).OrderBy(group => group.Key).ToArray();
        var chunks = new List<TrackChunk>();
        if (groups.Length == 0)
        {
            chunks.Add(CreateTrack(
            [
                new TimedMidiEvent(0, 0, new SequenceTrackNameEvent(score.Title)),
                new TimedMidiEvent(0, 1, new SetTempoEvent(microsecondsPerQuarterNote))
            ]));
        }
        for (var index = 0; index < groups.Length; index++)
        {
            var group = groups[index];
            var events = new List<TimedMidiEvent>((group.Count() * 2) + 2)
            {
                new(0, 0, new SequenceTrackNameEvent(index == 0 ? score.Title : $"Track {group.Key + 1}"))
            };
            if (index == 0)
                events.Add(new TimedMidiEvent(0, 1, new SetTempoEvent(microsecondsPerQuarterNote)));
            foreach (var note in group)
            {
                var channel = (FourBitNumber)(byte)Math.Clamp(note.Channel, 0, 15);
                var number = (SevenBitNumber)(byte)note.MidiNote;
                events.Add(new TimedMidiEvent(
                    ToTicks(note.StartMicroseconds, microsecondsPerQuarterNote),
                    1,
                    new NoteOnEvent(number, (SevenBitNumber)note.Velocity) { Channel = channel }));
                events.Add(new TimedMidiEvent(
                    ToTicks(note.EndMicroseconds, microsecondsPerQuarterNote),
                    0,
                    new NoteOffEvent(number, (SevenBitNumber)0) { Channel = channel }));
            }
            chunks.Add(CreateTrack(events));
        }

        var file = new MidiFile(chunks.ToArray())
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote)
        };
        file.Write(destination, chunks.Count == 1 ? MidiFileFormat.SingleTrack : MidiFileFormat.MultiTrack);
    }

    private static long ToTicks(long microseconds, long microsecondsPerQuarterNote) =>
        Math.Max(0, checked(microseconds * TicksPerQuarterNote / microsecondsPerQuarterNote));

    private static long ResolveTempo(Score score)
    {
        if (score.Metadata?.TryGetValue("bpm", out var value) == true &&
            double.TryParse(value, out var bpm) && bpm is >= 20 and <= 999)
            return (long)Math.Round(60_000_000d / bpm);
        return DefaultMicrosecondsPerQuarterNote;
    }

    // 按绝对时间排序后生成 MIDI 增量时间事件
    private static TrackChunk CreateTrack(IEnumerable<TimedMidiEvent> source)
    {
        var ordered = source.OrderBy(item => item.Time).ThenBy(item => item.Order).ToArray();
        long previousTime = 0;
        foreach (var item in ordered)
        {
            item.Event.DeltaTime = item.Time - previousTime;
            previousTime = item.Time;
        }
        return new TrackChunk(ordered.Select(item => item.Event));
    }

    private sealed record TimedMidiEvent(long Time, int Order, MidiEvent Event);
}
