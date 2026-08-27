using System.Windows;
using AutoAimLock.Core;
using AutoAimLock.Core.Macro;
using AutoAimLock.Database;
using AutoAimLock.Supabase;

namespace AutoAimLock;

public partial class App : System.Windows.Application
{
    public static OverlayWindow Overlay { get; private set; } = null!;
    public static MacroDatabase Db { get; private set; } = null!;
    public static MacroEngine Macros { get; private set; } = null!;
    public static AppSettings AppSettings { get; private set; } = new();
    public static GameProfile? CurrentProfile { get; private set; }
    public static Tray? Tray { get; private set; }
    /// <summary>Current license (set after successful auth). Null = not licensed.</summary>
    public static LicenseInfo? CurrentLicense { get; private set; }
    public static LocalSession? Session { get; private set; }

    public static void SetTray(Tray? tray)
    {
        Tray?.Dispose();
        Tray = tray;
    }

    public static void SetLicense(LicenseInfo? info, LocalSession? session)
    {
        CurrentLicense = info;
        Session = session;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // SQLite database + default profile (AegisMouse macro system).
        AppPaths.EnsureDirectories();
        Db = new MacroDatabase();

        AppSettings = new AutoAimLock.Core.AppSettings();
        var lastId = Db.GetSetting("last_profile_id");
        var profiles = Db.GetProfiles();
        CurrentProfile = profiles.FirstOrDefault(p => p.Id == lastId) ?? profiles.FirstOrDefault();

        if (CurrentProfile is null)
        {
            // Seed a default profile: Left → RapidFireRecoil with AR recoil table.
            CurrentProfile = new GameProfile
            {
                Name = "Default",
                RecoilTable = Presets.Weapons["AR (M4-style)"].Select(s => new RecoilStep { Shot = s.Shot, Dx = s.Dx, Dy = s.Dy }).ToList(),
                RecoilStrength = 1.0,
                Bindings = [new ButtonBinding { Trigger = MouseButton.Left, Mode = BindingMode.RapidFireRecoil, FireRateRpm = 600 }]
            };
            Db.SaveProfile(CurrentProfile);
        }

        Macros = new MacroEngine();
        Macros.Start();
        Macros.SetProfile(CurrentProfile);

        Overlay = new OverlayWindow();
        Overlay.Show();

        // Route: splash → cached session (login) else → main.
        SplashWindow.ShowThen(() =>
        {
            var cached = LocalSession.Load();
            if (cached is { AccessToken.Length: > 0 })
            {
                SetLicense(new LicenseInfo { Ok = true, Tier = cached.Tier, Plan = cached.Plan, ExpiresAt = cached.ExpiresAt }, cached);
                var win = new MainWindow();
                MainWindow = win;
                return win;
            }
            var login = new LoginWindow();
            MainWindow = login;
            return login;
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Overlay.Close();
        Macros?.Dispose();
        Db?.Dispose();
        base.OnExit(e);
    }
}
