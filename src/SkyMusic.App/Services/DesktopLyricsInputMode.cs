// 模块：桌面歌词 Win32 输入模式；锁定时整个窗口鼠标穿透，解锁后恢复原有扩展样式。
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SkyMusic.App.Services;

internal sealed class DesktopLyricsInputMode(nint hwnd)
{
    private const int ExtendedStyle = -20;
    private const long Transparent = 0x20;
    private const long Layered = 0x80000;
    private const long NoActivate = 0x08000000;
    private readonly long _originalInputFlags = GetWindowLongPtrW(hwnd, ExtendedStyle).ToInt64() & (Transparent | Layered | NoActivate);

    public void SetLocked(bool locked)
    {
        var style = GetWindowLongPtrW(hwnd, ExtendedStyle).ToInt64();
        // 保留 Avalonia 管理的其他样式位；解锁还原焦点能力，外观气泡内的色值仍可编辑。
        var next = (style & ~(Transparent | Layered | NoActivate))
            | (locked ? Transparent | Layered | NoActivate : _originalInputFlags);
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtrW(hwnd, ExtendedStyle, new nint(next));
        if (previous == 0 && Marshal.GetLastPInvokeError() is var error && error != 0)
            throw new Win32Exception(error);
    }

    [DllImport("user32.dll")] private static extern nint GetWindowLongPtrW(nint hwnd, int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowLongPtrW(nint hwnd, int index, nint value);
}
