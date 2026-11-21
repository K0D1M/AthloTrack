namespace AthloTrack.Core.Auth;

/// <summary>A link code was asked for: whether the login's email matches an athlete profile.</summary>
/// <param name="Match">An unclaimed athlete profile has this login's email: a code was sent.</param>
/// <param name="Coach">That athlete's coach, for «Ο προπονητής σου, …».</param>
public sealed record LinkCodeResult(bool Match, string? Coach = null, string? Error = null);

/// <param name="Remaining">Tries left for this code; 0 means ask for a new one.</param>
public sealed record LinkVerifyResult(bool Linked, string? Error = null, int? Remaining = null);

/// <summary>
/// Account emails through the Edge Function «email» (supabase/functions/email): a code for a new
/// password, and the code that links a login to the athlete profile with its email.
/// </summary>
public interface IAccountService
{
    /// <summary>Sends a 6-digit code for a new password. Says nothing about whether the account exists.</summary>
    Task<AuthResult> SendResetCodeAsync(string email);

    /// <summary>Checks the code, signs in and sets the new password (the session is saved).</summary>
    Task<AuthResult> ResetPasswordAsync(string email, string code, string newPassword);

    /// <summary>For the signed-in login: sends a link code if an athlete profile has its email.</summary>
    Task<LinkCodeResult> SendLinkCodeAsync();

    /// <summary>Links the signed-in login to that athlete profile when the code is right.</summary>
    Task<LinkVerifyResult> VerifyLinkCodeAsync(string code);
}
