using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AUTOAIMLOCK.App.ViewModels;
using AUTOAIMLOCK.Engine.Interop;
using WpfMedia = System.Windows.Media;
using WpfShapes = System.Windows.Shapes;
using WpfControls = System.Windows.Controls;

namespace AUTOAIMLOCK.App;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm = new();
    TrayIcon? _tray;
    bool _forceQuit;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // Keep tray/overlay/title synced with the assist on/off state
        _vm.PropertyChanged += OnVmChanged;

        // FOV visualization redraw on settings change
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.FovRadius)
                or nameof(MainViewModel.TargetDistance)
                or nameof(MainViewModel.TargetLocked))
                Dispatcher.BeginInvoke(DrawFov);
        };

        Loaded += (_, _) => DrawFov();
        Loaded += (_, _) => CreateTray();
    }

    void OnVmChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Enabled))
        {
            App.Overlay?.UpdateState(_vm.Enabled);
            _tray?.UpdateState();
        }
    }

    void CreateTray()
    {
        if (_tray != null) return;
        _tray = new TrayIcon(
            showWindow: () => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); }),
            toggleEnabled: () => Dispatcher.Invoke(() => _vm.Enabled = !_vm.Enabled),
            isEnabled: () => _vm.Enabled,
            quit: () => Dispatcher.Invoke(() => { _forceQuit = true; Close(); System.Windows.Application.Current.Shutdown(); }));
        _tray.UpdateState();
        App.SetTray(_tray);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        ModernChrome.Apply(hwnd);
        Background = FindResource("Bg") as WpfMedia.Brush;
        GlobalHotkey.Register();
        GlobalHotkey.HotkeyPressed += OnHotkey;
    }

    void OnHotkey(int vk)
    {
        if (vk == 0x77) // F8
            Dispatcher.Invoke(() => _vm.Enabled = !_vm.Enabled);
    }

    // Hide-to-tray: closing the window hides it (the app keeps running for the
    // aim engine). Use "Quit" in the tray to actually exit.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceQuit)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        GlobalHotkey.Unregister();
        _tray?.Dispose();
        App.SetTray(null);
        base.OnClosed(e);
    }

    void DrawFov()
    {
        var canvas = FovCanvas;
        canvas.Children.Clear();

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w < 10 || h < 10) return;

        double cx = w / 2;
        double cy = h / 2;

        double maxRadius = Math.Min(w, h) / 2 - 20;
        double scale = maxRadius / 400;
        double fovR = _vm.FovRadius * scale;

        var gridBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0x14, 0x18, 0x1F));
        for (int i = 0; i < w; i += 30)
            canvas.Children.Add(new WpfShapes.Line { X1 = i, Y1 = 0, X2 = i, Y2 = h, Stroke = gridBrush, StrokeThickness = 0.5 });
        for (int i = 0; i < h; i += 30)
            canvas.Children.Add(new WpfShapes.Line { X1 = 0, Y1 = i, X2 = w, Y2 = i, Stroke = gridBrush, StrokeThickness = 0.5 });

        var crossBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0x6B, 0x75, 0x85));
        canvas.Children.Add(new WpfShapes.Line { X1 = cx - 12, Y1 = cy, X2 = cx + 12, Y2 = cy, Stroke = crossBrush, StrokeThickness = 1.5 });
        canvas.Children.Add(new WpfShapes.Line { X1 = cx, Y1 = cy - 12, X2 = cx, Y2 = cy + 12, Stroke = crossBrush, StrokeThickness = 1.5 });

        var accent = FindResource("Accent") as WpfMedia.Brush;
        var fovCircle = new WpfShapes.Ellipse
        {
            Width = fovR * 2,
            Height = fovR * 2,
            Stroke = accent,
            StrokeThickness = 1.5,
            StrokeDashArray = new WpfMedia.DoubleCollection { 4, 3 }
        };
        WpfControls.Canvas.SetLeft(fovCircle, cx - fovR);
        WpfControls.Canvas.SetTop(fovCircle, cy - fovR);
        canvas.Children.Add(fovCircle);

        if (_vm.TargetLocked && _vm.TargetDistance > 0)
        {
            double angle = DateTime.Now.Millisecond / 1000.0 * Math.PI * 2;
            double dist = Math.Min(_vm.TargetDistance * scale, fovR);
            double tx = cx + Math.Cos(angle) * dist;
            double ty = cy + Math.Sin(angle) * dist;

            var dot = new WpfShapes.Ellipse { Width = 8, Height = 8, Fill = FindResource("Danger") as WpfMedia.Brush };
            WpfControls.Canvas.SetLeft(dot, tx - 4);
            WpfControls.Canvas.SetTop(dot, ty - 4);
            canvas.Children.Add(dot);

            canvas.Children.Add(new WpfShapes.Line
            {
                X1 = cx, Y1 = cy, X2 = tx, Y2 = ty,
                Stroke = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(120, 0xEF, 0x44, 0x44)),
                StrokeThickness = 1,
                StrokeDashArray = new WpfMedia.DoubleCollection { 2, 2 }
            });
        }
    }
}
