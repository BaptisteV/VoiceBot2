using System.Runtime.InteropServices;

namespace VoiceBot2.Maui.Platforms.Windows;

public class WindowsMessageMonitor : IDisposable
{
    private IntPtr _hwnd;
    private IntPtr _oldWndProc;
    private WndProcDelegate _newWndProc;

    public event EventHandler<WindowMessageEventArgs>? WindowMessageReceived;

    private const int GWL_WNDPROC = -4;

    public WindowsMessageMonitor(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _newWndProc = WndProc;

        _oldWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        WindowMessageReceived?.Invoke(this, new WindowMessageEventArgs
        {
            MessageId = msg,
            WParam = wParam,
            LParam = lParam
        });

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        SetWindowLongPtr(_hwnd, GWL_WNDPROC, _oldWndProc);
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time

    [DllImport("user32.dll")]
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
}
