using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfMedia = System.Windows.Media;

namespace AUTOAIMLOCK.App;

/// <summary>
/// Animated splash/loading screen shown on startup. Displays the product
/// logo + name with a fade-in and progress bar, then closes itself and opens
/// the main window.
/// </summary>
public sealed class SplashWindow : Window
{
    public static void ShowSplashThen(Action afterSplash)
    {
        var splash = new SplashWindow();
        splash.Loaded += async (_, _) =>
        {
            await Task.Delay(1600);
            splash.Close();
            afterSplash();
        };
        splash.Show();
    }

    SplashWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AllowsTransparency = true;
        Background = WpfMedia.Brushes.Transparent;
        Width = 420;
        Height = 240;

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        stack.Children.Add(new TextBlock
        {
            Text = "AUTOAIMLOCK",
            Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(255, 0x00, 0xE6, 0x76)),
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = "screen-based aim enhancement",
            Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(160, 0xE2, 0xE8, 0xF0)),
            FontSize = 13,
            Margin = new Thickness(0, 6, 0, 18),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });

        var barTrack = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(60, 0xFF, 0xFF, 0xFF)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var barFill = new Border
        {
            Width = 0,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(2),
            Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(255, 0x00, 0xE6, 0x76))
        };
        barTrack.Child = barFill;
        stack.Children.Add(barTrack);

        stack.Children.Add(new TextBlock
        {
            Text = "Loading…",
            Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(150, 0x6B, 0x75, 0x85)),
            FontSize = 11,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        });

        var root = new Border
        {
            Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(245, 11, 14, 19)),
            BorderBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(120, 0x00, 0xE6, 0x76)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24),
            Child = stack
        };
        Content = root;

        var anim = new DoubleAnimation(0, 385, TimeSpan.FromMilliseconds(1400))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        barFill.BeginAnimation(Border.WidthProperty, anim);

        Opacity = 0;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350));
        BeginAnimation(OpacityProperty, fade);
    }
}
