using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AthloTrack.Views.Controls;

/// <summary>
/// A window over the app: a title, scrollable content and «Κλείσιμο» (also a tap outside, Esc, or
/// Android's back via <see cref="CloseOpen"/>). Used by «Εμφάνιση ολόκληρου» and «Τι νέο υπάρχει».
/// It opens in <see cref="Host"/>, MainView's layer under the PIN lock, so a lock always covers it.
/// </summary>
public static class OverlayDialog
{
    /// <summary>MainView's dialog layer (set while it's on screen); without it, the window's overlay layer.</summary>
    public static Panel? Host { get; set; }

    // One window at a time: a new one closes the old.
    private static Action? s_closeOpen;

    /// <summary>Closes the open window, if any (Android's back does this first).</summary>
    public static bool CloseOpen()
    {
        if (s_closeOpen is not { } close) return false;
        close();
        return true;
    }

    /// <summary>Shows the window; returns what closes it (safe to call twice).</summary>
    public static Action? Show(Visual anchor, string title, Control content, string closeText = "Κλείσιμο")
    {
        Panel? host = Host;
        var fitToHost = false;
        if (host is null)
        {
            host = OverlayLayer.GetOverlayLayer(anchor);
            fitToHost = true; // the overlay layer sizes nothing for us
        }
        if (host is null) return null;
        s_closeOpen?.Invoke();

        var heading = new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        };
        heading.Bind(TextBlock.ForegroundProperty, heading.GetResourceObservable("HeadingBrush"));

        var scroll = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);

        var close = new Button { Content = closeText, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        close.Classes.Add("admin-dialog");
        Grid.SetRow(close, 2);

        var card = new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20, 18, 20, 16),
            Margin = new Thickness(16, 24),
            MaxWidth = 560,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Children = { heading, scroll, close } },
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("CardBrush"));

        var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(0x73, 0, 0, 0)), Focusable = true, Children = { card } };

        void Fit()
        {
            card.MaxHeight = Math.Max(240, host.Bounds.Height - 48);
            if (!fitToHost) return;
            root.Width = host.Bounds.Width;
            root.Height = host.Bounds.Height;
        }
        void OnHostChanged(object? s, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.BoundsProperty) Fit();
        }

        var closed = false;
        void Close()
        {
            if (closed) return;
            closed = true;
            host.PropertyChanged -= OnHostChanged;
            host.Children.Remove(root);
            if (s_closeOpen == Close) s_closeOpen = null;
        }

        close.Click += (_, _) => Close();
        root.Tapped += (_, e) =>
        {
            if (e.Source == root) Close(); // outside the card
        };
        root.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };

        Fit();
        host.PropertyChanged += OnHostChanged;
        host.Children.Add(root);
        s_closeOpen = Close;
        root.Focus();
        Motion.FadeInPage(root);
        return Close;
    }
}
