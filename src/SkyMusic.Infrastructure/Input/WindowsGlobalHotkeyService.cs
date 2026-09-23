// 模块：SkyMusic.Infrastructure Windows 全局快捷键
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SkyMusic.Infrastructure.Input;

public sealed class WindowsGlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmQuit = 0x0012;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;
    private const uint VkBack = 0x08;
    private const int ToggleId = 0x534D01;
    private const int StopId = 0x534D02;
    private readonly AutoResetEvent _ready = new(false);
    private readonly Thread? _thread;
    private uint _threadId;
    private bool _disposed;

    public WindowsGlobalHotkeyService()
    {
        if (!OperatingSystem.IsWindows())
            return;
        _thread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "SkyMusicPlay.GlobalHotkeys"
        };
        _thread.Start();
        if (!_ready.WaitOne(TimeSpan.FromSeconds(3)))
            throw new TimeoutException("全局快捷键服务启动超时");
    }

    public event Action? TogglePlaybackRequested;
    public event Action? StopRequested;

    public bool IsAvailable { get; private set; }
    public string? Error { get; private set; }

    private void MessageLoop()
    {
        _threadId = GetCurrentThreadId();
        try
        {
            var modifiers = ModControl | ModAlt | ModNoRepeat;
            var toggleRegistered = RegisterHotKey(IntPtr.Zero, ToggleId, modifiers, VkSpace);
            var stopRegistered = RegisterHotKey(IntPtr.Zero, StopId, modifiers, VkBack);
            IsAvailable = toggleRegistered && stopRegistered;
            if (!IsAvailable)
                Error = new Win32Exception(Marshal.GetLastWin32Error(), "无法注册全局快捷键").Message;
            _ready.Set();

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.Message != WmHotkey)
                    continue;
                if ((int)message.WParam == ToggleId)
                    TogglePlaybackRequested?.Invoke();
                else if ((int)message.WParam == StopId)
                    StopRequested?.Invoke();
            }

            if (toggleRegistered)
                UnregisterHotKey(IntPtr.Zero, ToggleId);
            if (stopRegistered)
                UnregisterHotKey(IntPtr.Zero, StopId);
        }
        finally
        {
            _ready.Set();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_thread is null || _threadId == 0)
            return;
        PostThreadMessage(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage message, IntPtr window, uint minimum, uint maximum);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
