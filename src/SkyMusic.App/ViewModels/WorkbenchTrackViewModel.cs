// 模块：SkyMusic.App 界面状态 WorkbenchTrackViewModel
using SkyMusic.Core.Projects;

namespace SkyMusic.App.ViewModels;

public sealed class WorkbenchTrackViewModel(ProjectTrack track, int index)
{
    public Guid Id => track.Id;

    public string Name => track.Name;

    public string Color => track.Color;

    public string Detail => track.Kind == ProjectTrackKind.Percussion
        ? $"打击乐 · {track.Notes.Count} 音符"
        : $"乐器 · {track.Notes.Count} 音符";

    public int Number => index + 1;
}
