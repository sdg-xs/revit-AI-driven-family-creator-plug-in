using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace FamilyCreator.Auth;

[SupportedOSPlatform("windows")]
public sealed class ChatGptConnection
{
    private static readonly HttpClient defaultHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    private readonly HttpClient http;
    private readonly CredentialStore store;
    public ChatGptConnection(CredentialStore? store = null, HttpClient? http = null)
    {
        this.store = store ?? new CredentialStore();
        this.http = http ?? defaultHttp;
    }

    public AccountState ReadAccounts()
    {
        using var lease = store.Acquire();
        return store.Read();
    }

    public async Task<AccountRegistration> SignInAsync(string? clientId, Action<Uri> openBrowser, CancellationToken cancellationToken)
    {
        using var lease = store.Acquire();
        var accounts = store.Read();
        var existing = clientId is null ? null : accounts.Accounts.SingleOrDefault(a => a.ClientId == clientId)
            ?? throw new AuthException("The selected account registration is no longer available.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var token = timeout.Token;
        using var callback = new LoopbackCallback();
        var state = OAuthProtocol.RandomValue();
        var nonce = OAuthProtocol.RandomValue();
        var verifier = OAuthProtocol.RandomValue();
        openBrowser(OAuthProtocol.AuthorizationUri(accounts.HostId, callback.RedirectUri, state, nonce, verifier, existing));
        var code = await callback.WaitAsync(state, existing?.ClientId, token).ConfigureAwait(false);
        using var response = await http.PostAsync(OAuthProtocol.Issuer + "/api/accounts/oauth/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["client_id"] = code.ClientId, ["code"] = code.Code,
                ["code_verifier"] = verifier, ["redirect_uri"] = callback.RedirectUri, ["resource"] = OAuthProtocol.Resource
            }), token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw SignInFailure.FromResponse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false),
                response.Headers.TryGetValues("x-request-id", out var ids) ? ids.FirstOrDefault() : null);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token).ConfigureAwait(false);
        var root = json.RootElement;
        string Required(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new AuthException("OpenAI returned an incomplete sign-in response.");
        var idToken = Required("id_token");
        var accessToken = Required("access_token");
        var tokenType = Required("token_type");
        if (!string.Equals(tokenType, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            !root.TryGetProperty("expires_in", out var expiry) || !expiry.TryGetInt32(out var seconds) || seconds <= 0)
            throw new AuthException("OpenAI returned an unsupported sign-in response.");
        var metadata = await ReadMetadataAsync(token).ConfigureAwait(false);
        var jwksUri = TrustedEndpoint(metadata, "jwks_uri");
        var jwks = await http.GetStringAsync(jwksUri, token).ConfigureAwait(false);
        var identity = await IdentityValidator.ValidateAsync(idToken, code.ClientId, nonce,
            new JsonWebKeySet(jwks).GetSigningKeys()).ConfigureAwait(false);
        if (existing is not null && existing.Subject != identity.Subject)
            throw new AuthException("The signed-in account did not match the selected registration. The previous connection was preserved.");
        var scopes = root.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.String
            ? scope.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
        var refresh = root.TryGetProperty("refresh_token", out var refreshToken) && refreshToken.ValueKind == JsonValueKind.String
            ? refreshToken.GetString()! : "";
        var registration = new AccountRegistration(code.ClientId, identity.Subject, identity.Email,
            new TokenSet(accessToken, refresh, idToken, scopes, DateTimeOffset.UtcNow.AddSeconds(seconds)), existing?.WelcomeShown ?? false);
        token.ThrowIfCancellationRequested();
        var conflict = accounts.Accounts.SingleOrDefault(a => a.ClientId == code.ClientId);
        if (conflict is not null && conflict.Subject != identity.Subject)
            throw new AuthException("The returned registration belongs to a different saved identity.");
        accounts.Accounts.RemoveAll(a => a.ClientId == registration.ClientId);
        accounts.Accounts.Add(registration);
        store.Save(accounts with { ActiveClientId = registration.ClientId });
        return registration;
    }

    public async Task<bool> SignOutAsync(string clientId, CancellationToken token)
    {
        using var lease = store.Acquire();
        var state = store.Read();
        var index = state.Accounts.FindIndex(a => a.ClientId == clientId);
        if (index < 0) return true;
        var account = state.Accounts[index];
        var confirmed = account.Tokens is null;
        if (!string.IsNullOrEmpty(account.Tokens?.RefreshToken))
        {
            for (var attempt = 0; attempt < 2 && !confirmed; attempt++)
            {
                try
                {
                    var metadata = await ReadMetadataAsync(token).ConfigureAwait(false);
                    using var response = await http.PostAsync(TrustedEndpoint(metadata, "revocation_endpoint"), new FormUrlEncodedContent(
                        new Dictionary<string, string> { ["token"] = account.Tokens.RefreshToken, ["token_type_hint"] = "refresh_token", ["client_id"] = clientId }), token).ConfigureAwait(false);
                    confirmed = response.StatusCode == System.Net.HttpStatusCode.OK;
                    if ((int)response.StatusCode < 500) break;
                }
                catch (Exception e) when (e is HttpRequestException or OperationCanceledException or AuthException or JsonException) { }
                if (attempt == 0 && !token.IsCancellationRequested) await Task.Delay(500).ConfigureAwait(false);
            }
        }
        state.Accounts[index] = account with { Tokens = null };
        store.Save(state with { ActiveClientId = state.ActiveClientId == clientId ? null : state.ActiveClientId });
        return confirmed;
    }

    public void MarkWelcomeShown(string clientId)
    {
        using var lease = store.Acquire();
        var state = store.Read();
        var index = state.Accounts.FindIndex(a => a.ClientId == clientId);
        if (index >= 0)
        {
            state.Accounts[index] = state.Accounts[index] with { WelcomeShown = true };
            store.Save(state);
        }
    }

    private async Task<JsonElement> ReadMetadataAsync(CancellationToken token)
    {
        var metadata = await http.GetFromJsonAsync<JsonElement>(OAuthProtocol.Issuer + "/.well-known/openid-configuration", token).ConfigureAwait(false);
        if (!metadata.TryGetProperty("issuer", out var issuer) || issuer.GetString() != OAuthProtocol.Issuer)
            throw new AuthException("The sign-in provider metadata could not be verified.");
        return metadata;
    }

    private static Uri TrustedEndpoint(JsonElement metadata, string name)
    {
        if (!metadata.TryGetProperty(name, out var value) || !Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.Host != "auth.openai.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            throw new AuthException("The sign-in provider endpoint could not be verified.");
        return uri;
    }
}
