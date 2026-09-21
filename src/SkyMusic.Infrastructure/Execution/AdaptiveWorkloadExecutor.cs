// 模块：SkyMusic.Infrastructure 执行策略 AdaptiveWorkloadExecutor
using SkyMusic.Core.Execution;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Execution;

public sealed class AdaptiveWorkloadExecutor : IWorkloadExecutor
{
    // 根据配置选择顺序或有界并行执行路径
    public async ValueTask ForEachAsync<T>(
        IReadOnlyCollection<T> items,
        Func<T, CancellationToken, ValueTask> action,
        WorkloadExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        var concurrency = options.ResolveConcurrency(items.Count);
        if (concurrency == 1)
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action(item, cancellationToken);
            }
            return;
        }

        // 只并行解析和推理等独立任务，实时演奏仍由单调度线程负责
        await Parallel.ForEachAsync(
            items,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = concurrency
            },
            async (item, token) => await action(item, token));
    }
}
