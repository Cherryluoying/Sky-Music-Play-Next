// 模块：SkyMusic.Core 桌面服务 IKeyMappingStore
using SkyMusic.Core.Mapping;

namespace SkyMusic.Core.Services;

public interface IKeyMappingStore
{
    ValueTask<IReadOnlyList<KeyMappingDefinition>> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(KeyMappingDefinition mapping, CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(string id, CancellationToken cancellationToken = default);
}
