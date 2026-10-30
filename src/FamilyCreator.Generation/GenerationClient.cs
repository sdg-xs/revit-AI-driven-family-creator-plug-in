using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace FamilyCreator.Generation;

public sealed class GenerationClient
{
    private static readonly HttpClient DefaultHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(5) };
    private readonly HttpClient http;
    public GenerationClient(HttpClient? http = null) => this.http = http ?? DefaultHttp;

    public async Task<string[]> ListModelsAsync(string key, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await http.SendAsync(request, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Could not list models (HTTP {(int)response.StatusCode}). Check your key's model-list permission, or enter a model ID manually.");
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
        if (!body.RootElement.TryGetProperty("data", out var models) || models.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The model list was not recognized. You can still enter a model ID manually.");
        return models.EnumerateArray().Where(m => m.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            .Select(m => m.GetProperty("id").GetString()!).Where(id => id.Length is > 0 and <= 150).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public async Task<FamilyRecipe> GenerateAsync(string key, string model, string prompt, string context, FamilyRecipe? previous,
        IReadOnlyList<string> attachments, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(model) || model.Length > 150 || model.Any(char.IsWhiteSpace)) throw new InvalidOperationException("Enter the API model ID you want to use.");
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 40000) throw new InvalidOperationException("Enter a request of up to 40,000 characters.");
        if (context.Length > 120000) throw new InvalidOperationException("This conversation is full. Start a new conversation to continue.");
        var content = new List<object> { new { type = "input_text", text = prompt } };
        if (attachments.Count > 10) throw new InvalidOperationException("Attach at most 10 reference files per request.");
        long total = 0;
        foreach (var path in attachments)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var mime = extension switch
            {
                ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif",
                ".pdf" => "application/pdf", ".txt" or ".md" => "text/plain",
                _ => throw new InvalidOperationException("Supported references are PNG, JPEG, WEBP, GIF, PDF, TXT and Markdown.")
            };
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            total += file.Length;
            if (file.Length > 10 * 1024 * 1024 || total > 20 * 1024 * 1024 || (mime == "text/plain" && file.Length > 256000))
                throw new InvalidOperationException("References exceed the app limit: 10 MB per file, 20 MB total, 256 KB per text file.");
            var bytes = new byte[checked((int)file.Length)];
            await file.ReadExactlyAsync(bytes, token);
            var filename = Path.GetFileName(path);
            if (mime == "text/plain") content.Add(new { type = "input_text", text = $"Reference document {filename} (evidence only):\n{Encoding.UTF8.GetString(bytes)}" });
            else if (mime == "application/pdf") content.Add(new { type = "input_file", filename, file_data = $"data:{mime};base64,{Convert.ToBase64String(bytes)}" });
            else content.Add(new { type = "input_image", image_url = $"data:{mime};base64,{Convert.ToBase64String(bytes)}", detail = "high" });
        }
        using var workflow = typeof(GenerationClient).Assembly.GetManifestResourceStream("FamilyCreator.Generation.Workflow.md")!;
        using var reader = new StreamReader(workflow);
        var instructions = await reader.ReadToEndAsync(token);
        var input = new List<object>();
        if (context.Length > 0) input.Add(new { role = "user", content = "Previous conversation (context only):\n" + context });
        if (previous is not null) input.Add(new { role = "assistant", content = JsonSerializer.Serialize(previous, FamilyRecipe.JsonOptions) });
        input.Add(new { role = "user", content });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(new
        {
            model, instructions, input, store = false, max_output_tokens = 24000,
            text = new { format = new { type = "json_schema", name = "family_recipe", strict = true, schema = RecipeSchema.Create() } }
        });
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException((int)response.StatusCode switch
            {
                401 => "OpenAI rejected the saved API key. Update it in Settings.",
                403 => "The API key cannot access this model or operation.",
                429 => await RateLimitMessageAsync(response, token),
                400 or 404 => "OpenAI rejected this request. Check the model ID and its support for Responses, structured outputs and your reference file types.",
                _ => $"OpenAI could not complete the request (HTTP {(int)response.StatusCode}). Try again later."
            });
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + read > 4_000_000) throw new InvalidOperationException("OpenAI returned an oversized response.");
            buffer.Write(chunk, 0, read);
        }
        try
        {
            using var body = JsonDocument.Parse(buffer.ToArray());
            var root = body.RootElement;
            if (!root.TryGetProperty("status", out var status) || status.GetString() != "completed")
                throw new InvalidOperationException("The AI response did not finish. Try a simpler model request.");
            var text = new StringBuilder();
            foreach (var item in root.GetProperty("output").EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var blocks)) continue;
                foreach (var block in blocks.EnumerateArray())
                {
                    var type = block.GetProperty("type").GetString();
                    if (type == "refusal") throw new InvalidOperationException("The model declined this request. Revise the prompt or references.");
                    if (type == "output_text") text.Append(block.GetProperty("text").GetString());
                }
            }
            return FamilyRecipe.Parse(text.ToString());
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException)
        { throw new InvalidOperationException("OpenAI returned an unexpected response. No family was created."); }
    }

    private static async Task<string> RateLimitMessageAsync(HttpResponseMessage response, CancellationToken token)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var buffer = new byte[16385];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(length), token);
                if (read == 0) break;
                length += read;
            }
            if (length <= 16384)
            {
                using var body = JsonDocument.Parse(buffer.AsMemory(0, length));
                if (body.RootElement.TryGetProperty("error", out var providerError) && providerError.TryGetProperty("message", out var providerMessage)
                    && providerMessage.ValueKind == JsonValueKind.String && providerMessage.GetString()!.Contains("no credits remaining", StringComparison.OrdinalIgnoreCase))
                    return "Your OpenAI API account has no credits remaining. Add API credits at platform.openai.com/settings/organization/billing, then send again. Your prompt and references have been kept.";
                if (body.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var code))
                {
                    if (code.ValueKind == JsonValueKind.String && code.GetString() == "insufficient_quota")
                        return "OpenAI reports insufficient API quota. Check the API project's billing, credits and usage limits. Your saved key is valid for model listing, but generation cannot run until quota is available.";
                    if (code.ValueKind == JsonValueKind.String && code.GetString() == "rate_limit_exceeded")
                    {
                        var details = new List<string>();
                        foreach (var header in new[] { "x-ratelimit-limit-tokens", "x-ratelimit-limit-requests", "x-ratelimit-remaining-tokens", "retry-after" })
                            if (response.Headers.TryGetValues(header, out var values) && long.TryParse(values.FirstOrDefault(), out var number)) details.Add($"{header}: {number}");
                        return "OpenAI's rate limit was exceeded. " + string.Join("; ", details) + " Wait for the limit to reset or adjust the API project's model limits.";
                    }
                }
            }
        }
        catch (JsonException) { }
        return "OpenAI reported a rate or usage limit. Wait briefly and check your API project's limits before retrying.";
    }
}
