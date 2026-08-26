using System.Runtime.InteropServices;
using AutoAimLock.Core;

namespace AutoAimLock.Interop;

/// <summary>OS-level input synthesis via SendInput (works with any mouse brand).</summary>
public static class NativeInput
{
    const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004,
        MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010,
        MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040,
        MOUSEEVENTF_XDOWN = 0x0080, MOUSEEVENTF_XUP = 0x0100,
        MOUSEEVENTF_MOVE = 0x0001, MOUSEEVENTF_WHEEL = 0x0800;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const int XBUTTON1 = 0x0001, XBUTTON2 = 0x0002;

    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] i, int sz);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vkey);
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint u);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint u);

    static INPUT[] OneMouse(uint flags, uint data = 0, int dx = 0, int dy = 0)
        => [new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags } } }];

    static void Inject(INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length) Log.Warning($"SendInput {sent}/{inputs.Length} (err {Marshal.GetLastWin32Error()})");
    }

    static (uint down, uint up) FlagsFor(MouseButton b) => b switch
    {
        MouseButton.Left => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
        MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
        MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
        _ => (MOUSEEVENTF_XDOWN, MOUSEEVENTF_XUP)
    };

    static uint DataFor(MouseButton b) => b switch { MouseButton.Side1 => XBUTTON1, MouseButton.Side2 => XBUTTON2, _ => 0 };

    public static void ButtonDown(MouseButton b) => Inject(OneMouse(FlagsFor(b).down, DataFor(b)));
    public static void ButtonUp(MouseButton b) => Inject(OneMouse(FlagsFor(b).up, DataFor(b)));
    public static void Click(MouseButton b, int holdMs) { ButtonDown(b); Thread.Sleep(Math.Max(0, holdMs)); ButtonUp(b); }
    public static void MoveRelative(int dx, int dy) => Inject(OneMouse(MOUSEEVENTF_MOVE, dx: dx, dy: dy));
    public static void Wheel(int ticks) => Inject(OneMouse(MOUSEEVENTF_WHEEL, (uint)(ticks * 120)));

    public static void KeyDown(int vk) => Inject([new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk } } }]);
    public static void KeyUp(int vk) => Inject([new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = KEYEVENTF_KEYUP } } }]);
    public static void KeyTap(int vk) { KeyDown(vk); Thread.Sleep(8); KeyUp(vk); }

    public static bool IsDown(MouseButton b) => b switch
    {
        MouseButton.Left => GetAsyncKeyState(0x01) < 0,
        MouseButton.Right => GetAsyncKeyState(0x02) < 0,
        MouseButton.Middle => GetAsyncKeyState(0x04) < 0,
        MouseButton.Side1 => GetAsyncKeyState(0x05) < 0,
        MouseButton.Side2 => GetAsyncKeyState(0x06) < 0,
        _ => false
    };
    public static bool IsKeyDown(int vk) => GetAsyncKeyState(vk) < 0;

    static readonly object _lock = new();
    static int _refs;
    public static void BeginHighResTimer() { lock (_lock) { if (_refs++ == 0) timeBeginPeriod(1); } }
    public static void EndHighResTimer() { lock (_lock) { if (--_refs <= 0) { _refs = 0; timeEndPeriod(1); } } }
}
