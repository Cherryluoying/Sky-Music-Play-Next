// 模块：SkyMusic.Core 外部工具状态 ExternalToolStatus
namespace SkyMusic.Core.ExternalTools;

public sealed record ExternalToolStatus(
    bool IsAvailable,
    string? ExecutablePath,
    string Message,
    string? Version = null,
    string? Source = null);
