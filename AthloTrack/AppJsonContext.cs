using System.Text.Json.Serialization;
using AthloTrack.Core.Supabase;

namespace AthloTrack;

/// <summary>
/// Source-generated JSON metadata. Reflection-based System.Text.Json is disabled under
/// WebAssembly (JsonSerializerIsReflectionDisabled), so deserializing through this context
/// is what lets the browser head read its config.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(SupabaseConfig))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
