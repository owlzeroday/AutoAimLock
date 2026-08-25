using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using AUTOAIMLOCK.Core;
using AUTOAIMLOCK.Core.Models;
using AUTOAIMLOCK.Engine;

namespace AUTOAIMLOCK.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    readonly MouseEngine _mouse = new();
    readonly AimSettings _settings = SettingsStore.Load();
    AimService? _service;
    bool _enabled;
    string _statusText = "Ready — press F8 to toggle";
    int _fps;
    int _targetDistance;
    bool _targetLocked;
    string _selectedPreset = "Knives Out — Hit Marker";

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            OnPropertyChanged();
            EnsureService();
            _service!.Enabled = value;
            StatusText = value ? "Active — tracking targets" : "Ready — press F8 to toggle";
        }
    }

    public int Fps { get => _fps; set { _fps = value; OnPropertyChanged(); } }
    public int TargetDistance { get => _targetDistance; set { _targetDistance = value; OnPropertyChanged(); } }
    public bool TargetLocked { get => _targetLocked; set { _targetLocked = value; OnPropertyChanged(); } }
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    // Settings bindings
    public int FovRadius { get => _settings.FovRadius; set { _settings.FovRadius = value; OnPropertyChanged(); } }
    public int Smoothing { get => _settings.Smoothing; set { _settings.Smoothing = value; OnPropertyChanged(); } }
    public int CaptureSize { get => _settings.CaptureSize; set { _settings.CaptureSize = value; OnPropertyChanged(); } }
    public int MinTargetPixels { get => _settings.MinTargetPixels; set { _settings.MinTargetPixels = value; OnPropertyChanged(); } }
    public bool Humanize { get => _settings.Humanize; set { _settings.Humanize = value; OnPropertyChanged(); } }
    public bool DetectBody
    {
        get => _settings.DetectBody;
        set { _settings.DetectBody = value; OnPropertyChanged(); }
    }
    public bool AutoLock
    {
        get => _settings.AutoLock;
        set { _settings.AutoLock = value; OnPropertyChanged(); }
    }
    public int JitterPx { get => _settings.JitterPx; set { _settings.JitterPx = value; OnPropertyChanged(); } }
    public int LeadMs { get => _settings.LeadMs; set { _settings.LeadMs = value; OnPropertyChanged(); } }
    public int StepDelayMs { get => _settings.StepDelayMs; set { _settings.StepDelayMs = value; OnPropertyChanged(); } }

    // Macro settings
    public bool EnableRapidFire { get => _settings.EnableRapidFire; set { _settings.EnableRapidFire = value; OnPropertyChanged(); } }
    public int RapidFireRpm { get => _settings.RapidFireRpm; set { _settings.RapidFireRpm = value; OnPropertyChanged(); } }
    public bool EnableNoRecoil { get => _settings.EnableNoRecoil; set { _settings.EnableNoRecoil = value; OnPropertyChanged(); } }
    public int RecoilVertical { get => _settings.RecoilVertical; set { _settings.RecoilVertical = value; OnPropertyChanged(); } }
    public int RecoilHorizontal { get => _settings.RecoilHorizontal; set { _settings.RecoilHorizontal = value; OnPropertyChanged(); } }
    public double AimStability { get => _settings.AimStability; set { _settings.AimStability = value; OnPropertyChanged(); } }
    public int MaxMovePx { get => _settings.MaxMovePx; set { _settings.MaxMovePx = value; OnPropertyChanged(); } }
    public int DeadzonePx { get => _settings.DeadzonePx; set { _settings.DeadzonePx = value; OnPropertyChanged(); } }

    // Color settings
    public int HMin { get => _settings.TargetColor.HMin; set { _settings.TargetColor.HMin = value; OnPropertyChanged(); } }
    public int HMax { get => _settings.TargetColor.HMax; set { _settings.TargetColor.HMax = value; OnPropertyChanged(); } }
    public int SMin { get => _settings.TargetColor.SMin; set { _settings.TargetColor.SMin = value; OnPropertyChanged(); } }
    public int SMax { get => _settings.TargetColor.SMax; set { _settings.TargetColor.SMax = value; OnPropertyChanged(); } }
    public int VMin { get => _settings.TargetColor.VMin; set { _settings.TargetColor.VMin = value; OnPropertyChanged(); } }
    public int VMax { get => _settings.TargetColor.VMax; set { _settings.TargetColor.VMax = value; OnPropertyChanged(); } }

    // Body (secondary band) color settings — BodyColor is guaranteed non-null by
    // SettingsStore.Load(); the null-coalescing guards the compiler only.
    public int BodyHMin { get => _settings.BodyColor?.HMin ?? 0; set { if (_settings.BodyColor != null) _settings.BodyColor.HMin = value; OnPropertyChanged(); } }
    public int BodyHMax { get => _settings.BodyColor?.HMax ?? 0; set { if (_settings.BodyColor != null) _settings.BodyColor.HMax = value; OnPropertyChanged(); } }
    public int BodySMin { get => _settings.BodyColor?.SMin ?? 0; set { if (_settings.BodyColor != null) _settings.BodyColor.SMin = value; OnPropertyChanged(); } }
    public int BodySMax { get => _settings.BodyColor?.SMax ?? 0; set { if (_settings.BodyColor != null) _settings.BodyColor.SMax = value; OnPropertyChanged(); } }
    public int BodyVMin { get => _settings.BodyColor?.VMin ?? 0; set { if (_settings.BodyColor != null) _settings.BodyColor.VMin = value; OnPropertyChanged(); } }
    public int BodyVMax { get => _settings.BodyColor?.VMax ?? 0; set { if (_settings.BodyColor != null) _settings.BodyColor.VMax = value; OnPropertyChanged(); } }

    public string SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            _selectedPreset = value;
            OnPropertyChanged();
            if (ColorSettings.Presets.TryGetValue(value, out var preset))
            {
                HMin = preset.HMin; HMax = preset.HMax;
                SMin = preset.SMin; SMax = preset.SMax;
                VMin = preset.VMin; VMax = preset.VMax;
            }
        }
    }

    public IReadOnlyList<string> PresetNames => ColorSettings.Presets.Keys.ToList();
    public IReadOnlyList<string> TriggerModes => ["Right mouse hold", "Left mouse hold", "Both (L or R)", "Always on", "Toggle"];
    public IReadOnlyList<string> GamePresetNames => GamePresets.All.Keys.ToList();

    string _selectedGamePreset = "Knives Out — Hit Assist";
    public string SelectedGamePreset
    {
        get => _selectedGamePreset;
        set
        {
            _selectedGamePreset = value;
            OnPropertyChanged();
            if (GamePresets.All.TryGetValue(value, out var config))
            {
                // Apply full game config — color, FOV, smoothing, algorithm, everything
                HMin = config.Color.HMin; HMax = config.Color.HMax;
                SMin = config.Color.SMin; SMax = config.Color.SMax;
                VMin = config.Color.VMin; VMax = config.Color.VMax;
                FovRadius = config.Settings.FovRadius;
                Smoothing = config.Settings.Smoothing;
                CaptureSize = config.Settings.CaptureSize;
                MinTargetPixels = config.Settings.MinTargetPixels;
                AlgorithmIndex = (int)config.Settings.Algorithm;
                TriggerIndex = (int)config.Settings.Trigger;
                Humanize = config.Settings.Humanize;
                JitterPx = config.Settings.JitterPx;
                StepDelayMs = config.Settings.StepDelayMs;
            }
        }
    }

    int _triggerIndex = 2; // Both (L or R)
    public int TriggerIndex
    {
        get => _triggerIndex;
        set { _triggerIndex = value; _settings.Trigger = (TriggerMode)value; OnPropertyChanged(); }
    }

    int _algorithmIndex = 1; // WindMouse (natural)
    public int AlgorithmIndex
    {
        get => _algorithmIndex;
        set { _algorithmIndex = value; _settings.Algorithm = (MovementAlgorithm)value; OnPropertyChanged(); }
    }

    public IReadOnlyList<string> AlgorithmNames => ["PID (snappy)", "WindMouse (natural)"];

    public string Version
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "1.0" : v.ToString(3);
        }
    }

    public string AppInfo =>
        $"AUTOAIMLOCK v{Version} — screen-based aim enhancement. Real-time screen capture (BitBlt) + HSV color detection, dual-band tracking (body + hit marker), velocity-lead lock, no-recoil & rapid-fire macros.";

    void EnsureService()
    {
        if (_service != null) return;
        _service = new AimService(_mouse, _settings);
        _service.StatsUpdated += (_, _) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Fps = _service.Fps;
                TargetDistance = _service.LastTargetDistance;
                TargetLocked = _service.TargetLocked;
            });
        };
    }

    void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        SettingsStore.Save(_settings);
    }
}
