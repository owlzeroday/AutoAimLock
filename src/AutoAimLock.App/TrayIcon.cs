using System.Drawing;
using System.Windows.Forms;

namespace AUTOAIMLOCK.App;

/// <summary>
/// System tray icon for AUTOAIMLOCK. Provides hide-to-tray behavior:
///  * a tray icon with tooltip showing the current on/off state
///  * double-click opens the main window
///  * a context menu (Open, Toggle Assist, Quit)
/// The tray icon keeps the app alive when the window is hidden, so the
/// aim-assist engine can keep running in the background.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _icon;
    readonly Action _showWindow;
    readonly Action _toggleEnabled;
    readonly Func<bool> _isEnabled;
    readonly Action _quit;

    public TrayIcon(Action showWindow, Action toggleEnabled, Func<bool> isEnabled, Action quit)
    {
        _showWindow = showWindow;
        _toggleEnabled = toggleEnabled;
        _isEnabled = isEnabled;
        _quit = quit;

        using (var iconBitmap = new Bitmap(16, 16))
        {
            using (var g = Graphics.FromImage(iconBitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Black);
                using (var brush = new SolidBrush(Color.FromArgb(0, 230, 118)))
                    g.FillEllipse(brush, 2, 2, 12, 12);
            }
            _icon = new NotifyIcon
            {
                Text = "AUTOAIMLOCK",
                Visible = true,
                Icon = System.Drawing.Icon.FromHandle(iconBitmap.GetHicon())
            };
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Setup", null, (_, _) => _showWindow());
        menu.Items.Add("Toggle Assist", null, (_, _) => _toggleEnabled());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => _quit());
        _icon.ContextMenuStrip = menu;

        _icon.DoubleClick += (_, _) => _showWindow();
    }

    /// <summary>Update the tray tooltip / icon state to reflect assist on/off.</summary>
    public void UpdateState()
    {
        _icon.Text = _isEnabled() ? "AUTOAIMLOCK — ASSIST ON" : "AUTOAIMLOCK — ASSIST OFF";
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
