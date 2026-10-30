using System.Windows;
using FamilyCreator.Auth;

namespace FamilyCreator.Revit;

public partial class CreatorWindow
{
    private readonly ApiKeyConnection apiKeyConnection;
    private bool preferencesLoaded;

    private void LoadConnectionPreferences()
    {
        try
        {
            var settings = apiKeyConnection.ReadSettings();
            if (settings.Method == ConnectionMethod.ApiKey) ApiKeyOption.IsChecked = true;
            else ChatGptOption.IsChecked = true;
            RefreshApiKeyStatus();
            preferencesLoaded = true;
        }
        catch (Exception exception) { ApiKeyStatus.Text = SafeMessage(exception); }
    }

    private void ConnectionMethod_Checked(object sender, RoutedEventArgs e)
    {
        if (ChatGptPanel is null || ApiKeyPanel is null) return;
        var useKey = ApiKeyOption.IsChecked == true;
        ChatGptPanel.Visibility = useKey ? Visibility.Collapsed : Visibility.Visible;
        ApiKeyPanel.Visibility = useKey ? Visibility.Visible : Visibility.Collapsed;
        ApiKeyInput.Clear();
        if (!preferencesLoaded) return;
        try
        {
            apiKeyConnection.SelectMethod(useKey ? ConnectionMethod.ApiKey : ConnectionMethod.ChatGpt);
            if (useKey) RefreshApiKeyStatus();
            RefreshWorkspaceNavigation();
        }
        catch (Exception exception)
        {
            if (useKey) ApiKeyStatus.Text = SafeMessage(exception);
            else ShowConnectionError(SafeMessage(exception));
        }
    }

    private void RefreshApiKeyStatus()
    {
        var saved = apiKeyConnection.ReadSettings().ApiKey is not null;
        TestApiKeyButton.IsEnabled = saved;
        RemoveApiKeyButton.IsEnabled = saved;
        ApiKeyStatus.Text = saved ? "API key saved on this device. Test the connection to check access." : "No API key saved.";
    }

    private void SaveApiKey_Click(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        try
        {
            apiKeyConnection.SaveKey(ApiKeyInput.Password);
            RefreshApiKeyStatus();
            TryOpenWorkspace();
        }
        catch (Exception exception) { ApiKeyStatus.Text = SafeMessage(exception); }
        finally { ApiKeyInput.Clear(); }
    }

    private void RemoveApiKey_Click(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        try
        {
            apiKeyConnection.RemoveKey();
            ApiKeyInput.Clear();
            RefreshApiKeyStatus();
            ApiKeyStatus.Text = "API key removed from this device. The key itself has not been revoked in OpenAI Platform.";
            RefreshWorkspaceNavigation();
        }
        catch (Exception exception) { ApiKeyStatus.Text = SafeMessage(exception); }
    }

    private async void TestApiKey_Click(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        if (ApiKeyInput.Password.Length != 0)
        {
            ApiKeyStatus.Text = "Save the new key first, or clear the field to test the previously saved key.";
            return;
        }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        operation = cancellation;
        ReturnToWorkspaceButton.IsEnabled = OpenWorkspaceButton.IsEnabled = false;
        OtherConnectionOptions.IsEnabled = false;
        ChatGptOption.IsEnabled = ApiKeyOption.IsEnabled = false;
        SaveApiKeyButton.IsEnabled = TestApiKeyButton.IsEnabled = RemoveApiKeyButton.IsEnabled = ApiKeyInput.IsEnabled = false;
        CancelApiKeyButton.Visibility = Visibility.Visible;
        ApiKeyStatus.Text = "Checking access to OpenAI…";
        string result;
        var passed = false;
        try
        {
            await apiKeyConnection.TestAsync(cancellation.Token);
            passed = true;
            result = "Connection check passed. OpenAI accepted the key for listing models. Generation access has not been tested.";
        }
        catch (OperationCanceledException) { result = "Connection check cancelled or timed out. Your saved key was kept."; }
        catch (Exception exception) { result = SafeMessage(exception); }
        finally
        {
            operation = null;
            if (!closed)
            {
                ChatGptOption.IsEnabled = ApiKeyOption.IsEnabled = true;
                SaveApiKeyButton.IsEnabled = TestApiKeyButton.IsEnabled = RemoveApiKeyButton.IsEnabled = ApiKeyInput.IsEnabled = true;
                CancelApiKeyButton.Visibility = Visibility.Collapsed;
                ReturnToWorkspaceButton.IsEnabled = OpenWorkspaceButton.IsEnabled = true;
                OtherConnectionOptions.IsEnabled = true;
            }
        }
        if (!closed)
        {
            ApiKeyStatus.Text = result;
            if (passed) TryOpenWorkspace();
        }
    }

    private void ApiKeyDashboard_Click(object sender, RoutedEventArgs e)
    {
        try { OpenBrowser(new Uri("https://platform.openai.com/api-keys")); }
        catch { ApiKeyStatus.Text = "Open https://platform.openai.com/api-keys in your browser to manage API keys."; }
    }

    private void ApiBilling_Click(object sender, RoutedEventArgs e)
    {
        try { OpenBrowser(new Uri("https://platform.openai.com/settings/organization/billing/")); }
        catch { ApiKeyStatus.Text = "Open https://platform.openai.com/settings/organization/billing/ in your browser to manage API credits."; }
    }
}
