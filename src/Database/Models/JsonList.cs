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
            var list = JsonSerializer.Deserialize<List<string>>(value) ?? [];

            // Null entries too ("[null]" is what JSON.stringify([undefined]) writes): every reader trims each one.
            list.RemoveAll(entry => entry is null);
            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
