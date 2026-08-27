using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wm = System.Windows.Media;

namespace AutoAimLock;

/// <summary>Animated splash shown first, then routed to Login (or straight to main
/// if a valid cached session exists).</summary>
public sealed class SplashWindow : Window
{
    public static void ShowThen(Func<Window> next)
    {
        var splash = new SplashWindow();
        splash.Loaded += async (_, _) =>
        {
            await Task.Delay(1700);
            splash.Close();
            next().Show();
        };
        splash.Show();
    }

    SplashWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AllowsTransparency = true;
        Background = Wm.Brushes.Transparent;
        Width = 440; Height = 260;

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        // Animated crosshair logo
        var logo = new Canvas { Width = 120, Height = 120, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        var ring = new System.Windows.Shapes.Ellipse
        {
            Width = 90, Height = 90,
            Stroke = new Wm.SolidColorBrush(Wm.Color.FromRgb(0x00, 0xE6, 0x76)),
            StrokeThickness = 3
        };
        Canvas.SetLeft(ring, 15); Canvas.SetTop(ring, 15);
        logo.Children.Add(ring);
        for (int i = 0; i < 4; i++)
        {
            var arm = new System.Windows.Shapes.Rectangle
            {
                Width = 5, Height = 26,
                Fill = new Wm.SolidColorBrush(Wm.Color.FromRgb(0x00, 0xE6, 0x76))
            };
            switch (i)
            {
                case 0: Canvas.SetLeft(arm, 57); Canvas.SetTop(arm, 0); break;
                case 1: Canvas.SetLeft(arm, 57); Canvas.SetTop(arm, 94); break;
                case 2: Canvas.SetLeft(arm, 0); Canvas.SetTop(arm, 47); break;
                case 3: Canvas.SetLeft(arm, 115); Canvas.SetTop(arm, 47); break;
            }
            logo.Children.Add(arm);
        }
        stack.Children.Add(logo);

        stack.Children.Add(new TextBlock
        {
            Text = "AUTOAIMLOCK",
            Foreground = new Wm.SolidColorBrush(Wm.Color.FromRgb(0x00, 0xE6, 0x76)),
            FontSize = 28, FontWeight = FontWeights.Bold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "stability-first aim & macro suite",
            Foreground = new Wm.SolidColorBrush(Wm.Color.FromArgb(160, 0xE2, 0xE8, 0xF0)),
            FontSize = 12, HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 8)
        });

        var track = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = new Wm.SolidColorBrush(Wm.Color.FromArgb(60, 255, 255, 255)), Margin = new Thickness(60, 0, 60, 0) };
        var fill = new Border { Width = 0, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, CornerRadius = new CornerRadius(2), Background = new Wm.SolidColorBrush(Wm.Color.FromRgb(0x00, 0xE6, 0x76)) };
        track.Child = fill;
        stack.Children.Add(track);

        var root = new Border
        {
            Background = new Wm.SolidColorBrush(Wm.Color.FromArgb(245, 11, 14, 19)),
            BorderBrush = new Wm.SolidColorBrush(Wm.Color.FromArgb(120, 0x00, 0xE6, 0x76)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24), Child = stack
        };
        Content = root;

        // Progress bar animation
        fill.BeginAnimation(Border.WidthProperty, new DoubleAnimation(320, TimeSpan.FromMilliseconds(1500))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        // Spin the ring
        ring.RenderTransform = new RotateTransform();
        ring.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2))
        { RepeatBehavior = RepeatBehavior.Forever });
        // Fade in
        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
    }
}
