namespace FitTrack.Core.Supabase;

/// <summary>
/// Project URL and public anon key from the Supabase dashboard (Project Settings &gt; API).
/// Populated per-platform (e.g. from a config file or embedded resource) — never hardcode
/// a service_role key here, only the anon key, since RLS is what enforces access control.
/// </summary>
public sealed class SupabaseConfig
{
    public required string Url { get; init; }
    public required string AnonKey { get; init; }
}
