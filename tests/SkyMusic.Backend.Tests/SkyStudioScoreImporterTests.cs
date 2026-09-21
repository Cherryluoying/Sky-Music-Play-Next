// 模块：SkyMusic.Backend.Tests 后端测试 SkyStudioScoreImporterTests
using System.Text;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.Backend.Tests;

public sealed class SkyStudioScoreImporterTests
{
    [Fact]
    public async Task ImportsLegacyKeysIntoUnifiedMidiRange()
    {
        const string json = """
            [{
              "name": "兼容测试",
              "author": "Sky",
              "bpm": 120,
              "pitchLevel": 0,
              "isEncrypted": false,
              "songNotes": [
                { "time": 0, "key": "1Key-14", "duration": 50 },
                { "time": 100, "key": "2Key0" },
                { "time": 100, "key": "2Key14" },
                { "time": "200", "key": "1Key28", "duration": "120" }
              ]
            }]
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await new SkyStudioScoreImporter().ImportAsync(stream, "legacy.txt");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Score);
        Assert.Equal("兼容测试", result.Score.Title);
        Assert.Equal("Sky", result.Score.Composer);
        Assert.Equal([36, 60, 84, 108], result.Score.Notes.Select(note => note.MidiNote));
        Assert.Equal(80_000, result.Score.Notes[1].DurationMicroseconds);
        Assert.Equal("sky-studio", result.Score.Metadata!["sourceFormat"]);
    }

    [Fact]
    public async Task ImportsEncryptedScores()
    {
        const string notes = """[{"time":0,"key":"Key0","duration":90}]""";
        var json = $$"""[{"name":"Encrypted","isEncrypted":true,"songNotes":{{Encrypt(notes)}}}]""";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await new SkyStudioScoreImporter().ImportAsync(stream, "encrypted.txt");

        Assert.True(result.IsSuccess);
        Assert.Equal(60, Assert.Single(result.Score!.Notes).MidiNote);
        Assert.Equal("true", result.Score.Metadata!["wasEncrypted"]);
    }

    [Fact]
    public async Task RejectsEncryptedScoresWithMissingSignature()
    {
        var json = $$"""[{"isEncrypted":true,"songNotes":{{Encrypt("[]", includeSignature: false)}}}]""";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await new SkyStudioScoreImporter().ImportAsync(stream, "encrypted.txt");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid_encrypted_score");
    }

    private static string Encrypt(string json, bool includeSignature = true)
    {
        const string key = "TB,R&Q}-ULFXF7={nU7v?fy#Khr9Mhuu";
        const string signature = "ztB_kaFeQe/wa8Kq{r_jz!r=P])hQL(f";
        var value = includeSignature ? json + signature : json;
        return "[" + string.Join(',', value.Select((character, index) =>
            character + key[index % key.Length] - 100)) + "]";
    }
}
