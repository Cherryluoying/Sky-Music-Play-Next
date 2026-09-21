// 模块：SkyMusic.Infrastructure 游戏乐谱领域 GenshinMusicGameScoreStore
using System.Globalization;
using System.Text.Json;
using SkyMusic.Core.GameScores;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.GameScores;

public sealed class GenshinMusicGameScoreStore : IGameScoreStore
{
    // 识别 genshin-music 新旧结构并载入游戏谱
    public async ValueTask<GameScoreDocument> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var json = await JsonDocument.ParseAsync(source, cancellationToken: cancellationToken);
        var song = UnwrapSong(json.RootElement);
        var profile = ReadProfile(song);
        var document = song.TryGetProperty("columns", out var columns) && columns.ValueKind == JsonValueKind.Array
            ? ParseComposed(song, columns, profile)
            : ParseRecorded(song, profile);
        GameScoreValidator.Validate(document);
        return document;
    }

    // 以现代格式保存完整编谱文档
    public async ValueTask SaveAsync(
        GameScoreDocument document,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        GameScoreValidator.Validate(document);
        await using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = true });
        writer.WriteStartArray();
        WriteModernSong(writer, document);
        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken);
    }

    public async ValueTask SaveLegacyAsync(
        GameScoreDocument document,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        GameScoreValidator.Validate(document);
        await using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = true });
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("name", document.Name);
        writer.WriteNumber("bpm", document.Bpm);
        writer.WriteNumber("bitsPerPage", 16);
        writer.WriteNumber("pitchLevel", PitchIndex(document.Pitch));
        writer.WriteBoolean("isComposed", true);
        writer.WritePropertyName("songNotes");
        writer.WriteStartArray();
        WriteLegacyNotes(writer, document);
        writer.WriteEndArray();
        writer.WriteBoolean("isEncrypted", false);
        writer.WriteEndObject();
        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken);
    }

    private static JsonElement UnwrapSong(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            if (root.GetArrayLength() == 0)
                throw new InvalidDataException("Sheet file does not contain a song");
            return root[0];
        }
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Sheet root must be an object or array");
        return root;
    }

    // 解析按列组织的编谱格式并保留多乐器图层
    private static GameScoreDocument ParseComposed(
        JsonElement song,
        JsonElement serializedColumns,
        GameScoreProfile profile)
    {
        var version = ReadInt(song, "version", 1);
        var columns = new List<GameScoreColumn>();
        ulong highestMask = 0;
        foreach (var serializedColumn in serializedColumns.EnumerateArray())
        {
            if (serializedColumn.ValueKind != JsonValueKind.Array || serializedColumn.GetArrayLength() < 2)
                throw new InvalidDataException("Composed sheet contains an invalid column");
            var tempoStep = serializedColumn[0].GetInt32();
            var masks = new Dictionary<int, ulong>();
            foreach (var serializedNote in serializedColumn[1].EnumerateArray())
            {
                if (serializedNote.ValueKind != JsonValueKind.Array || serializedNote.GetArrayLength() < 2)
                    continue;
                var key = serializedNote[0].GetInt32();
                var mask = ParseLayerMask(serializedNote[1], version);
                if (mask == 0)
                    continue;
                masks[key] = masks.GetValueOrDefault(key) | mask;
                highestMask |= mask;
            }
            columns.Add(new GameScoreColumn(
                Math.Clamp(tempoStep, 0, 3),
                masks.OrderBy(pair => pair.Key).Select(pair => new GameScoreNote(pair.Key, pair.Value)).ToArray()));
        }

        var instruments = ParseInstruments(song, profile).ToList();
        EnsureInstrumentCount(instruments, RequiredLayers(highestMask), profile);
        var validMask = instruments.Count == 64 ? ulong.MaxValue : (1UL << instruments.Count) - 1;
        columns = columns.Select(column => column with
        {
            Notes = column.Notes.Select(note => note with { LayerMask = note.LayerMask & validMask })
                .Where(note => note.LayerMask != 0 && note.KeyIndex >= 0 && note.KeyIndex < KeyCount(profile))
                .ToArray()
        }).ToList();
        if (columns.Count == 0)
            columns.AddRange(Enumerable.Range(0, 32).Select(_ => GameScoreColumn.Empty));

        return new GameScoreDocument(
            ReadString(song, "name", "Untitled"),
            profile,
            Math.Clamp(ReadInt(song, "bpm", 220), 20, 999),
            ReadPitch(song),
            ReadBool(song, "reverb", false),
            instruments,
            columns,
            ParseBreakpoints(song, columns.Count));
    }

    private static GameScoreDocument ParseRecorded(JsonElement song, GameScoreProfile profile)
    {
        var notes = new List<RecordedGameNote>();
        if (song.TryGetProperty("notes", out var modernNotes) && modernNotes.ValueKind == JsonValueKind.Array)
        {
            foreach (var serialized in modernNotes.EnumerateArray())
            {
                if (serialized.ValueKind != JsonValueKind.Array || serialized.GetArrayLength() < 2)
                    continue;
                notes.Add(new RecordedGameNote(
                    serialized[0].GetInt32(),
                    serialized[1].GetDouble(),
                    serialized.GetArrayLength() > 2 ? ParseLayerMask(serialized[2], 2) : 1));
            }
        }
        else if (song.TryGetProperty("songNotes", out var legacyNotes) && legacyNotes.ValueKind == JsonValueKind.Array)
        {
            foreach (var serialized in legacyNotes.EnumerateArray())
            {
                var keyText = ReadString(serialized, "key", "1Key0");
                var parts = keyText.Split("Key", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || !int.TryParse(parts[1], out var key))
                    continue;
                var prefixMask = int.TryParse(parts[0], out var prefix) ? prefix : 1;
                var mask = (ulong)Math.Max(1, ReadInt(serialized, "l", prefixMask));
                notes.Add(new RecordedGameNote(
                    MapLegacyKey(profile, key),
                    ReadDouble(serialized, "time", 0),
                    mask));
            }
        }

        var bpm = Math.Clamp(ReadInt(song, "bpm", 220), 20, 999);
        var columns = ConvertRecordedNotes(notes, bpm, profile);
        var highestMask = notes.Aggregate(0UL, (current, note) => current | note.LayerMask);
        var instruments = ParseInstruments(song, profile).ToList();
        EnsureInstrumentCount(instruments, RequiredLayers(highestMask), profile);
        return new GameScoreDocument(
            ReadString(song, "name", "Untitled"),
            profile,
            bpm,
            ReadPitch(song),
            ReadBool(song, "reverb", false),
            instruments,
            columns,
            [0]);
    }

    // 按节拍量化录制事件并拆分长间隔
    private static IReadOnlyList<GameScoreColumn> ConvertRecordedNotes(
        IReadOnlyList<RecordedGameNote> source,
        int bpm,
        GameScoreProfile profile)
    {
        var notes = source.Where(note => note.KeyIndex >= 0 && note.KeyIndex < KeyCount(profile))
            .OrderBy(note => note.TimeMilliseconds).ToArray();
        if (notes.Length == 0)
            return Enumerable.Range(0, 32).Select(_ => GameScoreColumn.Empty).ToArray();

        var beat = 60_000d / bpm;
        // 九分之一拍内的音符视为同一和弦列
        var groups = new List<List<RecordedGameNote>>();
        foreach (var note in notes)
        {
            if (groups.Count == 0 || note.TimeMilliseconds - groups[^1][0].TimeMilliseconds >= beat / 9d)
                groups.Add([note]);
            else
                groups[^1].Add(note);
        }

        var columns = new List<GameScoreColumn>();
        for (var index = 0; index < groups.Count; index++)
        {
            var masks = groups[index].GroupBy(note => note.KeyIndex)
                .ToDictionary(group => group.Key, group => group.Aggregate(0UL, (mask, note) => mask | note.LayerMask));
            var duration = index + 1 < groups.Count
                ? groups[index + 1][0].TimeMilliseconds - groups[index][0].TimeMilliseconds
                : beat;
            var steps = DecomposeDuration(duration, beat);
            columns.Add(new GameScoreColumn(
                steps[0],
                masks.OrderBy(pair => pair.Key).Select(pair => new GameScoreNote(pair.Key, pair.Value)).ToArray()));
            columns.AddRange(steps.Skip(1).Select(step => new GameScoreColumn(step, [])));
        }
        return columns;
    }

    private static IReadOnlyList<int> DecomposeDuration(double duration, double beat)
    {
        var result = new List<int>();
        var remaining = Math.Max(duration, beat / 8d);
        for (var guard = 0; guard < 10_000 && remaining >= beat / 8d - 0.5d; guard++)
        {
            var step = Enumerable.Range(0, 4)
                .FirstOrDefault(candidate => remaining >= beat * GameTempoSteps.GetRatio(candidate) - 1d, 3);
            result.Add(step);
            remaining -= beat * GameTempoSteps.GetRatio(step);
        }
        if (result.Count == 0)
            result.Add(3);
        return result;
    }

    private static IReadOnlyList<GameInstrumentLayer> ParseInstruments(JsonElement song, GameScoreProfile profile)
    {
        var result = new List<GameInstrumentLayer>();
        if (!song.TryGetProperty("instruments", out var instruments) || instruments.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var item in instruments.EnumerateArray().Take(GameScoreDocument.MaxLayers))
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                result.Add(CreateInstrument(item.GetString(), result.Count, profile));
                continue;
            }
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var icon = ReadString(item, "icon", "circle").ToLowerInvariant() switch
            {
                "border" => GameNoteIcon.Border,
                "line" => GameNoteIcon.Line,
                _ => GameNoteIcon.Circle
            };
            result.Add(new GameInstrumentLayer(
                Guid.NewGuid(),
                ReadString(item, "name", DefaultInstrument(profile)),
                Math.Clamp(ReadInt(item, "volume", 100), 0, 127),
                ReadString(item, "pitch", "C"),
                ReadBool(item, "visible", true),
                icon,
                ReadString(item, "alias", string.Empty),
                ReadBool(item, "muted", false),
                ReadNullableBool(item, "reverbOverride")));
        }
        return result;
    }

    // 写出 genshin-music 可直接读取的现代歌曲结构
    private static void WriteModernSong(Utf8JsonWriter writer, GameScoreDocument document)
    {
        writer.WriteStartObject();
        writer.WriteNull("id");
        writer.WriteString("type", "composed");
        writer.WriteNull("folderId");
        writer.WriteString("name", document.Name);
        writer.WritePropertyName("data");
        writer.WriteStartObject();
        writer.WriteBoolean("isComposed", true);
        writer.WriteBoolean("isComposedVersion", true);
        writer.WriteString("appName", document.Profile == GameScoreProfile.Genshin ? "Genshin" : "Sky");
        writer.WriteEndObject();
        writer.WriteNumber("bpm", document.Bpm);
        writer.WriteString("pitch", document.Pitch);
        writer.WriteNumber("version", 3);
        writer.WriteBoolean("reverb", document.Reverb);
        writer.WritePropertyName("breakpoints");
        JsonSerializer.Serialize(writer, document.Breakpoints);
        writer.WritePropertyName("instruments");
        writer.WriteStartArray();
        foreach (var instrument in document.Instruments)
        {
            writer.WriteStartObject();
            writer.WriteString("name", instrument.Name);
            writer.WriteNumber("volume", instrument.Volume);
            writer.WriteString("pitch", instrument.Pitch);
            writer.WriteBoolean("visible", instrument.IsVisible);
            writer.WriteString("icon", instrument.Icon.ToString().ToLowerInvariant());
            writer.WriteString("alias", instrument.Alias);
            writer.WriteBoolean("muted", instrument.IsMuted);
            if (instrument.ReverbOverride is bool reverb)
                writer.WriteBoolean("reverbOverride", reverb);
            else
                writer.WriteNull("reverbOverride");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WritePropertyName("columns");
        writer.WriteStartArray();
        foreach (var column in document.Columns)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(column.TempoStep);
            writer.WriteStartArray();
            foreach (var note in column.Notes)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(note.KeyIndex);
                writer.WriteStringValue(note.LayerMask.ToString("x", CultureInfo.InvariantCulture));
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteLegacyNotes(Utf8JsonWriter writer, GameScoreDocument document)
    {
        var time = 100;
        foreach (var column in document.Columns)
        {
            foreach (var note in column.Notes)
            {
                var mask = note.LayerMask & 0xFUL;
                if (mask == 0)
                    continue;
                var oldLayer = LegacyLayer(mask);
                writer.WriteStartObject();
                writer.WriteString("key", $"{Math.Min(oldLayer, 2)}Key{note.KeyIndex}");
                writer.WriteNumber("time", time);
                if (oldLayer > 2)
                    writer.WriteNumber("l", 3);
                writer.WriteEndObject();
            }
            time += (int)Math.Floor((60_000d / document.Bpm) * GameTempoSteps.GetRatio(column.TempoStep));
        }
    }

    private static ulong ParseLayerMask(JsonElement value, int version)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "0",
            JsonValueKind.Number => value.GetRawText(),
            _ => "0"
        };
        try
        {
            if (version == 1 && text.All(character => character is '0' or '1'))
            {
                // v1 从左到右保存图层位 后续版本改为十六进制掩码
                var reversed = new string(text.Reverse().ToArray());
                return Convert.ToUInt64(reversed, 2);
            }
            return ulong.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw new InvalidDataException($"Invalid layer mask '{text}'", exception);
        }
    }

    private static IReadOnlyList<int> ParseBreakpoints(JsonElement song, int columnCount)
    {
        if (!song.TryGetProperty("breakpoints", out var values) || values.ValueKind != JsonValueKind.Array)
            return [0];
        var result = values.EnumerateArray().Where(value => value.TryGetInt32(out _)).Select(value => value.GetInt32())
            .Where(value => value >= 0 && value < columnCount).Distinct().Order().ToArray();
        return result.Length == 0 ? [0] : result;
    }

    private static GameScoreProfile ReadProfile(JsonElement song)
    {
        if (song.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("appName", out var appName) &&
            string.Equals(appName.GetString(), "Genshin", StringComparison.OrdinalIgnoreCase))
            return GameScoreProfile.Genshin;
        return GameScoreProfile.Sky;
    }

    private static string ReadPitch(JsonElement song)
    {
        if (song.TryGetProperty("pitch", out var pitch) && pitch.ValueKind == JsonValueKind.String)
            return pitch.GetString() ?? "C";
        var index = Math.Clamp(ReadInt(song, "pitchLevel", 0), 0, 11);
        return new[] { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" }[index];
    }

    private static int PitchIndex(string pitch)
        => Array.IndexOf(new[] { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" }, pitch) is var index && index >= 0 ? index : 0;

    private static int ReadInt(JsonElement value, string property, int fallback)
        => value.TryGetProperty(property, out var item) && item.TryGetInt32(out var result) ? result : fallback;

    private static double ReadDouble(JsonElement value, string property, double fallback)
        => value.TryGetProperty(property, out var item) && item.TryGetDouble(out var result) ? result : fallback;

    private static string ReadString(JsonElement value, string property, string fallback)
        => value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() ?? fallback : fallback;

    private static bool ReadBool(JsonElement value, string property, bool fallback)
        => value.TryGetProperty(property, out var item) && item.ValueKind is JsonValueKind.True or JsonValueKind.False ? item.GetBoolean() : fallback;

    private static bool? ReadNullableBool(JsonElement value, string property)
        => value.TryGetProperty(property, out var item) && item.ValueKind is JsonValueKind.True or JsonValueKind.False ? item.GetBoolean() : null;

    private static int RequiredLayers(ulong mask)
    {
        var count = 0;
        while (mask != 0)
        {
            count++;
            mask >>= 1;
        }
        return Math.Max(1, count);
    }

    private static void EnsureInstrumentCount(List<GameInstrumentLayer> instruments, int count, GameScoreProfile profile)
    {
        while (instruments.Count < Math.Min(count, GameScoreDocument.MaxLayers))
            instruments.Add(CreateInstrument(null, instruments.Count, profile));
        if (instruments.Count == 0)
            instruments.Add(CreateInstrument(null, 0, profile));
    }

    private static GameInstrumentLayer CreateInstrument(string? name, int index, GameScoreProfile profile)
        => new(Guid.NewGuid(), name ?? DefaultInstrument(profile), Icon: (GameNoteIcon)(index % 3));

    private static string DefaultInstrument(GameScoreProfile profile)
        => profile == GameScoreProfile.Genshin ? "Lyre" : "Piano";

    private static int KeyCount(GameScoreProfile profile) => profile == GameScoreProfile.Genshin ? 21 : 15;

    private static int MapLegacyKey(GameScoreProfile profile, int key)
    {
        if (profile != GameScoreProfile.Genshin || key is < 0 or >= 15)
            return key;
        int[] positions = [14, 15, 16, 17, 18, 19, 20, 7, 8, 9, 10, 11, 12, 13, 0];
        return positions[key];
    }

    private static int LegacyLayer(ulong mask) => mask switch
    {
        2 or 4 or 6 or 8 or 10 or 14 => 2,
        3 or 5 or 7 or 9 or 11 or 12 or 13 or 15 => 3,
        _ => 1
    };

    private sealed record RecordedGameNote(int KeyIndex, double TimeMilliseconds, ulong LayerMask);
}
