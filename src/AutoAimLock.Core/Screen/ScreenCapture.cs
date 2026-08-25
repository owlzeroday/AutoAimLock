using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AUTOAIMLOCK.Core.Screen;

/// <summary>
/// Fast screen capture using GDI BitBlt. Captures a square region
/// centered on the current cursor position (crosshair).
/// </summary>
public sealed class ScreenCapture : IDisposable
{
    readonly IntPtr _screenDc;
    readonly IntPtr _memDc;

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dest, int dx, int dy, int w, int h,
                                                        IntPtr src, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out WinPoint pt);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);

    const int SRCCOPY = 0x00CC0020;
    const int SM_XVIRTUALSCREEN = 76;
    const int SM_YVIRTUALSCREEN = 77;
    const int SM_CXVIRTUALSCREEN = 78;
    const int SM_CYVIRTUALSCREEN = 79;

    struct WinPoint { public int X; public int Y; }

    int _lastWidth;
    int _lastHeight;
    IntPtr _hBitmap;
    Bitmap? _cachedBitmap;

    public ScreenCapture()
    {
        _screenDc = GetDC(IntPtr.Zero);
        _memDc = CreateCompatibleDC(_screenDc);
    }

    /// <summary>Capture a square region centered on the cursor. Returns a Bitmap in BGR format.</summary>
    public Bitmap? CaptureRegion(int size)
    {
        GetCursorPos(out var pt);
        // Virtual screen bounds — covers ALL monitors, not just the primary one.
        // Using the primary screen only makes the aim offset/wrong when the game
        // runs on a secondary monitor (the "wild/off-target" symptom).
        int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int screenW = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int screenH = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        int x = pt.X - size / 2;
        int y = pt.Y - size / 2;
        int w = size;
        int h = size;

        // Clamp capture region to virtual screen bounds (BUG 3 fix)
        // If cursor is near edge, shift the capture window so it stays on-screen
        if (x < vx) x = vx;
        if (y < vy) y = vy;
        if (x + w > vx + screenW) { x = vx + screenW - w; if (x < vx) x = vx; }
        if (y + h > vy + screenH) { y = vy + screenH - h; if (y < vy) y = vy; }

        // Recreate bitmap if size changed
        if (_hBitmap != IntPtr.Zero && (w != _lastWidth || h != _lastHeight))
        {
            DeleteObject(_hBitmap);
            _hBitmap = IntPtr.Zero;
            _cachedBitmap?.Dispose();
            _cachedBitmap = null;
        }

        if (_hBitmap == IntPtr.Zero)
        {
            _hBitmap = CreateCompatibleBitmap(_screenDc, w, h);
            _lastWidth = w;
            _lastHeight = h;
        }

        var oldObj = SelectObject(_memDc, _hBitmap);
        BitBlt(_memDc, 0, 0, w, h, _screenDc, x, y, SRCCOPY);
        SelectObject(_memDc, oldObj);

        // Convert to System.Drawing.Bitmap — force 24bpp BGR format (BUG 4 fix)
        _cachedBitmap?.Dispose();
        var raw = Image.FromHbitmap(_hBitmap);
        // Ensure 24bpp format that TargetDetector expects
        if (raw.PixelFormat != PixelFormat.Format24bppRgb)
        {
            var converted = new Bitmap(raw.Width, raw.Height, PixelFormat.Format24bppRgb);
            using (var g = System.Drawing.Graphics.FromImage(converted))
                g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
            raw.Dispose();
            _cachedBitmap = converted;
        }
        else
        {
            _cachedBitmap = raw;
        }

        // Compute crosshair position within the captured bitmap (BUG 3b fix)
        // When cursor is near screen edge, capture is clamped, so crosshair
        // is NOT at the center of the bitmap — it's offset
        CrosshairInBitmap = (pt.X - x, pt.Y - y);

        return _cachedBitmap;
    }

    /// <summary>Position of the cursor (crosshair) within the last captured bitmap.</summary>
    public (int cx, int cy) CrosshairInBitmap { get; private set; }

    public void Dispose()
    {
        _cachedBitmap?.Dispose();
        if (_hBitmap != IntPtr.Zero) DeleteObject(_hBitmap);
        DeleteDC(_memDc);
        ReleaseDC(IntPtr.Zero, _screenDc);
    }
}
