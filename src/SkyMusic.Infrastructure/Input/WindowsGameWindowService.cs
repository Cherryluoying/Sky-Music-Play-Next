// 模块：SkyMusic.Infrastructure 输入设备 WindowsGameWindowService
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Input;

public sealed class WindowsGameWindowService : IGameWindowService
{
    private const uint GetWindowOwner = 4;
    private const int RestoreWindow = 9;

    // 枚举可见顶层窗口供用户选择目标句柄
    public ValueTask<IReadOnlyList<GameWindowInfo>> GetAvailableWindowsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ValueTask.FromResult<IReadOnlyList<GameWindowInfo>>([]);
        }

        var currentProcessId = Environment.ProcessId;
        var windows = new List<GameWindowInfo>();
        var canceled = false;
        EnumWindows((handle, _) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                canceled = true;
                return false;
            }

            if (!IsWindowVisible(handle) || GetWindow(handle, GetWindowOwner) != IntPtr.Zero)
            {
                return true;
            }

            var titleLength = GetWindowTextLength(handle);
            if (titleLength == 0)
            {
                return true;
            }

            GetWindowThreadProcessId(handle, out var processId);
            if (processId == currentProcessId)
            {
                return true;
            }

            var title = new StringBuilder(titleLength + 1);
            GetWindowText(handle, title, title.Capacity);
            var processName = GetProcessName(checked((int)processId));
            windows.Add(new GameWindowInfo(handle.ToInt64(), title.ToString(), processName, checked((int)processId)));
            return true;
        }, IntPtr.Zero);
        if (canceled)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        return ValueTask.FromResult<IReadOnlyList<GameWindowInfo>>(windows
            .OrderBy(window => window.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray());
    }

    // 恢复并激活目标窗口后等待前台句柄稳定
    public async ValueTask<bool> ActivateAsync(long handle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var windowHandle = new IntPtr(handle);
        if (!IsWindow(windowHandle))
        {
            return false;
        }

        if (IsIconic(windowHandle))
        {
            ShowWindowAsync(windowHandle, RestoreWindow);
        }

        if (!SetForegroundWindow(windowHandle))
        {
            return false;
        }

        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        return GetForegroundWindow() == windowHandle;
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception) when (processId > 0)
        {
            return "Unknown";
        }
    }

    private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr handle, uint command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int capacity);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr handle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
