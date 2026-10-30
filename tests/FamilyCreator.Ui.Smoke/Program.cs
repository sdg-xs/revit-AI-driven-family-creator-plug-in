using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FamilyCreator.Revit;
using FamilyCreator.Auth;
using FamilyCreator.Generation;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new Application();
        var testDirectory = Path.Combine(Path.GetTempPath(), "FamilyCreatorUi-" + Guid.NewGuid());
        var testStore = new CredentialStore(testDirectory);
        var testConnection = new ApiKeyConnection(testStore);
        var window = new CreatorWindow(new ChatGptConnection(testStore), testConnection, modelPreferencePath: Path.Combine(testDirectory, "model.txt"));
        var content = (FrameworkElement)window.Content;
        object? Invoke(string method, params object[] args) => typeof(CreatorWindow).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, args);
        Invoke("OpenInitialScreen");
        if (((FrameworkElement)window.FindName("WelcomeScreen")).Visibility != Visibility.Visible)
            throw new InvalidOperationException("First launch did not show welcome screen.");
        Render(content, 624, 680, "welcome-window.png");
        Render(content, 424, 440, "welcome-window-narrow.png");
        var login = (Button)window.FindName("WelcomeChatGptButton");
        var apiChoice = (Button)window.FindName("WelcomeApiKeyButton");
        if (login.ActualWidth < 240 || apiChoice.TranslatePoint(new Point(), content).Y <= login.TranslatePoint(new Point(), content).Y)
            throw new InvalidOperationException("Welcome buttons were not stacked in the requested order.");
        Invoke("WelcomeApiKey_Click", window, new RoutedEventArgs());
        if (((FrameworkElement)window.FindName("ConnectionScreen")).Visibility != Visibility.Visible || ((RadioButton)window.FindName("ApiKeyOption")).IsChecked != true)
            throw new InvalidOperationException("Welcome API key button did not open key setup.");
        ((PasswordBox)window.FindName("ApiKeyInput")).Password = "unsaved-fixture";
        Invoke("OtherConnectionOptions_Click", window, new RoutedEventArgs());
        if (((FrameworkElement)window.FindName("WelcomeScreen")).Visibility != Visibility.Visible || ((PasswordBox)window.FindName("ApiKeyInput")).Password.Length != 0)
            throw new InvalidOperationException("Return to welcome did not clear unsaved key.");
        Invoke("ShowConnectionSettings");
        ((RadioButton)window.FindName("ChatGptOption")).IsChecked = true;
        var picker = (ComboBox)window.FindName("AccountPicker");
        picker.ItemsSource = new[] { new { Label = "Add a ChatGPT account" } };
        picker.SelectedIndex = 0;
        Render(content, 924, 720, "connection-window.png");
        Render(content, 764, 580, "connection-window-small.png");
        var details = (TextBox)window.FindName("BrowserErrorDetails");
        details.Text = "error_code: 3p_delegated_access_policy_denied\nrequest_id: f04f7775-4522-4f13-8d01-bfbe5f7e0676";
        typeof(CreatorWindow).GetMethod("CheckBrowserError_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, [window, new RoutedEventArgs()]);
        if (!((TextBlock)window.FindName("ConnectionStatus")).Text.Contains("workspace administrator") || details.Text.Length != 0)
            throw new InvalidOperationException("Browser error was not handled safely in the UI.");
        Render(content, 924, 920, "connection-window-policy-error.png");
        var signIn = (Button)window.FindName("SignInButton");
        if (signIn.Content?.ToString() != "Continue with ChatGPT" || signIn.ActualWidth <= 0)
            throw new InvalidOperationException("Sign-in button failed layout.");
        ((RadioButton)window.FindName("ApiKeyOption")).IsChecked = true;
        if (((FrameworkElement)window.FindName("ApiKeyPanel")).Visibility != Visibility.Visible ||
            ((FrameworkElement)window.FindName("ChatGptPanel")).Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("API key choice did not switch connection panels.");
        Render(content, 924, 820, "connection-api-key.png");
        Render(content, 764, 580, "connection-api-key-small.png");
        ((PasswordBox)window.FindName("ApiKeyInput")).Password = "test-unsaved-key";
        ((RadioButton)window.FindName("ChatGptOption")).IsChecked = true;
        if (((PasswordBox)window.FindName("ApiKeyInput")).Password.Length != 0)
            throw new InvalidOperationException("Switching method did not clear unsaved input.");
        Console.WriteLine("Both connection choices render; switching clears unsaved API key input.");
        testConnection.SaveKey("fake-key-for-local-layout-test");
        Invoke("OpenInitialScreen");
        if (((FrameworkElement)window.FindName("WelcomeScreen")).Visibility != Visibility.Collapsed || ((FrameworkElement)window.FindName("ConversationScreen")).Visibility != Visibility.Visible)
            throw new InvalidOperationException("Saved connection did not bypass welcome.");
        if (!(bool)Invoke("TryOpenWorkspace")!) throw new InvalidOperationException("Saved API key did not open workspace.");
        var prompt = (TextBox)window.FindName("PromptInput");
        var fixture = Path.Combine(testDirectory, "product-reference.txt");
        File.WriteAllText(fixture, "Local layout fixture");
        Invoke("AddReferenceFiles", (object)new[] { fixture, fixture });
        var attachments = (ItemsControl)window.FindName("ReferenceAttachments");
        if (attachments.Items.Count != 1) throw new InvalidOperationException("Reference selection did not deduplicate files.");
        Render(content, 424, 740, "conversation-attachment.png");
        Invoke("WorkspaceSettings_Click", window, new RoutedEventArgs());
        Invoke("TryOpenWorkspace");
        if (attachments.Items.Count != 1) throw new InvalidOperationException("Settings navigation lost references.");
        Invoke("RemoveAttachment_Click", new Button { Tag = attachments.Items[0] }, new RoutedEventArgs());
        if (attachments.Items.Count != 0) throw new InvalidOperationException("Remove reference failed.");
        Render(content, 924, 720, "conversation-window.png");
        Render(content, 424, 740, "conversation-window-narrow.png");
        prompt.Text = "Create a chair from these product references.";
        Invoke("WorkspaceSettings_Click", window, new RoutedEventArgs());
        if (((FrameworkElement)window.FindName("ConnectionScreen")).Visibility != Visibility.Visible)
            throw new InvalidOperationException("Settings did not open.");
        if (!(bool)Invoke("TryOpenWorkspace")! || prompt.Text != "Create a chair from these product references.")
            throw new InvalidOperationException("Draft did not survive settings navigation.");
        Render(content, 424, 740, "conversation-window-draft.png");
        var composer = (FrameworkElement)window.FindName("ComposerArea");
        var composerTop = composer.TranslatePoint(new Point(0, 0), content).Y;
        if (composerTop < 400 || composerTop + composer.ActualHeight > 740)
            throw new InvalidOperationException("Composer was not anchored inside the bottom of the viewport.");
        if (((Button)window.FindName("SendPromptButton")).IsEnabled)
            throw new InvalidOperationException("Send must not pretend inference is connected.");
        var responses = (ItemsControl)window.FindName("ConversationMessages");
        for (var i = 0; i < 20; i++) responses.Items.Add("Layout fixture only: response paragraph " + i);
        Render(content, 424, 740, "conversation-response-layout-test.png");
        var scroller = (ScrollViewer)window.FindName("ResponseScrollViewer");
        if (scroller.ScrollableHeight <= 0 || ((FrameworkElement)window.FindName("ConversationEmptyState")).Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Response area did not scroll or empty state remained visible.");
        if (Math.Abs(composer.TranslatePoint(new Point(0, 0), content).Y - composerTop) > 1)
            throw new InvalidOperationException("Response content moved the bottom composer.");
        responses.Items.Clear();
        testConnection.RemoveKey();
        if ((bool)Invoke("TryOpenWorkspace")!) throw new InvalidOperationException("Removed key still opened workspace.");
        Console.WriteLine("Workspace entry, settings return, draft preservation, narrow layout and revoked-credential navigation passed.");
        Console.WriteLine("WPF controls rendered at normal and minimum size; browser error presentation passed.");
        VerifyGeneration(testStore, testConnection, testDirectory);
        window.Close();
        application.Shutdown();
        Directory.Delete(testDirectory, true);
    }

    private static void VerifyGeneration(CredentialStore store, ApiKeyConnection connection, string directory)
    {
        connection.SaveKey("fake-key-not-sent-to-network");
        var service = new FixtureBuildService();
        using var http = new HttpClient(new FixtureHttpHandler());
        var window = new CreatorWindow(new ChatGptConnection(store), connection, service, new GenerationClient(http), Path.Combine(directory, "model.txt"));
        object? Invoke(string method, params object[] args) => typeof(CreatorWindow).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, args);
        Invoke("OpenInitialScreen");
        Invoke("SetSelectedModel", "gpt-6.1-sol");
        ((TextBox)window.FindName("PromptInput")).Text = "Preview a test fixture";
        var handle = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        var enter = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, System.Windows.Interop.HwndSource.FromHwnd(handle), 0, System.Windows.Input.Key.Enter)
        { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
        ((TextBox)window.FindName("PromptInput")).RaiseEvent(enter);
        if (!enter.Handled) throw new InvalidOperationException("Enter was not handled as Send.");
        Pump(WaitForGeneration(window));
        if (((FrameworkElement)window.FindName("PreviewCard")).Visibility != Visibility.Visible || !((Button)window.FindName("CreateFamilyButton")).IsEnabled)
            throw new InvalidOperationException("Successful generation did not show a creatable preview.");
        var content = (FrameworkElement)window.Content;
        Render(content, 624, 850, "generation-preview-fixture.png");
        Render(content, 424, 740, "generation-preview-narrow-fixture.png");
        new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        var dialog = new CreateFamilyDialog(window, service.Preview!, [new FamilyCategoryChoice(-2000151, "Generic Models")], "fixture-template.rft");
        Render((FrameworkElement)dialog.Content, 464, 530, "create-family-dialog-fixture.png");
        dialog.Close();
        var modelPicker = new ModelPickerDialog(window, ["gpt-6.1-sol", "gpt-6-astra"], "gpt-6.1-sol");
        Render((FrameworkElement)modelPicker.Content, 524, 620, "model-picker.png");
        Render((FrameworkElement)modelPicker.Content, 404, 460, "model-picker-compact.png");
        var search = (TextBox)modelPicker.FindName("ModelSearch");
        var modelList = (ListBox)modelPicker.FindName("ModelList");
        var useModel = (Button)modelPicker.FindName("UseModelButton");
        search.Text = "GPT-6.1 Sol";
        if (modelList.Items.Count != 1 || !useModel.IsEnabled) throw new InvalidOperationException("Friendly-name search did not preserve selected model.");
        search.Text = "no-such-model";
        if (modelList.Items.Count != 0 || useModel.IsEnabled) throw new InvalidOperationException("Empty search could select an invisible model.");
        var manual = (TextBox)modelPicker.FindName("ManualModel");
        manual.Text = "custom-model";
        if (!useModel.IsEnabled) throw new InvalidOperationException("Manual model entry was unavailable.");
        search.Text = "";
        modelList.SelectedIndex = 0;
        if (manual.Text.Length != 0 || ((TextBlock)modelPicker.FindName("SelectionSummary")).Text != "GPT-6.1 Sol")
            throw new InvalidOperationException("Card selection did not replace manual input.");
        if (modelPicker.SelectedModel is not null) throw new InvalidOperationException("Browsing the picker changed the model before confirmation.");
        modelPicker.Close();
        service.FailNext = true;
        ((TextBox)window.FindName("PromptInput")).Text = "Revision that fails native validation";
        Pump((Task)Invoke("GeneratePreviewAsync")!);
        if (((FrameworkElement)window.FindName("PreviewCard")).Visibility != Visibility.Collapsed || ((Button)window.FindName("CreateFamilyButton")).IsEnabled)
            throw new InvalidOperationException("Failed revision left old preview creatable.");
        if (!((TextBlock)window.FindName("GenerationStatus")).Text.Contains("fixture native failure"))
            throw new InvalidOperationException("Native validation failure did not reach the conversation UI.");
        Invoke("NewConversation_Click", window, new RoutedEventArgs());
        if (((ItemsControl)window.FindName("ConversationMessages")).Items.Count != 0)
            throw new InvalidOperationException("New conversation did not clear previous context.");
        window.Close();
        if (!service.Disposed) throw new InvalidOperationException("Closing did not dispose the Revit service.");
        Console.WriteLine("Mocked generation, preview rendering, creation dialog, failed-revision invalidation and new-chat checks passed. No live AI or Revit execution in this harness.");
    }

    private static void Pump(Task task)
    {
        var frame = new DispatcherFrame();
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (task.IsCompleted || DateTime.UtcNow - started > TimeSpan.FromSeconds(15)) frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (!task.IsCompleted) throw new TimeoutException("UI generation fixture timed out.");
        task.GetAwaiter().GetResult();
    }

    private static async Task WaitForGeneration(CreatorWindow window)
    {
        var field = typeof(CreatorWindow).GetField("generationCancellation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        while (field.GetValue(window) is not null) await Task.Delay(10);
    }

    private sealed class FixtureBuildService : IFamilyBuildService
    {
        public bool FailNext { get; set; }
        public bool Disposed { get; private set; }
        public NativePreview? Preview { get; private set; }
        public Task<NativePreview> PreviewAsync(FamilyRecipe recipe, CancellationToken token)
        {
            if (FailNext) throw new InvalidOperationException("fixture native failure");
            // Explicitly synthetic UI fixture, not evidence of native Revit geometry.
            Preview = new NativePreview(recipe, [new PreviewMesh("#2288AA", [[0,0,0],[100,0,0],[0,100,0], [0,0,0],[0,0,100],[100,0,0], [100,0,0],[0,0,100],[0,100,0], [0,100,0],[0,0,100],[0,0,0]])], "UI fixture project");
            return Task.FromResult(Preview);
        }
        public Task<(FamilyCategoryChoice[] Categories, string TemplatePath)> GetOptionsAsync(CancellationToken token) => Task.FromResult((new[] { new FamilyCategoryChoice(-2000151, "Generic Models") }, "fixture.rft"));
        public Task<string> CreateAsync(NativePreview preview, FamilyCreationOptions options, CancellationToken token) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
    }

    private sealed class FixtureHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var recipe = new FamilyRecipe("UI fixture only: inspect preview before creating.", "Fixture family", "Standard", "Generic Models",
                [new SolidPart("Fixture", "#2288AA", [new Profile("circle", [0,0,0], [0,0,1], 10, []), new Profile("circle", [0,0,100], [0,0,1], 10, [])])]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { status = "completed", output = new[] { new { content = new[] { new { type = "output_text", text = JsonSerializer.Serialize(recipe, FamilyRecipe.JsonOptions) } } } } })) });
        }
    }

    private static void Render(FrameworkElement content, int width, int height, string name)
    {
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name));
        encoder.Save(stream);
    }
}
