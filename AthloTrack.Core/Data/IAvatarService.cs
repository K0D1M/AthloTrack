namespace AthloTrack.Core.Data;

/// <summary>
/// Athlete profile photos in the private Supabase Storage bucket "avatars".
/// Objects live at "{athleteId}/...", which is what the storage RLS policies key on.
/// Returns raw image bytes so view models stay UI-framework-neutral.
/// </summary>
public interface IAvatarService
{
    /// <summary>Uploads a new photo and returns its storage path (store it on athletes.profile_image_path).</summary>
    Task<string> UploadAsync(Guid athleteId, byte[] image, string contentType);

    /// <summary>The photo's bytes, or null when there is no photo or it can't be fetched. Cached per path.</summary>
    Task<byte[]?> GetAsync(string? path);

    /// <summary>Deletes a stored photo. Missing objects are ignored.</summary>
    Task RemoveAsync(string? path);
}
