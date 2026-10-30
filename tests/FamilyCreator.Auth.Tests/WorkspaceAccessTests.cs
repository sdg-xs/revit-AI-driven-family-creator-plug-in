namespace FamilyCreator.Auth.Tests;

public class WorkspaceAccessTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void SavedApiKeyOpensWorkspaceOnlyWhenSelected()
    {
        var state = new AccountState("host", null, [], new ApiKeyCredential("saved"), ConnectionMethod.ApiKey);
        Assert.True(WorkspaceAccess.CanOpen(state, Now));
        Assert.False(WorkspaceAccess.CanOpen(state with { Method = ConnectionMethod.ChatGpt }, Now));
        Assert.False(WorkspaceAccess.CanOpen(state with { ApiKey = null }, Now));
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(-60, false)]
    public void ActiveChatGptSessionMustBeCurrent(int seconds, bool expected)
    {
        var account = new AccountRegistration("client", "subject", "email", new TokenSet("access", "refresh", "id", ["chatgpt.tokens.use.direct"], Now.AddSeconds(seconds)));
        Assert.Equal(expected, WorkspaceAccess.CanOpen(new AccountState("host", "client", [account]), Now));
        Assert.False(WorkspaceAccess.CanOpen(new AccountState("host", "other", [account]), Now));
        Assert.False(WorkspaceAccess.CanOpen(new AccountState("host", "client", [account with { Tokens = null }]), Now));
    }
}
