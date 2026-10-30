using FamilyCreator.Auth;
using FamilyCreator.Generation;
using System.Text.Json;

// Opt-in live diagnostic: never print credentials, never attach user documents.
try
{
    var settings = new ApiKeyConnection().ReadSettings();
    if (settings.Method != ConnectionMethod.ApiKey || settings.ApiKey is null) throw new InvalidOperationException("Select and save an API key in the application first.");
    var client = new GenerationClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    if (args.Length == 1 && args[0] == "models")
    {
        var models = await client.ListModelsAsync(settings.ApiKey.Value, cancellation.Token);
        Console.WriteLine(JsonSerializer.Serialize(models));
    }
    else if (args.Length == 3 && args[0] == "generate")
    {
        var recipe = await client.GenerateAsync(settings.ApiKey.Value, args[1],
            "Create a verification fixture: one solid vertical cylinder, radius 20 mm, height 100 mm, bottom at Z=0, center X=Y=0, blue #226688. Family name FC Live Check, type 100 mm, category Generic Models. Use exactly two circle profiles; all dimensions are supplied. This is a synthetic test, not a product reference.",
            "", null, [], cancellation.Token);
        if (recipe.Parts.Length != 1) throw new InvalidOperationException("Expected a single-part verification fixture.");
        var path = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(recipe, FamilyRecipe.JsonOptions));
        Console.WriteLine($"Live generation passed: {recipe.Parts.Length} part, model {args[1]}. Recipe saved to {path}");
    }
    else if (args.Length == 2 && args[0] == "probe")
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey.Value);
        request.Content = System.Net.Http.Json.JsonContent.Create(new { model = args[1], input = "Reply OK.", max_output_tokens = 64, store = false });
        using var response = await http.SendAsync(request, cancellation.Token);
        Console.WriteLine($"Minimal Responses request: HTTP {(int)response.StatusCode}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation.Token));
        if (json.RootElement.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var text) ? text.GetString() ?? "" : "No error message";
            message = message.Replace(settings.ApiKey.Value, "[redacted key]");
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\b(?:sk-|org-|proj_)[A-Za-z0-9_-]+", "[redacted identifier]");
            Console.WriteLine(message[..Math.Min(message.Length, 600)]);
        }
        else Console.WriteLine("Response keys: " + string.Join(", ", json.RootElement.EnumerateObject().Select(p => p.Name)));
    }
    else throw new InvalidOperationException("Usage: models OR generate <explicit-model-id> <output-recipe.json>. Generate makes a billable API request.");
}
catch (Exception e)
{
    Console.Error.WriteLine(e is System.Net.Http.HttpRequestException ? "Network request failed." : e.Message);
    Environment.ExitCode = 1;
}
