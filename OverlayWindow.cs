using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wm = System.Windows.Media;

namespace AutoAimLock;

/// <summary>Bottom-left always-on-top ON/OFF badge (in-game state at a glance).</summary>
public sealed class OverlayWindow : Window
{
    readonly TextBlock _label;
    readonly System.Windows.Shapes.Ellipse _dot;

    public OverlayWindow()
    {
        Title = "AutoAimLock Overlay";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Wm.Brushes.Transparent;
        Width = 190;
        Height = 34;

        _dot = new System.Windows.Shapes.Ellipse
        {
            Width = 9, Height = 9,
            Fill = new Wm.SolidColorBrush(Wm.Color.FromRgb(0xEF, 0x44, 0x44)),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _label = new TextBlock
        {
            Text = "AutoAimLock: OFF",
            Foreground = new Wm.SolidColorBrush(Wm.Color.FromRgb(0xE2, 0xE8, 0xF0)),
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var dock = new DockPanel();
        dock.Children.Add(_dot);
        dock.Children.Add(_label);

        Content = new Border
        {
            Background = new Wm.SolidColorBrush(Wm.Color.FromArgb(205, 13, 16, 22)),
            BorderBrush = new Wm.SolidColorBrush(Wm.Color.FromArgb(140, 0x00, 0xE6, 0x76)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 4, 10, 4),
            Child = dock
        };

        var wa = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                 ?? new System.Drawing.Rectangle(0, 0, 1920, 1040);
        Left = wa.Left + 12;
        Top = wa.Bottom - Height - 12;
    }

    public void SetState(bool on)
    {
        Dispatcher.Invoke(() =>
        {
            _label.Text = on ? "AutoAimLock: ON" : "AutoAimLock: OFF";
            _dot.Fill = new Wm.SolidColorBrush(on ? Wm.Color.FromRgb(0x22, 0xC5, 0x5E) : Wm.Color.FromRgb(0xEF, 0x44, 0x44));
        });
    }
}
