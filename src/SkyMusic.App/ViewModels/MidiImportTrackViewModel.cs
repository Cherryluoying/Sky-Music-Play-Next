// 模块：SkyMusic.App 界面状态 MidiImportTrackViewModel
namespace SkyMusic.App.ViewModels;

public sealed class MidiImportTrackViewModel(int track, int noteCount) : ObservableObject
{
    private bool _isIncluded = true;
    private int _transpose;

    public int Track => track;
    public string Name => $"Track {track + 1}";
    public string Detail => $"{noteCount} 音符";

    public bool IsIncluded
    {
        get => _isIncluded;
        set => SetProperty(ref _isIncluded, value);
    }

    public int Transpose
    {
        get => _transpose;
        set => SetProperty(ref _transpose, Math.Clamp(value, -48, 48));
    }
}
