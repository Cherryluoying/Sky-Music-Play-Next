// 模块：SkyMusic.Backend.Tests 后端测试 PianoTransAdapterTests
using SkyMusic.Core.Transcription;
using SkyMusic.Infrastructure.Transcription;

namespace SkyMusic.Backend.Tests;

public sealed class PianoTransAdapterTests
{
    [Fact]
    public async Task ReportsMissingExtensionPackage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(directory, "piano.wav");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(source, [0]);
        try
        {
            var adapter = new PianoTransAdapter(new PianoTransOptions(directory));

            Assert.False(adapter.IsAvailable);
            var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
                adapter.TranscribeAsync(new TranscriptionRequest(source)));
            Assert.EndsWith("PianoTrans.exe", exception.FileName, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
