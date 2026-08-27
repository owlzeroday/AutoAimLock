using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AutoAimLock.Core;
using AutoAimLock.Engine;
using Wm = System.Windows.Media;

namespace AutoAimLock;

public partial class MainWindow : Window
{
    readonly Settings _s = Settings.Load();
    readonly AimEngine _engine = new();
    Tray? _tray;
    bool _quit;
    bool _loading = true;

    public MainWindow()
    {
        InitializeComponent();
        LoadUiFromSettings();
        WireEvents();

        // License plan badge
        var lic = App.CurrentLicense;
        PlanText.Text = lic?.Plan switch { "1month" => "1 MONTH", "7days" => "7 DAYS", "3days" => "3 DAYS", _ => "TRIAL" };

        Loaded += (_, _) =>
        {
            Hotkey.Register();
            Hotkey.Pressed += ToggleEnabled;
            CreateTray();
            SyncMacroProfile();
        };
    }

    // ── UI ↔ settings ─────────────────────────────────────────────

    void LoadUiFromSettings()
    {
        var b = _s.ColorBand;
        HMin.Value = b.HMin; HMax.Value = b.HMax;
        SMin.Value = b.SMin; SMax.Value = b.SMax;
        VMin.Value = b.VMin; VMax.Value = b.VMax;
        Fov.Value = _s.Fov; Gain.Value = _s.GainPct; MaxStep.Value = _s.MaxStep;
        Deadzone.Value = _s.Deadzone; MinPixels.Value = _s.MinPixels;
        RecoilOn.IsChecked = _s.RecoilOn; RecoilStrength.Value = _s.RecoilStrength; RecoilHorizontal.Value = _s.RecoilHorizontal;
        RapidOn.IsChecked = _s.RapidOn; RapidRpm.Value = _s.RapidRpm;
        MacroKeyCombo.Items.Add("Left Mouse");
        MacroKeyCombo.Items.Add("Side Button 5");
        MacroKeyCombo.Items.Add("Side Button 6");
        // Macro controls reflect the PROFILE (single source of truth), so a fresh
        // seed (Left → RapidFireRecoil) isn't clobbered by the legacy engine JSON.
        var binding = App.CurrentProfile?.Bindings.FirstOrDefault();
        if (binding is not null)
        {
            int sel = binding.Trigger switch
            {
                MouseButton.Side1 => 1,
                MouseButton.Side2 => 2,
                _ => 0
            };
            MacroKeyCombo.SelectedIndex = sel;
            RapidRpm.Value = binding.FireRateRpm;
            RecoilStrength.Value = App.CurrentProfile!.RecoilStrength;
            RecoilOn.IsChecked = binding.Mode is BindingMode.RecoilComp or BindingMode.RapidFireRecoil;
            RapidOn.IsChecked = binding.Mode is BindingMode.RapidFire or BindingMode.RapidFireRecoil;
        }
        else
        {
            MacroKeyCombo.SelectedIndex = 0; // default Left
        }
        MacroKeyCombo.SelectionChanged += (_, _) => { ApplyUiToSettings(); SyncMacroProfile(); };
        foreach (var name in Settings.Presets.Keys) PresetCombo.Items.Add(name);
        PresetCombo.SelectedItem = "Knives Out — Bot Body";
        RefreshLabels();
        _loading = false;
    }

    void WireEvents()
    {
        foreach (var sl in new[] { HMin, HMax, SMin, SMax, VMin, VMax, Fov, Gain, MaxStep, Deadzone, MinPixels, RecoilStrength, RecoilHorizontal, RapidRpm })
            sl.ValueChanged += (_, _) => { ApplyUiToSettings(); RefreshLabels(); SyncMacroProfile(); };

        PresetCombo.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            if (Settings.Presets.TryGetValue((string)PresetCombo.SelectedItem, out var p))
            {
                HMin.Value = p.HMin; HMax.Value = p.HMax;
                SMin.Value = p.SMin; SMax.Value = p.SMax;
                VMin.Value = p.VMin; VMax.Value = p.VMax;
            }
        };

        EnabledCheck.Checked += (_, _) => SetEnabled(true);
        EnabledCheck.Unchecked += (_, _) => SetEnabled(false);
        RecoilOn.Checked += (_, _) => { ApplyUiToSettings(); SyncMacroProfile(); };
        RecoilOn.Unchecked += (_, _) => { ApplyUiToSettings(); SyncMacroProfile(); };
        RapidOn.Checked += (_, _) => { ApplyUiToSettings(); SyncMacroProfile(); };
        RapidOn.Unchecked += (_, _) => { ApplyUiToSettings(); SyncMacroProfile(); };

        _engine.Stats += () => Dispatcher.Invoke(UpdateStats);
    }

    void ApplyUiToSettings()
    {
        if (_loading) return;
        var b = _s.ColorBand;
        b.HMin = (int)HMin.Value; b.HMax = (int)HMax.Value;
        b.SMin = (int)SMin.Value; b.SMax = (int)SMax.Value;
        b.VMin = (int)VMin.Value; b.VMax = (int)VMax.Value;
        _s.Fov = (int)Fov.Value; _s.GainPct = (int)Gain.Value; _s.MaxStep = (int)MaxStep.Value;
        _s.Deadzone = (int)Deadzone.Value; _s.MinPixels = (int)MinPixels.Value;
        _s.RecoilOn = RecoilOn.IsChecked == true;
        _s.RecoilStrength = Math.Round(RecoilStrength.Value, 1);
        _s.RecoilHorizontal = (int)RecoilHorizontal.Value;
        _s.RapidOn = RapidOn.IsChecked == true;
        _s.RapidRpm = (int)RapidRpm.Value;
        _s.Save();
    }

    /// <summary>Push the on-screen macro controls into the MacroEngine profile
    /// (single macro driver) and persist to SQLite.</summary>
    void SyncMacroProfile()
    {
        if (_loading || App.CurrentProfile is null) return;
        var profile = App.CurrentProfile;
        bool recoil = RecoilOn.IsChecked == true;
        bool rapid = RapidOn.IsChecked == true;

        // Trigger from combo: 0=Left, 1=Side5, 2=Side6
        var trigger = MacroKeyCombo.SelectedIndex switch
        {
            1 => MouseButton.Side1,
            2 => MouseButton.Side2,
            _ => MouseButton.Left
        };

        // One binding handles the chosen trigger.
        var binding = profile.Bindings.FirstOrDefault();
        if (binding is null)
        {
            binding = new ButtonBinding();
            profile.Bindings.Add(binding);
        }
        binding.Trigger = trigger;
        binding.FireRateRpm = Math.Clamp((int)RapidRpm.Value, 1, 5000);
        binding.Mode = (recoil, rapid) switch
        {
            (true, true) => BindingMode.RapidFireRecoil,
            (true, false) => BindingMode.RecoilComp,
            (false, true) => BindingMode.RapidFire,
            (false, false) => BindingMode.Default
        };
        profile.RecoilStrength = Math.Round(RecoilStrength.Value, 2);
        profile.Humanize = new HumanizeSettings { Enabled = true };

        App.Macros.SetProfile(profile);
        App.Db.SaveProfile(profile);
    }

    void RefreshLabels()
    {
        HMinV.Text = ((int)HMin.Value).ToString();
        HMaxV.Text = ((int)HMax.Value).ToString();
        SMinV.Text = ((int)SMin.Value).ToString();
        SMaxV.Text = ((int)SMax.Value).ToString();
        VMinV.Text = ((int)VMin.Value).ToString();
        VMaxV.Text = ((int)VMax.Value).ToString();
        FovV.Text = ((int)Fov.Value).ToString();
        GainV.Text = ((int)Gain.Value).ToString() + "%";
        MaxStepV.Text = ((int)MaxStep.Value).ToString() + " px";
        DeadzoneV.Text = ((int)Deadzone.Value).ToString() + " px (hold ×1.6)";
        MinPixelsV.Text = ((int)MinPixels.Value).ToString();
        RecoilVals.Text = $"×{_s.RecoilStrength:0.0} multiplier   h={(int)RecoilHorizontal.Value} px/shot";
        RapidVals.Text = $"{(int)RapidRpm.Value} RPM (weapon fire rate)";
        UpdateModeLabel();
    }

    void UpdateModeLabel()
    {
        bool recoil = RecoilOn.IsChecked == true;
        bool rapid = RapidOn.IsChecked == true;
        string mode = (recoil, rapid) switch
        {
            (true, true) => "COMBINED — per-shot synced pulls + rapid fire",
            (true, false) => "RECOIL — per-shot at fire rate",
            (false, true) => "CLICKS ONLY — rapid fire",
            (false, false) => "OFF"
        };
        ModeLabel.Text = "MODE: " + mode;
        ModeLabel.Foreground = new Wm.SolidColorBrush(
            (recoil || rapid) ? Wm.Color.FromRgb(0x00, 0xE6, 0x76) : Wm.Color.FromRgb(0x6B, 0x75, 0x85));
    }

    // ── enable / state ────────────────────────────────────────────

    void ToggleEnabled() => Dispatcher.Invoke(() => EnabledCheck.IsChecked = EnabledCheck.IsChecked != true);

    void SetEnabled(bool on)
    {
        if (on && (App.CurrentLicense is not { IsUsable: true }))
        {
            StatusText.Text = "LICENSE REQUIRED — sign in / trial to activate";
            MessageText.Text = "Sign in to activate. Free 1-hour trial on new accounts.";
            return;
        }
        ApplyUiToSettings();
        if (on) _engine.Start(_s); else _engine.Stop();
        // AegisMouse macro subsystem: hooks stay live at all times; this gates dispatch.
        App.Macros.SetEnabled(on);
        StateDot.Fill = new Wm.SolidColorBrush(on ? Wm.Color.FromRgb(0x22, 0xC5, 0x5E) : Wm.Color.FromRgb(0xEF, 0x44, 0x44));
        App.Overlay.SetState(on);
        _tray?.SetState(on);
        StatusText.Text = on ? "ON — scanning for targets" : "OFF";
    }

    void UpdateStats()
    {
        FpsText.Text = _engine.Fps.ToString();
        TargetText.Text = _engine.Locked ? "LOCKED" : "none";
        if (EnabledCheck.IsChecked == true)
            StatusText.Text = _engine.Locked ? "ON — target locked" : "ON — scanning for targets";
    }

    // ── tray / lifecycle ──────────────────────────────────────────

    void CreateTray()
    {
        _tray = new Tray(
            open: () => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); }),
            toggle: ToggleEnabled,
            quit: () => Dispatcher.Invoke(() => { _quit = true; Close(); System.Windows.Application.Current.Shutdown(); }));
        App.SetTray(_tray);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_quit) { e.Cancel = true; Hide(); return; }
        Hotkey.Unregister();
        _engine.Dispose();
        _tray?.Dispose();
        base.OnClosing(e);
    }
}
