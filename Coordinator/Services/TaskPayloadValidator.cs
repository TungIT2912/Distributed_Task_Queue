using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace Coordinator.Services;

public static class TaskPayloadValidator
{
    public static string? Validate(string? taskType, string? payload)
    {
        if (taskType is not ("Compute" or "DataProcessing" or "Email" or "Default"))
            return "Unsupported task type";
        if (string.IsNullOrWhiteSpace(payload)) return "Payload is required";

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return "Payload must be a JSON object";

            static bool HasString(JsonElement obj, string key, out string value)
            {
                value = "";
                if (!obj.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String) return false;
                value = el.GetString() ?? "";
                return true;
            }
            static bool HasFiniteNumber(JsonElement obj, string key, out double value)
            {
                value = 0;
                return obj.TryGetProperty(key, out var el)
                    && el.ValueKind == JsonValueKind.Number
                    && el.TryGetDouble(out value)
                    && double.IsFinite(value);
            }

            switch (taskType)
            {
                case "Compute":
                    if (!HasString(root, "operation", out var op) ||
                        op is not ("add" or "subtract" or "multiply" or "divide"))
                        return "Compute operation must be add, subtract, multiply or divide";
                    if (!HasFiniteNumber(root, "a", out _) || !HasFiniteNumber(root, "b", out var b))
                        return "Compute requires numeric a and b";
                    if (op == "divide" && b == 0) return "Cannot divide by zero";
                    break;
                case "DataProcessing":
                    if (!HasString(root, "action", out var action) ||
                        action is not ("sum" or "average" or "max" or "min"))
                        return "Unsupported data processing action";
                    if (!root.TryGetProperty("inputData", out var data) ||
                        data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                        return "inputData must be a non-empty array";
                    foreach (var number in data.EnumerateArray())
                        if (number.ValueKind != JsonValueKind.Number ||
                            !number.TryGetDouble(out var n) || !double.IsFinite(n))
                            return "inputData must contain only finite numbers";
                    break;
                case "Email":
                    if (!HasString(root, "to", out var to) ||
                        !new EmailAddressAttribute().IsValid(to))
                        return "A valid recipient email is required";
                    if (!HasString(root, "subject", out var subject) || string.IsNullOrWhiteSpace(subject))
                        return "Email subject is required";
                    if (!HasString(root, "body", out var body) || string.IsNullOrWhiteSpace(body))
                        return "Email body is required";
                    break;
            }
            return null;
        }
        catch (JsonException)
        {
            return "Payload must contain valid JSON";
        }
    }
}
