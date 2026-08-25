using System.Drawing;

namespace AutoAimLock;

/// <summary>System tray icon: Open / Toggle / Quit. Keeps the engine alive when the window is hidden.</summary>
public sealed class Tray : IDisposable
{
    readonly System.Windows.Forms.NotifyIcon _icon;

    public Tray(Action open, Action toggle, Action quit)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Black);
            using var brush = new SolidBrush(Color.FromArgb(0, 230, 118));
            g.FillEllipse(brush, 2, 2, 12, 12);
        }
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = "AutoAimLock",
            Visible = true,
            Icon = System.Drawing.Icon.FromHandle(bmp.GetHicon())
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => open());
        menu.Items.Add("Toggle (F8)", null, (_, _) => toggle());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => quit());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => open();
    }

    public void SetState(bool on) => _icon.Text = on ? "AutoAimLock — LOCK ON" : "AutoAimLock — LOCK OFF";

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
