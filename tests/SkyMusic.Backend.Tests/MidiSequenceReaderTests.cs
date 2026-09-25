// 模块：MIDI 文件保真回归，覆盖短音/重复音/控制器/速度图与导入标题。
using Melanchall.DryWetMidi.Core;
using SkyMusic.Infrastructure.Catalog;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.Backend.Tests;

public sealed class MidiSequenceReaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));

    private string WriteMidi()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "月光 · 琶音.mid");
        var file = new MidiFile(
            new TrackChunk(new SetTempoEvent(500_000), new SetTempoEvent(1_000_000) { DeltaTime = 480 },
                new TextEvent("tail rest") { DeltaTime = 480 }),
            new TrackChunk(
                new ProgramChangeEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)5) { Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)2 },
                new NoteOnEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)60, (Melanchall.DryWetMidi.Common.SevenBitNumber)23) { Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)2 },
                new NoteOffEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)60, (Melanchall.DryWetMidi.Common.SevenBitNumber)17) { DeltaTime = 12, Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)2 },
                new NoteOnEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)60, (Melanchall.DryWetMidi.Common.SevenBitNumber)110) { Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)2 },
                new NoteOffEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)60, (Melanchall.DryWetMidi.Common.SevenBitNumber)0) { DeltaTime = 12, Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)2 },
                new ControlChangeEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)64, (Melanchall.DryWetMidi.Common.SevenBitNumber)127),
                new PitchBendEvent(12288),
                new NoteOnEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)12, (Melanchall.DryWetMidi.Common.SevenBitNumber)80) { Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)9 },
                new NoteOffEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)12, (Melanchall.DryWetMidi.Common.SevenBitNumber)0) { DeltaTime = 600, Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)9 },
                new ControlChangeEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)64, (Melanchall.DryWetMidi.Common.SevenBitNumber)0)));
        file.TimeDivision = new TicksPerQuarterNoteTimeDivision(480);
        file.Write(path, overwriteFile: true);
        return path;
    }

    [Fact]
    public void PreservesShortRepeatedNotesChannelsPedalBendAndTempoChanges()
    {
        var sequence = MidiSequenceReader.Read(WriteMidi());
        var notes = sequence.Events.Where(e => e.Kind is 0 or 1 && e.Channel == 2).ToArray();
        Assert.Equal(new long[] { 0, 12_500, 12_500, 25_000 }, notes.Select(n => n.Time));
        Assert.Equal(new[] { 0, 1, 0, 1 }, notes.Select(n => n.Kind));
        Assert.Equal(new[] { 23, 17, 110, 0 }, notes.Select(n => n.Data2));
        Assert.Contains(sequence.Events, e => e.Kind == 2 && e.Data1 == 64 && e.Data2 == 127);
        Assert.Contains(sequence.Events, e => e.Kind == 3 && e.Data1 == 12288);
        Assert.Contains(sequence.Events, e => e.Kind == 6 && e.Channel == 2 && e.Data1 == 5);
        Assert.Contains(sequence.Events, e => e.Kind == 1 && e.Channel == 9 && e.Data1 == 12 && e.Time == 800_000);
        Assert.Equal(TimeSpan.FromSeconds(1.5), sequence.Duration); // 尾部休止来自 EOT。
    }

    [Fact]
    public void SharedChannelTracksKeepIndependentNotePairsAndOriginalPitches()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "双声部.mid");
        // 上声部先列于文件，但后发声；下声部先松键，不能按合并后的 FIFO 配对。
        var file = new MidiFile(
            new TrackChunk(new NoteOnEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)81, (Melanchall.DryWetMidi.Common.SevenBitNumber)100) { DeltaTime = 240 },
                new NoteOffEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)81, (Melanchall.DryWetMidi.Common.SevenBitNumber)0) { DeltaTime = 480 }),
            new TrackChunk(new NoteOnEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)81, (Melanchall.DryWetMidi.Common.SevenBitNumber)40),
                new NoteOffEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)81, (Melanchall.DryWetMidi.Common.SevenBitNumber)0) { DeltaTime = 480 }));
        file.TimeDivision = new TicksPerQuarterNoteTimeDivision(480);
        file.Write(path);
        var notes = MidiSequenceReader.Read(path).Events.Where(e => e.Kind <= 1).ToArray();
        Assert.Equal(new long[] { 0, 250_000, 500_000, 750_000 }, notes.Select(e => e.Time));
        Assert.All(notes, e => Assert.Equal(81, e.Data1));
        Assert.NotEqual(notes[0].NoteId, notes[1].NoteId);
        Assert.Equal(notes[0].NoteId, notes[2].NoteId);
        Assert.Equal(notes[1].NoteId, notes[3].NoteId);
    }

    [Fact]
    public void SharedPitchHandoffReleasesOldVoiceBeforeNewAttackButKeepsZeroLengthNotes()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "声部交接.mid");
        var pitch = (Melanchall.DryWetMidi.Common.SevenBitNumber)81;
        var velocity = (Melanchall.DryWetMidi.Common.SevenBitNumber)76;
        var file = new MidiFile(
            new TrackChunk(new NoteOnEvent(pitch, velocity) { DeltaTime = 480 },
                new NoteOffEvent(pitch, velocity) { DeltaTime = 480 },
                new NoteOnEvent(pitch, velocity), new NoteOffEvent(pitch, velocity)),
            new TrackChunk(new NoteOnEvent(pitch, velocity), new NoteOffEvent(pitch, velocity) { DeltaTime = 480 }));
        file.TimeDivision = new TicksPerQuarterNoteTimeDivision(480);
        file.Write(path);
        var notes = MidiSequenceReader.Read(path).Events.Where(e => e.Kind <= 1).ToArray();
        Assert.Equal(new[] { 0, 1, 0, 1, 0, 1 }, notes.Select(e => e.Kind));
        Assert.Equal(new long[] { 0, 500_000, 500_000, 1_000_000, 1_000_000, 1_000_000 }, notes.Select(e => e.Time));
        Assert.Equal(notes[0].NoteId, notes[1].NoteId);
        Assert.Equal(notes[2].NoteId, notes[3].NoteId);
        Assert.Equal(notes[4].NoteId, notes[5].NoteId);
    }

    [Fact]
    public async Task ImportRetainsOriginalTitleWhileDeduplicatingStoredFile()
    {
        var path = WriteMidi();
        var library = Path.Combine(_directory, "library");
        var importer = new MediaImportService(library, new ScoreImportService());
        var first = await importer.ImportAsync(path);
        var second = await importer.ImportAsync(path);
        Assert.Equal("月光 · 琶音", first.Title);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.SourcePath, second.SourcePath);
        Assert.Equal(TimeSpan.FromSeconds(1.5), first.Duration);
        Assert.Single(Directory.GetFiles(Path.Combine(library, "midi")));
        Assert.Equal(await File.ReadAllBytesAsync(path), await File.ReadAllBytesAsync(first.SourcePath!));
    }

    [Fact]
    public async Task ImportUsesSavedMidiDirectoryImmediatelyAndKeepsPreviousFile()
    {
        var source = WriteMidi();
        var library = Path.Combine(_directory, "library");
        var store = new SkyMusic.Infrastructure.Settings.JsonAppSettingsStore(Path.Combine(_directory, "settings.json"));
        var importer = new MediaImportService(library, new ScoreImportService(), settingsStore: store);
        var original = await importer.ImportAsync(source);
        var target = Path.Combine(_directory, "新的 MIDI 库");
        await store.SaveAsync(new SkyMusic.Core.Settings.AppSettings
        {
            Storage = new SkyMusic.Core.Settings.StorageSettings { MidiLibraryDirectory = target }
        });
        var updated = await importer.ImportAsync(source);
        Assert.Equal(target, Path.GetDirectoryName(updated.SourcePath));
        Assert.Equal(original.Id, updated.Id);
        Assert.True(File.Exists(original.SourcePath));
        Assert.True(File.Exists(source));
        Assert.Equal(await File.ReadAllBytesAsync(source), await File.ReadAllBytesAsync(updated.SourcePath!));
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
