// 模块：SkyMusic.App 专业工作区轨道状态
using SkyMusic.Core.Projects;

namespace SkyMusic.App.ViewModels;

public sealed class ProfessionalTrackViewModel
{
    public ProfessionalTrackViewModel(ProjectTrack track, int index)
    {
        Track = track;
        Number = index + 1;
    }

    internal ProjectTrack Track { get; }

    public Guid Id => Track.Id;
    public int Number { get; }
    public string Name => Track.Name;
    public string Color => Track.Color;
    public int NoteCount => Track.Notes.Count;
    public string Detail => Track.Kind == ProjectTrackKind.Percussion
        ? $"鼓组 · {NoteCount} 音符"
        : $"乐器 · {NoteCount} 音符";
    public bool IsMuted => Track.IsMuted;
    public bool IsSolo => Track.IsSolo;
}
