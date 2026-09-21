// 模块：SkyMusic.Core 工程领域 ProjectMetadata
namespace SkyMusic.Core.Projects;

public sealed record ProjectMetadata(
    string Title,
    string Author,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt);
