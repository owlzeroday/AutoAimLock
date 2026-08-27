using System.Windows;
using System.Windows.Threading;
using AutoAimLock.Core;
using AutoAimLock.Supabase;

namespace AutoAimLock;

public partial class LoginWindow : Window
{
    readonly SupabaseAuthClient _auth;
    readonly LicenseClient _license;
    bool _busy;

    public LoginWindow()
    {
        InitializeComponent();
        _auth = new SupabaseAuthClient(SupabaseConfig.Url, SupabaseConfig.AnonKey);
        _license = new LicenseClient(SupabaseConfig.Url);
    }

    async void SignInClicked(object sender, RoutedEventArgs e) => await DoAuth(false);
    async void SignUpClicked(object sender, RoutedEventArgs e) => await DoAuth(true);

    async Task DoAuth(bool signup)
    {
        if (_busy) return;
        var email = EmailBox.Text.Trim();
        var pass = PassBox.Password;
        if (email.Length == 0 || pass.Length == 0) { Message.Text = "Enter email and password."; return; }

        _busy = true;
        LoginBtn.IsEnabled = SignupBtn.IsEnabled = false;
        Message.Text = signup ? "Creating account…" : "Signing in…";
        try
        {
            var res = signup ? await _auth.SignUp(email, pass) : await _auth.SignIn(email, pass);
            if (!res.Ok) { Message.Text = res.Error; return; }

            if (res.Session is null) // email confirmation required
            {
                Message.Text = "Account created! Check your email to confirm, then sign in.";
                return;
            }

            await CompleteAuth(res.Session);
        }
        finally { _busy = false; LoginBtn.IsEnabled = SignupBtn.IsEnabled = true; }
    }

    async Task CompleteAuth(AuthSession session)
    {
        var info = await _license.Check(session.AccessToken);
        if (!info.IsUsable)
        {
            Message.Text = string.IsNullOrEmpty(info.Error) ? "License unavailable." : $"License error: {info.Error}";
            return;
        }

        // Persist session + license (DPAPI) for offline grace + auto-login.
        var cached = new LocalSession
        {
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            UserId = session.User?.Id ?? "",
            Email = session.User?.Email ?? "",
            Tier = info.Tier,
            Plan = info.Plan,
            ExpiresAt = info.ExpiresAt,
            CachedAtUtc = DateTime.UtcNow
        };
        cached.Save();
        App.SetLicense(info, cached);
        OpenMain();
    }

    void OpenMain()
    {
        var main = new MainWindow();
        main.Show();
        Close();
    }
}
