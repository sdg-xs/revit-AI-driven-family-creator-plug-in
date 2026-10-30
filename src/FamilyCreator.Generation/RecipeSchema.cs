using System.Text.Json;

namespace FamilyCreator.Generation;

internal static class RecipeSchema
{
    private static object Obj(Dictionary<string, object> properties) => new { type = "object", properties, required = properties.Keys.ToArray(), additionalProperties = false };
    private static object Arr(object items) => new { type = "array", items };
    public static JsonElement Create()
    {
        var number = new { type = "number" };
        var text = new { type = "string" };
        var profile = Obj(new()
        {
            ["kind"] = new { type = "string", @enum = new[] { "circle", "polygon" } },
            ["center"] = Arr(number), ["normal"] = Arr(number), ["radius"] = number, ["vertices"] = Arr(Arr(number))
        });
        var part = Obj(new() { ["name"] = text, ["color"] = text, ["profiles"] = Arr(profile) });
        return JsonSerializer.SerializeToElement(Obj(new()
        {
            ["message"] = text, ["familyName"] = text, ["typeName"] = text, ["category"] = text, ["parts"] = Arr(part)
        }));
    }
}
