using System.Windows;

namespace AUTOAIMLOCK.App;

public partial class App : System.Windows.Application
{
    public static AssistOverlay Overlay { get; private set; } = null!;
    public static TrayIcon? Tray { get; private set; }

    public static void SetTray(TrayIcon? tray) => Tray = tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Overlay HUD (bottom-left on/off indicator) — always present, even when
        // the main window is hidden to tray.
        Overlay = new AssistOverlay();
        Overlay.Show();

        // Loading/splash screen, then open the main window.
        SplashWindow.ShowSplashThen(() =>
        {
            var win = new MainWindow();
            MainWindow = win;
            win.Show();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Overlay?.CloseOverlay();
        Tray?.Dispose();
        base.OnExit(e);
    }
}
