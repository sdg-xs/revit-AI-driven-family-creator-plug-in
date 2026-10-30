namespace FamilyCreator.Auth.Tests;

public class SignInFailureTests
{
    [Fact]
    public void BrowserOnlyErrorRetainsOnlySafeDiagnostics()
    {
        var result = SignInFailure.FromBrowserText("Auth login details\nrequest_id: f04f7775-4522-4f13-8d01-bfbe5f7e0676\nerror_code: 3p_delegated_access_policy_denied\naccess_token: SECRET");
        Assert.Contains("workspace administrator", result.Message);
        Assert.Contains("f04f7775-4522-4f13-8d01-bfbe5f7e0676", result.Message);
        Assert.DoesNotContain("SECRET", result.Message);
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":\"3p_delegated_access_policy_denied\",\"message\":\"SECRET\"}}")]
    [InlineData("{\"error_code\":\"3p_delegated_access_policy_denied\"}")]
    public void TokenExchangePreservesPolicyDenialWithoutRawResponse(string body)
    {
        var result = SignInFailure.FromResponse(body, "req_safe123");
        Assert.Contains(SignInFailure.PolicyDenied, result.Message);
        Assert.Contains("req_safe123", result.Message);
        Assert.DoesNotContain("SECRET", result.Message);
    }

    [Theory]
    [InlineData("not json SECRET")]
    [InlineData("{\"error\":\"SECRET\"}")]
    [InlineData("[]")]
    public void UnknownErrorsDoNotExposeResponseContent(string body)
    {
        var result = SignInFailure.FromResponse(body, "SECRET");
        Assert.DoesNotContain("SECRET", result.Message);
    }
}
