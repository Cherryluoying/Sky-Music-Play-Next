// 模块：SkyMusic.Backend.Tests 后端测试 MidiPerformanceRecorderTests
using SkyMusic.Core.Midi;

namespace SkyMusic.Backend.Tests;

public sealed class MidiPerformanceRecorderTests
{
    [Fact]
    public void RecordsNoteOnAndNoteOffAsScoreNote()
    {
        var recorder = new MidiPerformanceRecorder();
        recorder.Start();
        recorder.Process(new MidiNoteMessage(60, 96, 2, true, 0));
        Thread.Sleep(15);
        recorder.Process(new MidiNoteMessage(60, 0, 2, false, 15_000));

        var score = recorder.Stop("take");

        var note = Assert.Single(score.Notes);
        Assert.Equal(60, note.MidiNote);
        Assert.Equal(96, note.Velocity);
        Assert.Equal(2, note.Channel);
        Assert.True(note.DurationMicroseconds >= 10_000);
    }

    [Fact]
    public void StopClosesHeldNotes()
    {
        var recorder = new MidiPerformanceRecorder();
        recorder.Start();
        recorder.Process(new MidiNoteMessage(72, 80, 0, true, 0));

        var score = recorder.Stop("take");

        Assert.Single(score.Notes);
        Assert.False(recorder.IsRecording);
    }
}
