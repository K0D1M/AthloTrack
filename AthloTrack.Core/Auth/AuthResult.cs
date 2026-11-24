namespace AthloTrack.Core.Auth;

public sealed record AuthResult(bool Success, string? ErrorMessage, Guid? UserId, string? AccessToken, string? RefreshToken)
{
    public static AuthResult Fail(string message) => new(false, message, null, null, null);
    public static AuthResult Ok(Guid userId, string accessToken, string refreshToken) =>
        new(true, null, userId, accessToken, refreshToken);

    /// <summary>A saved sign-in that couldn't be checked: no internet. The app may open from its offline copy.</summary>
    public bool IsOffline { get; init; }

    public static AuthResult Offline(Guid userId) =>
        new(false, "Χωρίς σύνδεση στο internet.", userId, null, null) { IsOffline = true };
}
