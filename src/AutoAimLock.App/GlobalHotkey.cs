using System.Runtime.InteropServices;

namespace AUTOAIMLOCK.App;

/// <summary>
/// Global F8 hotkey via a low-level keyboard hook (WH_KEYBOARD_LL).
/// Works regardless of which window has focus and doesn't depend on
/// RegisterHotKey (which can silently fail when another app owns the key).
/// The hook callback is marshaled on the UI thread's message pump (WPF Dispatcher).
/// </summary>
internal static class GlobalHotkey
{
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100;
    const int VK_F8 = 0x77;

    delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    static IntPtr _hook;
    static LowLevelKeyboardProc? _proc; // keep the delegate alive for the hook

    public static event Action<int>? HotkeyPressed;

    public static void Register()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            System.Windows.MessageBox.Show(
                $"F8 hotkey hook failed to install (Win32 error {err}). Use the Enabled checkbox on the dashboard instead.",
                "AUTOAIMLOCK");
        }
    }

    static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (kbd.VkCode == VK_F8)
                HotkeyPressed?.Invoke(VK_F8);
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public static void Unregister()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _proc = null;
    }
}
