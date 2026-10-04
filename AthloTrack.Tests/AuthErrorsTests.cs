using AthloTrack.Core.Auth;

namespace AthloTrack.Tests;

public class AuthErrorsTests
{
    [Fact]
    public void Wrong_password_body_becomes_greek()
    {
        var ex = new Exception("""{"code":400,"error_code":"invalid_credentials","msg":"Invalid login credentials"}""");
        Assert.Equal("Λάθος email ή κωδικός.", AuthErrors.Describe(ex));
    }

    [Fact]
    public void Unknown_json_body_is_not_shown_raw()
    {
        var ex = new Exception("""{"code":500,"error_code":"unexpected_failure","msg":"boom"}""");
        Assert.DoesNotContain("{", AuthErrors.Describe(ex));
    }

    [Fact]
    public void Network_failure_says_so()
    {
        Assert.Contains("internet", AuthErrors.Describe(new HttpRequestException("no route")));
    }
}
