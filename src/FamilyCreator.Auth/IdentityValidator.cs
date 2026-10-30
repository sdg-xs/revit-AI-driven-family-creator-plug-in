using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FamilyCreator.Auth;

public sealed record VerifiedIdentity(string Subject, string Email);

public static class IdentityValidator
{
    public static async Task<VerifiedIdentity> ValidateAsync(string token, string clientId, string nonce,
        IEnumerable<SecurityKey> signingKeys)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = OAuthProtocol.Issuer, ValidAudience = clientId,
            IssuerSigningKeys = signingKeys, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
            ValidateIssuerSigningKey = true, RequireSignedTokens = true, RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        });
        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt ||
            !jwt.TryGetClaim("nonce", out var nonceClaim) || nonceClaim.Value != nonce ||
            string.IsNullOrWhiteSpace(jwt.Subject))
            throw new AuthException("OpenAI's sign-in response could not be verified. No account was connected.");
        return new VerifiedIdentity(jwt.Subject, jwt.TryGetClaim("email", out var email) ? email.Value : jwt.Subject);
    }
}
