// 模块：SkyMusic.App 界面状态 NavigationItemViewModel
using SkyMusic.App.Models;

namespace SkyMusic.App.ViewModels;

public sealed class NavigationItemViewModel(string label, string glyph, AppPage page) : ObservableObject
{
    private bool _isSelected;

    public string Label { get; } = label;

    public string Glyph { get; } = glyph;

    public AppPage Page { get; } = page;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
