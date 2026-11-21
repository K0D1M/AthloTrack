using AthloTrack.Core.Supabase;
using Newtonsoft.Json.Linq;
using Supabase.Functions.Exceptions;

namespace AthloTrack.Core.Auth;

public sealed class AccountService : IAccountService
{
    private const string Function = "email";

    private readonly SupabaseClientFactory _clientFactory;
    private readonly ICredentialStore _credentialStore;

    public AccountService(SupabaseClientFactory clientFactory, ICredentialStore credentialStore)
    {
        _clientFactory = clientFactory;
        _credentialStore = credentialStore;
    }

    public async Task<AuthResult> SendResetCodeAsync(string email)
    {
        var (_, error) = await InvokeAsync(new() { ["action"] = "reset", ["email"] = email.Trim() }, signedIn: false);
        return error is null ? new AuthResult(true, null, null, null, null) : AuthResult.Fail(error);
    }

    public async Task<AuthResult> ResetPasswordAsync(string email, string code, string newPassword)
    {
        var client = await _clientFactory.GetClientAsync();
        try
        {
            var session = await client.Auth.VerifyOTP(email.Trim(), Digits(code),
                global::Supabase.Gotrue.Constants.EmailOtpType.Recovery);
            if (session?.AccessToken is null || session.RefreshToken is null || session.User?.Id is null)
                return AuthResult.Fail(WrongResetCode);

            await client.Auth.Update(new global::Supabase.Gotrue.UserAttributes { Password = newPassword });
            // The password change can renew the tokens: save the ones in use now.
            var current = client.Auth.CurrentSession ?? session;
            await _credentialStore.SaveSessionAsync(current.AccessToken!, current.RefreshToken!);
            return AuthResult.Ok(Guid.Parse(session.User.Id), current.AccessToken!, current.RefreshToken!);
        }
        catch (Exception ex)
        {
            var message = AuthErrors.Describe(ex);
            // Supabase answers a wrong or old code with otp_expired / "Token has expired or is invalid".
            if (ex.Message.Contains("otp_expired", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase))
                message = WrongResetCode;
            return AuthResult.Fail(message);
        }
    }

    public const string WrongResetCode = "Λάθος ή ληγμένος κωδικός. Έλεγξε το email ή ζήτα νέο κωδικό.";

    public async Task<LinkCodeResult> SendLinkCodeAsync()
    {
        var (json, error) = await InvokeAsync(new() { ["action"] = "link-code" }, signedIn: true);
        if (error is not null || json is null) return new LinkCodeResult(false, null, error);
        return new LinkCodeResult(json.Value<bool?>("match") == true, json.Value<string?>("coach"));
    }

    public async Task<LinkVerifyResult> VerifyLinkCodeAsync(string code)
    {
        var (json, error) = await InvokeAsync(new() { ["action"] = "link-verify", ["code"] = Digits(code) }, signedIn: true);
        if (error is not null || json is null)
            return new LinkVerifyResult(false, error, json?.Value<int?>("remaining"));
        return new LinkVerifyResult(json.Value<bool?>("linked") == true);
    }

    /// <summary>«481 927» → «481927».</summary>
    public static string Digits(string? code) => new((code ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>The function's JSON, or its { error } (with the JSON, for "remaining").</summary>
    private async Task<(JObject? Json, string? Error)> InvokeAsync(Dictionary<string, object> body, bool signedIn)
    {
        var client = await _clientFactory.GetClientAsync();
        var token = signedIn ? client.Auth.CurrentSession?.AccessToken : null;
        try
        {
            var text = await client.Functions.Invoke(Function, token,
                new global::Supabase.Functions.Client.InvokeFunctionOptions { Body = body });
            return (Parse(text), null);
        }
        catch (FunctionsException ex)
        {
            var json = Parse(ex.Content);
            var message = json?.Value<string?>("error");
            if (ex.StatusCode == 404) message = "Η υπηρεσία email δεν είναι διαθέσιμη ακόμη.";
            return (json, message ?? "Κάτι πήγε στραβά. Δοκίμασε ξανά.");
        }
        catch (Exception ex)
        {
            return (null, AuthErrors.Describe(ex));
        }
    }

    private static JObject? Parse(string? text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : JObject.Parse(text);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }
}
