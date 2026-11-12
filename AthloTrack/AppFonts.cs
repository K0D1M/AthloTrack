using Avalonia;
using Avalonia.Media;

namespace AthloTrack;

/// <summary>
/// The app's typeface: Manrope (Assets/Fonts, five weights, SIL OFL), on every head. Inter stays
/// behind it for the few symbols Manrope doesn't draw (⌫ ✓ ✕ ☐ ▲ ▼ …), and is what the PDFs use.
/// The motto keeps GFS Didot (GreekAccentFont).
/// </summary>
public static class AppFonts
{
    public const string Manrope = "avares://AthloTrack/Assets/Fonts#Manrope";
    public const string Inter = "fonts:Inter#Inter";

    public static AppBuilder WithAppFonts(this AppBuilder builder) =>
        builder
            .WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = Manrope,
                FontFallbacks = [new FontFallback { FontFamily = new FontFamily(Inter) }],
            });
}
