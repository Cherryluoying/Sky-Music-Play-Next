// 模块：SkyMusic.Core MIDI 领域 MidiPerformanceRecorder
using System.Diagnostics;
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Midi;

public sealed class MidiPerformanceRecorder
{
    private readonly object _gate = new();
    private readonly Stopwatch _clock = new();
    private readonly Dictionary<(int Note, int Channel), ActiveNote> _active = [];
    private readonly List<NoteEvent> _notes = [];

    public bool IsRecording { get; private set; }

    public int NoteCount
    {
        get
        {
            lock (_gate)
            {
                return _notes.Count + _active.Count;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            _notes.Clear();
            _active.Clear();
            _clock.Restart();
            IsRecording = true;
        }
    }

    public void Process(MidiNoteMessage message)
    {
        if (message.Note is < 21 or > 108)
        {
            return;
        }

        lock (_gate)
        {
            if (!IsRecording)
            {
                return;
            }

            var now = ElapsedMicroseconds();
            var key = (message.Note, message.Channel);
            if (message.IsNoteOn && message.Velocity > 0)
            {
                // 重复按下时先闭合旧音符，避免悬挂音符
                CloseNote(key, now);
                _active[key] = new ActiveNote(now, message.Velocity);
                return;
            }

            CloseNote(key, now);
        }
    }

    public Score Stop(string title, string composer = "")
    {
        lock (_gate)
        {
            var now = ElapsedMicroseconds();
            foreach (var key in _active.Keys.ToArray())
            {
                CloseNote(key, now);
            }

            _clock.Stop();
            IsRecording = false;
            return new Score(
                string.IsNullOrWhiteSpace(title) ? "MIDI 录制" : title.Trim(),
                composer.Trim(),
                _notes.OrderBy(note => note.StartMicroseconds).ThenBy(note => note.MidiNote).ToArray(),
                new Dictionary<string, string> { ["sourceFormat"] = "midi-input" });
        }
    }

    private void CloseNote((int Note, int Channel) key, long now)
    {
        if (!_active.Remove(key, out var active))
        {
            return;
        }

        _notes.Add(new NoteEvent(
            key.Note,
            active.StartMicroseconds,
            Math.Max(10_000, now - active.StartMicroseconds),
            active.Velocity,
            Channel: key.Channel));
    }

    private long ElapsedMicroseconds() =>
        checked(_clock.ElapsedTicks * 1_000_000L / Stopwatch.Frequency);

    private readonly record struct ActiveNote(long StartMicroseconds, byte Velocity);
}
