namespace AthloTrack.Core.Auth;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(string email, string password);

    /// <summary>Restores a previously saved session (silent auto-login) if one is stored.</summary>
    Task<AuthResult> RestoreSessionAsync();

    /// <summary>
    /// Creates a login. Success with a UserId means signed in; success without one means the
    /// project requires email confirmation first.
    /// </summary>
    Task<AuthResult> SignUpAsync(string email, string password);

    /// <summary>Changes the signed-in user's password.</summary>
    Task<AuthResult> UpdatePasswordAsync(string newPassword);

    Task SignOutAsync();
}
