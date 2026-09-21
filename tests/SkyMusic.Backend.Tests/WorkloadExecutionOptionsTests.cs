// 模块：SkyMusic.Backend.Tests 后端测试 WorkloadExecutionOptionsTests
using SkyMusic.Core.Execution;

namespace SkyMusic.Backend.Tests;

public sealed class WorkloadExecutionOptionsTests
{
    [Fact]
    public void SingleThreadModeAlwaysUsesOneWorker()
    {
        var options = new WorkloadExecutionOptions(WorkloadExecutionMode.SingleThread, 16);
        Assert.Equal(1, options.ResolveConcurrency(100));
    }

    [Fact]
    public void ParallelModeNeverExceedsWorkOrAvailableProcessors()
    {
        var options = new WorkloadExecutionOptions(WorkloadExecutionMode.Parallel, int.MaxValue);
        var concurrency = options.ResolveConcurrency(3);
        Assert.InRange(concurrency, 1, Math.Min(3, Math.Max(1, Environment.ProcessorCount - 1)));
    }
}
