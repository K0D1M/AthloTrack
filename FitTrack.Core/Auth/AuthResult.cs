namespace FitTrack.Core.Auth;

public sealed record AuthResult(bool Success, string? ErrorMessage, Guid? UserId, string? AccessToken, string? RefreshToken)
{
    public static AuthResult Fail(string message) => new(false, message, null, null, null);
    public static AuthResult Ok(Guid userId, string accessToken, string refreshToken) =>
        new(true, null, userId, accessToken, refreshToken);
}
