using System;
using System.IO;
using System.Text.Json;
using AthloTrack.Core.Supabase;

namespace AthloTrack;

/// <summary>
/// WinUI counterpart of the Avalonia head's AppBootstrap. Shared ViewModels call
/// <see cref="IsConfigured"/>, so this type must live in the AthloTrack namespace.
/// </summary>
public static class AppBootstrap
{
    /// <summary>
    /// Reads Assets/supabase.config.json from the app directory (copied at build time
    /// from the Avalonia project so both heads use one config file).
    /// </summary>
    public static SupabaseConfig LoadSupabaseConfig()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "supabase.config.json");
            if (File.Exists(path))
            {
                var config = JsonSerializer.Deserialize<SupabaseConfig>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (config is not null &&
                    !string.IsNullOrWhiteSpace(config.Url) &&
                    !string.IsNullOrWhiteSpace(config.AnonKey))
                {
                    return config;
                }
            }
        }
        catch
        {
            // fall through to placeholder
        }

        return new SupabaseConfig
        {
            Url = "https://YOUR-PROJECT-ref.supabase.co",
            AnonKey = "YOUR-ANON-PUBLIC-KEY",
        };
    }

    /// <summary>True once the config holds real (non-placeholder) values.</summary>
    public static bool IsConfigured(SupabaseConfig config) =>
        !config.Url.Contains("YOUR-PROJECT", StringComparison.OrdinalIgnoreCase) &&
        !config.AnonKey.Contains("YOUR-ANON", StringComparison.OrdinalIgnoreCase);
}
