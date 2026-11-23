using System.Text;
using AthloTrack.Core.Supabase;

namespace AthloTrack.Tests;

/// <summary>
/// «JWT expired» after an hour in the background: the client factory reads the token's expiry to
/// renew it before a request.
/// </summary>
public sealed class SessionRefreshTests
{
    private static string Jwt(string payloadJson)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{B64(payloadJson)}.signature";
    }

    [Fact]
    public void Reads_the_expiry_of_a_token()
    {
        var exp = new DateTimeOffset(2026, 11, 23, 21, 35, 0, TimeSpan.Zero);

        Assert.Equal(exp, SupabaseClientFactory.ExpiresAt(Jwt($"{{\"sub\":\"x\",\"exp\":{exp.ToUnixTimeSeconds()}}}")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    public void Unreadable_tokens_have_no_expiry(string? token)
    {
        Assert.Null(SupabaseClientFactory.ExpiresAt(token));
    }

    [Fact]
    public void A_token_without_exp_has_no_expiry()
    {
        Assert.Null(SupabaseClientFactory.ExpiresAt(Jwt("{\"sub\":\"x\"}")));
    }

    [Fact]
    public void Base64url_payloads_of_any_length_are_read()
    {
        // Lengths that need 0, 1 and 2 padding characters.
        foreach (var name in new[] { "a", "ab", "abc" })
        {
            Assert.NotNull(SupabaseClientFactory.ExpiresAt(Jwt($"{{\"n\":\"{name}\",\"exp\":1800000000}}")));
        }
    }
}
