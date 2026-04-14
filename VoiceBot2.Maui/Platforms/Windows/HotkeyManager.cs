using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;
namespace VoiceBot2.Maui.Platforms.Windows;

public sealed partial class HotkeyManager : IDisposable
{
    private SafeHHOOK _hookID = SafeHHOOK.Null;
    private readonly HookProc _proc;
    private readonly ILogger<HotkeyManager> _logger;

    public event Action? HotkeyPressed;
    public event Action? HotkeyReleased;

    private bool _isPressed = false;

    public HotkeyManager(ILogger<HotkeyManager> logger)
    {
        _proc = HookCallback;
        _logger = logger;
    }

    public void Register()
    {
        _hookID = SetHook(_proc);
    }

    public void Unregister()
    {
        UnhookWindowsHookEx(_hookID);
    }

    private static SafeHHOOK SetHook(HookProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        return SetWindowsHookEx(HookType.WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName));
    }

    private nint HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
            bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
            bool isW = vkCode == 0x57;

            // Key down
            if (wParam == (IntPtr)0x0100 && ctrl && shift && isW && !_isPressed) // WM_KEYDOWN
            {
                _isPressed = true;
                _logger.LogInformation("Hotkey Ctrl+Shift+W pressed");
                HotkeyPressed?.Invoke();
            }

            // Key up
            if (wParam == (IntPtr)0x0101 && ctrl && shift && _isPressed && isW) // WM_KEYUP
            {
                _isPressed = false;
                _logger.LogInformation("Hotkey Ctrl+Shift+W released");
                HotkeyReleased?.Invoke();
            }
        }

        return Vanara.PInvoke.User32.CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Unregister();
    }
}