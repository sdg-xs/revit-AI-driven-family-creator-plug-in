namespace FamilyCreator.Auth;

public static class WorkspaceAccess
{
    public static bool CanOpen(AccountState state, DateTimeOffset now) => state.Method switch
    {
        ConnectionMethod.ApiKey => !string.IsNullOrWhiteSpace(state.ApiKey?.Value),
        ConnectionMethod.ChatGpt => state.Accounts.Any(account => account.ClientId == state.ActiveClientId &&
            account.Tokens is { } tokens && tokens.ExpiresAt > now && !string.IsNullOrEmpty(tokens.AccessToken)),
        _ => false
    };
}
