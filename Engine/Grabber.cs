using System.Drawing;
using System.Drawing.Imaging;

namespace AutoAimLock.Engine;

/// <summary>
/// Grabs a square screen region centered on the cursor via BitBlt into a
/// reusable 24bpp bitmap. Reports where the cursor landed inside the bitmap
/// (clamped at screen edges) and the bitmap's screen origin, so the engine
/// can convert bitmap coordinates to true screen coordinates.
/// </summary>
public sealed class Grabber : IDisposable
{
    readonly nint _screenDc;
    readonly nint _memDc;
    nint _hBitmap;
    Bitmap? _bmp;
    int _size;

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern nint GetDC(nint h);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(nint h, nint dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern nint CreateCompatibleDC(nint dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern nint CreateCompatibleBitmap(nint dc, int w, int h);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern nint SelectObject(nint dc, nint obj);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool BitBlt(nint dst, int dx, int dy, int w, int h, nint src, int sx, int sy, int rop);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(nint obj);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteDC(nint dc);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetSystemMetrics(int i);

    const int SRCCOPY = 0x00CC0020;

    public Grabber()
    {
        _screenDc = GetDC(nint.Zero);
        _memDc = CreateCompatibleDC(_screenDc);
    }

    /// <summary>Capture. Returns the bitmap; outputs cursor position inside it and its screen origin.</summary>
    public Bitmap? Grab(int size, out int cursorX, out int cursorY, out int originX, out int originY)
    {
        cursorX = cursorY = originX = originY = 0;
        Native.GetCursorPos(out var cur);

        int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77);
        int vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);

        int x = cur.X - size / 2, y = cur.Y - size / 2;
        if (x < vx) x = vx;
        if (y < vy) y = vy;
        if (x + size > vx + vw) x = vx + vw - size;
        if (y + size > vy + vh) y = vy + vh - size;
        if (x < vx) x = vx;
        if (y < vy) y = vy;

        if (_hBitmap != nint.Zero && _size != size)
        {
            DeleteObject(_hBitmap);
            _hBitmap = nint.Zero;
            _bmp?.Dispose();
            _bmp = null;
        }
        if (_hBitmap == nint.Zero)
        {
            _hBitmap = CreateCompatibleBitmap(_screenDc, size, size);
            _size = size;
        }

        var old = SelectObject(_memDc, _hBitmap);
        BitBlt(_memDc, 0, 0, size, size, _screenDc, x, y, SRCCOPY);
        SelectObject(_memDc, old);

        _bmp?.Dispose();
        _bmp = Image.FromHbitmap(_hBitmap);

        cursorX = cur.X - x;
        cursorY = cur.Y - y;
        originX = x;
        originY = y;
        return _bmp;
    }

    public void Dispose()
    {
        _bmp?.Dispose();
        if (_hBitmap != nint.Zero) DeleteObject(_hBitmap);
        DeleteDC(_memDc);
        ReleaseDC(nint.Zero, _screenDc);
    }
}
