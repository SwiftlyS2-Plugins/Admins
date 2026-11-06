using System.Text.Json;

namespace Admins.Database.Models;

internal static class JsonList
{
    // Rows edited by hand or by web panels can hold an empty string or invalid JSON; a throw here would
    // make every admin unloadable, so a bad cell reads as an empty list instead.
    public static List<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
