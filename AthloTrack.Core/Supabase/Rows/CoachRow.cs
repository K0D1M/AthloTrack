using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AthloTrack.Core.Supabase.Rows;

[Table("coaches")]
public sealed class CoachRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("auth_user_id")]
    public Guid AuthUserId { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("email")]
    public string? Email { get; set; }

    [Column("profile_image_path")]
    public string? ProfileImagePath { get; set; }

    [Column("must_set_password")]
    public bool MustSetPassword { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
