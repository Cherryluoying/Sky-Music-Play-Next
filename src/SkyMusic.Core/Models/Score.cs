// 模块：SkyMusic.Core 界面模型 Score
namespace SkyMusic.Core.Models;

public sealed record Score(
    string Title,
    string Composer,
    IReadOnlyList<NoteEvent> Notes,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public long DurationMicroseconds => Notes.Count == 0 ? 0 : Notes.Max(note => note.EndMicroseconds);
}
