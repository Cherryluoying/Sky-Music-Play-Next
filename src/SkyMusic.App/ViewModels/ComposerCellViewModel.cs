// 模块：SkyMusic.App 界面状态 ComposerCellViewModel
namespace SkyMusic.App.ViewModels;

public sealed class ComposerCellViewModel
{
    public ComposerCellViewModel(int keyIndex, bool isActive, bool hasOtherLayer, Action<int> toggle)
    {
        KeyIndex = keyIndex;
        IsActive = isActive;
        HasOtherLayer = hasOtherLayer;
        ToggleCommand = new RelayCommand(_ => toggle(keyIndex));
    }

    public int KeyIndex { get; }
    public bool IsActive { get; }
    public bool HasOtherLayer { get; }
    public string Label => (KeyIndex + 1).ToString();
    public RelayCommand ToggleCommand { get; }
}
