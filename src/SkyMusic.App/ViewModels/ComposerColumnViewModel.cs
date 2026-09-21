// 模块：SkyMusic.App 界面状态 ComposerColumnViewModel
using System.Collections.ObjectModel;
using SkyMusic.Core.GameScores;

namespace SkyMusic.App.ViewModels;

public sealed class ComposerColumnViewModel : ObservableObject
{
    private bool _isSelected;

    public ComposerColumnViewModel(
        int index,
        GameScoreColumn column,
        IReadOnlyList<int> displayOrder,
        int selectedLayer,
        bool isBreakpoint,
        bool isSelected,
        Action<int> select,
        Action<int, int> toggle)
    {
        Index = index;
        IsBreakpoint = isBreakpoint;
        _isSelected = isSelected;
        SelectCommand = new RelayCommand(_ => select(index));
        TempoLabel = column.TempoStep switch
        {
            1 => "1/2",
            2 => "1/4",
            3 => "1/8",
            _ => "1"
        };
        foreach (var key in displayOrder)
        {
            var note = column.Notes.FirstOrDefault(item => item.KeyIndex == key);
            Cells.Add(new ComposerCellViewModel(
                key,
                note?.HasLayer(selectedLayer) == true,
                note is not null && (note.LayerMask & ~(1UL << selectedLayer)) != 0,
                selectedKey => toggle(index, selectedKey)));
        }
    }

    public ObservableCollection<ComposerCellViewModel> Cells { get; } = [];
    public int Index { get; }
    public string Number => (Index + 1).ToString();
    public string TempoLabel { get; }
    public bool IsBreakpoint { get; }
    public RelayCommand SelectCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
