using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FamilyCreator.Generation;

public sealed record Profile(string Kind, double[] Center, double[] Normal, double Radius, double[][] Vertices);
public sealed record SolidPart(string Name, string Color, Profile[] Profiles);
public sealed record FamilyRecipe(string Message, string FamilyName, string TypeName, string Category, SolidPart[] Parts)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public static FamilyRecipe Parse(string json)
    {
        try
        {
            if (json.Length > 2_000_000) throw new InvalidOperationException("The geometry response is too large.");
            var recipe = JsonSerializer.Deserialize<FamilyRecipe>(json, JsonOptions) ?? throw new JsonException();
            recipe.Validate();
            return recipe;
        }
        catch (JsonException) { throw new InvalidOperationException("The AI returned an invalid geometry recipe. Ask it to simplify or correct the model."); }
    }

    public void Validate()
    {
        if (Message is null || Message.Length > 20000 || FamilyName is null || TypeName is null || Category is null || Parts is null || Parts.Length > 100)
            throw new InvalidOperationException("Invalid family response or too many solid parts (maximum 100).");
        var budget = 0;
        foreach (var part in Parts)
        {
            if (part is null || string.IsNullOrWhiteSpace(part.Name) || part.Name.Length > 150 || part.Color is null || !Regex.IsMatch(part.Color, "^#[0-9a-fA-F]{6}$") || part.Profiles is null || part.Profiles.Length is < 2 or > 48)
                throw new InvalidOperationException("Each solid needs a name, hex color and 2–48 profiles.");
            foreach (var profile in part.Profiles)
            {
                if (profile is null || profile.Center is null || profile.Normal is null || profile.Vertices is null)
                    throw new InvalidOperationException("A solid profile is missing geometry.");
                Vector(profile.Center); Vector(profile.Normal);
                if (profile.Kind == "circle")
                {
                    if (!double.IsFinite(profile.Radius) || profile.Radius < 0.5 || profile.Radius > 50000 || profile.Normal.Sum(v => v * v) < 0.000001 || profile.Vertices.Length != 0)
                        throw new InvalidOperationException("Circle profiles need a valid normal, radius and empty vertices.");
                    budget += 16;
                }
                else if (profile.Kind == "polygon")
                {
                    if (profile.Vertices.Length is < 3 or > 64) throw new InvalidOperationException("Polygon profiles need 3–64 vertices.");
                    foreach (var vertex in profile.Vertices) Vector(vertex);
                    budget += profile.Vertices.Length;
                }
                else throw new InvalidOperationException("Only circle and polygon profiles are supported.");
                if (budget > 12000) throw new InvalidOperationException("This model is too complex. Simplify it before previewing.");
            }
        }
    }

    private static void Vector(double[]? value)
    {
        if (value is null || value.Length != 3 || value.Any(n => !double.IsFinite(n) || Math.Abs(n) > 100000))
            throw new InvalidOperationException("Geometry coordinates must be finite XYZ values within 100 metres of the origin.");
    }

    public static string ValidateName(string value, string label)
    {
        var name = value.Trim();
        if (name.Length is < 1 or > 120 || name.IndexOfAny("\\/:{}[]|;<>?`~\"".ToCharArray()) >= 0 || name.Any(char.IsControl))
            throw new InvalidOperationException($"Enter a valid {label} (1–120 characters, without reserved symbols).");
        return name;
    }
}

public sealed record PreviewMesh(string Color, double[][] Vertices);
public sealed record NativePreview(FamilyRecipe Recipe, PreviewMesh[] Meshes, string TargetTitle);
public sealed record FamilyCategoryChoice(long Id, string Name)
{
    public override string ToString() => Name;
}
public sealed record FamilyCreationOptions(long CategoryId, string FamilyName, string TypeName, string TemplatePath);
public interface IFamilyBuildService : IDisposable
{
    Task<NativePreview> PreviewAsync(FamilyRecipe recipe, CancellationToken token);
    Task<(FamilyCategoryChoice[] Categories, string TemplatePath)> GetOptionsAsync(CancellationToken token);
    Task<string> CreateAsync(NativePreview preview, FamilyCreationOptions options, CancellationToken token);
}
