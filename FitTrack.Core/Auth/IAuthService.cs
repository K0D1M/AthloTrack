namespace FitTrack.Core.Auth;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(string email, string password);

    /// <summary>Restores a previously saved session (silent auto-login) if one is stored.</summary>
    Task<AuthResult> RestoreSessionAsync();

    Task SignOutAsync();
}
