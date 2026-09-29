using System;
using System.IO;
using System.Text.Json;
using Avalonia.Platform;
using AthloTrack.Core.Supabase;
using Microsoft.Extensions.DependencyInjection;

namespace AthloTrack;

/// <summary>
/// Configuration and platform-service hooks each head (Desktop/Browser/Android) fills in
/// before Avalonia starts. Keeps the shared UI project free of platform-specific references.
/// </summary>
public static class AppBootstrap
{
    /// <summary>
    /// Each head sets this to register its platform-specific services
    /// (notably the <c>ICredentialStore</c>) into the DI container.
    /// </summary>
    public static Action<IServiceCollection>? RegisterPlatformServices { get; set; }

    /// <summary>
    /// Loads the Supabase config from the embedded asset Assets/supabase.config.json.
    /// Edit that file with your Project URL and anon key (Supabase dashboard &gt; Settings &gt; API).
    /// </summary>
    public static SupabaseConfig LoadSupabaseConfig()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://AthloTrack/Assets/supabase.config.json"));
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            // Source-generated metadata: reflection-based System.Text.Json is disabled under WASM.
            var config = JsonSerializer.Deserialize(json, AppJsonContext.Default.SupabaseConfig);

            if (config is not null && !string.IsNullOrWhiteSpace(config.Url) && !string.IsNullOrWhiteSpace(config.AnonKey))
            {
                return config;
            }
        }
        catch (Exception ex)
        {
            // Surfaces in the browser devtools console / debug output.
            Console.WriteLine($"[AthloTrack] Failed to load supabase.config.json: {ex.GetType().Name}: {ex.Message}");
        }

        // Placeholder — the app still runs and shows the login screen, but sign-in will
        // fail with a network error until Assets/supabase.config.json is filled in.
        return new SupabaseConfig
        {
            Url = "https://YOUR-PROJECT-ref.supabase.co",
            AnonKey = "YOUR-ANON-PUBLIC-KEY",
        };
    }

    /// <summary>True once the config has real (non-placeholder) values.</summary>
    public static bool IsConfigured(SupabaseConfig config) =>
        !config.Url.Contains("YOUR-PROJECT", StringComparison.OrdinalIgnoreCase) &&
        !config.AnonKey.Contains("YOUR-ANON", StringComparison.OrdinalIgnoreCase);
}
