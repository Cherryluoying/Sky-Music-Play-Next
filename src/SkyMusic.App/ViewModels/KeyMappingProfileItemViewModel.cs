// 模块：SkyMusic.App 界面状态 KeyMappingProfileItemViewModel
using SkyMusic.Core.Mapping;

namespace SkyMusic.App.ViewModels;

public sealed record KeyMappingProfileItemViewModel(KeyMappingDefinition Definition)
{
    public string Name => Definition.Name;
}
