// 模块：SkyMusic.Infrastructure 插件领域 Vst3ProcessHostOptions
namespace SkyMusic.Infrastructure.Plugins;

public sealed record Vst3ProcessHostOptions(string HostExecutablePath, TimeSpan? CommandTimeout = null)
{
    public TimeSpan EffectiveCommandTimeout => CommandTimeout ?? TimeSpan.FromSeconds(10);
}
