# AUTOAIMLOCK

Screen-based aim enhancement tool for Windows. Uses real-time screen capture (BitBlt) and HSV color detection to identify targets, then applies smooth Bezier-curve or physics-based mouse movement toward them.

**OS-level only** — Win32 SendInput for mouse movement, BitBlt for screen capture. No game memory reading, no GPU hooks, no process injection. Works with any game in borderless/windowed mode.

## Features

- **Real-time screen capture** — BitBlt GDI capture centered on the crosshair
- **HSV color detection** — configurable color ranges with presets for common games
- **Dual movement algorithms** — PID controller (snappy, for FPS flick shots) and WindMouse physics model (gravity/wind/friction, natural trajectories for TPS tracking)
- **FOV radius** — only targets within a configurable pixel radius of the crosshair are tracked
- **Humanization** — movement jitter to reduce robotic patterns
- **Trigger modes** — right mouse hold, left mouse hold, always on, or F8 toggle
- **F8 global hotkey** — toggle aim assist on/off from any window
- **JSON settings persistence** — settings saved to `%AppData%\AUTOAIMLOCK\settings.json` (no database needed)
- **Dark terminal UI** — Arch Linux-inspired aesthetic

## Prerequisites

1. **Windows 10 or 11** (64-bit)
2. **.NET 8 SDK** — download from https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify: `dotnet --version` should show 8.x.x
3. **Visual Studio 2022** (recommended) OR the .NET CLI alone
   - If using VS: install the ".NET desktop development" workload
   - If using CLI: no VS needed, just the SDK above

## Build Instructions

### Option A: Visual Studio 2022

1. Clone the repository:
   ```
   git clone https://github.com/owlzeroday/AutoAimLock.git
   cd AutoAimLock
   ```
2. Open `AUTOAIMLOCK.sln` in Visual Studio 2022
3. Set `AUTOAIMLOCK.App` as the startup project (right-click → Set as Startup Project)
4. Press `F5` to build and run, or `Ctrl+Shift+B` to build only

### Option B: .NET CLI

1. Clone and build:
   ```
   git clone https://github.com/owlzeroday/AutoAimLock.git
   cd AutoAimLock
   dotnet restore
   dotnet build -c Release
   ```
2. Run:
   ```
   dotnet run --project src/AUTOAIMLOCK.App -c Release
   ```
3. Or run the compiled exe directly:
   ```
   src/AUTOAIMLOCK.App/bin/Release/net8.0-windows/AUTOAIMLOCK.App.exe
   ```

### Option C: Publish a standalone exe

```
dotnet publish src/AUTOAIMLOCK.App -c Release -r win-x64 --self-contained
```
Output: `src/AUTOAIMLOCK.App/bin/Release/net8.0-windows/win-x64/publish/AUTOAIMLOCK.App.exe`
This produces a single .exe that doesn't require .NET to be installed on the target machine.

## How to Use

1. **Launch the app** — the dashboard tab shows live FPS, target lock status, and FOV visualization
2. **Pick a color preset** (Detection tab) — choose the preset that matches your target's outline/health bar color
3. **Tune HSV range** (Detection tab) — fine-tune the hue/saturation/value sliders if the preset doesn't catch everything
4. **Set FOV radius** (Aim tab) — how far from the crosshair the assist will look for targets
5. **Set smoothing** (Aim tab) — lower = snappier, higher = smoother/slower
6. **Pick a movement algorithm** (Aim tab):
   - **PID** — fast, precise, low-latency (best for FPS flick shots)
   - **WindMouse** — physics-based, natural trajectories (best for TPS tracking)
7. **Choose trigger mode** (Aim tab) — right mouse hold (ADS), toggle (F8), or always on
8. **Enable humanization** (Aim tab) — adds jitter for anti-detection
9. **Press F8** to toggle the aim assist on/off at any time

### Settings persistence
All settings are automatically saved to:
```
%AppData%\AUTOAIMLOCK\settings.json
```
They load automatically on startup. No database required.

## FPS / TPS Tuning Guide

### FPS (First-Person Shooters)
- **Algorithm**: PID
- **Smoothing**: 20-40 (snappy for flick shots)
- **FOV radius**: 100-150px (tight crosshair tracking)
- **Trigger**: Right mouse hold (activates on ADS)
- **Capture size**: 300-400px (fast capture)
- **Color preset**: Red (Enemy) for red outlines/health bars

### TPS (Third-Person Shooters)
- **Algorithm**: WindMouse
- **Smoothing**: 50-70 (smooth tracking)
- **FOV radius**: 200-300px (wider target acquisition)
- **Trigger**: Always on or Toggle (F8)
- **Capture size**: 500-600px (wider view)
- **Humanize**: ON with 2-3px jitter

### Knives Out (NetEase) — ⚠️ Special Setup Required
Knives Out is a TPS battle royale with **no built-in enemy outlines**. Unlike Valorant or Overwatch where enemies glow red, you need a different detection approach.

**Use Quick Game Setup** (Detection tab → dropdown) to apply all settings at once:

| Preset | Mode | How it works | FOV | Smoothing | Min Pixels |
|--------|------|-------------|-----|-----------|------------|
| Knives Out — Hit Assist | Hit Marker | Detects red hit flash → aim correction | 80px | 55 | 12 |
| Knives Out — Squad Nametag | Nametag | Detects red nametags above enemies (squad) | 250px | 60 | 8 |
| Knives Out — Damage Track | Damage Numbers | Tracks floating red damage numbers mid-combat | 120px | 50 | 10 |

**Knives Out — Hit Assist (recommended)**
- Tightest detection: H 0-8, S 200-255, V 200-255 — only catches the very bright, saturated hit marker flash
- Small FOV (80px) — only tracks near the crosshair, avoids red UI elements at screen edges
- Low min pixels (12) — hit markers are tiny clusters (10-25px), not full player outlines
- WindMouse algorithm for natural TPS tracking
- The detector now also rejects targets at screen edges (health bars, minimap, kill feed) and wide-flat clusters (UI strips)

**Knives Out — Squad Nametag**
- Medium saturation red (S 160-230) — catches nametag text but not bright effects
- Wider FOV (250px) — nametags appear above enemies at various distances
- Very low min pixels (8) — text clusters are small

**Knives Out — Damage Track**
- Bright red-orange (H 2-12, S 180-255, V 190-255) — damage numbers are bright
- Medium FOV (120px) — damage numbers appear near the enemy's body
- Higher jitter (3px) — more natural during active combat

**Detection enhancements added:**
- **Edge rejection** — ignores targets within 30px of the capture border (filters out minimap, health bars, kill feed)
- **Aspect ratio filter** — rejects wide-flat clusters (likely UI strips, not compact targets)
- **Tighter HSV ranges** — Knives Out presets use S 200+ / V 200+ to only catch the actual hit marker flash, not duller red UI elements

**Important:** Knives Out PC client must be set to **borderless windowed mode** for screen capture to work. Check Settings → Graphics → Display Mode.

### Common color presets
| Preset | Hue Range | Best for |
|--------|-----------|----------|
| Red (Enemy) | 0-10 | Valorant, Apex red outlines |
| Red (Wide) | 0-20 | Broader red detection |
| Orange | 10-30 | Overwatch enemy outlines |
| Magenta | 140-170 | Purple/magenta outlines |
| Cyan | 85-105 | Blue/cyan outlines |
| Knives Out — Hit Marker | 0-8 (S200+/V200+) | Bright hit flash only |
| Knives Out — Hit Marker (Wide) | 0-10 (S170+/V170+) | Wider hit detection |
| Knives Out — Nametag | 0-12 (S160-230) | Squad mode nametags |
| Knives Out — Blood | 5-18 | Enemy hit blood effect |
| Knives Out — Damage Numbers | 2-12 (S180+/V190+) | Floating damage numbers |
| PUBG — Hit Marker | 0-10 | PUBG hit markers |
| Free Fire — Hit Marker | 0-10 | Free Fire hit markers |
| Skin/White Tone | 0-25 | Player model detection (no outlines) |

## Architecture

```
AUTOAIMLOCK.sln
├── src/AUTOAIMLOCK.Engine/          # Win32 mouse input (SendInput)
│   ├── MouseEngine.cs              # Relative mouse movement
│   └── Interop/Win32.cs            # P/Invoke declarations
├── src/AUTOAIMLOCK.Core/             # Detection + aim logic
│   ├── AimService.cs               # Main loop: capture → detect → move
│   ├── SettingsStore.cs            # JSON settings persistence (no DB)
│   ├── Screen/ScreenCapture.cs    # BitBlt screen capture
│   ├── Detection/TargetDetector.cs # HSV color detection + clustering
│   ├── Aim/AimController.cs        # PID smoothing + Bezier paths
│   ├── Aim/WindMouse.cs            # Physics-based movement (gravity/wind/friction)
│   └── Models/AimSettings.cs       # Configuration models + color presets
└── src/AUTOAIMLOCK.App/              # WPF UI
    ├── MainWindow.xaml             # Dashboard, Detection, Aim, Settings tabs
    ├── ModernChrome.cs             # Dark mode window chrome
    ├── GlobalHotkey.cs             # F8 system hotkey
    ├── App.xaml                    # Terminal-themed resource dictionary
    └── ViewModels/
        ├── MainViewModel.cs        # MVVM bindings + settings sync
        └── BoolToTextConverter.cs  # Status display converter
```

## No Database

AutoAimLock does NOT use SQLite or any database. Settings are stored as a simple JSON file in `%AppData%\AUTOAIMLOCK\settings.json`. This keeps the app lightweight with zero external dependencies beyond .NET 8.

## v1.3.0 — Premium UI, tray + HUD overlay, splash, exe metadata

- **Premium UI refinements:** fully dark-templated ComboBox so the dropdown list is readable (dark popup + light text instead of the white-on-white that made dropdown text invisible).
- **Compact fixed-size window:** now semi-small, fixed, non-resizable — behaves like a proper tool panel.
- **Dynamic version/about:** the status bar and About card now show the real version from the exe (was hardcoded "v1.0"). The exe carries full AssemblyInfo metadata (Product, Company, Description, Copyright, FileVersion).
- **System tray integration:** hide-to-tray on close (the app keeps running for the aim engine), tray icon with tooltip showing ASSIST ON/OFF, context menu (Open Setup / Toggle Assist / Quit), double-click to reopen.
- **In-game HUD overlay:** small always-on-top indicator pinned to the bottom-left of the screen showing "AUTOAIMLOCK: ON/OFF" — so you can see the assist state without the main window.
- **Animated splash/loading screen:** product logo + name with fade-in and an animated progress bar on startup.

## v1.2.9 — Macro engine hardening

Bugs found & fixed in the macro engine (reviewed + dry-run validated):

- **Fixed: stuck fire button on cancel (CRITICAL).** Rapid-fire previously sent a synthetic `down` and, if the loop was cancelled mid-pulse, skipped the matching `up` — leaving the game firing uncontrollably. Every pulse now guarantees its `up` via `try/finally`.
- **Fixed: rapid-fire cadence was inaccurate.** The click-hold time was added on top of the full shot interval, making actual RPM slower than configured. The shot period is now exact (validated: 600→600, 300→300, 1000→1000 RPM).
- **Fixed: same-button rapid-fire aborted after one shot.** A synthetic `up` makes `GetAsyncKeyState` report the button up even while physically held, stopping the train. Added a **release-debounce** (~60ms) so the tool only treats the fire button as released after it reads up consistently — real release still stops it, synthetic pulses no longer do.
- **Enhanced: smooth sub-stepped recoil.** No-recoil displacement is applied in up to 4 small increments instead of one hard jump, easing the crosshair down and avoiding a jerk. Exact total preserved (no rounding drift).
- **Added** middle-button (`0x04`) support in the mouse engine.

Tests: dry-run harness validated RPM accuracy, release-debounce behavior, and recoil sub-step totals (all exact).

## v1.2.8 — Macros (no-recoil + rapid-fire) & aim stabilization

- **New: Macros card on the Aim tab.**
  - **No-recoil** (`EnableNoRecoil` + vertical/horizontal pull px/shot): while you hold fire, the tool applies a compensating mouse pull that counters the gun's upward kick, keeping the lock stable during spray.
  - **Rapid-fire** (`EnableRapidFire` + RPM): while you hold fire, it re-clicks at the set rate — for semi/bolt weapons that need fast clicks.
  - Both run on a **separate background thread** (new `MacroEngine`) so their timing never perturbs the aim loop. They only act when the aim-assist trigger is engaged AND the fire button is physically held.
- **New: Aim stability slider** (Aim tab, Movement card). Smooths the detected target centroid frame-to-frame with an EMA, damping the micro-shiver of the aim point while locked. Higher = steadier (a touch more lag); 0 = off.
- Mouse engine upgraded with `SetButton(down/up)` so macros can synthesize clicks at the OS level.
- Defaults: no-recoil off, rapid-fire off, pull 2px, RPM 600, stability 0.5. Update `settings.json` or flip the checkboxes.

## v1.2.7 — Kill the lock-on shake (proportional aim control)

Researched how production no-recoil/aimbot tools implement smooth locking (TaFFe proportional-control article, Sunone aimbot, colorbot overshoot guides) and applied the consensus technique.

**Root cause of the violent shake:** both movement algorithms kept a **velocity memory** (low-pass filter / wind random-walk). When the crosshair reaches the target and the tiny detected offset flips sign frame-to-frame, the accumulated velocity resists and swings the other way → oscillation that never settles.

**Fixes:**
- **AimController → pure proportional control.** Movement is now `error × gain` with NO velocity accumulator. This converges geometrically and mathematically cannot overrun the target (the same model as production aimbots). Removing the low-pass filter removes the ringing.
- **Deadzone checked BEFORE predictive lead.** The crosshair now stops moving when it's actually on the target — previously the lead applied first could push the cursor off the target and cause re-acquisition jitter.
- **Lead velocity capped to 50% of current error** so prediction can't overshoot and shake the aim.
- **WindMouse — settle zone.** Below ~4px the physics brake hard (velocity ×0.35/frame) and wind goes to zero, so the cursor *settles* on the target instead of swinging around it. Wind is also damped in the 4–10px braking zone.
- **Jitter is distance-scaled** in both algorithms — it only applies while clearly off-target and scales down toward zero as the crosshair approaches, so the random "humanization" can't rattle a locked target.

The result: the crosshair now glides onto the target and holds still (or tracks smoothly with the lead), instead of vibrating.

## v1.2.6 — Auto-lock on moving targets (velocity lead + sticky tracking)

- **Detection and tracking now run EVERY frame**, even while the trigger is not held. The lock is kept "hot" — when you ADS, acquisition is instant instead of waiting for the first detection frame.
- **Velocity lead prediction.** The target's movement velocity is tracked across frames and the aim point is predicted ahead by **Target lead (ms)** (new Aim tab slider, default 60). The crosshair now rides MOVING bodies instead of always lagging behind — strafing/running targets stay locked.
- **Sticky target preference.** The last tracked position is fed back to the detector, so the lock stays "magnetic" to ONE body instead of frame-to-frame switching to whatever is nearest the crosshair (the cause of wild rotation).
- **Hit-marker + body combined lock.** While firing, the red blood/hit flash confirms the hit point and the body silhouette anchors the lock — the two bands cooperate instead of competing.
- **Optimized default profile** (code defaults + `Knives Out — Hit Assist` preset + `settings.json`): FOV 160, Smoothing 35, Capture 400, Min pixels 12, Max move 14, Deadzone 5, **Lead 60ms**, WindMouse, Both trigger, body band H95-120/S40-95/V180-245. Body band is broader than before because humanoid shape-validation (not color tightness) is what rejects walls.

## v1.2.5 — Fix false positive locks on walls/scenery (body-shape validation)

- **Body color band tightened to the real bot color.** Verified calibration: training bots are RGB(157,194,204) = **HSV H104 / S60 / V206**. The body band is now a tight box around that (H98-115 / S45-90 / V185-235) instead of the previous wide H90-120/S40-255/V140-255, which was matching walls and terrain.
- **New: shape validation for body clusters.** A cluster only registers as a target if it actually looks like a humanoid silhouette — rejecting the geometry that walls/scenery produce:
  - extreme horizontal strips (wall tops, health bars, long edges) — width > 4× height
  - extreme vertical strips (building edges, lampposts, tall narrow walls) — height > 4× width
  - sparse/scattered clusters (fill ratio < 0.12 → terrain at an angle)
  - near-solid blocks (fill ratio > 0.85 → a wall face filling its bounding box)
  - silhouettes that are wider than half their height (a humanoid is taller than wide; a wall lying across the view is not)
- Hit-marker cluster detection is unchanged — those are tiny, dense flashes near the crosshair and pass all filters.

The result: body detection now only fires on real player/bot silhouettes. Pointing at a wall produces no target, so no recoil/rotation.

## v1.2.4 — Mid/long-range lock (dual-band detection)

- **New: dual-band detection.** The detector now accepts a **secondary body band** in addition to the hit-marker band. A pixel counts toward a target cluster if it matches *either* band. This locks the crosshair onto the enemy *character model* (blue-gray uniform, default H90-120 / S≥40 / V≥140) even at mid/long range, where the hit-marker flash alone is too small to satisfy `MinTargetPixels`.
- New **Body Detection** card on the Quick Game Setup tab: enable/disable toggle + its own HSV sliders (Body hue/sat/val).
- `Knives Out — Hit Assist` preset now includes the body band, so re-applying the profile keeps dual-band enabled.
- Older configs are normalized on load — the body band is added automatically, so mid/long range works without a manual reset.

## v1.2.3

- **Dropdown list text now readable** — the app-wide dark theme forced near-white text, which was invisible against the default (light) popup background in every dropdown. ComboBox items now use a light background with black text, plus a light hover/selection highlight.
- **Defaults now match your working setup** — on a fresh install (or if `settings.json` is missing), the app starts with the exact tuned profile you verified in-game: FOV 120, Smoothing 30, Capture 350, Min pixels 40, trigger **Both (L or R)**, algorithm **WindMouse**, Max move 12px, Deadzone 5px, jitter 2px, step 1ms, and the **Knives Out — Hit Marker** color preset. The Trigger, Algorithm, Game profile, and Color preset dropdowns open pre-selected to your applied values.

## Bug Fixes (v1.2.2) — Aim stability overhaul

- **Max move per frame cap** (new setting, default 12px) — movement is now hard-clamped per axis per frame. Previously a large offset produced a huge single-frame SendInput move (violent fling), and the velocity low-pass filter could ring/oscillate around the target ("wild" aim, sometimes refusing to settle on the target).
- **Deadzone** (new setting, default 3px) — once the crosshair reaches the target, the assist stops pushing instead of hunting around it in tiny oscillations.
- **FOV measured from the actual crosshair**, not the bitmap center — when the cursor was near a screen edge, the capture was clamped but the FOV check still used the center, so valid targets just outside the (wrong) center FOV were rejected ("sometimes won't lock").
- **Target persistence (40ms)** — if detection flickers for a frame or two, the last known target position is kept instead of dropping the lock and stuttering.
- **Multi-monitor capture** — screen metrics now use the virtual screen (all monitors). Previously only the primary screen was measured, so on a secondary monitor the capture was misaligned and the aim pulled toward the wrong position.
- **Loop CPU floor** — minimum 2ms step delay so the loop can't spin a full CPU core while ADS.

## Bug Fixes (v1.2.1)

- **F8 toggle sometimes dead** — the old implementation used `RegisterHotKey`, which silently fails when another application already owns the F8 hotkey (and the result was never checked), and depended on the WPF `HwndSource` hook being installed at the right lifecycle moment. Replaced with a **low-level keyboard hook (WH_KEYBOARD_LL)** — the same mechanism used by AegisMouse — which fires regardless of window focus or hotkey conflicts. If the hook ever fails to install, a message box now tells you (use the Enabled checkbox as fallback).
- Installer version bump (1.2.1).

## Bug Fixes (v1.2)

- **CRITICAL: crash on startup** — the F8 hotkey was registered in the `MainWindow` constructor, but the window handle doesn't exist yet at that point (`Handle` is zero), causing `HwndSource.FromHwnd` to throw and the app to die with a XamlParseException on every launch. Registration now happens in `OnSourceInitialized` where the HWND is valid.
- **Unused `System.Windows.Media` reference** in `AUTOAIMLOCK.Core` — the Core class library doesn't reference WPF; removed the stray using in `AimSettings.cs`.
- **Missing `<AllowUnsafeBlocks>`** — the pixel scanner uses `unsafe` pointer access but the Core project never enabled unsafe compilation.
- **Variable name collision in cluster detection** — BFS neighbor loop variables `dx`/`dy` collided with the offset locals of the same name in the enclosing scope (CS0136). Renamed to `nx`/`ny`.
- **Ambiguous `Application` reference** — with both WPF and WinForms enabled, `Application` was ambiguous between `System.Windows.Application` and `System.Windows.Forms.Application`. Qualified explicitly.
- **Ambiguous `Brush`/`Color` references** — same WinForms/WPF ambiguity in `MainWindow.xaml.cs`; qualified with `System.Windows.Media.`. Removed the unused tray-icon field.
- `publish/` added to `.gitignore`.

## Bug Fixes (v1.1)

- **GetAsyncKeyState return type** — was marshaled as `bool` but should be `short`. The `& 0x8000` check would silently fail since you can't bitwise AND a bool. Fixed to return `short`.
- **Toggle mode** — `_active` field was never set to `true`, so Toggle trigger mode never actually activated. Removed the field — Toggle now delegates to the F8 hotkey (which already handles on/off).
- **Screen capture edge clamping** — when cursor was near screen edges, capture coordinates went negative, causing offset/wrong captures. Now clamped to screen bounds.
- **Crosshair offset bug** — after edge clamping, the crosshair is no longer at the bitmap center, but the detector assumed it was. Now `ScreenCapture.CrosshairInBitmap` tracks the actual crosshair position.
- **Bitmap format mismatch** — `Image.FromHbitmap` could return 32bpp bitmaps on some systems, but `TargetDetector.LockBits` expected 24bpp. Could crash on high-DPI or multi-monitor setups. Now converts to 24bpp explicitly.
- **BothHold trigger** — new trigger mode: either left OR right mouse button held activates the assist.

## Legal Note

Automated aim assistance may violate the terms of service of some games. Use responsibly — only on games and servers that allow assistive tools, or for accessibility purposes. Never for cheating in competitive play.

