using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;

namespace Admins.Commands;

public partial class AdminCommands
{
    private ISwiftlyCore Core = null!;

    public void Init()
    {
        Core = Admins.SwiftlyCore;
        Core.Registrator.Register(this);
    }

    public void SendSyntax(ICommandContext context, string cmdname, string[] arguments)
    {
        var localizer = GetPlayerLocalizer(context);
        context.Reply(localizer["command.syntax", Admins.Config.CurrentValue.Prefix, context.Prefix, cmdname, string.Join(" ", arguments)]);
    }

    public ILocalizer GetPlayerLocalizer(ICommandContext context)
    {
        if (context.IsSentByPlayer) return Core.Translation.GetPlayerLocalizer(context.Sender!);
        else return Core.Localizer;
    }

    public void SendByPlayerOnly(ICommandContext context)
    {
        var localizer = GetPlayerLocalizer(context);
        context.Reply(localizer["command.player_only", Admins.Config.CurrentValue.Prefix]);
    }

    public void SendMessageToPlayers(IEnumerable<IPlayer> players, IPlayer? sender, Func<IPlayer, ILocalizer, (string, MessageType)> messageBuilder)
    {
        foreach (var player in players)
        {
            var localizer = Core.Translation.GetPlayerLocalizer(player);
            var message = messageBuilder(player, localizer);
            if (sender != player && sender != null) sender.SendMessage(message.Item2, message.Item1);
            player.SendMessage(message.Item2, message.Item1);
        }
    }
}