using FamilyCreator.Auth;
using FamilyCreator.Generation;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FamilyCreator.Revit;

public partial class CreatorWindow
{
    private readonly IFamilyBuildService? buildService;
    private readonly GenerationClient generationClient;
    private readonly string modelPreferencePath;
    private CancellationTokenSource? generationCancellation;
    private FamilyRecipe? previousRecipe;
    private NativePreview? nativePreview;
    private string conversationContext = "";
    private bool generationUsesApiKey;
    private string selectedModelId = "";
    private string[]? availableModelIds;

    private void InitializeGeneration()
    {
        try { if (File.Exists(modelPreferencePath)) SetSelectedModel(File.ReadAllText(modelPreferencePath).Trim()); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Closed += (_, _) => { generationCancellation?.Cancel(); buildService?.Dispose(); };
    }

    private void SetSelectedModel(string id)
    {
        selectedModelId = id.Trim();
        SelectedModelLabel.Text = ModelPickerDialog.DisplayName(selectedModelId);
        ChooseModelButton.ToolTip = selectedModelId.Length == 0 ? "Choose an API model" : "Change model · " + selectedModelId;
        RefreshGenerationControls();
    }

    private void RefreshGenerationControls()
    {
        if (SendPromptButton is null || SelectedModelLabel is null || CreateFamilyButton is null) return;
        var busy = generationCancellation is not null;
        var reason = busy ? "Working… Cancel to stop waiting."
            : buildService is null ? "Open this window from Revit to generate a family."
            : !generationUsesApiKey ? "Select Use API key in Settings to generate."
            : string.IsNullOrWhiteSpace(selectedModelId) ? "Choose a model to enable Send."
            : string.IsNullOrWhiteSpace(PromptInput.Text) ? "Describe your family to enable Send."
            : "Enter to send · Shift+Enter for a new line";
        SendPromptButton.IsEnabled = !busy && generationUsesApiKey && buildService is not null && !string.IsNullOrWhiteSpace(PromptInput.Text) && !string.IsNullOrWhiteSpace(selectedModelId);
        SendPromptButton.ToolTip = reason;
        if (ComposerHint is not null) ComposerHint.Text = reason;
        if (ChooseModelButton is not null) ChooseModelButton.IsEnabled = !busy && generationUsesApiKey;
        CreateFamilyButton.IsEnabled = !busy && nativePreview is not null;
        WorkspaceSettingsButton.IsEnabled = !busy;
        NewConversationButton.IsEnabled = !busy;
        PromptInput.IsReadOnly = busy;
        AddAttachmentButton.IsEnabled = !busy;
        ReferenceAttachments.IsEnabled = !busy;
        CancelGenerationButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SendPrompt_Click(object sender, RoutedEventArgs e)
        => await GeneratePreviewAsync();

    private async void PromptInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) != 0) return;
        e.Handled = true;
        if (SendPromptButton.IsEnabled) await GeneratePreviewAsync();
        else if (generationCancellation is null && string.IsNullOrWhiteSpace(selectedModelId)) ChooseModelButton.Focus();
    }

    private async void ChooseModel_Click(object sender, RoutedEventArgs e)
    {
        if (generationCancellation is not null) return;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        generationCancellation = cancellation;
        RefreshGenerationControls();
        try
        {
            var settings = apiKeyConnection.ReadSettings();
            if (settings.Method != ConnectionMethod.ApiKey || settings.ApiKey is null) throw new InvalidOperationException("Select an API key in Settings first.");
            GenerationStatus.Text = "Loading models available to your API key…";
            string[] models;
            string? catalogNotice = null;
            try { models = availableModelIds ??= await generationClient.ListModelsAsync(settings.ApiKey.Value, cancellation.Token); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                models = [];
                catalogNotice = GenerationError(exception) + " You can enter the model ID manually below.";
            }
            if (closed) return;
            var picker = new ModelPickerDialog(this, models, selectedModelId, catalogNotice);
            if (picker.ShowDialog() == true && picker.SelectedModel is string selectedModel)
            {
                SetSelectedModel(selectedModel);
                SaveModelPreference(selectedModel);
            }
            GenerationStatus.Text = "Send shares your prompt and attached references with OpenAI. API charges apply.";
        }
        catch (OperationCanceledException) { if (!closed) GenerationStatus.Text = "Model lookup cancelled or timed out. Click the model selector to try again."; }
        catch (Exception exception) { if (!closed) GenerationStatus.Text = GenerationError(exception); }
        finally { generationCancellation = null; if (!closed) RefreshGenerationControls(); }
    }

    private async Task GeneratePreviewAsync()
    {
        if (generationCancellation is not null || buildService is null) return;
        var prompt = PromptInput.Text.Trim();
        if (prompt.Length == 0) return;
        using var cancellation = new CancellationTokenSource();
        generationCancellation = cancellation;
        nativePreview = null;
        PreviewCard.Visibility = Visibility.Collapsed;
        RefreshGenerationControls();
        var sent = false;
        try
        {
            var settings = apiKeyConnection.ReadSettings();
            if (settings.Method != ConnectionMethod.ApiKey || settings.ApiKey is null)
                throw new InvalidOperationException("Generation currently uses an OpenAI API key. Select Use API key in Settings; ChatGPT sign-in is not connected to generation yet.");
            var model = selectedModelId;
            if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("Enter an API model ID first.");
            GenerationStatus.Text = "Checking the active Revit project…";
            await buildService.GetOptionsAsync(cancellation.Token);
            if (closed) return;
            cancellation.Token.ThrowIfCancellationRequested();
            SaveModelPreference(model);
            ConversationMessages.Items.Add("You\n" + prompt);
            sent = true;
            GenerationStatus.Text = "Reading references and generating geometry… Cancel stops waiting; API usage already incurred may still be billed.";
            var paths = ReferenceAttachments.Items.Cast<ReferenceAttachment>().Select(a => a.FilePath).ToArray();
            var recipe = await generationClient.GenerateAsync(settings.ApiKey.Value, model, prompt, conversationContext, previousRecipe, paths, cancellation.Token);
            if (closed) return;
            cancellation.Token.ThrowIfCancellationRequested();
            previousRecipe = recipe;
            conversationContext += $"\nUser: {prompt}\nAssistant: {recipe.Message}\n";
            ConversationMessages.Items.Add("Family Creator\n" + recipe.Message);
            if (recipe.Parts.Length == 0)
            {
                GenerationStatus.Text = "Reply with the requested details to continue. No geometry or family was created.";
                PromptInput.Clear();
                return;
            }
            GenerationStatus.Text = "Validating native Revit solids and preparing the preview…";
            var preview = await buildService.PreviewAsync(recipe, cancellation.Token);
            if (closed) return;
            cancellation.Token.ThrowIfCancellationRequested();
            nativePreview = preview;
            PreviewDimensions.Text = NativeGeometryView.Show(preview);
            PreviewCard.Visibility = Visibility.Visible;
            PromptInput.Clear();
            GenerationStatus.Text = $"Preview ready for {preview.TargetTitle}. Inspect it, request a revision, or choose Create family. Send shares attached references with OpenAI.";
            ResponseScrollViewer.ScrollToEnd();
        }
        catch (OperationCanceledException)
        {
            if (!closed) GenerationStatus.Text = cancellation.IsCancellationRequested ? "Request cancelled. No family was created." : "The request timed out. No family was created; try again.";
        }
        catch (Exception exception)
        {
            if (!closed)
            {
                var message = GenerationError(exception);
                GenerationStatus.Text = message;
                if (sent)
                {
                    ConversationMessages.Items.Add("Request stopped\n" + message);
                    conversationContext += "\nApplication validation: " + message;
                }
            }
        }
        finally
        {
            generationCancellation = null;
            if (!closed) RefreshGenerationControls();
        }
    }

    private async void CreateFamily_Click(object sender, RoutedEventArgs e)
    {
        if (generationCancellation is not null || buildService is null || nativePreview is null) return;
        using var cancellation = new CancellationTokenSource();
        generationCancellation = cancellation;
        RefreshGenerationControls();
        try
        {
            var options = await buildService.GetOptionsAsync(cancellation.Token);
            if (closed) return;
            var dialog = new CreateFamilyDialog(this, nativePreview, options.Categories, options.TemplatePath);
            if (dialog.ShowDialog() != true || dialog.Selection is null) return;
            GenerationStatus.Text = "Creating and loading the previewed family…";
            var result = await buildService.CreateAsync(nativePreview, dialog.Selection, cancellation.Token);
            if (closed) return;
            ConversationMessages.Items.Add(result);
            GenerationStatus.Text = result;
            nativePreview = null;
            ResponseScrollViewer.ScrollToEnd();
        }
        catch (OperationCanceledException) { if (!closed) GenerationStatus.Text = "Creation cancelled before loading."; }
        catch (Exception exception) { if (!closed) GenerationStatus.Text = GenerationError(exception); }
        finally
        {
            generationCancellation = null;
            if (!closed) RefreshGenerationControls();
        }
    }

    private void CancelGeneration_Click(object sender, RoutedEventArgs e) => generationCancellation?.Cancel();

    private void SaveModelPreference(string model)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(modelPreferencePath)!); File.WriteAllText(modelPreferencePath, model); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void NewConversation_Click(object sender, RoutedEventArgs e)
    {
        if (generationCancellation is not null) return;
        previousRecipe = null; nativePreview = null; conversationContext = "";
        ConversationMessages.Items.Clear(); ReferenceAttachments.Items.Clear(); PromptInput.Clear();
        PreviewCard.Visibility = Visibility.Collapsed;
        GenerationStatus.Text = "Send shares your prompt and attached files with OpenAI. API charges apply.";
        UpdateAttachmentStatus(); RefreshGenerationControls();
    }

    private static string GenerationError(Exception e) => e switch
    {
        FileNotFoundException or DirectoryNotFoundException => "A reference file is missing. Remove it and attach the current file.",
        UnauthorizedAccessException => "A reference file could not be read. Check access and attach it again.",
        System.Net.Http.HttpRequestException => "Could not reach OpenAI. Check your connection and try again.",
        IOException => "A reference file could not be read. Close any application locking it and try again.",
        _ => e.Message
    };
}
