using System.Runtime.InteropServices;
using AutoAimLock.Core;

namespace AutoAimLock.Interop;

/// <summary>Low-level mouse/keyboard hooks (WH_MOUSE_LL / WH_KEYBOARD_LL).</summary>
public sealed class GlobalHook : IDisposable
{
    const int WH_MOUSE_LL = 14, WH_KEYBOARD_LL = 13;
    const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205,
        WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C;
    const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
    const uint LLMHF_INJECTED = 0x00000001, LLKHF_INJECTED = 0x00000010;

    public delegate void MouseButtonHandler(MouseButton button, bool down);
    public delegate void KeyHandler(int vk, bool down);
    public event MouseButtonHandler? Button;
    public event KeyHandler? Key;
    public Func<MouseButton, bool>? ShouldSuppress { get; set; }

    IntPtr _mouseHook, _keyHook;
    Thread? _thread;
    uint _threadId;
    volatile bool _running;
    HookProc? _mouseProc, _keyProc;
    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)] struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc f, nint h, uint tid);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern nint GetModuleHandle(string? m);
    [DllImport("user32.dll")] static extern bool GetMessage(out MSG m, IntPtr h, uint a, uint b);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] static extern bool PostThreadMessage(uint id, uint msg, IntPtr w, IntPtr l);
    const uint WM_QUIT = 0x0012;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(ThreadProc) { IsBackground = true, Name = "GlobalHook" };
        _thread.Start();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 2000 && (_mouseHook == IntPtr.Zero || _keyHook == IntPtr.Zero)) Thread.Sleep(5);
        if (_mouseHook == IntPtr.Zero || _keyHook == IntPtr.Zero) Log.Warning("Global hook failed to install");
    }

    void ThreadProc()
    {
        _threadId = GetCurrentThreadId();
        _mouseProc = MouseProc; _keyProc = KeyProc;
        var mod = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, mod, 0);
        _keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, mod, 0);
        while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0)) { TranslateMessage(ref msg); DispatchMessage(ref msg); }
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_keyHook != IntPtr.Zero) UnhookWindowsHookEx(_keyHook);
        _mouseHook = _keyHook = IntPtr.Zero;
    }

    IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        bool blocked = false;
        try
        {
            if (nCode >= 0)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                switch ((int)wParam)
                {
                    case WM_LBUTTONDOWN: blocked = Raise(MouseButton.Left, true, data); break;
                    case WM_LBUTTONUP: blocked = Raise(MouseButton.Left, false, data); break;
                    case WM_RBUTTONDOWN: blocked = Raise(MouseButton.Right, true, data); break;
                    case WM_RBUTTONUP: blocked = Raise(MouseButton.Right, false, data); break;
                    case WM_MBUTTONDOWN: blocked = Raise(MouseButton.Middle, true, data); break;
                    case WM_MBUTTONUP: blocked = Raise(MouseButton.Middle, false, data); break;
                    case WM_XBUTTONDOWN:
                    case WM_XBUTTONUP:
                        int which = ((int)data.mouseData >> 16) & 0xFFFF;
                        var btn = which == 1 ? MouseButton.Side1 : MouseButton.Side2;
                        blocked = Raise(btn, (int)wParam == WM_XBUTTONDOWN, data);
                        break;
                }
            }
        }
        catch (Exception ex) { Log.Warning(ex, "Mouse hook"); }
        return blocked ? new IntPtr(1) : CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    bool Raise(MouseButton b, bool down, MSLLHOOKSTRUCT data)
    {
        if ((data.flags & LLMHF_INJECTED) != 0) return false; // ignore our own injected input
        bool suppress = ShouldSuppress?.Invoke(b) == true;
        Button?.Invoke(b, down);
        return suppress;
    }

    IntPtr KeyProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if ((data.flags & LLKHF_INJECTED) == 0)
                    Key?.Invoke((int)data.vkCode, (int)wParam is WM_KEYDOWN or WM_SYSKEYDOWN);
            }
        }
        catch (Exception ex) { Log.Warning(ex, "Key hook"); }
        return CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        _running = false;
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        try { _thread?.Join(1000); } catch { }
        _thread = null;
    }
}
