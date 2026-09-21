// 模块：SkyMusic.Core 工程领域 ProjectMarker
namespace SkyMusic.Core.Projects;

public sealed record ProjectMarker(Guid Id, long Tick, string Name, string Color);
