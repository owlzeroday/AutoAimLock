using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace AUTOAIMLOCK.App;

/// <summary>Forces dark mode title bar and solid black background (no Mica/Acrylic).</summary>
internal static class ModernChrome
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    const int DWMWA_MICA_EFFECT = 1029;

    public static void Apply(IntPtr hwnd)
    {
        // Dark mode FIRST
        int dark = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        // Disable all translucent backdrops
        int none = 0;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref none, sizeof(int));
    }
}
