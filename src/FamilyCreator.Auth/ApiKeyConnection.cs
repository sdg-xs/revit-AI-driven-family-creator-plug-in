using System.Net;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text.Json;

namespace FamilyCreator.Auth;

[SupportedOSPlatform("windows")]
public sealed class ApiKeyConnection
{
    private static readonly HttpClient defaultHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    private readonly CredentialStore store;
    private readonly HttpClient http;

    public ApiKeyConnection(CredentialStore? store = null, HttpClient? http = null)
    {
        this.store = store ?? new CredentialStore();
        this.http = http ?? defaultHttp;
    }

    public AccountState ReadSettings()
    {
        using var lease = store.Acquire();
        return store.Read();
    }

    public void SelectMethod(ConnectionMethod method)
    {
        if (!Enum.IsDefined(method)) throw new ArgumentOutOfRangeException(nameof(method));
        using var lease = store.Acquire();
        store.Save(store.Read() with { Method = method });
    }

    public void SaveKey(string value)
    {
        var key = value.Trim();
        if (key.Length == 0 || key.Length > 4096 || key.Any(c => c <= 32 || c >= 127))
            throw new AuthException("Enter an API key without spaces or line breaks.");
        using var lease = store.Acquire();
        store.Save(store.Read() with { ApiKey = new ApiKeyCredential(key), Method = ConnectionMethod.ApiKey });
    }

    public void RemoveKey()
    {
        using var lease = store.Acquire();
        store.Save(store.Read() with { ApiKey = null });
    }

    public async Task TestAsync(CancellationToken token)
    {
        var key = ReadSettings().ApiKey ?? throw new AuthException("Save an API key before testing the connection.");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Value);
        using var response = await http.SendAsync(request, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new AuthException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "The API key was not accepted. Replace it with an active key from OpenAI Platform.",
                HttpStatusCode.Forbidden => "This key lacks permission to list models, or access is restricted. Check its project permissions. The saved key has been kept.",
                HttpStatusCode.TooManyRequests => "OpenAI reported a usage or rate limit. Check API billing and project limits before trying again.",
                _ => "The OpenAI API check is unavailable. Your saved key has been kept; try again later."
            });
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token).ConfigureAwait(false);
        if (body.RootElement.ValueKind != JsonValueKind.Object || !body.RootElement.TryGetProperty("data", out var models) || models.ValueKind != JsonValueKind.Array)
            throw new AuthException("OpenAI returned an unexpected response. API access has not been verified.");
    }
}
