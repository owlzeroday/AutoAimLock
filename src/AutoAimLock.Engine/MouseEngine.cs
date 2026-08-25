using System.Runtime.InteropServices;
using AUTOAIMLOCK.Engine.Interop;

namespace AUTOAIMLOCK.Engine;

/// <summary>
/// Low-level mouse movement engine using Win32 SendInput.
/// Moves are relative (not absolute) to avoid fighting the user's mouse.
/// </summary>
public sealed class MouseEngine
{
    /// <summary>Move the mouse by (dx, dy) pixels relative to current position.</summary>
    public void MoveRelative(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;

        var input = new Win32.INPUT
        {
            Type = Win32.INPUT_MOUSE,
            U = new Win32.InputUnion
            {
                MouseInput = new Win32.MOUSEINPUT
                {
                    Dx = dx,
                    Dy = dy,
                    MouseData = 0,
                    DwFlags = Win32.MOUSEEVENTF_MOVE,
                    Time = 0,
                    DwExtraInfo = IntPtr.Zero
                }
            }
        };

        Win32.SendInput(1, new[] { input }, Marshal.SizeOf<Win32.INPUT>());
    }

    /// <summary>Get the current cursor position in screen coordinates.</summary>
    public (int x, int y) GetCursorPosition()
    {
        Win32.GetCursorPos(out var pt);
        return (pt.X, pt.Y);
    }

    /// <summary>Press (down) or release (up) a mouse button (0x01 left, 0x02 right, 0x04 middle).</summary>
    public void SetButton(int button, bool down)
    {
        uint flag = button switch
        {
            0x01 => down ? Win32.MOUSEEVENTF_LEFTDOWN : Win32.MOUSEEVENTF_LEFTUP,
            0x02 => down ? Win32.MOUSEEVENTF_RIGHTDOWN : Win32.MOUSEEVENTF_RIGHTUP,
            0x04 => down ? Win32.MOUSEEVENTF_MIDDLEDOWN : Win32.MOUSEEVENTF_MIDDLEUP,
            _ => 0
        };
        if (flag == 0) return;

        var input = new Win32.INPUT
        {
            Type = Win32.INPUT_MOUSE,
            U = new Win32.InputUnion
            {
                MouseInput = new Win32.MOUSEINPUT
                {
                    Dx = 0,
                    Dy = 0,
                    MouseData = 0,
                    DwFlags = flag,
                    Time = 0,
                    DwExtraInfo = IntPtr.Zero
                }
            }
        };
        Win32.SendInput(1, new[] { input }, Marshal.SizeOf<Win32.INPUT>());
    }

    /// <summary>Check if a virtual key is currently pressed.</summary>
    public bool IsKeyDown(int vk) => (Win32.GetAsyncKeyState(vk) & 0x8000) != 0;
}
