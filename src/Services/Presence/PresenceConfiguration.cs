using System.ComponentModel;

namespace Admins.Services.Presence;

public sealed class PresenceConfiguration
{
    [Description("Replace the game's join and leave chat messages with the plugin's own ('presence.*' in the translations). !hide only fakes a leave and a rejoin while this is on; when off the game keeps its messages and !hide is silent.")]
    public bool ConnectMessages { get; set; } = true;
}

public sealed class StatusConfiguration
{
    [Description("Answer the 'status' console command of players with the lines below, leaving hidden admins out. The server console and RCON keep the real output.")]
    public bool Enabled { get; set; } = true;

    [Description("Lines above the player list. Placeholders: {hostname}, {map}, {humans}, {bots}, {maxplayers}, {date}, {time}.")]
    public List<string> Header { get; set; } =
    [
        "hostname : {hostname}",
        "map      : {map}",
        "players  : {humans} humans, {bots} bots ({maxplayers} max)",
        "---------players--------",
        "# userid name                     steamid             time     ping",
    ];

    [Description("One line per player. Placeholders: {userid}, {name}, {steamid}, {duration}, {ping}. A width pads or cuts the value: {name,-24} is left aligned in 24 columns, {ping,4} right aligned in 4.")]
    public string Row { get; set; } = "# {userid,-6} {name,-24} {steamid,-19} {duration,-8} {ping,4}";

    [Description("Lines below the player list. Same placeholders as Header.")]
    public List<string> Footer { get; set; } = ["#end"];
}
