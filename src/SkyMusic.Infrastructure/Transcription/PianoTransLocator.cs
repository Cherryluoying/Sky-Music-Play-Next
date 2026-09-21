// 模块：SkyMusic.Infrastructure 扒谱领域 PianoTransLocator
namespace SkyMusic.Infrastructure.Transcription;

public static class PianoTransLocator
{
    public static string LocatePackage(string startDirectory, string? preferredPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);
        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            return Path.GetFullPath(preferredPath);
        }

        var configured = Environment.GetEnvironmentVariable("SKYMUSIC_PIANOTRANS_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        for (var depth = 0; depth < 10 && directory is not null; depth++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "PianoTrans-v1.0");
            if (File.Exists(Path.Combine(candidate, "PianoTrans.exe")))
            {
                return candidate;
            }
        }

        return Path.Combine(Path.GetFullPath(startDirectory), "PianoTrans-v1.0");
    }
}
