using System.Security.Cryptography;
using System.Text;

namespace FamilyCreator.Auth;

public sealed class AuthException(string message) : Exception(message);
public sealed record AuthorizationCode(string Code, string ClientId);

public static class OAuthProtocol
{
    public const string Issuer = "https://auth.openai.com";
    public const string Resource = "https://api.openai.com/v1";
    public const string AppName = "Revit Family Creator";
    public static string RandomValue() => Base64Url(RandomNumberGenerator.GetBytes(32));
    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static AuthorizationCode ValidateCallback(string query, string state, string? existingClient)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
            if (!values.TryAdd(Decode(pair[0]), pair.Length == 2 ? Decode(pair[1]) : ""))
                throw new AuthException("The sign-in callback contained duplicate parameters. Please try again.");
        }
        if (!values.TryGetValue("state", out var returnedState) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(returnedState)))
            throw new AuthException("The sign-in callback did not match this attempt. Please try again.");
        if (values.ContainsKey("error") || values.ContainsKey("error_code"))
            throw SignInFailure.FromProvider(values.GetValueOrDefault("error_code") ?? values.GetValueOrDefault("error"), values.GetValueOrDefault("request_id"));
        var clientId = values.GetValueOrDefault("client_id") ?? existingClient;
        if (string.IsNullOrWhiteSpace(clientId) || clientId == "dynamic_agent_client" ||
            (existingClient is not null && clientId != existingClient))
            throw new AuthException("The returned account registration was missing or did not match. Please try again.");
        if (!values.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new AuthException("Sign-in did not return an authorization code.");
        return new AuthorizationCode(code, clientId);
    }

    public static Uri AuthorizationUri(string hostId, string redirectUri, string state, string nonce,
        string verifier, AccountRegistration? existing)
    {
        var values = new Dictionary<string, string>
        {
            ["client_id"] = existing?.ClientId ?? "dynamic_agent_client",
            ["ext_agent_host_id"] = hostId, ["response_type"] = "code", ["redirect_uri"] = redirectUri,
            ["scope"] = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct",
            ["resource"] = Resource, ["state"] = state, ["nonce"] = nonce,
            ["code_challenge_method"] = "S256", ["code_challenge"] = Challenge(verifier)
        };
        if (existing is null) values["agent_name_hint"] = AppName;
        else
        {
            if (existing.Tokens is not null) values["id_token_hint"] = existing.Tokens.IdToken;
            if (!string.IsNullOrWhiteSpace(existing.Email)) values["login_hint"] = existing.Email;
        }
        return new Uri(Issuer + "/api/accounts/authorize?" + string.Join('&',
            values.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value))));
    }
}
