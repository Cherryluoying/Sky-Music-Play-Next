// 模块：SkyMusic.App 界面状态 KeyMappingProfileItemViewModel
using SkyMusic.Core.Mapping;

namespace SkyMusic.App.ViewModels;

public sealed record KeyMappingProfileItemViewModel(KeyMappingDefinition Definition)
{
    public string Name => Definition.Name;
    public string Description => IsBuiltIn
        ? "随程序提供的标准键位，不能删除；可直接用于自动演奏。"
        : "自定义扫描码方案，保存后立即刷新演奏目标。";
    public string ProcessNames => Definition.WindowProcessNames is { Count: > 0 } names
        ? string.Join(", ", names)
        : "手动选择窗口";
    public bool IsBuiltIn => Definition.Id.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase);
    public bool CanEdit => !IsBuiltIn;
}
