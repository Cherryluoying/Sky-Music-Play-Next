// 模块：SkyMusic.Infrastructure 通用模型 SkyStudioScoreImporter
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkyMusic.Core.Importing;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Scores;

public sealed partial class SkyStudioScoreImporter : IScoreImporter
{
    private static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".json",
        ".skysheet"
    };

    private readonly SkyStudioImportOptions _options;
    private readonly SkyStudioEncryptedNotesDecoder _encryptedNotesDecoder;

    public SkyStudioScoreImporter(
        SkyStudioImportOptions? options = null,
        SkyStudioEncryptedNotesDecoder? encryptedNotesDecoder = null)
    {
        _options = options ?? new SkyStudioImportOptions();
        _encryptedNotesDecoder = encryptedNotesDecoder ?? new SkyStudioEncryptedNotesDecoder();
        if (_options.DefaultDurationMilliseconds <= 0 || _options.MinimumDurationMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Note durations must be positive");
        }
    }

    public IReadOnlySet<string> SupportedExtensions => Extensions;

    // 解码 Sky Studio 文本并返回统一乐谱模型
    public async ValueTask<ScoreImportResult> ImportAsync(
        Stream source,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            var json = TextFileDecoder.Decode(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            return Parse(document.RootElement, sourceName, cancellationToken);
        }
        catch (JsonException exception)
        {
            return ScoreImportResult.Failed(Error("invalid_json", $"Invalid Sky Studio JSON: {exception.Message}"));
        }
        catch (DecoderFallbackException exception)
        {
            return ScoreImportResult.Failed(Error("unsupported_encoding", $"Unsupported text encoding: {exception.Message}"));
        }
        catch (IOException exception)
        {
            return ScoreImportResult.Failed(Error("read_failed", $"Unable to read score: {exception.Message}"));
        }
    }

    // 兼容普通谱与加密谱的根节点结构
    private ScoreImportResult Parse(JsonElement root, string sourceName, CancellationToken cancellationToken)
    {
        var sheet = root.ValueKind switch
        {
            JsonValueKind.Array when root.GetArrayLength() > 0 => root[0],
            JsonValueKind.Object => root,
            _ => default
        };

        if (sheet.ValueKind != JsonValueKind.Object)
        {
            return ScoreImportResult.Failed(Error("invalid_root", "Sky Studio score must be an object or a non-empty array"));
        }

        if (!sheet.TryGetProperty("songNotes", out var songNotes) || songNotes.ValueKind != JsonValueKind.Array)
        {
            return ScoreImportResult.Failed(Error("missing_notes", "Sky Studio score does not contain songNotes"));
        }

        var wasEncrypted = TryGetBoolean(sheet, "isEncrypted", out var encrypted) && encrypted;
        if (wasEncrypted)
        {
            try
            {
                songNotes = _encryptedNotesDecoder.Decode(songNotes);
            }
            catch (InvalidDataException exception)
            {
                return ScoreImportResult.Failed(Error("invalid_encrypted_score", exception.Message));
            }
        }

        var issues = new List<ScoreImportIssue>();
        var parsedNotes = ParseNotes(songNotes, issues, cancellationToken);
        if (parsedNotes.Count == 0)
        {
            issues.Add(Error("empty_score", "No playable notes were found"));
            return new ScoreImportResult(null, issues);
        }

        var pitchLevel = GetInt64(sheet, "pitchLevel") is { } level ? checked((int)level) : 0;
        var notes = BuildNotes(parsedNotes, pitchLevel, issues);
        if (notes.Count == 0)
        {
            issues.Add(Error("empty_piano_range", "No notes are inside the 88-key piano range"));
            return new ScoreImportResult(null, issues);
        }

        var title = GetString(sheet, "name") ?? Path.GetFileNameWithoutExtension(sourceName);
        var composer = GetString(sheet, "author") ?? GetString(sheet, "transcribedBy") ?? string.Empty;
        var metadata = ReadMetadata(sheet);
        metadata["sourceFormat"] = "sky-studio";
        metadata["sourceName"] = sourceName;
        metadata["wasEncrypted"] = wasEncrypted.ToString(CultureInfo.InvariantCulture).ToLowerInvariant();

        return new ScoreImportResult(new Score(title, composer, notes, metadata), issues);
    }

    private static List<ParsedNote> ParseNotes(
        JsonElement songNotes,
        ICollection<ScoreImportIssue> issues,
        CancellationToken cancellationToken)
    {
        var notes = new List<ParsedNote>(songNotes.GetArrayLength());
        var index = 0;

        foreach (var item in songNotes.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var time = GetInt64(item, "time");
            var key = GetString(item, "key");
            var match = key is null ? null : KeyPattern().Match(key);

            if (time is null || time < 0 || match is null || !match.Success ||
                !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var keyIndex))
            {
                issues.Add(Warning("invalid_note", "Skipped a note with an invalid time or key", index));
                index++;
                continue;
            }

            var duration = GetInt64(item, "duration");
            notes.Add(new ParsedNote(time.Value, keyIndex, duration, index));
            index++;
        }

        notes.Sort(static (left, right) =>
        {
            var result = left.TimeMilliseconds.CompareTo(right.TimeMilliseconds);
            return result != 0 ? result : left.SourceIndex.CompareTo(right.SourceIndex);
        });
        return notes;
    }

    // 将游戏键名和相邻时间转换为标准音符事件
    private IReadOnlyList<NoteEvent> BuildNotes(
        IReadOnlyList<ParsedNote> parsedNotes,
        int pitchLevel,
        ICollection<ScoreImportIssue> issues)
    {
        var distinctTimes = parsedNotes.Select(note => note.TimeMilliseconds).Distinct().ToArray();
        var nextTimeByTime = distinctTimes
            .Select((time, index) => new
            {
                Time = time,
                Next = index + 1 < distinctTimes.Length ? distinctTimes[index + 1] : (long?)null
            })
            .ToDictionary(item => item.Time, item => item.Next);
        var notes = new List<NoteEvent>(parsedNotes.Count);

        foreach (var parsed in parsedNotes)
        {
            if (!SkyStudioKeyMapper.TryMap(parsed.KeyIndex, pitchLevel, _options.ApplyPitchLevel, out var midiNote))
            {
                issues.Add(Warning("note_out_of_range", "Skipped a note outside the 88-key piano range", parsed.SourceIndex));
                continue;
            }

            var durationMilliseconds = ResolveDuration(parsed, nextTimeByTime[parsed.TimeMilliseconds]);
            notes.Add(new NoteEvent(
                midiNote,
                checked(parsed.TimeMilliseconds * 1_000),
                checked(durationMilliseconds * 1_000)));
        }

        return notes
            .Distinct()
            .OrderBy(note => note.StartMicroseconds)
            .ThenBy(note => note.MidiNote)
            .ToArray();
    }

    private long ResolveDuration(ParsedNote note, long? nextTime)
    {
        if (note.DurationMilliseconds is > 0)
        {
            return Math.Max(_options.MinimumDurationMilliseconds, note.DurationMilliseconds.Value);
        }

        var duration = _options.DefaultDurationMilliseconds;
        if (nextTime is not null)
        {
            duration = (int)Math.Min(duration, Math.Max(_options.MinimumDurationMilliseconds, nextTime.Value - note.TimeMilliseconds));
        }

        return duration;
    }

    private static Dictionary<string, string> ReadMetadata(JsonElement sheet)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in sheet.EnumerateObject())
        {
            if (property.NameEquals("songNotes") || property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                continue;
            }

            metadata[property.Name] = property.Value.ToString();
        }

        return metadata;
    }

    private static long? GetInt64(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryGetBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static ScoreImportIssue Warning(string code, string message, int? index = null) =>
        new(code, message, ScoreImportIssueSeverity.Warning, index);

    private static ScoreImportIssue Error(string code, string message) =>
        new(code, message, ScoreImportIssueSeverity.Error);

    [GeneratedRegex(@"Key(-?\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex KeyPattern();

    private readonly record struct ParsedNote(
        long TimeMilliseconds,
        int KeyIndex,
        long? DurationMilliseconds,
        int SourceIndex);
}
