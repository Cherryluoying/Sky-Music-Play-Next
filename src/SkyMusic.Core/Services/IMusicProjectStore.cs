// 模块：SkyMusic.Core 桌面服务 IMusicProjectStore
using SkyMusic.Core.Projects;

namespace SkyMusic.Core.Services;

public interface IMusicProjectStore
{
    ValueTask<MusicProject> LoadAsync(Stream source, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(MusicProject project, Stream destination, CancellationToken cancellationToken = default);
}
