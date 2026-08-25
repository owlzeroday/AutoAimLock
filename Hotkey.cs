using System.Runtime.InteropServices;

namespace AutoAimLock;

/// <summary>Global F8 toggle via low-level keyboard hook.</summary>
internal static class Hotkey
{
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100;

    delegate IntPtr Proc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, Proc p, nint mod, uint tid);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern nint GetModuleHandle(string? m);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);

    [StructLayout(LayoutKind.Sequential)]
    struct Kbd { public uint Vk; public uint Scan; public uint Flags; public uint Time; public nint Extra; }

    static IntPtr _hook;
    static Proc? _proc;
    public static event Action? Pressed;

    public static void Register()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = (code, wParam, lParam) =>
        {
            if (code >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                var k = System.Runtime.InteropServices.Marshal.PtrToStructure<Kbd>(lParam);
                if (k.Vk == 0x77) Pressed?.Invoke(); // F8
            }
            return CallNextHookEx(_hook, code, wParam, lParam);
        };
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public static void Unregister()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        _proc = null;
    }
}
