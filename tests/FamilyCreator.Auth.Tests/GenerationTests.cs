using FamilyCreator.Generation;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FamilyCreator.Auth.Tests;

public sealed class GenerationTests
{
    private static FamilyRecipe Recipe() => new("Dimensions confirmed: 100 mm tall cylinder.", "Cylinder", "100 mm", "Generic Models",
        [new SolidPart("Body", "#226688", [new Profile("circle", [0, 0, 0], [0, 0, 1], 20, []), new Profile("circle", [0, 0, 100], [0, 0, 1], 20, [])])]);

    [Fact]
    public async Task ModelCatalogUsesKeyAndReturnsUniqueSortedIds()
    {
        using var http = new HttpClient(new Handler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.openai.com/v1/models", request.RequestUri!.AbsoluteUri);
            Assert.Equal("fixture-key", request.Headers.Authorization!.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"id\":\"model-b\"},{\"id\":\"model-a\"},{\"id\":\"model-b\"}]}") });
        }));
        Assert.Equal(new[] { "model-a", "model-b" }, await new GenerationClient(http).ListModelsAsync("fixture-key", default));
    }

    [Fact]
    public async Task MissingQuotaExplainsWhyModelListingIsNotGenerationAccess()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        { Content = new StringContent("{\"error\":{\"code\":\"insufficient_quota\",\"message\":\"secret details\"}}") })));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new GenerationClient(http).GenerateAsync("key", "model", "Create", "", null, [], default));
        Assert.Contains("insufficient API quota", error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Fact]
    public async Task EmptyCreditsMessageIsActionableWithoutEchoingProviderDetails()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        { Content = new StringContent("{\"error\":{\"code\":null,\"message\":\"You have no credits remaining. Secret provider details\"}}") })));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new GenerationClient(http).GenerateAsync("key", "model", "Create", "", null, [], default));
        Assert.Contains("no credits remaining", error.Message);
        Assert.DoesNotContain("Secret", error.Message);
    }

    [Fact]
    public void RecipeRoundTripsAndRetainsDimensions()
    {
        var recipe = FamilyRecipe.Parse(JsonSerializer.Serialize(Recipe(), FamilyRecipe.JsonOptions));
        Assert.Equal(100, recipe.Parts[0].Profiles[1].Center[2]);
        Assert.Equal("#226688", recipe.Parts[0].Color);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(100001)]
    public void InvalidCoordinatesCannotReachRevit(double coordinate)
    {
        var recipe = Recipe();
        recipe.Parts[0].Profiles[0].Center[0] = coordinate;
        Assert.Throws<InvalidOperationException>(recipe.Validate);
    }

    [Fact]
    public void MissingFieldsAndUnknownExecutableFieldsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => FamilyRecipe.Parse("{}"));
        var json = JsonSerializer.Serialize(Recipe(), FamilyRecipe.JsonOptions);
        Assert.Throws<InvalidOperationException>(() => FamilyRecipe.Parse(json.Insert(1, "\"python\":\"print(1)\",")));
    }

    [Fact]
    public void InvalidProfilesAndComplexityAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => (Recipe() with { Parts = Enumerable.Repeat(Recipe().Parts[0], 101).ToArray() }).Validate());
        var recipe = Recipe();
        recipe.Parts[0].Profiles[0].Normal[2] = 0;
        Assert.Throws<InvalidOperationException>(recipe.Validate);
    }

    [Theory]
    [InlineData("")] [InlineData("a/b")] [InlineData("x\ny")]
    public void InvalidFamilyNamesAreRejected(string name) => Assert.Throws<InvalidOperationException>(() => FamilyRecipe.ValidateName(name, "family name"));

    [Fact]
    public async Task SendsSelectedModelAndReferencesWithStrictSchemaAndNoStoredResponse()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FamilyCreatorGeneration-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var text = Path.Combine(directory, "spec.txt");
            var pdf = Path.Combine(directory, "drawing.pdf");
            var png = Path.Combine(directory, "reference.png");
            await File.WriteAllTextAsync(text, "Height: 100 mm");
            await File.WriteAllBytesAsync(pdf, Encoding.ASCII.GetBytes("%PDF-fixture"));
            await File.WriteAllBytesAsync(png, [137, 80, 78, 71]);
            using var http = new HttpClient(new Handler(async (request, token) =>
            {
                Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.AbsoluteUri);
                Assert.Equal("fixture-key", request.Headers.Authorization!.Parameter);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                var root = body.RootElement;
                Assert.Equal("user-selected-model", root.GetProperty("model").GetString());
                Assert.False(root.GetProperty("store").GetBoolean());
                Assert.True(root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
                Assert.Contains("REFERENCE BASIS", root.GetProperty("instructions").GetString());
                var content = root.GetProperty("input")[0].GetProperty("content");
                Assert.Equal(4, content.GetArrayLength());
                Assert.Contains("100 mm", content[1].GetProperty("text").GetString());
                Assert.StartsWith("data:application/pdf;base64,", content[2].GetProperty("file_data").GetString());
                Assert.StartsWith("data:image/png;base64,", content[3].GetProperty("image_url").GetString());
                Assert.DoesNotContain(directory, root.ToString());
                return Response(Recipe());
            }));
            var result = await new GenerationClient(http).GenerateAsync("fixture-key", "user-selected-model", "Create this", "", null, [text, pdf, png], default);
            Assert.Equal("Cylinder", result.FamilyName);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(401)] [InlineData(403)] [InlineData(429)] [InlineData(400)] [InlineData(500)]
    public async Task ProviderErrorsDoNotExposeBodiesOrProduceGeometry(int status)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("secret provider body") })));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new GenerationClient(http).GenerateAsync("key", "selected", "Build", "", null, [], default));
        Assert.DoesNotContain("secret", error.Message);
    }

    [Theory]
    [InlineData("{\"status\":\"incomplete\",\"output\":[]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[{\"content\":[{\"type\":\"refusal\"}]}]}")]
    public async Task IncompleteAndRefusedResponsesCannotCreateRecipes(string body)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GenerationClient(http).GenerateAsync("key", "selected", "Build", "", null, [], default));
    }

    [Fact]
    public async Task CancellationStopsTheRequest()
    {
        using var cancellation = new CancellationTokenSource();
        using var http = new HttpClient(new Handler(async (_, token) => { cancellation.Cancel(); await Task.Delay(10000, token); return Response(Recipe()); }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GenerationClient(http).GenerateAsync("key", "selected", "Build", "", null, [], cancellation.Token));
    }

    [Fact]
    public async Task TooManyAttachmentsAreRejectedBeforeNetwork()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new Exception("Network must not be called")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GenerationClient(http).GenerateAsync("key", "selected", "Build", "", null, Enumerable.Repeat("fixture.pdf", 11).ToArray(), default));
    }

    private static HttpResponseMessage Response(FamilyRecipe recipe) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { status = "completed", output = new[] { new { content = new[] { new { type = "output_text", text = JsonSerializer.Serialize(recipe, FamilyRecipe.JsonOptions) } } } } }))
    };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request, cancellationToken);
    }
}
