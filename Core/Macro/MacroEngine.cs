using System.Collections.Concurrent;
using AutoAimLock.Core;
using AutoAimLock.Interop;
using AutoAimLock.Timing;

namespace AutoAimLock.Core.Macro;

/// <summary>
/// Orchestrates bindings: the low-level hook detects real input and the engine
/// dispatches rapid-fire / burst / hold-macro / recoil-comp / rapid-recoil /
/// key-remap behaviors with precise ms timing. Same architecture as AegisMouse.
/// </summary>
public sealed class MacroEngine : IDisposable
{
    readonly GlobalHook _hook = new();
    readonly Humanizer _humanizer = new();
    readonly ConcurrentDictionary<MouseButton, CancellationTokenSource> _active = new();
    readonly HashSet<MouseButton> _held = new();

    GameProfile? _profile;
    volatile bool _enabled = true;

    public event Action<string>? InputEvent;
    public event Action? StateChanged;
    public event Action<int, bool>? KeyEvent;
    public event Action<MouseButton, bool>? ButtonEvent;

    public bool Enabled => _enabled;

    public MacroEngine()
    {
        _hook.Button += OnMouseButton;
        _hook.Key += (vk, down) => KeyEvent?.Invoke(vk, down);
    }

    public void Start() => _hook.Start();

    public void SetProfile(GameProfile? profile)
    {
        CancelAll();
        _profile = profile;
        _hook.ShouldSuppress = profile is null ? null
            : b => _enabled && profile.Bindings.Any(x => x.Trigger == b && x.Mode == BindingMode.KeyRemap);
        StateChanged?.Invoke();
    }

    public void SetEnabled(bool enabled)
    {
        if (!enabled) CancelAll();
        _enabled = enabled;
        StateChanged?.Invoke();
    }

    void OnMouseButton(MouseButton button, bool down)
    {
        lock (_held) { if (down) _held.Add(button); else _held.Remove(button); }
        if (!down) Cancel(button);
        ButtonEvent?.Invoke(button, down);
        if (!_enabled || _profile is null) return;

        var binding = _profile.Bindings.FirstOrDefault(x => x.Trigger == button);
        if (binding is null || binding.Mode == BindingMode.Default) return;

        if (down)
        {
            switch (binding.Mode)
            {
                case BindingMode.RapidFire: StartLoop(button, () => RapidFireLoop(binding)); break;
                case BindingMode.BurstFire: StartLoop(button, () => BurstFire(binding)); break;
                case BindingMode.HoldMacro: StartLoop(button, () => HoldMacro(binding)); break;
                case BindingMode.RecoilComp: if (Armed(binding)) StartLoop(button, () => RecoilLoop(binding)); break;
                case BindingMode.RapidFireRecoil: if (Armed(binding)) StartLoop(button, () => RapidFireRecoil(binding)); break;
                case BindingMode.KeyRemap: if (binding.RemapVk != 0) NativeInput.KeyDown(binding.RemapVk); break;
            }
            InputEvent?.Invoke($"{button} down → {binding.Mode}");
        }
        else
        {
            if (binding.Mode == BindingMode.KeyRemap && binding.RemapVk != 0) NativeInput.KeyUp(binding.RemapVk);
            Cancel(button);
        }
    }

    bool Armed(ButtonBinding b) => b.ArmButton is null || NativeInput.IsDown(b.ArmButton.Value);
    bool Held(MouseButton b) { lock (_held) return _held.Contains(b); }

    void StartLoop(MouseButton button, Action body)
    {
        Cancel(button);
        var cts = new CancellationTokenSource();
        _active[button] = cts;
        var t = new Thread(() =>
        {
            try { body(); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log.Warning(ex, "Macro loop {Button}", button); }
            finally
            {
                if (_active.TryGetValue(button, out var cur) && ReferenceEquals(cur, cts)) _active.TryRemove(button, out _);
            }
        }) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = $"Macro-{button}" };
        t.Start();
    }

    void RapidFireLoop(ButtonBinding b)
    {
        int interval = Math.Clamp(60000 / Math.Max(1, b.FireRateRpm), 1, 5000);
        var ct = _active.TryGetValue(b.Trigger, out var c) ? c.Token : CancellationToken.None;
        var h = _profile?.Humanize ?? new HumanizeSettings();
        while (Held(b.Trigger) && !ct.IsCancellationRequested)
        {
            NativeInput.ButtonDown(MouseButton.Left);
            WaitMs(Math.Max(1, _humanizer.Hold(8, h)), ct);
            NativeInput.ButtonUp(MouseButton.Left);
            WaitMs(_humanizer.Delay(interval, h), ct);
        }
    }

    void BurstFire(ButtonBinding b)
    {
        int interval = Math.Clamp(60000 / Math.Max(1, b.FireRateRpm), 1, 5000);
        var ct = _active.TryGetValue(b.Trigger, out var c) ? c.Token : CancellationToken.None;
        var h = _profile?.Humanize ?? new HumanizeSettings();
        for (int i = 0; i < Math.Max(1, b.BurstCount) && !ct.IsCancellationRequested; i++)
        {
            NativeInput.ButtonDown(MouseButton.Left);
            WaitMs(Math.Max(1, _humanizer.Hold(8, h)), ct);
            NativeInput.ButtonUp(MouseButton.Left);
            if (i < b.BurstCount - 1) WaitMs(_humanizer.Delay(interval, h), ct);
        }
    }

    void HoldMacro(ButtonBinding b)
    {
        var ct = _active.TryGetValue(b.Trigger, out var c) ? c.Token : CancellationToken.None;
        new MacroPlayer().Play(b.Actions, _profile?.Humanize ?? new HumanizeSettings(), ct);
    }

    void RecoilLoop(ButtonBinding b)
    {
        var profile = _profile;
        if (profile is null || profile.RecoilTable.Count == 0) return;
        int interval = Math.Clamp(60000 / Math.Max(1, b.FireRateRpm), 1, 5000);
        var ct = _active.TryGetValue(b.Trigger, out var c) ? c.Token : CancellationToken.None;
        var h = profile.Humanize;
        double strength = profile.RecoilStrength;

        foreach (var step in profile.RecoilTable)
        {
            WaitMs(_humanizer.Delay(interval, h), ct);
            if (ct.IsCancellationRequested || !Held(b.Trigger)) break;
            if (_humanizer.SkipStep(h)) continue;
            var (jx, jy) = _humanizer.Move((int)Math.Round(step.Dx * strength), (int)Math.Round(step.Dy * strength), h);
            NativeInput.MoveRelative(jx, jy);
        }
    }

    void RapidFireRecoil(ButtonBinding b)
    {
        var profile = _profile;
        if (profile is null) return;
        if (profile.RecoilTable.Count == 0) { RapidFireLoop(b); return; }
        int interval = Math.Clamp(60000 / Math.Max(1, b.FireRateRpm), 1, 5000);
        var ct = _active.TryGetValue(b.Trigger, out var c) ? c.Token : CancellationToken.None;
        var h = profile.Humanize;
        double strength = profile.RecoilStrength;
        int stepIndex = 0;

        while (Held(b.Trigger) && !ct.IsCancellationRequested)
        {
            NativeInput.ButtonDown(MouseButton.Left);
            WaitMs(Math.Max(1, _humanizer.Hold(8, h)), ct);
            NativeInput.ButtonUp(MouseButton.Left);
            if (ct.IsCancellationRequested || !Held(b.Trigger)) break;
            if (!_humanizer.SkipStep(h))
            {
                var step = profile.RecoilTable[stepIndex % profile.RecoilTable.Count];
                var (jx, jy) = _humanizer.Move((int)Math.Round(step.Dx * strength), (int)Math.Round(step.Dy * strength), h);
                NativeInput.MoveRelative(jx, jy);
            }
            stepIndex++;
            WaitMs(_humanizer.Delay(interval, h), ct);
        }
    }

    public void CancelAll()
    {
        foreach (var kv in _active) { try { kv.Value.Cancel(); } catch { } }
        _active.Clear();
    }

    void Cancel(MouseButton b)
    {
        if (_active.TryRemove(b, out var cts)) try { cts.Cancel(); } catch { }
    }

    static void WaitMs(int ms, CancellationToken ct)
    {
        var deadline = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var rem = deadline - Environment.TickCount64;
            if (rem > 4) Thread.Sleep(1); else Thread.SpinWait(40);
        }
    }

    public void Dispose()
    {
        _hook.Button -= OnMouseButton;
        CancelAll();
        _hook.Dispose();
    }
}