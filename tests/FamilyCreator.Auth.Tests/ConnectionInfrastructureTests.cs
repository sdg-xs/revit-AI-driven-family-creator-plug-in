using System.Net;
using System.Runtime.Versioning;
using System.Text;

namespace FamilyCreator.Auth.Tests;

public class ConnectionInfrastructureTests
{
    [Fact]
    public async Task BrowserCallbackReceivesCodeAndClosesListener()
    {
        using var callback = new LoopbackCallback();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = callback.WaitAsync("expected", null, timeout.Token);
        using var http = new HttpClient();
        var response = await http.GetAsync(callback.RedirectUri + "?state=expected&code=code&client_id=client", timeout.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("code", (await result).Code);
    }

    [Fact]
    public async Task CancelStopsWaitingWithoutBrowserCallback()
    {
        using var callback = new LoopbackCallback();
        using var cancel = new CancellationTokenSource();
        var waiting = callback.WaitAsync("state", null, cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public async Task UnrelatedRequestDoesNotCompleteSignIn()
    {
        using var callback = new LoopbackCallback();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waiting = callback.WaitAsync("state", null, timeout.Token);
        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(new Uri(new Uri(callback.RedirectUri), "/favicon.ico"), timeout.Token)).StatusCode);
        Assert.False(waiting.IsCompleted);
        await http.GetAsync(callback.RedirectUri + "?state=state&code=code&client_id=client", timeout.Token);
        Assert.Equal("client", (await waiting).ClientId);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void StorageEncryptsTokensAndPreservesHostAcrossReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FamilyCreatorTest-" + Guid.NewGuid());
        try
        {
            var store = new CredentialStore(directory);
            using var lease = store.Acquire();
            var state = store.Read();
            state.Accounts.Add(new AccountRegistration("client", "subject", "user@example.com",
                new TokenSet("secret-access", "secret-refresh", "secret-id", ["chatgpt.tokens.use.direct"], DateTimeOffset.UtcNow.AddMinutes(30))));
            store.Save(state);
            var bytes = File.ReadAllBytes(Path.Combine(directory, "accounts.bin"));
            Assert.DoesNotContain("secret-access", Encoding.UTF8.GetString(bytes));
            var reopened = new CredentialStore(directory).Read();
            Assert.Equal(state.HostId, reopened.HostId);
            Assert.Equal("secret-refresh", reopened.Accounts.Single().Tokens!.RefreshToken);
            Assert.Throws<AuthException>(() => new CredentialStore(directory).Acquire());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void ReturningAccountUsesSavedClientAndHint()
    {
        var saved = new AccountRegistration("saved-client", "subject", "user@example.com", null);
        var uri = OAuthProtocol.AuthorizationUri("host", "http://127.0.0.1:1234/auth/callback", "state", "nonce", "verifier", saved);
        Assert.Contains("client_id=saved-client", uri.Query);
        Assert.DoesNotContain("agent_name_hint", uri.Query);
        Assert.DoesNotContain("id_token_hint", uri.Query);
    }
}
