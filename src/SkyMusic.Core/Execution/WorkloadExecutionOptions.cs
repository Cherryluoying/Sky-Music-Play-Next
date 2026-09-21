// 模块：SkyMusic.Core 执行策略 WorkloadExecutionOptions
namespace SkyMusic.Core.Execution;

public sealed record WorkloadExecutionOptions(
    WorkloadExecutionMode Mode = WorkloadExecutionMode.Automatic,
    int? MaximumConcurrency = null)
{
    public int ResolveConcurrency(int itemCount)
    {
        if (itemCount <= 1 || Mode == WorkloadExecutionMode.SingleThread)
        {
            return 1;
        }

        var processorLimit = Math.Max(1, Environment.ProcessorCount - 1);
        var requested = MaximumConcurrency is > 0 ? MaximumConcurrency.Value : processorLimit;
        return Math.Clamp(requested, 1, Math.Min(itemCount, processorLimit));
    }
}
