// 模块：SkyMusic.Core 工程领域 TimeSignatureChange
namespace SkyMusic.Core.Projects;

public readonly record struct TimeSignatureChange(long Tick, int Numerator, int Denominator);
