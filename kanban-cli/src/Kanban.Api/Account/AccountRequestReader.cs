using System.Text.Json;
using Account.Domain;

namespace Kanban.Api.Account;

public static class AccountRequestReader
{
    public const int MaxBodyBytes = 16 * 1024;

    public static async Task<JsonDocument> ReadStrictObjectAsync(HttpRequest request, params string[] allowedProperties)
    {
        request.EnableBuffering();
        if (request.ContentLength is > MaxBodyBytes)
        {
            throw new AccountRefusalException(RefusalCodes.RequestInvalid);
        }

        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        if (System.Text.Encoding.UTF8.GetByteCount(body) > MaxBodyBytes)
        {
            throw new AccountRefusalException(RefusalCodes.RequestInvalid);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new AccountRefusalException(RefusalCodes.RequestInvalid);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new AccountRefusalException(RefusalCodes.RequestInvalid);
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            doc.Dispose();
            throw new AccountRefusalException(RefusalCodes.RequestInvalid);
        }

        var allowed = new HashSet<string>(allowedProperties, StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!allowed.Contains(prop.Name))
            {
                doc.Dispose();
                throw new AccountRefusalException(RefusalCodes.RequestInvalid);
            }
        }

        return doc;
    }

    public static string RequireString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new AccountRefusalException(RefusalCodes.RequestInvalid);
        }

        return value.GetString() ?? string.Empty;
    }

    public static string? OptionalNullableString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new AccountRefusalException(RefusalCodes.RequestInvalid);
        }

        return value.GetString();
    }

    public static IReadOnlyList<string>? OptionalStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new AccountRefusalException(RefusalCodes.RequestInvalid);
        }

        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new AccountRefusalException(RefusalCodes.RequestInvalid);
            }

            list.Add(item.GetString() ?? string.Empty);
        }

        return list;
    }
}