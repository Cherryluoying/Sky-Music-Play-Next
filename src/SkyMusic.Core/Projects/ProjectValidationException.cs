// 模块：SkyMusic.Core 工程领域 ProjectValidationException
namespace SkyMusic.Core.Projects;

public sealed class ProjectValidationException(string message) : Exception(message);
