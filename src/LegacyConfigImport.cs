using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace Admins;

/// <summary>
/// Seeds <c>config.jsonc</c> from the settings of the former Admins.Core, Admins.Bans and Admins.Comms plugins,
/// once, when this plugin has no config yet. The old files are left in place.
/// </summary>
internal static class LegacyConfigImport
{
    private static readonly JsonDocumentOptions ReadOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static void Run(ISwiftlyCore core)
    {
        var target = core.Configuration.GetConfigPath("config.jsonc");
        if (File.Exists(target)) return;

        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(core.Configuration.BasePath));
        if (root is null) return;

        var coreMain = ReadMain(Path.Combine(root, "Admins.Core", "config.jsonc"));
        var bansMain = ReadMain(Path.Combine(root, "Admins.Bans", "config.jsonc"));
        var commsMain = ReadMain(Path.Combine(root, "Admins.Comms", "config.jsonc"));
        if (coreMain is null && bansMain is null && commsMain is null) return;

        var main = new JsonObject();
        Copy(coreMain, "Prefix", main, "Prefix");
        Copy(coreMain, "UseDatabase", main, "UseDatabase");
        Copy(coreMain, "TimeZone", main, "TimeZone");
        Copy(coreMain, "ImmunityMode", main, "ImmunityMode");

        var admins = new JsonObject();
        Copy(coreMain, "AdminsDatabaseSyncIntervalSeconds", admins, "DatabaseSyncIntervalSeconds");
        main["Admins"] = admins;

        var bans = new JsonObject();
        Copy(coreMain, "BansDatabaseSyncIntervalSeconds", bans, "DatabaseSyncIntervalSeconds");
        Copy(bansMain, "BansReasons", bans, "Reasons");
        Copy(bansMain, "BansDurationsInSeconds", bans, "DurationsInSeconds");
        main["Bans"] = bans;

        var comms = new JsonObject();
        Copy(coreMain, "SanctionsDatabaseSyncIntervalSeconds", comms, "DatabaseSyncIntervalSeconds");
        Copy(commsMain, "CommsReasons", comms, "Reasons");
        Copy(commsMain, "CommsDurationsInSeconds", comms, "DurationsInSeconds");
        Copy(commsMain, "EnableAdminChat", comms, "EnableAdminChat");
        Copy(commsMain, "AdminChatStartCharacter", comms, "AdminChatStartCharacter");
        main["Comms"] = comms;

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, new JsonObject { ["Main"] = main }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        core.Logger.LogInformation("Imported settings from the Admins.Core, Admins.Bans and Admins.Comms configs into {Path}.", target);
    }

    private static JsonObject? ReadMain(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path), documentOptions: ReadOptions)?["Main"] as JsonObject : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Copy(JsonObject? source, string from, JsonObject target, string to)
    {
        if (source?[from] is { } value) target[to] = value.DeepClone();
    }
}
