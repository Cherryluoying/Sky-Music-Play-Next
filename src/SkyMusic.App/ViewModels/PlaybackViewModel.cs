// 模块：SkyMusic.App 界面状态 PlaybackViewModel
using System.Collections.ObjectModel;
using Avalonia.Threading;
using SkyMusic.Core.Models;
using SkyMusic.Core.Lyrics;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.App.ViewModels;

public sealed class PlaybackViewModel : ObservableObject, IDisposable
{
    private readonly IPlaybackController _player;
    private readonly ILyricsProvider? _lyricsProvider;
    private TrackItemViewModel? _currentItem;
    private double _positionSeconds;
    private double _durationSeconds = 1;
    private bool _isPlaying;
    private bool _applyingSnapshot;
    private int _currentLyricIndex = -1;
    private bool _isDesktopLyricsVisible;
    private CancellationTokenSource? _lyricsRequest;

    public PlaybackViewModel(IPlaybackController player, ILyricsProvider? lyricsProvider = null)
    {
        _player = player;
        _lyricsProvider = lyricsProvider;
        _player.SnapshotChanged += OnSnapshotChanged;

        TogglePlayCommand = new RelayCommand(_ => TogglePlay());
        PreviousCommand = new RelayCommand(_ => MoveTrack(-1));
        NextCommand = new RelayCommand(_ => MoveTrack(1));
        OpenPlayerCommand = new RelayCommand(_ =>
        {
            if (HasTrack)
            {
                OpenPlayerRequested?.Invoke(this, EventArgs.Empty);
            }
        });
        ToggleDesktopLyricsCommand = new RelayCommand(_ =>
            SetDesktopLyricsVisible(!IsDesktopLyricsVisible));
    }

    public event EventHandler? OpenPlayerRequested;

    public event Action<bool>? DesktopLyricsVisibilityChanged;

    public ObservableCollection<TrackItemViewModel> Queue { get; } = [];

    public ObservableCollection<TrackItemViewModel> RecentTracks { get; } = [];

    public ObservableCollection<LyricLineViewModel> Lyrics { get; } = [];

    public RelayCommand TogglePlayCommand { get; }

    public RelayCommand PreviousCommand { get; }

    public RelayCommand NextCommand { get; }

    public RelayCommand OpenPlayerCommand { get; }

    public RelayCommand ToggleDesktopLyricsCommand { get; }

    public TrackItemViewModel? CurrentItem
    {
        get => _currentItem;
        private set
        {
            if (!SetProperty(ref _currentItem, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CurrentTrack));
            OnPropertyChanged(nameof(HasTrack));
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(CurrentDesktopLyric));
            OnPropertyChanged(nameof(NextDesktopLyric));
        }
    }

    public MusicTrack? CurrentTrack => CurrentItem?.Track;

    public bool HasTrack => CurrentItem is not null;

    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            if (!SetProperty(ref _positionSeconds, value))
            {
                return;
            }

            OnPropertyChanged(nameof(PositionText));
            if (!_applyingSnapshot)
            {
                _player.Seek(TimeSpan.FromSeconds(value));
            }
        }
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        private set => SetProperty(ref _durationSeconds, Math.Max(1, value));
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayGlyph));
            }
        }
    }

    public int CurrentLyricIndex
    {
        get => _currentLyricIndex;
        private set
        {
            if (SetProperty(ref _currentLyricIndex, value))
            {
                OnPropertyChanged(nameof(CurrentDesktopLyric));
                OnPropertyChanged(nameof(NextDesktopLyric));
            }
        }
    }

    public bool IsDesktopLyricsVisible
    {
        get => _isDesktopLyricsVisible;
        private set => SetProperty(ref _isDesktopLyricsVisible, value);
    }

    public string CurrentDesktopLyric => CurrentLyricIndex >= 0 && CurrentLyricIndex < Lyrics.Count
        ? Lyrics[CurrentLyricIndex].Text
        : CurrentItem?.Title ?? "SkyMusicPlay";

    public string NextDesktopLyric => CurrentLyricIndex + 1 >= 0 && CurrentLyricIndex + 1 < Lyrics.Count
        ? Lyrics[CurrentLyricIndex + 1].Text
        : CurrentItem?.Artist ?? string.Empty;

    public string PlayGlyph => IsPlaying ? "\uE769" : "\uE768";

    public string PositionText => TimeSpan.FromSeconds(PositionSeconds).ToString(@"mm\:ss");

    public string DurationText => CurrentTrack?.Duration.ToString(@"mm\:ss") ?? "00:00";

    // 替换播放队列并保持当前曲目索引
    public void SetQueue(IEnumerable<TrackItemViewModel> tracks)
    {
        Queue.Clear();
        foreach (var track in tracks)
        {
            Queue.Add(track);
        }

        if (CurrentItem is null && Queue.Count > 0)
        {
            LoadTrack(Queue[0], false);
        }
    }

    public void PlayTrack(TrackItemViewModel item, bool autoplay = true)
        => LoadTrack(item, autoplay);

    public void SetDesktopLyricsVisible(bool isVisible)
    {
        if (!SetProperty(ref _isDesktopLyricsVisible, isVisible, nameof(IsDesktopLyricsVisible)))
        {
            return;
        }

        DesktopLyricsVisibilityChanged?.Invoke(isVisible);
    }

    private void LoadTrack(TrackItemViewModel item, bool autoplay)
    {
        foreach (var track in Queue)
        {
            track.IsSelected = ReferenceEquals(track, item);
        }

        CurrentItem = item;
        Lyrics.Clear();
        foreach (var line in item.Track.Lyrics)
        {
            Lyrics.Add(new LyricLineViewModel(line));
        }

        CurrentLyricIndex = -1;
        AddRecent(item);
        _player.Load(item.Track, autoplay);
        LoadCloudLyrics(item.Track);
    }

    // 后台获取歌词并忽略已切换曲目的过期结果
    private async void LoadCloudLyrics(MusicTrack track)
    {
        if (_lyricsProvider is null)
        {
            return;
        }

        _lyricsRequest?.Cancel();
        _lyricsRequest?.Dispose();
        _lyricsRequest = new CancellationTokenSource();
        var cancellationToken = _lyricsRequest.Token;
        try
        {
            var result = await _lyricsProvider.GetLyricsAsync(
                new LyricsQuery(track.Title, track.Artist, track.Album, track.Duration),
                cancellationToken);
            if (result is null || result.Lines.Count == 0 || CurrentTrack?.Id != track.Id)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Lyrics.Clear();
                foreach (var line in result.Lines)
                {
                    Lyrics.Add(new LyricLineViewModel(line));
                }

                UpdateCurrentLyric(TimeSpan.FromSeconds(PositionSeconds));
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void AddRecent(TrackItemViewModel item)
    {
        var existing = RecentTracks.FirstOrDefault(track => track.Track.Id == item.Track.Id);
        if (existing is not null)
        {
            RecentTracks.Remove(existing);
        }

        RecentTracks.Insert(0, item);
    }

    private void TogglePlay()
    {
        if (_player.Snapshot.State == PlaybackState.Playing)
        {
            _player.Pause();
        }
        else
        {
            _player.Play();
        }
    }

    private void MoveTrack(int offset)
    {
        if (Queue.Count == 0)
        {
            return;
        }

        var currentIndex = CurrentItem is null ? 0 : Queue.IndexOf(CurrentItem);
        var nextIndex = (currentIndex + offset + Queue.Count) % Queue.Count;
        LoadTrack(Queue[nextIndex], true);
    }

    private void OnSnapshotChanged(object? sender, PlaybackSnapshot snapshot)
        => Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));

    // 把播放器快照同步到进度和播放状态
    private void ApplySnapshot(PlaybackSnapshot snapshot)
    {
        _applyingSnapshot = true;
        try
        {
            if (snapshot.Track is not null && CurrentTrack?.Id != snapshot.Track.Id)
            {
                var item = Queue.FirstOrDefault(track => track.Track.Id == snapshot.Track.Id);
                if (item is not null)
                {
                    LoadTrack(item, false);
                }
            }

            DurationSeconds = snapshot.Duration.TotalSeconds;
            PositionSeconds = snapshot.Position.TotalSeconds;
            IsPlaying = snapshot.State == PlaybackState.Playing;
            UpdateCurrentLyric(snapshot.Position);
        }
        finally
        {
            _applyingSnapshot = false;
        }
    }

    // 按播放位置选择当前歌词并更新高亮
    private void UpdateCurrentLyric(TimeSpan position)
    {
        var index = -1;
        for (var i = 0; i < Lyrics.Count; i++)
        {
            if (Lyrics[i].Line.Timestamp > position)
            {
                break;
            }

            index = i;
        }

        if (index == CurrentLyricIndex)
        {
            return;
        }

        CurrentLyricIndex = index;
        for (var i = 0; i < Lyrics.Count; i++)
        {
            Lyrics[i].IsCurrent = i == index;
        }
    }

    public void Dispose()
    {
        _lyricsRequest?.Cancel();
        _lyricsRequest?.Dispose();
        _player.SnapshotChanged -= OnSnapshotChanged;
    }
}
