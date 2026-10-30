using System.Windows;
using System.Windows.Controls;
using FamilyCreator.Auth;

namespace FamilyCreator.Revit;

public partial class CreatorWindow
{
    private sealed record ReferenceAttachment(string FilePath)
    {
        public string Name => System.IO.Path.GetFileName(FilePath);
    }

    private void AddAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (generationCancellation is not null) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Attach product references",
            Filter = "Reference files|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.pdf;*.txt;*.md|Images|*.png;*.jpg;*.jpeg;*.webp;*.gif|PDF documents|*.pdf|Text documents|*.txt;*.md",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true) AddReferenceFiles(dialog.FileNames);
    }

    private void AddReferenceFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!System.IO.File.Exists(path)) continue;
            var fullPath = System.IO.Path.GetFullPath(path);
            if (ReferenceAttachments.Items.Cast<ReferenceAttachment>().Any(item => string.Equals(item.FilePath, fullPath, StringComparison.OrdinalIgnoreCase))) continue;
            ReferenceAttachments.Items.Add(new ReferenceAttachment(fullPath));
        }
        UpdateAttachmentStatus();
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (generationCancellation is not null) return;
        if (sender is Button { Tag: ReferenceAttachment attachment }) ReferenceAttachments.Items.Remove(attachment);
        UpdateAttachmentStatus();
    }

    private void UpdateAttachmentStatus() => AttachmentStatus.Text = ReferenceAttachments.Items.Count == 0
        ? "Draft · stays in this window"
        : $"{ReferenceAttachments.Items.Count} attached · included when you send";

    private void OpenInitialScreen()
    {
        try
        {
            if (WorkspaceAccess.CanOpen(apiKeyConnection.ReadSettings(), DateTimeOffset.UtcNow)) TryOpenWorkspace();
            else ShowWelcomeScreen();
        }
        catch (Exception exception)
        {
            ShowConnectionSettings();
            ShowConnectionError(SafeMessage(exception));
        }
    }

    private void ShowWelcomeScreen()
    {
        if (operation is not null) return;
        MinWidth = 420;
        MinHeight = 480;
        ConnectionScreen.Visibility = ConversationScreen.Visibility = Visibility.Collapsed;
        WelcomeScreen.Visibility = Visibility.Visible;
        ApiKeyInput.Clear();
    }

    private void WelcomeChatGpt_Click(object sender, RoutedEventArgs e)
    {
        ShowConnectionSettings();
        ChatGptOption.IsChecked = true;
        SignIn_Click(sender, e);
    }

    private void WelcomeApiKey_Click(object sender, RoutedEventArgs e)
    {
        ShowConnectionSettings();
        ApiKeyOption.IsChecked = true;
        ApiKeyInput.Focus();
    }

    private void OtherConnectionOptions_Click(object sender, RoutedEventArgs e) => ShowWelcomeScreen();

    private bool TryOpenWorkspace()
    {
        if (closed || operation is not null) return false;
        try
        {
            var settings = apiKeyConnection.ReadSettings();
            if (!WorkspaceAccess.CanOpen(settings, DateTimeOffset.UtcNow))
            {
                ShowConnectionSettings();
                return false;
            }
            ShowWorkspace(settings.Method == ConnectionMethod.ApiKey ? "OpenAI API key · saved on this device" : "ChatGPT account");
            return true;
        }
        catch (Exception exception)
        {
            ShowConnectionSettings();
            ShowConnectionError(SafeMessage(exception));
            return false;
        }
    }

    private void ShowWorkspace(string connectionLabel)
    {
        availableModelIds = null;
        var connectionSettings = apiKeyConnection.ReadSettings();
        generationUsesApiKey = connectionSettings.Method == ConnectionMethod.ApiKey && connectionSettings.ApiKey is not null;
        MinWidth = 420;
        MinHeight = 480;
        ConnectionScreen.Visibility = Visibility.Collapsed;
        WelcomeScreen.Visibility = Visibility.Collapsed;
        ConversationScreen.Visibility = Visibility.Visible;
        WorkspaceConnectionLabel.Text = connectionLabel;
        ApiKeyInput.Clear();
        PromptInput.Focus();
        RefreshGenerationControls();
    }

    private void ShowConnectionSettings()
    {
        MinWidth = 780;
        MinHeight = 620;
        ConversationScreen.Visibility = Visibility.Collapsed;
        WelcomeScreen.Visibility = Visibility.Collapsed;
        ConnectionScreen.Visibility = Visibility.Visible;
        RefreshWorkspaceNavigation();
    }

    private void RefreshWorkspaceNavigation()
    {
        if (operation is not null) return;
        try
        {
            var allowed = WorkspaceAccess.CanOpen(apiKeyConnection.ReadSettings(), DateTimeOffset.UtcNow);
            ReturnToWorkspaceButton.Visibility = OpenWorkspaceButton.Visibility = allowed ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            ReturnToWorkspaceButton.Visibility = OpenWorkspaceButton.Visibility = Visibility.Collapsed;
        }
    }

    private void WorkspaceSettings_Click(object sender, RoutedEventArgs e)
    {
        ShowConnectionSettings();
        LoadAccounts();
        LoadConnectionPreferences();
    }

    private void ReturnToWorkspace_Click(object sender, RoutedEventArgs e) => TryOpenWorkspace();

    private void PromptInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PromptPlaceholder is not null)
            PromptPlaceholder.Visibility = PromptInput.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshGenerationControls();
    }
}
