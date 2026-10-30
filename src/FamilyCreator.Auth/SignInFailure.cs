using System.Text.Json;
using System.Text.RegularExpressions;

namespace FamilyCreator.Auth;

public static class SignInFailure
{
    public const string PolicyDenied = "3p_delegated_access_policy_denied";
    public static AuthException FromProvider(string? code, string? requestId = null)
    {
        var message = code switch
        {
            PolicyDenied => "OpenAI denied third-party access for the selected account or workspace. Ask your workspace administrator or OpenAI support to check eligibility and delegated-access policy. Repeating this sign-in will not change that policy.",
            "subscription_sharing_user_not_eligible" => "ChatGPT plan access is not available for this account or workspace. Ask your workspace administrator or OpenAI support to check eligibility.",
            "access_denied" => "Sign-in was declined. No new account was connected.",
            "invalid_grant" => "The sign-in code expired or was already used. Start a new sign-in attempt.",
            _ => "OpenAI could not complete sign-in. Check account eligibility with OpenAI support if this continues."
        };
        if (code is PolicyDenied or "subscription_sharing_user_not_eligible" or "access_denied" or "invalid_grant")
            message += "\nError code: " + code;
        if (requestId is not null && Regex.IsMatch(requestId, @"\A(?:[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}|req_[a-zA-Z0-9_-]{1,80})\z"))
            message += "\nRequest ID: " + requestId;
        return new AuthException(message);
    }

    public static AuthException FromBrowserText(string text)
    {
        var code = Regex.Match(text, @"(?:error_code|error)\s*[""']?\s*[:=]\s*[""']?([a-zA-Z0-9_]+)").Groups[1].Value;
        var requestId = Regex.Match(text, @"request_id\s*[""']?\s*[:=]\s*[""']?([a-zA-Z0-9_-]+)").Groups[1].Value;
        return FromProvider(code, requestId);
    }

    public static AuthException FromResponse(string body, string? requestId)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            string? Read(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var code = Read(root, "error_code") ?? Read(root, "error");
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                code ??= Read(error, "code");
            return FromProvider(code, requestId ?? Read(root, "request_id"));
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return FromProvider(null, requestId);
        }
    }
}
