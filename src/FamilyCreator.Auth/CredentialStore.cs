using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

namespace FamilyCreator.Auth;

public sealed record TokenSet(string AccessToken, string RefreshToken, string IdToken, string[] Scopes, DateTimeOffset ExpiresAt);
public sealed record AccountRegistration(string ClientId, string Subject, string Email, TokenSet? Tokens, bool WelcomeShown = false)
{
    public string Label => $"{Email} · {ClientId}";
    public bool PlanEnabled => Tokens?.Scopes.Contains("chatgpt.tokens.use.direct", StringComparer.Ordinal) == true;
}
public enum ConnectionMethod { ChatGpt, ApiKey }
public sealed record ApiKeyCredential(string Value)
{
    public override string ToString() => "[API key protected]";
}
public sealed record AccountState(string HostId, string? ActiveClientId, List<AccountRegistration> Accounts,
    ApiKeyCredential? ApiKey = null, ConnectionMethod Method = ConnectionMethod.ChatGpt);

[SupportedOSPlatform("windows")]
public sealed class CredentialStore
{
    private readonly string directory;
    private string StatePath => Path.Combine(directory, "accounts.bin");
    public CredentialStore(string? directory = null) => this.directory = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyCreator", "Auth");

    public IDisposable Acquire()
    {
        Directory.CreateDirectory(directory);
        try { return new FileStream(Path.Combine(directory, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new AuthException("Another Family Creator window is updating sign-in. Finish there and try again."); }
    }

    public AccountState Read()
    {
        if (!File.Exists(StatePath))
        {
            var initial = new AccountState("urn:uuid:" + Guid.NewGuid(), null, []);
            Save(initial);
            return initial;
        }
        byte[]? plaintext = null;
        try
        {
            plaintext = ProtectedData.Unprotect(File.ReadAllBytes(StatePath), null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<AccountState>(plaintext) ?? throw new JsonException();
        }
        catch (Exception e) when (e is CryptographicException or JsonException)
        {
            throw new AuthException("Saved sign-in data could not be read for this Windows user. It has not been overwritten.");
        }
        finally { if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext); }
    }

    public void Save(AccountState state)
    {
        Directory.CreateDirectory(directory);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(state);
        var temporary = Path.Combine(directory, Guid.NewGuid() + ".tmp");
        try
        {
            var encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(encrypted);
                file.Flush(true);
            }
            File.Move(temporary, StatePath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
