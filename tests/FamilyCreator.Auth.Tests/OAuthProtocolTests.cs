using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FamilyCreator.Auth.Tests;

public class OAuthProtocolTests
{
    [Fact]
    public void PolicyDenialExplainsWorkspaceRestrictionAndKeepsSupportId()
    {
        var error = Assert.Throws<AuthException>(() => OAuthProtocol.ValidateCallback(
            "?state=expected&error=access_denied&error_code=3p_delegated_access_policy_denied&request_id=f04f7775-4522-4f13-8d01-bfbe5f7e0676", "expected", null));
        Assert.Contains("workspace administrator", error.Message);
        Assert.Contains("f04f7775-4522-4f13-8d01-bfbe5f7e0676", error.Message);
        Assert.DoesNotContain("Please try again", error.Message);
    }

    [Fact]
    public void PkceMatchesRfc7636Vector() => Assert.Equal(
        "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
        OAuthProtocol.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void NewRegistrationUsesIssuedClientId()
    {
        var result = OAuthProtocol.ValidateCallback("?state=expected&code=code&client_id=oaiapp_123", "expected", null);
        Assert.Equal("oaiapp_123", result.ClientId);
        Assert.Equal("code", result.Code);
    }

    [Theory]
    [InlineData("?state=wrong&code=code&client_id=oaiapp_123")]
    [InlineData("?state=expected&code=code")]
    [InlineData("?state=expected&code=code&client_id=dynamic_agent_client")]
    [InlineData("?state=expected&state=other&code=code&client_id=oaiapp_123")]
    [InlineData("?state=expected&error=access_denied")]
    public void RejectsInvalidCallback(string query) => Assert.Throws<AuthException>(() =>
        OAuthProtocol.ValidateCallback(query, "expected", null));

    [Fact]
    public void ReturningRegistrationCannotSwitchClient()
    {
        Assert.Throws<AuthException>(() => OAuthProtocol.ValidateCallback(
            "?state=s&code=c&client_id=other", "s", "original"));
        Assert.Equal("original", OAuthProtocol.ValidateCallback("?state=s&code=c", "s", "original").ClientId);
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("nonce", false)]
    [InlineData("issuer", false)]
    [InlineData("audience", false)]
    [InlineData("expired", false)]
    [InlineData("signature", false)]
    [InlineData("subject", false)]
    public async Task ValidatesSignedIdentityBeforeAcceptingAccount(string variant, bool valid)
    {
        using var rsa = RSA.Create(2048);
        using var otherRsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test" };
        var signingKey = variant == "signature" ? new RsaSecurityKey(otherRsa) { KeyId = "test" } : key;
        var jwt = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = variant == "issuer" ? "https://wrong.example" : OAuthProtocol.Issuer,
            Audience = variant == "audience" ? "wrong" : "client",
            IssuedAt = DateTime.UtcNow.AddHours(-2), NotBefore = DateTime.UtcNow.AddHours(-2),
            Expires = variant == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = variant == "subject" ? "" : "user", ["nonce"] = variant == "nonce" ? "wrong" : "nonce", ["email"] = "user@example.com" },
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        });
        if (valid)
            Assert.Equal("user", (await IdentityValidator.ValidateAsync(jwt, "client", "nonce", [key])).Subject);
        else
            await Assert.ThrowsAsync<AuthException>(() => IdentityValidator.ValidateAsync(jwt, "client", "nonce", [key]));
    }
}
