// 模块：SkyMusic.App 界面状态 TranscriptionViewModel
using SkyMusic.Core.Services;
using SkyMusic.Core.Transcription;

namespace SkyMusic.App.ViewModels;

public sealed class TranscriptionViewModel : ObservableObject, IDisposable
{
    private readonly ITranscriptionAdapter _adapter;
    private readonly Func<string, Task> _openMidi;
    private CancellationTokenSource? _runCancellation;
    private string? _sourcePath;
    private string? _midiPath;
    private string _statusText;
    private bool _cpuOnly;
    private bool _isRunning;

    public TranscriptionViewModel(ITranscriptionAdapter adapter, Func<string, Task> openMidi)
    {
        _adapter = adapter;
        _openMidi = openMidi;
        _statusText = adapter.IsAvailable
            ? "PianoTrans 扩展已就绪"
            : "未找到 PianoTrans-v1.0 扩展包";
        CancelCommand = new RelayCommand(_ => _runCancellation?.Cancel(), _ => IsRunning);
        OpenMidiCommand = new AsyncRelayCommand(
            _ => MidiPath is null ? Task.CompletedTask : _openMidi(MidiPath),
            _ => MidiPath is not null && !IsRunning,
            SetError);
    }

    public string Title => "音频转 MIDI";

    public string AdapterName => _adapter.Name;

    public bool IsAvailable => _adapter.IsAvailable;

    public RelayCommand CancelCommand { get; }

    public AsyncRelayCommand OpenMidiCommand { get; }

    public string SourceText => SourcePath is null ? "尚未选择音频文件" : Path.GetFileName(SourcePath);

    public string? SourcePath
    {
        get => _sourcePath;
        private set
        {
            if (SetProperty(ref _sourcePath, value))
            {
                OnPropertyChanged(nameof(SourceText));
            }
        }
    }

    public string? MidiPath
    {
        get => _midiPath;
        private set
        {
            if (SetProperty(ref _midiPath, value))
            {
                OnPropertyChanged(nameof(HasMidi));
                OpenMidiCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasMidi => MidiPath is not null;

    public bool CpuOnly
    {
        get => _cpuOnly;
        set => SetProperty(ref _cpuOnly, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                CancelCommand.NotifyCanExecuteChanged();
                OpenMidiCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    // 跟踪外部扒谱进度并打开生成的 MIDI
    public async Task TranscribeAsync(string sourcePath)
    {
        if (IsRunning)
        {
            return;
        }

        SourcePath = sourcePath;
        MidiPath = null;
        IsRunning = true;
        _runCancellation = new CancellationTokenSource();
        var progress = new Progress<TranscriptionProgress>(value => StatusText = value.Message);
        try
        {
            var mode = CpuOnly ? TranscriptionMode.CpuOnly : TranscriptionMode.Automatic;
            var result = await _adapter.TranscribeAsync(
                new TranscriptionRequest(sourcePath, mode),
                progress,
                _runCancellation.Token);
            MidiPath = result.MidiPath;
            StatusText = $"已生成 {Path.GetFileName(result.MidiPath)} · {result.Elapsed.ToString(@"mm\:ss")}";
        }
        catch (OperationCanceledException) when (_runCancellation.IsCancellationRequested)
        {
            StatusText = "转写已取消";
        }
        catch (Exception exception)
        {
            SetError(exception);
        }
        finally
        {
            _runCancellation.Dispose();
            _runCancellation = null;
            IsRunning = false;
        }
    }

    public void Dispose()
    {
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
    }

    private void SetError(Exception exception) => StatusText = exception.Message;
}
