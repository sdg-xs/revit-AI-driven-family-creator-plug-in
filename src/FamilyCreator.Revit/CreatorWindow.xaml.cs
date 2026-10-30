using System.Windows;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using FamilyCreator.Auth;

namespace FamilyCreator.Revit;

public partial class CreatorWindow : Window
{
    private readonly ChatGptConnection connection;
    private CancellationTokenSource? operation;
    private bool closed;
    private string? browserFailure;
    private sealed record AccountChoice(AccountRegistration? Account)
    {
        public string Label => Account?.Label ?? "Add a ChatGPT account";
    }

    public CreatorWindow() : this(new ChatGptConnection(), new ApiKeyConnection()) { }

    internal CreatorWindow(ChatGptConnection connection, ApiKeyConnection apiKeyConnection, FamilyCreator.Generation.IFamilyBuildService? buildService = null,
        FamilyCreator.Generation.GenerationClient? generationClient = null, string? modelPreferencePath = null)
    {
        this.connection = connection;
        this.apiKeyConnection = apiKeyConnection;
        this.buildService = buildService;
        this.generationClient = generationClient ?? new();
        this.modelPreferencePath = modelPreferencePath ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyCreator", "model.txt");
        InitializeComponent();
        InitializeGeneration();
        Loaded += (_, _) => { LoadAccounts(); LoadConnectionPreferences(); OpenInitialScreen(); };
        Closed += (_, _) => { closed = true; operation?.Cancel(); ApiKeyInput.Clear(); };
    }

    private void LoadAccounts(string? selectClient = null)
    {
        try
        {
            var accounts = connection.ReadAccounts();
            var choices = accounts.Accounts.Select(a => new AccountChoice(a)).ToList();
            choices.Add(new AccountChoice(null));
            AccountPicker.ItemsSource = choices;
            AccountPicker.SelectedItem = choices.FirstOrDefault(c => c.Account?.ClientId == (selectClient ?? accounts.ActiveClientId)) ?? choices[^1];
            UpdateStatus();
        }
        catch (Exception exception) { ConnectionStatus.Text = SafeMessage(exception); }
    }

    private void AccountPicker_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateStatus();

    private void UpdateStatus()
    {
        if (ConnectionStatus is null || SignOutButton is null || operation is not null) return;
        var account = (AccountPicker.SelectedItem as AccountChoice)?.Account;
        SignOutButton.IsEnabled = account?.Tokens is not null;
        ConnectionStatus.Text = account?.Tokens is null ? "Not connected. Continue with ChatGPT to sign in." :
            account.Tokens.ExpiresAt <= DateTimeOffset.UtcNow ? "Saved account — sign in again to renew the connection." :
            account.PlanEnabled ? $"Connected as {account.Email}. ChatGPT plan permission granted; AI requests are not enabled yet." :
            $"Signed in as {account.Email}. ChatGPT plan usage was not granted.";
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        var clientId = (AccountPicker.SelectedItem as AccountChoice)?.Account?.ClientId;
        using var cancellation = new CancellationTokenSource();
        operation = cancellation;
        browserFailure = null;
        SetBusy(true);
        ConnectionStatus.Text = "Complete sign-in in your browser. Waiting for OpenAI…";
        AccountRegistration? account = null;
        string? error = null;
        try
        {
            account = await connection.SignInAsync(clientId, OpenBrowser, cancellation.Token);
            if (!closed && account.PlanEnabled && !account.WelcomeShown)
            {
                MessageBox.Show(this, "Eligible AI requests in Family Creator will use your ChatGPT plan. You can manage usage in ChatGPT settings. No AI requests are sent by this sign-in milestone.",
                    "Your ChatGPT plan is connected", MessageBoxButton.OK, MessageBoxImage.Information);
                connection.MarkWelcomeShown(account.ClientId);
            }
        }
        catch (OperationCanceledException) { error = browserFailure ?? (cancellation.IsCancellationRequested ? "Sign-in cancelled." : "No callback received. If your browser shows an error, open sign-in help and paste its error code and request ID."); }
        catch (Exception exception) { error = SafeMessage(exception); }
        finally
        {
            operation = null;
            if (!closed)
            {
                SetBusy(false);
                LoadAccounts(account?.ClientId ?? clientId);
                if (error is not null) ShowConnectionError(error);
                else if (account is not null) TryOpenWorkspace();
            }
        }
    }

    private async void SignOut_Click(object sender, RoutedEventArgs e)
    {
        var account = (AccountPicker.SelectedItem as AccountChoice)?.Account;
        if (operation is not null || account is null) return;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        operation = cancellation;
        SetBusy(true);
        CancelSignInButton.Visibility = Visibility.Collapsed;
        ConnectionStatus.Text = "Signing out…";
        string? message = null;
        try
        {
            message = await connection.SignOutAsync(account.ClientId, cancellation.Token) ? "Signed out." :
                "Signed out locally. Remote revocation was not confirmed; you can disconnect this app in ChatGPT settings.";
        }
        catch (Exception exception) { message = SafeMessage(exception); }
        finally
        {
            operation = null;
            if (!closed)
            {
                SetBusy(false);
                LoadAccounts(account.ClientId);
                ConnectionStatus.Text = message;
                RefreshWorkspaceNavigation();
            }
        }
    }

    private void SetBusy(bool busy)
    {
        ChatGptOption.IsEnabled = !busy;
        ReturnToWorkspaceButton.IsEnabled = OpenWorkspaceButton.IsEnabled = !busy;
        OtherConnectionOptions.IsEnabled = !busy;
        ApiKeyOption.IsEnabled = !busy;
        StatusPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
        AccountPicker.IsEnabled = !busy;
        SignInButton.IsEnabled = !busy;
        SignOutButton.IsEnabled = !busy;
        CancelSignInButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string SafeMessage(Exception exception) => exception is AuthException ? exception.Message :
        "The connection could not be completed. Check your internet connection and local account storage, then try again.";

    private static void OpenBrowser(Uri uri) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    private void CancelSignIn_Click(object sender, RoutedEventArgs e) => operation?.Cancel();

    private void ShowConnectionError(string message)
    {
        ConnectionStatus.Text = message;
        StatusPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
        if (message.Contains(SignInFailure.PolicyDenied, StringComparison.Ordinal)) SignInHelp.IsExpanded = true;
    }

    private void CheckBrowserError_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BrowserErrorDetails.Text)) return;
        browserFailure = SignInFailure.FromBrowserText(BrowserErrorDetails.Text).Message;
        BrowserErrorDetails.Clear();
        operation?.Cancel();
        ShowConnectionError(browserFailure);
    }

    private void ManageUsage_Click(object sender, RoutedEventArgs e)
    {
        try { OpenBrowser(new Uri("https://chatgpt.com/settings/usage")); }
        catch { ConnectionStatus.Text = "Open https://chatgpt.com/settings/usage in your browser to manage usage."; }
    }

}
