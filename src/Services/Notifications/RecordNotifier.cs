using Admins.Contract;
using Admins.Database;
using Admins.Services.Bans;
using Admins.Services.Comms;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;

namespace Admins.Services.Notifications;

/// <summary>
/// Tells online admins (<c>admins.notify</c>) when a player with bans or sanctions on record connects.
/// </summary>
public sealed class RecordNotifier
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<RecordNotifier> _logger;
    private readonly AdminsDatabase _database;
    private readonly BansService _bans;
    private readonly CommsService _comms;
    private readonly Messenger _messenger;

    public RecordNotifier(ISwiftlyCore core, ILogger<RecordNotifier> logger, AdminsDatabase database, BansService bans, CommsService comms, Messenger messenger)
    {
        _core = core;
        _logger = logger;
        _database = database;
        _bans = bans;
        _comms = comms;
        _messenger = messenger;

        core.Registrator.Register(this);
    }

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent e)
    {
        var playerId = e.PlayerId;
        _core.Scheduler.NextTick(() =>
        {
            if (!_database.Enabled || _core.PlayerManager.GetPlayer(playerId) is not { IsFakeClient: false } player) return;

            var target = PlayerRef.Of(player);

            _ = Task.Run(async () =>
            {
                try
                {
                    var bans = await _bans.FindBansAsync((long)target.SteamId, target.Ip).ConfigureAwait(false);
                    var sanctions = await _comms.FindSanctionsAsync((long)target.SteamId, target.Ip).ConfigureAwait(false);
                    var gags = sanctions.Count(s => s.SanctionKind == SanctionKind.Gag);
                    var mutes = sanctions.Count(s => s.SanctionKind == SanctionKind.Mute);

                    if (bans.Count == 0 && gags == 0 && mutes == 0) return;

                    _core.Scheduler.NextTick(() => _messenger.ToPermission("admins.notify", "notification.player_record", target.Name, bans.Count, gags, mutes));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load the record of {SteamId}.", target.SteamId);
                }
            });
        });
    }
}
