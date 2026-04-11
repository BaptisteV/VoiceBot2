using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceBot2.Maui.Platforms.Windows;

public sealed class HotkeyManager : IDisposable
{
    private IntPtr _hookID = IntPtr.Zero;
    private readonly HookProc _proc;

    public event Action? HotkeyPressed;
    public event Action? HotkeyReleased;

    private bool _isPressed = false;

    public HotkeyManager()
    {
        _proc = HookCallback;
    }

    public void Register()
    {
        _hookID = SetHook(_proc);
    }

    public void Unregister()
    {
        UnhookWindowsHookEx(_hookID);
    }

    private IntPtr SetHook(HookProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        return SetWindowsHookEx(13, proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
            bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
            bool isW = vkCode == 0x57;

            // Key down
            if (wParam == (IntPtr)0x0100) // WM_KEYDOWN
            {
                if (ctrl && shift && isW && !_isPressed)
                {
                    _isPressed = true;
                    HotkeyPressed?.Invoke();
                }
            }

            // Key up
            if (wParam == (IntPtr)0x0101) // WM_KEYUP
            {
                if (_isPressed && isW)
                {
                    _isPressed = false;
                    HotkeyReleased?.Invoke();
                }
            }
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Unregister();
    }

    #region Win32

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    #endregion
}