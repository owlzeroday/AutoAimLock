using System.Drawing;
using System.Drawing.Imaging;

namespace AutoAimLock.Engine;

/// <summary>
/// Fresh detection algorithm: "nearest-seed + windowed centroid".
///  1. Scan pixels inside the FOV circle; take the matching pixel CLOSEST to the
///     crosshair as the seed — this inherently picks ONE target (the one you're
///     closest to aiming at) instead of merging multiple enemies into one blob.
///  2. Recompute the exact centroid over a small window around the seed.
///  3. Validate shape (compactness) to reject walls/scenery.
/// Single-pass friendly, allocation-free, and structurally simpler than
/// connected-component clustering.
/// </summary>
public static class Detector
{
    const int WindowRadius = 48;   // centroid window around seed
    const int ScanStep = 1;        // pixel stride for pass 1

    /// <summary>Find a target. Coordinates are bitmap-relative. False if none valid.</summary>
    public static bool Find(Bitmap bmp, Band band, int cx, int cy, int fov, int minPixels,
        out double x, out double y, out int count)
    {
        x = y = 0; count = 0;
        int w = bmp.Width, h = bmp.Height;
        int fov2 = fov * fov;

        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            unsafe
            {
                byte* basePtr = (byte*)data.Scan0;
                int stride = data.Stride;

                // ── Pass 1: nearest matching pixel inside the FOV circle ──
                int seedX = -1, seedY = -1;
                long bestD = long.MaxValue;
                int yStart = Math.Max(0, cy - fov), yEnd = Math.Min(h - 1, cy + fov);
                for (int py = yStart; py <= yEnd; py++)
                {
                    int dy = py - cy;
                    int halfWidth = (int)Math.Sqrt(fov2 - dy * dy);
                    int xStart = Math.Max(0, cx - halfWidth), xEnd = Math.Min(w - 1, cx + halfWidth);
                    byte* row = basePtr + py * stride;
                    for (int px = xStart; px <= xEnd; px += ScanStep)
                    {
                        byte* p = row + px * 3;
                        // BGR
                        if (!IsMatch(p[2], p[1], p[0], band)) continue;
                        long dx = px - cx, dyy = dy;
                        long d = dx * dx + dyy * dyy;
                        if (d < bestD) { bestD = d; seedX = px; seedY = py; }
                    }
                }
                if (seedX < 0) return false;

                // ── Pass 2: centroid over a window around the seed ──
                int x0 = Math.Max(0, seedX - WindowRadius), x1 = Math.Min(w - 1, seedX + WindowRadius);
                int yy0 = Math.Max(0, seedY - WindowRadius), yy1 = Math.Min(h - 1, seedY + WindowRadius);
                long sx = 0, sy = 0; int cnt = 0;
                int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
                for (int py = yy0; py <= yy1; py++)
                {
                    byte* row = basePtr + py * stride;
                    for (int px = x0; px <= x1; px++)
                    {
                        byte* p = row + px * 3;
                        if (!IsMatch(p[2], p[1], p[0], band)) continue;
                        sx += px; sy += py; cnt++;
                        if (px < minX) minX = px;
                        if (px > maxX) maxX = px;
                        if (py < minY) minY = py;
                        if (py > maxY) maxY = py;
                    }
                }
                if (cnt < minPixels) return false;

                // Shape validation — reject strips and solid/scattered scenery.
                int bw = maxX - minX + 1, bh = maxY - minY + 1;
                if (bw > bh * 4 || bh > bw * 4) return false;
                double density = cnt / ((double)bw * bh);
                if (density < 0.22 || density > 0.88) return false;

                // Size cap: reject blobs that fill a large chunk of the capture —
                // a wall face or mountain in view is a near-fullscreen solid region
                // that must NEVER be treated as a target.
                double areaFrac = (double)(bw * bh) / ((double)w * h);
                if (areaFrac > 0.30) return false;

                x = (double)sx / cnt;
                y = (double)sy / cnt;
                count = cnt;
                return true;
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    /// <summary>Inline HSV band test (r,g,b in). Hue on the OpenCV 0-180 scale.</summary>
    static bool IsMatch(byte r, byte g, byte b, Band c)
    {
        int max = r > g ? (r > b ? r : b) : (g > b ? g : b);
        int min = r < g ? (r < b ? r : b) : (g < b ? g : b);
        int delta = max - min;
        if (delta < 20) return false;                       // grayscale reject
        if (delta * 100 / max < c.SMin / 2) return false;   // cheap saturation pre-check

        // Saturation / value on 0-255 scale
        int sat = delta * 255 / max;
        if (sat < c.SMin || sat > c.SMax) return false;
        if (max < c.VMin || max > c.VMax) return false;

        // Hue (0-360) -> OpenCV 0-180
        int hue;
        if (max == r) hue = 60 * (g - b) / delta;
        else if (max == g) hue = 120 + 60 * (b - r) / delta;
        else hue = 240 + 60 * (r - g) / delta;
        if (hue < 0) hue += 360;
        hue /= 2;

        return hue >= c.HMin && hue <= c.HMax;
    }
}
