// 模块：SkyMusic.App 界面状态 KeyMappingEntryViewModel
using SkyMusic.App.Services;

namespace SkyMusic.App.ViewModels;

public sealed class KeyMappingEntryViewModel : ObservableObject
{
    private int _midiNote;
    private int _scanCode;
    private bool _isExtended;

    public KeyMappingEntryViewModel(int midiNote, int scanCode, bool isExtended)
    {
        _midiNote = midiNote;
        _scanCode = scanCode;
        _isExtended = isExtended;
    }

    public int MidiNote
    {
        get => _midiNote;
        set
        {
            if (SetProperty(ref _midiNote, Math.Clamp(value, 21, 108)))
            {
                OnPropertyChanged(nameof(NoteName));
            }
        }
    }

    public int ScanCode
    {
        get => _scanCode;
        set => SetProperty(ref _scanCode, Math.Clamp(value, 1, ushort.MaxValue));
    }

    public bool IsExtended
    {
        get => _isExtended;
        set => SetProperty(ref _isExtended, value);
    }

    // 未知历史键位仍保留原始值，打开/保存界面不会静默改成 A。
    public IEnumerable<KeyboardKey> KeyOptions => KeyboardKeyCatalog.Keys.Contains(SelectedKey)
        ? KeyboardKeyCatalog.Keys : KeyboardKeyCatalog.Keys.Append(SelectedKey);

    public KeyboardKey SelectedKey
    {
        get => KeyboardKeyCatalog.FromScanCode(ScanCode, IsExtended);
        set
        {
            if (value is null || value == SelectedKey) return;
            ScanCode = value.ScanCode;
            IsExtended = value.IsExtended;
            OnPropertyChanged();
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
