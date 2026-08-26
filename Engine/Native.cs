using System.Runtime.InteropServices;

namespace AutoAimLock.Engine;

/// <summary>Win32 input/cursor primitives (fresh minimal set).</summary>
internal static class Native
{
    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint n, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vk);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int Dx; public int Dy; public uint MouseData; public uint Flags; public uint Time; public nint Extra;
    }

    internal const uint MOVE = 0x0001;
    internal const uint LEFTDOWN = 0x0002;
    internal const uint LEFTUP = 0x0004;
    internal const int VK_LBUTTON = 0x01;

    /// <summary>Relative mouse move.</summary>
    internal static void Move(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;
        var i = new INPUT { Type = 0, U = new InputUnion { Mouse = new MOUSEINPUT { Dx = dx, Dy = dy, Flags = MOVE } } };
        SendInput(1, new[] { i }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    internal static void LeftButton(bool down)
    {
        var i = new INPUT { Type = 0, U = new InputUnion { Mouse = new MOUSEINPUT { Flags = down ? LEFTDOWN : LEFTUP } } };
        SendInput(1, new[] { i }, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    internal static bool LeftDown => (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

    /// <summary>Generic key/button state check (VK codes incl. 0x05/0x06 side buttons).</summary>
    internal static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
}
