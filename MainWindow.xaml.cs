using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        Loaded += (_, _) =>
        {
            Hotkey.Register();
            Hotkey.Pressed += ToggleEnabled;
            CreateTray();
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
        foreach (var name in Settings.Presets.Keys) PresetCombo.Items.Add(name);
        PresetCombo.SelectedItem = "Knives Out — Bot Body";
        RefreshLabels();
        _loading = false;
    }

    void WireEvents()
    {
        foreach (var sl in new[] { HMin, HMax, SMin, SMax, VMin, VMax, Fov, Gain, MaxStep, Deadzone, MinPixels, RecoilStrength, RecoilHorizontal, RapidRpm })
            sl.ValueChanged += (_, _) => { ApplyUiToSettings(); RefreshLabels(); };

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
        RecoilOn.Checked += (_, _) => ApplyUiToSettings();
        RecoilOn.Unchecked += (_, _) => ApplyUiToSettings();
        RapidOn.Checked += (_, _) => ApplyUiToSettings();
        RapidOn.Unchecked += (_, _) => ApplyUiToSettings();

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
        _s.RecoilStrength = (int)RecoilStrength.Value;
        _s.RecoilHorizontal = (int)RecoilHorizontal.Value;
        _s.RapidOn = RapidOn.IsChecked == true;
        _s.RapidRpm = (int)RapidRpm.Value;
        _s.Save();
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
        RecoilVals.Text = $"v={(int)RecoilStrength.Value} px/s   h={(int)RecoilHorizontal.Value} px/s";
        RapidVals.Text = $"{(int)RapidRpm.Value} RPM";
    }

    // ── enable / state ────────────────────────────────────────────

    void ToggleEnabled() => Dispatcher.Invoke(() => EnabledCheck.IsChecked = EnabledCheck.IsChecked != true);

    void SetEnabled(bool on)
    {
        ApplyUiToSettings();
        if (on) _engine.Start(_s); else _engine.Stop();
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
