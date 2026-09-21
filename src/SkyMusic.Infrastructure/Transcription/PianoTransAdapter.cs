// 模块：SkyMusic.Infrastructure 扒谱领域 PianoTransAdapter
using System.Diagnostics;
using SkyMusic.Core.Services;
using SkyMusic.Core.Transcription;

namespace SkyMusic.Infrastructure.Transcription;

public sealed class PianoTransAdapter : ITranscriptionAdapter
{
    private readonly PianoTransOptions _options;

    public PianoTransAdapter(PianoTransOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => "PianoTrans 1.0";

    public string ExecutablePath => Path.GetFullPath(Path.Combine(_options.PackageDirectory, _options.ExecutableName));

    public bool IsAvailable => OperatingSystem.IsWindows() && File.Exists(ExecutablePath);

    // 调用外部 PianoTrans 并收集本次生成的 MIDI
    public async Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        IProgress<TranscriptionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("PianoTrans extension is only available on Windows");
        }

        var sourcePath = Path.GetFullPath(request.SourcePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Audio source was not found", sourcePath);
        }

        if (!File.Exists(ExecutablePath))
        {
            throw new FileNotFoundException("PianoTrans extension package was not found", ExecutablePath);
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath)!;
        // 对比运行前后快照以兼容扩展包的输出命名策略
        var before = CaptureMidiFiles(sourceDirectory);
        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        progress?.Report(new TranscriptionProgress("starting", "正在启动 PianoTrans"));

        using var process = new Process
        {
            StartInfo = CreateStartInfo(sourcePath, request.Mode),
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("PianoTrans process could not be started");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = new CancellationTokenSource(_options.EffectiveTimeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            progress?.Report(new TranscriptionProgress("running", "PianoTrans 正在分析音频"));
            await process.WaitForExitAsync(linkedCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"PianoTrans exceeded the {_options.EffectiveTimeout.TotalMinutes:0.#} minute timeout");
            }

            throw;
        }

        var output = await standardOutput;
        var error = await standardError;
        var diagnostic = string.Join(Environment.NewLine, new[] { output.Trim(), error.Trim() }
            .Where(value => !string.IsNullOrEmpty(value)));
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(diagnostic)
                    ? $"PianoTrans exited with code {process.ExitCode}"
                    : $"PianoTrans exited with code {process.ExitCode}: {diagnostic}");
        }

        var midiPath = FindOutputMidi(sourcePath, before, startedAt)
            ?? throw new InvalidDataException("PianoTrans completed but no generated MIDI file was found");
        progress?.Report(new TranscriptionProgress("completed", "音频转 MIDI 已完成"));
        return new TranscriptionResult(midiPath, stopwatch.Elapsed, diagnostic);
    }

    // 构建无 Shell 注入风险的外部进程参数
    private ProcessStartInfo CreateStartInfo(string sourcePath, TranscriptionMode mode)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            WorkingDirectory = Path.GetFullPath(_options.PackageDirectory),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(sourcePath);
        if (mode == TranscriptionMode.CpuOnly)
        {
            startInfo.Environment["CUDA_VISIBLE_DEVICES"] = "-1";
        }

        return startInfo;
    }

    // 记录运行前文件状态以识别新输出
    private static Dictionary<string, FileStamp> CaptureMidiFiles(string directory) =>
        EnumerateMidiFiles(directory).ToDictionary(
            path => path,
            path => new FileInfo(path) is var file ? new FileStamp(file.Length, file.LastWriteTimeUtc) : default,
            StringComparer.OrdinalIgnoreCase);

    private static string? FindOutputMidi(
        string sourcePath,
        IReadOnlyDictionary<string, FileStamp> before,
        DateTime startedAt)
    {
        var expected = Path.ChangeExtension(sourcePath, ".mid");
        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        return EnumerateMidiFiles(Path.GetDirectoryName(sourcePath)!)
            .Select(path => new FileInfo(path))
            .Where(file => !before.TryGetValue(file.FullName, out var stamp) ||
                           stamp.Length != file.Length || stamp.LastWriteTimeUtc != file.LastWriteTimeUtc)
            .OrderByDescending(file => string.Equals(file.FullName, expected, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(file => file.Name.StartsWith(sourceName, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(file => file.LastWriteTimeUtc >= startedAt.AddSeconds(-2))
            .ThenByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateMidiFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path) is ".mid" or ".midi" ||
                           string.Equals(Path.GetExtension(path), ".MID", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(Path.GetExtension(path), ".MIDI", StringComparison.OrdinalIgnoreCase));

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                process.WaitForExit();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private readonly record struct FileStamp(long Length, DateTime LastWriteTimeUtc);
}
