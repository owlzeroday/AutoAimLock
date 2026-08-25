using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfMedia = System.Windows.Media;

namespace AUTOAIMLOCK.App;

/// <summary>
/// Small, always-on-top overlay HUD pinned to the bottom-left of the primary
/// screen. Shows the aim-assist on/off state at a glance while in-game.
/// Kept compact and out of the way.
/// </summary>
public sealed class AssistOverlay : Window
{
    readonly TextBlock _label;
    readonly Ellipse _dot;

    public AssistOverlay()
    {
        Title = "AUTOAIMLOCK Overlay";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = WpfMedia.Brushes.Transparent;
        Width = 172;
        Height = 34;

        var dock = new DockPanel();
        _dot = new Ellipse
        {
            Width = 9,
            Height = 9,
            Fill = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(255, 0xEF, 0x44, 0x44)),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        dock.Children.Add(_dot);
        _label = new TextBlock
        {
            Text = "AUTOAIMLOCK: OFF",
            Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(255, 0xE2, 0xE8, 0xF0)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        dock.Children.Add(_label);

        var border = new Border
        {
            Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(205, 13, 16, 22)),
            BorderBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(140, 0x00, 0xE6, 0x76)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 4, 10, 4),
            Child = dock
        };
        Content = border;

        // Pin to bottom-left of the primary screen
        var wa = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
        Left = wa.Left + 12;
        Top = wa.Bottom - Height - 12;
    }

    /// <summary>Update the overlay to reflect assist on/off.</summary>
    public void UpdateState(bool enabled)
    {
        Dispatcher.Invoke(() =>
        {
            _label.Text = enabled ? "AUTOAIMLOCK: ON" : "AUTOAIMLOCK: OFF";
            _dot.Fill = new WpfMedia.SolidColorBrush(enabled
                ? WpfMedia.Color.FromArgb(255, 0x22, 0xC5, 0x5E)
                : WpfMedia.Color.FromArgb(255, 0xEF, 0x44, 0x44));
        });
    }

    public void CloseOverlay()
    {
        Dispatcher.Invoke(Close);
    }
}
