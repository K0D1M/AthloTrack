using System;
using System.IO;
using SkiaSharp;

namespace AthloTrack.Services;

/// <summary>
/// Turns a picked photo into a small square avatar: EXIF orientation applied (phone photos
/// are otherwise often sideways), centre-cropped, scaled to <see cref="Size"/>px, JPEG-encoded.
/// </summary>
public static class PhotoProcessor
{
    public const int Size = 400;
    public const string ContentType = "image/jpeg";

    /// <returns>JPEG bytes, or null when the file isn't a readable image.</returns>
    public static byte[]? ToAvatarJpeg(byte[] source)
    {
        using var codec = SKCodec.Create(new MemoryStream(source));
        if (codec is null) return null;

        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null) return null;

        using var image = SKImage.FromBitmap(bitmap);
        var (degrees, mirror) = Transform(codec.EncodedOrigin);
        var quarterTurn = degrees is 90 or 270;

        // Dimensions as the photo should appear, after rotating.
        var shownWidth = quarterTurn ? bitmap.Height : bitmap.Width;
        var shownHeight = quarterTurn ? bitmap.Width : bitmap.Height;
        var scale = (float)Size / Math.Min(shownWidth, shownHeight); // cover the square

        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Translate(Size / 2f, Size / 2f);
        canvas.RotateDegrees(degrees);
        if (mirror) canvas.Scale(-1, 1);
        canvas.Scale(scale);
        var half = new SKRect(-bitmap.Width / 2f, -bitmap.Height / 2f, bitmap.Width / 2f, bitmap.Height / 2f);
        canvas.DrawImage(image, half, new SKSamplingOptions(SKCubicResampler.Mitchell));

        using var result = surface.Snapshot();
        using var data = result.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data?.ToArray();
    }

    // Rotation (clockwise) and horizontal mirror needed to display each EXIF origin upright.
    private static (float Degrees, bool Mirror) Transform(SKEncodedOrigin origin) => origin switch
    {
        SKEncodedOrigin.TopRight => (0, true),
        SKEncodedOrigin.BottomRight => (180, false),
        SKEncodedOrigin.BottomLeft => (180, true),
        SKEncodedOrigin.LeftTop => (90, true),
        SKEncodedOrigin.RightTop => (90, false),
        SKEncodedOrigin.RightBottom => (270, true),
        SKEncodedOrigin.LeftBottom => (270, false),
        _ => (0, false),
    };
}
