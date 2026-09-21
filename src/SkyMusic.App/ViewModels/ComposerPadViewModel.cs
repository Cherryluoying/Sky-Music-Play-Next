// 模块：SkyMusic.App 界面状态 ComposerPadViewModel
using SkyMusic.Core.GameScores;
using Avalonia.Media;
using SkyMusic.App.Services;

namespace SkyMusic.App.ViewModels;

public sealed class ComposerPadViewModel : ObservableObject
{
    private bool _isActive;

    public ComposerPadViewModel(int index, string label, GameScoreProfile profile)
    {
        Index = index;
        Label = label;
        Profile = profile;
        NoteName = NoteNames[GameKeyLayout.ToMidiNote(profile, index) % 12];
        Glyph = ComposerKeyGlyphCache.Get(profile, index);
        ToggleCommand = new RelayCommand(_ => IsActive = !IsActive);
    }

    private static readonly string[] NoteNames =
        ["C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B"];

    public int Index { get; }

    public string Label { get; }

    public string NoteName { get; }

    public Geometry? Glyph { get; }

    public GameScoreProfile Profile { get; }

    public bool IsSky => Profile == GameScoreProfile.Sky;

    public bool IsGenshin => Profile == GameScoreProfile.Genshin;

    public bool IsCustom => Profile == GameScoreProfile.Custom;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public RelayCommand ToggleCommand { get; }
}
