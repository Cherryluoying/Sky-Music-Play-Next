// 模块：SkyMusic.Core 桌面服务 IWorkloadExecutor
using SkyMusic.Core.Execution;

namespace SkyMusic.Core.Services;

public interface IWorkloadExecutor
{
    ValueTask ForEachAsync<T>(
        IReadOnlyCollection<T> items,
        Func<T, CancellationToken, ValueTask> action,
        WorkloadExecutionOptions options,
        CancellationToken cancellationToken = default);
}
