// 模块：SkyMusic.App 界面状态 WorkbenchWindowViewModel
using System.Collections.ObjectModel;
using SkyMusic.Core.GameScores;
using SkyMusic.Core.Projects;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.GameScores;
using SkyMusic.Infrastructure.Assets;
using SkyMusic.Infrastructure.Audio;
using SkyMusic.Infrastructure.Projects;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.App.ViewModels;

public sealed class WorkbenchWindowViewModel : ObservableObject, IDisposable
{
    private readonly IMusicProjectStore _projectStore;
    private readonly IGameScoreStore _gameScoreStore;
    private MusicProject _project;
    // 游戏编谱是工作台的默认入口，专业半 DAW 通过顶部模式按钮进入。
    private WorkbenchMode _mode = WorkbenchMode.GameComposer;
    private string _statusText = "就绪";

    public WorkbenchWindowViewModel(
        IMusicProjectStore? projectStore = null,
        IGameScoreStore? gameScoreStore = null,
        IScorePlaybackController? playbackController = null,
        IInstrumentAssetCatalog? instrumentAssets = null,
        IInstrumentPreviewService? instrumentPreview = null)
    {
        _projectStore = projectStore ?? new JsonMusicProjectStore();
        _gameScoreStore = gameScoreStore ?? new GenshinMusicGameScoreStore();
        _project = CreateBlankProject();
        var assets = instrumentAssets ?? new GenshinMusicAssetCatalog();
        Composer = new GameComposerViewModel(
            CurrentGameScore(),
            UpdateGameScore,
            playbackController,
            assets,
            instrumentPreview ?? new NativeInstrumentPreviewService(assets));
        Professional = new ProfessionalWorkspaceViewModel(_project, UpdateProfessionalProject, playbackController);
        SwitchToGameComposerCommand = new RelayCommand(_ => Mode = WorkbenchMode.GameComposer);
        SwitchToProfessionalCommand = new RelayCommand(_ => Mode = WorkbenchMode.Professional);
        NewProjectCommand = new RelayCommand(_ => NewProject());
        AddTrackCommand = new RelayCommand(_ => Professional.AddTrackCommand.Execute(null));
        RefreshProjectState();
    }

    public ObservableCollection<WorkbenchTrackViewModel> Tracks { get; } = [];
    public GameComposerViewModel Composer { get; }
    public ProfessionalWorkspaceViewModel Professional { get; }
    public string ProjectTitle => _project.Metadata.Title;
    public string ProjectSummary => $"{_project.Ppq} PPQ · {_project.Tracks.Count} 轨道 · {Composer.Document.Columns.Count} 列";

    public WorkbenchMode Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsGameComposer));
                OnPropertyChanged(nameof(IsProfessional));
            }
        }
    }

    public bool IsGameComposer => Mode == WorkbenchMode.GameComposer;
    public bool IsProfessional => Mode == WorkbenchMode.Professional;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public RelayCommand SwitchToGameComposerCommand { get; }
    public RelayCommand SwitchToProfessionalCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand AddTrackCommand { get; }

    // 载入工程并恢复轨道与游戏谱工作区状态
    public async ValueTask LoadAsync(Stream source, string sourceName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(sourceName);
        if (extension.Equals(".skymusicproj", StringComparison.OrdinalIgnoreCase))
        {
            _project = await _projectStore.LoadAsync(source, cancellationToken);
            Composer.LoadDocument(CurrentGameScore(), $"已打开 {sourceName}");
        }
        else if (extension.Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
                 extension.Equals(".midi", StringComparison.OrdinalIgnoreCase))
        {
            var result = await new MidiScoreImporter().ImportAsync(source, sourceName, cancellationToken);
            if (!result.IsSuccess || result.Score is null)
                throw new InvalidDataException(string.Join("; ", result.Issues.Select(issue => issue.Message)));
            _project = new MusicProjectScoreConverter().FromScore(result.Score);
            Composer.LoadMidiScore(result.Score);
            SetProjectTitle(result.Score.Title);
        }
        else
        {
            var document = await _gameScoreStore.LoadAsync(source, cancellationToken);
            Composer.LoadDocument(document, $"已导入 {sourceName}");
            UpdateGameScore(document);
            SetProjectTitle(document.Name);
        }
        StatusText = Composer.StatusText;
        RefreshProjectState();
        Professional.LoadProject(_project, StatusText);
    }

    // 将当前工作区快照保存为工程文件
    public async ValueTask SaveAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        _project = _project with { Metadata = _project.Metadata with { ModifiedAt = now } };
        await _projectStore.SaveAsync(_project, destination, cancellationToken);
        StatusText = "工程已保存";
        OnPropertyChanged(nameof(ProjectSummary));
    }

    public async ValueTask ExportGameScoreAsync(
        Stream destination,
        bool legacy,
        CancellationToken cancellationToken = default)
    {
        if (legacy)
            await _gameScoreStore.SaveLegacyAsync(Composer.Document, destination, cancellationToken);
        else
            await _gameScoreStore.SaveAsync(Composer.Document, destination, cancellationToken);
        StatusText = legacy ? "旧版兼容乐谱已导出" : "乐谱已导出";
    }

    public void ExportMidi(Stream destination)
    {
        var score = Mode == WorkbenchMode.Professional
            ? new MusicProjectScoreConverter().ToScore(_project)
            : new GameScorePlaybackConverter().ToScore(Composer.Document, 0);
        new MidiScoreExporter().Export(score, destination);
        StatusText = "MIDI 已导出";
    }

    private void NewProject()
    {
        _project = CreateBlankProject();
        Composer.LoadDocument(CurrentGameScore(), "已创建新工程");
        Professional.LoadProject(_project, "已创建新工程");
        StatusText = "已创建新工程";
        RefreshProjectState();
    }

    // 把编谱文档回写到统一工程编排
    private void UpdateGameScore(GameScoreDocument document)
    {
        var arrangement = _project.GameArrangements.FirstOrDefault();
        var updated = arrangement is null
            ? CreateArrangement(document)
            : arrangement with
            {
                Name = document.Profile.ToString(),
                InstrumentProfileId = document.Profile.ToString(),
                GameScore = document
            };
        _project = _project with { GameArrangements = [updated] };
        OnPropertyChanged(nameof(ProjectSummary));
    }

    private void UpdateProfessionalProject(MusicProject project)
    {
        _project = project;
        StatusText = Professional.StatusText;
        RefreshProjectState();
    }

    private void SetProjectTitle(string title)
    {
        _project = _project with
        {
            Metadata = _project.Metadata with { Title = title, ModifiedAt = DateTimeOffset.UtcNow }
        };
        OnPropertyChanged(nameof(ProjectTitle));
    }

    private GameScoreDocument CurrentGameScore()
        => _project.GameArrangements.FirstOrDefault()?.GameScore
           ?? GameScoreDocument.Create(_project.Metadata.Title);

    // 从工程模型刷新所有可观察界面状态
    private void RefreshProjectState()
    {
        RefreshTracks();
        OnPropertyChanged(nameof(ProjectTitle));
        OnPropertyChanged(nameof(ProjectSummary));
    }

    private void RefreshTracks()
    {
        Tracks.Clear();
        for (var index = 0; index < _project.Tracks.Count; index++)
            Tracks.Add(new WorkbenchTrackViewModel(_project.Tracks[index], index));
        OnPropertyChanged(nameof(ProjectSummary));
    }

    private static MusicProject CreateBlankProject()
    {
        var project = MusicProject.Create("未命名工程");
        var piano = new ProjectTrack(
            Guid.NewGuid(),
            "Piano",
            ProjectTrackKind.Instrument,
            "#3B82F6",
            [],
            "builtin:piano");
        var score = GameScoreDocument.Create(project.Metadata.Title);
        return project with { Tracks = [piano], GameArrangements = [CreateArrangement(score)] };
    }

    private static GameArrangement CreateArrangement(GameScoreDocument document)
        => new(
            Guid.NewGuid(),
            document.Profile.ToString(),
            document.Profile.ToString(),
            0,
            OutOfRangePolicy.Keep,
            [],
            new Dictionary<Guid, int>(),
            GameScore: document);

    public void Dispose()
    {
        Composer.Dispose();
        Professional.Dispose();
    }
}
