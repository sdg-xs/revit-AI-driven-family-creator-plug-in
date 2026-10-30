using System.Net;
using System.Runtime.Versioning;
using System.Text;
using System.Security.Cryptography;

namespace FamilyCreator.Auth.Tests;

[SupportedOSPlatform("windows")]
public sealed class ApiKeyConnectionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FamilyCreatorKeyTest-" + Guid.NewGuid());
    private CredentialStore Store => new(directory);
    private const string Key = "sk-test-not-a-real-key-123456789";

    [Fact]
    public void ExistingOAuthStorageLoadsWithoutApiKeyFields()
    {
        Directory.CreateDirectory(directory);
        var oldState = Encoding.UTF8.GetBytes("{\"HostId\":\"urn:uuid:existing\",\"ActiveClientId\":null,\"Accounts\":[]}");
        File.WriteAllBytes(Path.Combine(directory, "accounts.bin"), ProtectedData.Protect(oldState, null, DataProtectionScope.CurrentUser));
        var state = new ApiKeyConnection(Store).ReadSettings();
        Assert.Equal("urn:uuid:existing", state.HostId);
        Assert.Equal(ConnectionMethod.ChatGpt, state.Method);
        Assert.Null(state.ApiKey);
    }

    [Fact]
    public async Task NoSavedKeyNeverMakesARequest()
    {
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Must not send")));
        await Assert.ThrowsAsync<AuthException>(() => new ApiKeyConnection(Store, http).TestAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CancellationKeepsStoredKey()
    {
        using var http = new HttpClient(new Handler(_ => throw new OperationCanceledException()));
        var connection = new ApiKeyConnection(Store, http);
        connection.SaveKey(Key);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.TestAsync(CancellationToken.None));
        Assert.Equal(Key, connection.ReadSettings().ApiKey!.Value);
    }

    [Fact]
    public void SaveReplaceRemoveKeyPreservesChatGptRegistrationAndSelection()
    {
        using (Store.Acquire())
        {
            var state = Store.Read();
            state.Accounts.Add(new AccountRegistration("client", "subject", "email", null));
            Store.Save(state with { ActiveClientId = "client" });
        }
        var connection = new ApiKeyConnection(Store);
        connection.SaveKey(Key);
        var stateAfterSave = connection.ReadSettings();
        Assert.Equal(ConnectionMethod.ApiKey, stateAfterSave.Method);
        Assert.Equal(Key, stateAfterSave.ApiKey!.Value);
        Assert.DoesNotContain(Key, Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "accounts.bin"))));
        Assert.DoesNotContain(Key, stateAfterSave.ToString());
        connection.SaveKey("sk-replacement-not-real-987654321");
        Assert.NotEqual(Key, connection.ReadSettings().ApiKey!.Value);
        connection.SelectMethod(ConnectionMethod.ChatGpt);
        Assert.Equal(ConnectionMethod.ChatGpt, new ApiKeyConnection(Store).ReadSettings().Method);
        connection.RemoveKey();
        var removed = connection.ReadSettings();
        Assert.Null(removed.ApiKey);
        Assert.Equal("client", removed.ActiveClientId);
        Assert.Single(removed.Accounts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("sk-key\r\nInjected: header")]
    public void InvalidInputDoesNotReplaceSavedKey(string value)
    {
        var connection = new ApiKeyConnection(Store);
        connection.SaveKey(Key);
        Assert.Throws<AuthException>(() => connection.SaveKey(value));
        Assert.Equal(Key, connection.ReadSettings().ApiKey!.Value);
    }

    [Fact]
    public async Task TestUsesOnlySavedKeyAndModelsEndpoint()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.openai.com/v1/models", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal(Key, request.Headers.Authorization.Parameter);
            Assert.Null(request.Content);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"object\":\"list\",\"data\":[]}") };
        }));
        var connection = new ApiKeyConnection(Store, http);
        connection.SaveKey(Key);
        await connection.TestAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(401, "not accepted")]
    [InlineData(403, "permission")]
    [InlineData(429, "limit")]
    [InlineData(500, "unavailable")]
    public async Task FailedCheckKeepsKeyAndNeverReflectsResponseSecrets(int status, string expected)
    {
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(Key) }));
        var connection = new ApiKeyConnection(Store, http);
        connection.SaveKey(Key);
        var error = await Assert.ThrowsAsync<AuthException>(() => connection.TestAsync(CancellationToken.None));
        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain(Key, error.Message);
        Assert.Equal(Key, connection.ReadSettings().ApiKey!.Value);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
