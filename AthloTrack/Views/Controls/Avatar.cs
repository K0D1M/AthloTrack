using System;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AthloTrack.Views.Controls;

/// <summary>
/// Circular athlete avatar: the photo when <see cref="Photo"/> has bytes, otherwise the
/// <see cref="Initial"/> on brand blue. Size it with Width/Height.
/// </summary>
public class Avatar : Border
{
    public static readonly StyledProperty<byte[]?> PhotoProperty =
        AvaloniaProperty.Register<Avatar, byte[]?>(nameof(Photo));

    public static readonly StyledProperty<string?> InitialProperty =
        AvaloniaProperty.Register<Avatar, string?>(nameof(Initial));

    /// <summary>Tapping the photo shows it large (<see cref="AthloTrack.Services.PhotoPreview"/>).</summary>
    public static readonly StyledProperty<bool> CanPreviewProperty =
        AvaloniaProperty.Register<Avatar, bool>(nameof(CanPreview));

    // Decoded bitmaps keyed by the byte array, so list re-renders don't re-decode.
    private static readonly ConditionalWeakTable<byte[], Bitmap> Cache = new();

    private readonly Image _image = new() { Stretch = Stretch.UniformToFill };
    private readonly TextBlock _initial = new()
    {
        Foreground = Brushes.White,
        FontWeight = FontWeight.Bold,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public Avatar()
    {
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.Parse("#1565C0"));
        Child = new Panel { Children = { _initial, _image } };
        // The photo usually arrives after the row: fade it in over the blue instead of popping.
        _image.Opacity = 0;
        _image.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(180) },
        };
    }

    public byte[]? Photo
    {
        get => GetValue(PhotoProperty);
        set => SetValue(PhotoProperty, value);
    }

    public string? Initial
    {
        get => GetValue(InitialProperty);
        set => SetValue(InitialProperty, value);
    }

    public bool CanPreview
    {
        get => GetValue(CanPreviewProperty);
        set => SetValue(CanPreviewProperty, value);
    }

    protected override void OnTapped(Avalonia.Input.TappedEventArgs e)
    {
        base.OnTapped(e);
        if (CanPreview && Photo is { Length: > 0 } photo)
        {
            AthloTrack.Services.PhotoPreview.Show(photo);
            e.Handled = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PhotoProperty || change.Property == CanPreviewProperty)
        {
            Cursor = CanPreview && Photo is { Length: > 0 } ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) : null;
        }

        if (change.Property == PhotoProperty)
        {
            _image.Source = ToBitmap(Photo);
            _initial.IsVisible = _image.Source is null;
            _image.Opacity = _image.Source is null ? 0 : 1;
        }
        else if (change.Property == InitialProperty)
        {
            _initial.Text = Initial;
        }
        else if (change.Property == BoundsProperty)
        {
            var side = System.Math.Min(Bounds.Width, Bounds.Height);
            if (side > 0)
            {
                CornerRadius = new CornerRadius(side / 2);
                _initial.FontSize = System.Math.Max(8, side * 0.42);
            }
        }
    }

    internal static Bitmap? ToBitmap(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        if (Cache.TryGetValue(bytes, out var cached)) return cached;
        try
        {
            var bitmap = new Bitmap(new MemoryStream(bytes));
            Cache.AddOrUpdate(bytes, bitmap);
            return bitmap;
        }
        catch
        {
            return null; // unreadable image: fall back to the initial
        }
    }
}
