using System.Windows;

namespace AutoAimLock;

public partial class App : System.Windows.Application
{
    public static OverlayWindow Overlay { get; private set; } = null!;
    public static Tray? Tray { get; private set; }

    public static void SetTray(Tray? tray)
    {
        Tray?.Dispose();
        Tray = tray;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Overlay = new OverlayWindow();
        Overlay.Show();
        var win = new MainWindow();
        MainWindow = win;
        win.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Overlay.Close();
        base.OnExit(e);
    }
}
