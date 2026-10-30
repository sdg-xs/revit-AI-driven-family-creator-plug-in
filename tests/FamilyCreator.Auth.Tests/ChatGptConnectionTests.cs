using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FamilyCreator.Auth.Tests;

[SupportedOSPlatform("windows")]
public sealed class ChatGptConnectionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FamilyCreatorFlowTest-" + Guid.NewGuid());
    private readonly RSA rsa = RSA.Create(2048);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConnectsOnlyAfterValidatedCallbackAndHonorsGrantedScopes(bool grantPlan)
    {
        Dictionary<string, string>? authorization = null;
        var key = new RsaSecurityKey(rsa) { KeyId = "test" };
        using var api = new HttpClient(new StubHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/oauth/token"))
            {
                var form = Query(await request.Content!.ReadAsStringAsync());
                Assert.Equal("issued-client", form["client_id"]);
                Assert.Equal(authorization!["redirect_uri"], form["redirect_uri"]);
                Assert.Equal(authorization["code_challenge"], OAuthProtocol.Challenge(form["code_verifier"]));
                var jwt = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
                {
                    Issuer = OAuthProtocol.Issuer, Audience = "issued-client", Expires = DateTime.UtcNow.AddMinutes(5),
                    Claims = new Dictionary<string, object> { ["sub"] = "subject", ["email"] = "user@example.com", ["nonce"] = authorization["nonce"] },
                    SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
                });
                return Json(new { id_token = jwt, access_token = "access", refresh_token = "refresh", token_type = "Bearer", expires_in = 3600,
                    scope = grantPlan ? "openid chatgpt.tokens.use.direct" : "openid" });
            }
            if (request.RequestUri.AbsolutePath.EndsWith("openid-configuration"))
                return Json(new { issuer = OAuthProtocol.Issuer, jwks_uri = OAuthProtocol.Issuer + "/jwks", revocation_endpoint = OAuthProtocol.Issuer + "/revoke" });
            if (request.RequestUri.AbsolutePath == "/jwks")
            {
                var parameters = rsa.ExportParameters(false);
                return Json(new { keys = new[] { new { kty = "RSA", kid = "test", alg = "RS256", use = "sig", n = Base64(parameters.Modulus!), e = Base64(parameters.Exponent!) } } });
            }
            throw new InvalidOperationException("Unexpected API request.");
        }));
        var connection = new ChatGptConnection(new CredentialStore(directory), api);
        Task<HttpResponseMessage>? browser = null;
        using var browserHttp = new HttpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var connected = await connection.SignInAsync(null, uri =>
        {
            authorization = Query(uri.Query);
            Assert.Equal("dynamic_agent_client", authorization["client_id"]);
            browser = browserHttp.GetAsync(authorization["redirect_uri"] + "?code=code&client_id=issued-client&state=" + authorization["state"], timeout.Token);
        }, timeout.Token);
        using var callbackResponse = await browser!;
        Assert.Equal(HttpStatusCode.OK, callbackResponse.StatusCode);
        Assert.Equal(grantPlan, connected.PlanEnabled);
        var saved = connection.ReadAccounts();
        Assert.Equal("issued-client", saved.ActiveClientId);
        Assert.Equal("subject", saved.Accounts.Single().Subject);
    }

    [Fact]
    public async Task FailedBrowserLaunchPreservesExistingConnection()
    {
        var store = new CredentialStore(directory);
        using (store.Acquire())
        {
            var state = store.Read();
            state.Accounts.Add(new AccountRegistration("old", "subject", "email", new TokenSet("access", "refresh", "id", [], DateTimeOffset.UtcNow.AddHours(1))));
            store.Save(state with { ActiveClientId = "old" });
        }
        var connection = new ChatGptConnection(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.SignInAsync(null,
            _ => throw new InvalidOperationException("Browser unavailable"), CancellationToken.None));
        Assert.Equal("old", connection.ReadAccounts().ActiveClientId);
        Assert.Equal("access", connection.ReadAccounts().Accounts.Single().Tokens!.AccessToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SignOutClearsTokensAndRetainsRegistrationEvenWhenRevocationFails(bool revokes)
    {
        var store = new CredentialStore(directory);
        string host;
        using (store.Acquire())
        {
            var state = store.Read();
            host = state.HostId;
            state.Accounts.Add(new AccountRegistration("client", "subject", "email", new TokenSet("access", "refresh", "id", [], DateTimeOffset.UtcNow.AddHours(1))));
            store.Save(state with { ActiveClientId = "client" });
        }
        using var api = new HttpClient(new StubHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("openid-configuration"))
                return Json(new { issuer = OAuthProtocol.Issuer, revocation_endpoint = OAuthProtocol.Issuer + "/revoke" });
            var form = Query(await request.Content!.ReadAsStringAsync());
            Assert.Equal("refresh", form["token"]);
            Assert.Equal("client", form["client_id"]);
            return new HttpResponseMessage(revokes ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        }));
        var connection = new ChatGptConnection(store, api);
        Assert.Equal(revokes, await connection.SignOutAsync("client", CancellationToken.None));
        var saved = connection.ReadAccounts();
        Assert.Null(saved.ActiveClientId);
        Assert.Null(saved.Accounts.Single().Tokens);
        Assert.Equal("client", saved.Accounts.Single().ClientId);
        Assert.Equal(host, saved.HostId);
    }

    private static Dictionary<string, string> Query(string query) => query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
        .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json") };
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => respond(request);
    }
    public void Dispose()
    {
        rsa.Dispose();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
