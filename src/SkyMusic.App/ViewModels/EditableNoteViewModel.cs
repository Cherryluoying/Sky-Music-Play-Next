// 模块：SkyMusic.App 界面状态 EditableNoteViewModel
namespace SkyMusic.App.ViewModels;

public sealed class EditableNoteViewModel : ObservableObject
{
    private int _midiNote;
    private long _startMilliseconds;
    private long _durationMilliseconds;
    private int _velocity;

    public EditableNoteViewModel(int midiNote, long startMilliseconds, long durationMilliseconds, int velocity)
    {
        _midiNote = midiNote;
        _startMilliseconds = startMilliseconds;
        _durationMilliseconds = durationMilliseconds;
        _velocity = velocity;
    }

    public event Action<EditableNoteViewModel, string, long>? ValueChanging;

    public int MidiNote
    {
        get => _midiNote;
        set
        {
            var next = Math.Clamp(value, 21, 108);
            if (next == _midiNote)
            {
                return;
            }
            ValueChanging?.Invoke(this, nameof(MidiNote), _midiNote);
            SetProperty(ref _midiNote, next);
            OnPropertyChanged(nameof(NoteName));
        }
    }

    public long StartMilliseconds
    {
        get => _startMilliseconds;
        set
        {
            var next = Math.Max(0, value);
            if (next == _startMilliseconds)
            {
                return;
            }
            ValueChanging?.Invoke(this, nameof(StartMilliseconds), _startMilliseconds);
            SetProperty(ref _startMilliseconds, next);
        }
    }

    public long DurationMilliseconds
    {
        get => _durationMilliseconds;
        set
        {
            var next = Math.Max(10, value);
            if (next == _durationMilliseconds)
            {
                return;
            }
            ValueChanging?.Invoke(this, nameof(DurationMilliseconds), _durationMilliseconds);
            SetProperty(ref _durationMilliseconds, next);
        }
    }

    public int Velocity
    {
        get => _velocity;
        set
        {
            var next = Math.Clamp(value, 1, 127);
            if (next == _velocity)
            {
                return;
            }
            ValueChanging?.Invoke(this, nameof(Velocity), _velocity);
            SetProperty(ref _velocity, next);
        }
    }

    public string NoteName
    {
        get
        {
            string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
            return $"{names[MidiNote % 12]}{(MidiNote / 12) - 1}";
        }
    }
}
