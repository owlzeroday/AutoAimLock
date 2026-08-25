using System.Drawing;
using System.Drawing.Imaging;
using AUTOAIMLOCK.Core.Models;

namespace AUTOAIMLOCK.Core.Detection;

/// <summary>
/// Color-based target detector. Scans a captured bitmap for pixels matching
/// the target HSV color range, groups them into connected clusters, and
/// returns the nearest target to the center.
///
/// Uses unsafe pointer access for maximum speed — processes ~160k pixels
/// in under 2ms on modern hardware.
/// </summary>
public sealed class TargetDetector
{
    /// <summary>
    /// Detect targets in the captured bitmap. Returns the nearest target
    /// to the crosshair (fovCx, fovCy), or null if none found.
    /// Supports dual-band detection: a pixel counts toward a cluster if it
    /// matches the primary <paramref name="color"/> (e.g. hit marker) OR the
    /// secondary <paramref name="bodyColor"/> (enemy model). This lets the
    /// assist lock at mid/long range where the hit flash alone is too small.
    /// </summary>
    public DetectedTarget? Detect(Bitmap bmp, ColorSettings color, ColorSettings? bodyColor, bool detectBody,
        int minPixels, int fovRadius, int fovCx, int fovCy,
        bool usePreference = false, double prefX = 0, double prefY = 0, double prefRadius = 80)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        int cx = fovCx;
        int cy = fovCy;
        int fovR2 = fovRadius * fovRadius;

        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        int stride = data.Stride;
        int bytesPerPixel = 3;

        // Accumulate matching pixels into a grid of "cells" for connected-component clustering.
        // Each cell is 8x8 pixels — fast spatial bucketing.
        // Two counters per cell: hit-marker pixels and body pixels, so a cluster can be
        // classified as "body" (apply humanoid shape validation) vs "hit marker" (tiny flash).
        const int CellSize = 8;
        int gw = (w + CellSize - 1) / CellSize;
        int gh = (h + CellSize - 1) / CellSize;
        var cellHitCount = new int[gh, gw];
        var cellBodyCount = new int[gh, gw];
        var cellSumX = new int[gh, gw];
        var cellSumY = new int[gh, gw];

        unsafe
        {
            byte* ptr = (byte*)data.Scan0;
            for (int y = 0; y < h; y++)
            {
                byte* row = ptr + y * stride;
                int gy = y / CellSize;
                for (int x = 0; x < w; x++)
                {
                    byte* px = row + x * bytesPerPixel;
                    // BGR format: px[0]=B, px[1]=G, px[2]=R
                    int b = px[0], g = px[1], r = px[2];

                    // Quick reject: skip if all channels are similar (grayscale) — saves HSV conversion
                    int maxC = Math.Max(r, Math.Max(g, b));
                    int minC = Math.Min(r, Math.Min(g, b));
                    if (maxC - minC < 25) continue; // Skip low-saturation pixels early

                    // Convert BGR to HSV
                    var (hue, sat, val) = BgrToHsv(r, g, b);

                    bool hit = Matches(color, hue, sat, val);
                    bool body = detectBody && bodyColor != null && Matches(bodyColor, hue, sat, val);
                    if (!hit && !body) continue;

                    int gx = x / CellSize;
                    if (hit) cellHitCount[gy, gx]++;
                    if (body) cellBodyCount[gy, gx]++;
                    cellSumX[gy, gx] += x;
                    cellSumY[gy, gx] += y;
                }
            }
        }

        bmp.UnlockBits(data);

        // Flood-fill connected cells into clusters
        var visited = new bool[gh, gw];
        DetectedTarget? best = null;
        double bestScore = double.MaxValue;

        for (int gy = 0; gy < gh; gy++)
        {
            for (int gx = 0; gx < gw; gx++)
            {
                if (visited[gy, gx] || (cellHitCount[gy, gx] == 0 && cellBodyCount[gy, gx] == 0)) continue;

                // BFS to find connected cluster
                var queue = new Queue<(int cx, int cy)>();
                queue.Enqueue((gx, gy));
                visited[gy, gx] = true;

                int hitPixels = 0, bodyPixels = 0;
                int sumX = 0, sumY = 0;
                int minCx = gx, maxCx = gx, minCy = gy, maxCy = gy;

                while (queue.Count > 0)
                {
                    var (ccx, ccy) = queue.Dequeue();
                    hitPixels += cellHitCount[ccy, ccx];
                    bodyPixels += cellBodyCount[ccy, ccx];
                    sumX += cellSumX[ccy, ccx];
                    sumY += cellSumY[ccy, ccx];
                    if (ccx < minCx) minCx = ccx;
                    if (ccx > maxCx) maxCx = ccx;
                    if (ccy < minCy) minCy = ccy;
                    if (ccy > maxCy) maxCy = ccy;

                    // Check 4-neighbors
                    for (int ny = -1; ny <= 1; ny++)
                    for (int nx = -1; nx <= 1; nx++)
                    {
                        if (nx == 0 && ny == 0) continue;
                        int nnx = ccx + nx, nny = ccy + ny;
                        if (nnx < 0 || nnx >= gw || nny < 0 || nny >= gh) continue;
                        if (visited[nny, nnx]) continue;
                        if (cellHitCount[nny, nnx] == 0 && cellBodyCount[nny, nnx] == 0) continue;
                        visited[nny, nnx] = true;
                        queue.Enqueue((nnx, nny));
                    }
                }

                int totalPixels = hitPixels + bodyPixels;
                if (totalPixels < minPixels) continue;

                double targetX = (double)sumX / totalPixels;
                double targetY = (double)sumY / totalPixels;
                double dx = targetX - cx;
                double dy = targetY - cy;
                double dist = dx * dx + dy * dy;

                // Check FOV
                if (dist > fovR2) continue;

                // Edge rejection: ignore clusters too close to the capture border.
                // UI elements (health bars, minimap, kill feed) live at screen edges.
                // Hit markers and nametags appear near the center (near crosshair).
                const int EdgeMargin = 30; // pixels from capture border
                if (targetX < EdgeMargin || targetX > w - EdgeMargin ||
                    targetY < EdgeMargin || targetY > h - EdgeMargin)
                    continue;

                // ── Shape validation ────────────────────────────────────────────
                // A real target (player/bot) is a COMPACT, roughly-filling blob.
                // Walls/terrain/scenery register as very wide, very tall, OR sparse
                // (low fill ratio) clusters. Reject those regardless of color.
                int clusterWidth = (maxCx - minCx + 1) * CellSize;
                int clusterHeight = (maxCy - minCy + 1) * CellSize;
                if (clusterWidth <= 0 || clusterHeight <= 0) continue;

                // Reject extreme horizontal strips (health bars, long wall tops)
                if (clusterWidth > clusterHeight * 4) continue;
                // Reject extreme vertical strips (building edges, lampposts, tall narrow walls)
                if (clusterHeight > clusterWidth * 4) continue;

                // A real body fills its bounding box moderately. A wall/terrain face
                // is nearly solid (fill ~1.0); scattered scenery is sparse (fill ~0).
                double fill = (double)totalPixels / ((double)clusterWidth * clusterHeight);
                if (bodyPixels > hitPixels)
                {
                    // Body-dominant cluster: must look like a solid-ish humanoid silhouette.
                    // Too sparse -> scattered scenery/walls at an angle. Too solid -> wall face.
                    if (fill < 0.12) continue;
                    if (fill > 0.85) continue;
                    // Humanoid is taller than wide but not insanely so (allow crouched ~1:1).
                    if (clusterHeight < clusterWidth / 2) continue;
                }

                // ── Selection scoring ─────────────────────────────────────────
                // A real human target is preferred over UI/noise. Weight by type:
                //   * body clusters (full humanoid) get a strong pull
                //   * pure hit-marker flashes count too (they're how bullets land)
                double baseDistSq = dist; // distance to crosshair
                if (usePreference)
                {
                    // Prefer the cluster nearest the last tracked position — this keeps
                    // the lock "magnetic" to ONE moving body instead of switching every frame
                    // to whatever is nearest the crosshair (the cause of wild rotation).
                    double prefDx = targetX - prefX;
                    double prefDy = targetY - prefY;
                    double prefD = prefDx * prefDx + prefDy * prefDy;
                    if (prefD <= prefRadius * prefRadius)
                        baseDistSq = prefD;
                }

                // Body weighting: a silhouette beats a flash at the same distance.
                double score = bodyPixels > 0 ? baseDistSq * 0.7 : baseDistSq;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = new DetectedTarget
                    {
                        CenterX = targetX,
                        CenterY = targetY,
                        PixelCount = totalPixels,
                        DistanceFromCenter = Math.Sqrt(dist),
                        IsBody = bodyPixels > 0,
                        BodyPixelCount = bodyPixels,
                        HitPixelCount = hitPixels
                    };
                }
            }
        }

        return best;
    }

    /// <summary>True if the HSV pixel (sat/val scaled 0-1, hue 0-180) falls inside the band range.</summary>
    static bool Matches(ColorSettings c, double hue, double sat, double val)
        => sat * 255 >= c.SMin && sat * 255 <= c.SMax &&
           val * 255 >= c.VMin && val * 255 <= c.VMax &&
           hue >= c.HMin && hue <= c.HMax;

    /// <summary>Convert BGR to HSV. Returns (H: 0-180, S: 0-1, V: 0-1).</summary>
    static (double h, double s, double v) BgrToHsv(int r, int g, int b)
    {
        double max = Math.Max(r, Math.Max(g, b)) / 255.0;
        double min = Math.Min(r, Math.Min(g, b)) / 255.0;
        double delta = max - min;

        double v = max;
        double s = max <= 0 ? 0 : delta / max;

        double h = 0;
        if (delta > 0)
        {
            if (max == r / 255.0)
                h = 60 * (((g - b) / 255.0) / delta);
            else if (max == g / 255.0)
                h = 60 * (2 + ((b - r) / 255.0) / delta);
            else
                h = 60 * (4 + ((r - g) / 255.0) / delta);
        }

        if (h < 0) h += 360;

        // Scale to 0-180 (OpenCV scale)
        h /= 2;

        return (h, s, v);
    }
}
