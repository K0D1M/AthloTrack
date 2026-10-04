namespace AthloTrack.Core.Auth;

/// <summary>
/// Turns Supabase Auth failures into Greek messages. The client library's exception message is
/// the raw response body, e.g. {"code":400,"error_code":"invalid_credentials","msg":"…"}.
/// </summary>
public static class AuthErrors
{
    private static readonly (string Code, string Message)[] Known =
    [
        ("invalid_credentials", "Λάθος email ή κωδικός."),
        ("email_not_confirmed", "Το email δεν έχει επιβεβαιωθεί ακόμη. Άνοιξε τον σύνδεσμο που σου στάλθηκε."),
        ("user_already_exists", "Υπάρχει ήδη λογαριασμός με αυτό το email. Συνδέσου από το «Αθλητής»."),
        ("email_exists", "Υπάρχει ήδη λογαριασμός με αυτό το email. Συνδέσου από το «Αθλητής»."),
        ("weak_password", "Ο κωδικός είναι πολύ αδύναμος. Διάλεξε μεγαλύτερο ή πιο σύνθετο."),
        ("same_password", "Ο νέος κωδικός πρέπει να διαφέρει από τον τρέχοντα."),
        ("over_email_send_rate_limit", "Πάρα πολλές προσπάθειες. Δοκίμασε ξανά σε λίγα λεπτά."),
        ("over_request_rate_limit", "Πάρα πολλές προσπάθειες. Δοκίμασε ξανά σε λίγα λεπτά."),
    ];

    public static string Describe(Exception ex)
    {
        if (ex is HttpRequestException or TaskCanceledException)
            return "Δεν υπάρχει σύνδεση με τον διακομιστή. Έλεγξε το internet και δοκίμασε ξανά.";

        foreach (var (code, message) in Known)
        {
            if (ex.Message.Contains(code, StringComparison.OrdinalIgnoreCase))
                return message;
        }

        // An unknown error body is no use to the user; a plain message (rare) is kept as is.
        return ex.Message.TrimStart().StartsWith('{') || string.IsNullOrWhiteSpace(ex.Message)
            ? "Κάτι πήγε στραβά. Δοκίμασε ξανά."
            : ex.Message;
    }
}
