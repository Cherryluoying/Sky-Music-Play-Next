// 模块：SkyMusic.Core 应用设置 AppSettings
using SkyMusic.Core.Execution;

namespace SkyMusic.Core.Settings;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;

    public GeneralSettings General { get; init; } = new();

    public ExternalToolSettings ExternalTools { get; init; } = new();

    public AudioPluginSettings AudioPlugins { get; init; } = new();

    public PerformanceSettings Performance { get; init; } = new();

    public NetworkSettings Network { get; init; } = new();

    public StorageSettings Storage { get; init; } = new();
}

public sealed record GeneralSettings
{
    public string DefaultPlaybackTargetId { get; init; } = "sky-15";

    public bool RememberLastTarget { get; init; } = true;

    // 悬浮演奏入口默认关闭，用户可在设置中开启并记住选择。
    public bool FloatingWindowEnabled { get; init; }
}

public sealed record ExternalToolSettings
{
    public string? FfmpegPath { get; init; }

    public string? PianoTransPath { get; init; }
}

public sealed record AudioPluginSettings
{
    public IReadOnlyList<string> Vst3SearchPaths { get; init; } = [];

    public int BufferSize { get; init; } = 512;
}

public sealed record PerformanceSettings
{
    public WorkloadExecutionMode Mode { get; init; } = WorkloadExecutionMode.Automatic;

    public int MaximumConcurrency { get; init; } = Math.Max(1, Environment.ProcessorCount - 1);
}

public sealed record NetworkSettings
{
    public string CloudServiceUrl { get; init; } = "http://127.0.0.1:8787/";

    public int TimeoutSeconds { get; init; } = 3;
}

public sealed record StorageSettings
{
    public string? CacheDirectory { get; init; }

    public string? ScoreLibraryDirectory { get; init; }

    public string? MidiLibraryDirectory { get; init; }
}
