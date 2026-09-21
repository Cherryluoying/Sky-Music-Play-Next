// 模块：SkyMusic.Core 桌面服务 IAppSettingsStore
using SkyMusic.Core.Settings;

namespace SkyMusic.Core.Services;

public interface IAppSettingsStore
{
    ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
