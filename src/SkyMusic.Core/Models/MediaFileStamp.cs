// 模块：目录扫描索引；文件大小和修改时间未变时复用已解析信息，避免启动时重读上万份乐谱。
namespace SkyMusic.Core.Models;

public sealed record MediaFileStamp(string Path, long Length, long ModifiedTicks, string TrackId);
