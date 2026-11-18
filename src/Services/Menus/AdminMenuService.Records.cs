using Admins.Contract;
using Admins.Database;
using Admins.Database.Models;
using Admins.Shared;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Players;
using Punishment = Admins.Contract.AdminPunishment;

namespace Admins.Services.Menus;

public sealed partial class AdminMenuService
{
    private const int ListLimit = 100;

    // All first: the header counts every status, so a list opening on active ones looks like it lost the rest.
    private static readonly RecordStatus[] StatusFilters = [RecordStatus.All, RecordStatus.Active, RecordStatus.Expired, RecordStatus.Lifted];
    private static readonly SanctionKind?[] KindFilters = [null, SanctionKind.Gag, SanctionKind.Mute];

    /// <summary>A ban or sanction the way the menu shows it.</summary>
    private readonly record struct RecordView(
        Punishment Kind,
        long Id,
        string Name,
        string Subject,
        long SteamId64,
        string Ip,
        bool ByIp,
        string Reason,
        string Admin,
        long CreatedAt,
        long Length,
        long ExpiresAt,
        bool Global,
        PunishmentStatus Status);

    private sealed class ListView : IAdminRecordList
    {
        public bool Comms { get; init; }

        public RecordStatus Status { get; set; } = StatusFilters[0];

        public SanctionKind? Kind { get; set; }

        public int Version { get; set; }

        public bool Loading { get; set; }

        public bool Failed { get; set; }

        /// <summary>-1 until something has loaded.</summary>
        public int Shown { get; set; } = -1;

        public int Active { get; set; } = -1;

        public int Expired { get; set; } = -1;

        public int Lifted { get; set; } = -1;

        public AdminRecordCounts? Counts => Active < 0 ? null : new AdminRecordCounts(Active, Expired) { Lifted = Lifted };
    }

    private bool BanListAvailable(IPlayer viewer) => CanLiftAny(viewer, Punishment.Ban) || CanPunishAny(viewer, Punishment.Ban);

    private bool CommsListAvailable(IPlayer viewer) => CommsKinds.Any(k => CanLiftAny(viewer, k) || CanPunishAny(viewer, k));

    private IMenu BansPage(IPlayer viewer)
    {
        Func<Func<IMenu>, IMenuComponent>? give = CanPunishAny(viewer, Punishment.Ban)
            ? list => new Link(T(viewer, "menu.bans.give"), _ => TargetPage(viewer, NewDraft(viewer, Punishment.Ban, null, list())))
            {
                Tag = new AdminActionRow(AdminAction.Give),
            }
            : null;

        return RecordsPage(viewer, AdminMenuIds.Bans, T(viewer, "menu.bans.title"), kinds: false, give, async (status, _) =>
        {
            var bans = await _bans.ListAsync(status, ListLimit).ConfigureAwait(false);
            return (bans.Cast<object>().ToList(), await _bans.CountBansAsync().ConfigureAwait(false));
        });
    }

    private IMenu CommsPage(IPlayer viewer)
    {
        var kinds = CommsKinds.Where(k => CanPunishAny(viewer, k)).ToList();

        // With one kind allowed there is nothing to ask after the player.
        Func<Func<IMenu>, IMenuComponent>? give = kinds.Count == 0
            ? null
            : list => new Link(T(viewer, "menu.comms.give"), _ => TargetPage(viewer, NewDraft(viewer, kinds[0], null, list(), picksKind: kinds.Count > 1)))
            {
                Tag = new AdminActionRow(AdminAction.Give),
            };

        return RecordsPage(viewer, AdminMenuIds.Comms, T(viewer, "menu.comms.title"), kinds: true, give, async (status, kind) =>
        {
            var sanctions = await _comms.ListAsync(status, kind, ListLimit).ConfigureAwait(false);
            return (sanctions.Cast<object>().ToList(), await _comms.CountSanctionsAsync(kind).ConfigureAwait(false));
        });
    }

    /// <summary>
    /// The records that apply to this server, newest first, behind a status filter. Loaded each time it is shown, so a
    /// record lifted elsewhere drops out.
    /// </summary>
    /// <param name="give">The row that starts a new one, given this page to land back on.</param>
    private IMenu RecordsPage(
        IPlayer viewer,
        string id,
        string title,
        bool kinds,
        Func<Func<IMenu>, IMenuComponent>? give,
        Func<RecordStatus, SanctionKind?, Task<(IReadOnlyList<object> Records, AdminRecordCounts Counts)>> load)
    {
        var (builder, page) = NewPage(id, title);
        var view = new ListView { Comms = kinds };
        IMenu? self = null;

        builder.AddHeader(Line(_ => ListHeader(viewer, view), _ => ListHeader(viewer, view) is not null));

        if (give?.Invoke(() => self!) is { } giveRow)
        {
            builder.Add(giveRow);
            page.RowsAt++;
        }

        builder.Add(new Row(string.Empty, session =>
        {
            view.Status = Next(StatusFilters, view.Status);
            Reload(fresh: true);
            session.Invalidate();
        })
        {
            TextProvider = _ => T(viewer, StatusFilterKey(view), CountLabel(view)),
            Tag = new AdminActionRow(AdminAction.StatusFilter),
        });
        page.RowsAt++;

        if (kinds)
        {
            builder.Add(new Row(string.Empty, session =>
            {
                view.Kind = Next(KindFilters, view.Kind);
                view.Active = view.Expired = view.Lifted = -1;
                Reload(fresh: true);
                session.Invalidate();
            })
            {
                TextProvider = _ => T(viewer, view.Kind switch
                {
                    SanctionKind.Gag => "menu.list.type_gags",
                    SanctionKind.Mute => "menu.list.type_mutes",
                    _ => "menu.list.type_all",
                }),
                Tag = new AdminActionRow(AdminAction.KindFilter),
            });
            page.RowsAt++;
        }

        builder.Add(new Row(T(viewer, "menu.retry"), _ => Reload(fresh: false))
        {
            VisibleWhen = _ => view.Failed && !view.Loading,
            Tag = new AdminActionRow(AdminAction.Retry),
        });

        void Reload(bool fresh)
        {
            if (self is null) return;

            var version = ++view.Version;
            var (status, kind) = (view.Status, view.Kind);
            view.Loading = true;

            // A changed filter must not leave the last filter's records on screen to be picked.
            if (fresh)
            {
                view.Shown = -1;
                ShowRows(self, page, []);
            }

            _ = Task.Run(async () =>
            {
                (IReadOnlyList<object> Records, AdminRecordCounts Counts)? loaded = null;
                try
                {
                    loaded = await load(status, kind).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Loading the {List} list failed.", id);
                }

                _core.Scheduler.NextTick(() =>
                {
                    if (version != view.Version) return;

                    view.Loading = false;
                    view.Failed = loaded is null;
                    if (loaded is not { } result) return;

                    view.Active = result.Counts.Active;
                    view.Expired = result.Counts.Expired;
                    view.Lifted = result.Counts.Lifted;
                    view.Shown = result.Records.Count;
                    ShowRows(self, page, [.. result.Records.Select(record => ListRow(viewer, record, self, () => Reload(fresh: false)))]);
                });
            });
        }

        self = Build(builder, page);
        self.Tag = view;
        self.Opened += _ => Reload(fresh: false);
        return self;
    }

    private string? ListHeader(IPlayer viewer, ListView view) =>
        view.Failed ? T(viewer, "menu.load_failed")
        : view.Shown < 0 ? T(viewer, "menu.loading")
        : view.Shown == 0 ? T(viewer, "menu.list.none")
        : view.Shown >= ListLimit ? T(viewer, "menu.list.newest", ListLimit)
        : null;

    private static string CountLabel(ListView view) =>
        view.Active < 0
            ? "-"
            : (view.Status switch
            {
                RecordStatus.Active => view.Active,
                RecordStatus.Expired => view.Expired,
                RecordStatus.Lifted => view.Lifted,
                _ => view.Active + view.Expired + view.Lifted,
            }).ToString();

    // Lifted gags and mutes are not "unbanned".
    private static string StatusFilterKey(ListView view) =>
        view.Status == RecordStatus.Lifted && view.Comms ? "menu.list.show_lifted_comms" : $"menu.list.show_{view.Status.ToString().ToLowerInvariant()}";

    private static T Next<T>(T[] cycle, T current) => cycle[(Array.IndexOf(cycle, current) + 1) % cycle.Length];

    private static RecordView ViewOf(object record) => record switch
    {
        Ban ban => new(
            Punishment.Ban,
            ban.Id,
            ban.PlayerName,
            Subject(ban.PlayerName, ban.SteamId64, ban.PlayerIp, ban.BanType == BanType.IP),
            ban.SteamId64,
            ban.PlayerIp,
            ban.BanType == BanType.IP,
            ban.Reason,
            ban.AdminName,
            ban.CreatedAt,
            ban.Length,
            ban.ExpiresAt,
            ban.GlobalBan,
            ban.Status),
        Sanction sanction => new(
            KindOf(sanction.SanctionKind),
            sanction.Id,
            sanction.PlayerName,
            Subject(sanction.PlayerName, sanction.SteamId64, sanction.PlayerIp, sanction.SanctionType == SanctionType.IP),
            sanction.SteamId64,
            sanction.PlayerIp,
            sanction.SanctionType == SanctionType.IP,
            sanction.Reason,
            sanction.AdminName,
            sanction.CreatedAt,
            sanction.Length,
            sanction.ExpiresAt,
            sanction.GlobalSanction,
            sanction.Status),
        _ => throw new ArgumentException("Not a ban or a sanction.", nameof(record)),
    };

    private static AdminRecordRow RecordRowOf(object record) => record switch
    {
        Ban ban => new AdminRecordRow(ban),
        Sanction sanction => new AdminRecordRow(sanction),
        _ => throw new ArgumentException("Not a ban or a sanction.", nameof(record)),
    };

    // The SteamID or IP only when there is no name.
    private static string Name(RecordView record) => string.IsNullOrWhiteSpace(record.Name) || record.Name == "Unknown" ? record.Subject : record.Name;

    private Link ListRow(IPlayer viewer, object record, IMenu list, Action reload)
    {
        var r = ViewOf(record);

        return new Link(string.Empty, _ => RecordPage(viewer, record, list, reload, fromPlayer: false))
        {
            TextProvider = _ => r.Kind == Punishment.Ban
                ? T(viewer, "menu.bans.row", r.Id, Name(r), Remaining(viewer, r))
                : T(viewer, "menu.comms.row", r.Id, Name(r), KindName(viewer, r.Kind), Remaining(viewer, r)),
            Tag = RecordRowOf(record),
        };
    }

    private Link HistoryRow(IPlayer viewer, object record, IMenu sheet, Action reload)
    {
        var r = ViewOf(record);
        var text = T(viewer, "menu.history.row", KindName(viewer, r.Kind), Readable(viewer, TimeSpan.FromMilliseconds(r.Length)), ReasonText(viewer, r.Reason));

        return new Link(text, _ => RecordPage(viewer, record, sheet, reload, fromPlayer: true))
        {
            Tag = RecordRowOf(record),
        };
    }

    /// <param name="land">The page it was opened from, where removing it or changing its length lands.</param>
    /// <param name="fromPlayer">Opened from the player's own page, which it should not offer to open again.</param>
    private IMenu RecordPage(IPlayer viewer, object record, IMenu land, Action reload, bool fromPlayer)
    {
        var r = ViewOf(record);
        var (builder, page) = NewPage(r.Kind == Punishment.Ban ? AdminMenuIds.BanRecord : AdminMenuIds.CommsRecord, T(viewer, $"menu.record.title_{Key(r.Kind)}", r.Id));

        // A player's history includes other servers' records, which are theirs to change.
        bool Changeable() =>
            (r.ExpiresAt == 0 || r.ExpiresAt > AdminsDatabase.NowMs) && (record is Ban ban ? _bans.AppliesHere(ban) : _comms.AppliesHere((Sanction)record));

        builder.Add(new Row(T(viewer, "menu.record.summary"), _ => SummarizeRecord(viewer, r)) { Tag = new AdminActionRow(AdminAction.Summary) });

        if (CanLift(viewer, r.Kind, r.Global))
        {
            builder.Add(new Row(T(viewer, $"menu.record.lift_{Key(r.Kind)}"), _ =>
            {
                if (record is Ban ban) LiftBan(viewer, ban, reload);
                else LiftSanction(viewer, (Sanction)record, reload);
                land.Open(viewer);
            })
            {
                Style = DangerStyle,
                VisibleWhen = _ => Changeable(),
                Validating = page.Guard,
                Tag = new AdminActionRow(AdminAction.Remove),
            });
        }

        if (CanLift(viewer, r.Kind, r.Global) && CanPunish(viewer, r.Kind, r.Global))
        {
            var target = new PlayerRef(-1, 0, (ulong)Math.Max(0, r.SteamId64), r.Name, r.Ip);
            builder.Add(new Link(T(viewer, "menu.record.change_length"), _ => LengthPage(viewer, LengthDraft(r.Kind, target, record, r.Global, land)))
            {
                VisibleWhen = _ => Changeable(),
                Tag = new AdminActionRow(AdminAction.ChangeLength),
            });
        }

        if (!fromPlayer && r.SteamId64 > 0)
        {
            builder.Add(new Link(T(viewer, "menu.record.open_player"), _ => OnlineBySteamId((ulong)r.SteamId64) is { } player ? PlayerPage(viewer, PlayerRef.Of(player), land) : null)
            {
                VisibleWhen = _ => OnlineBySteamId((ulong)r.SteamId64) is not null,
                Tag = new AdminActionRow(AdminAction.OpenPlayer),
            });
        }

        var menu = Build(builder, page);
        menu.Tag = RecordRowOf(record);
        return menu;
    }

    // A lift ends the record at the moment it was lifted.
    private string Remaining(IPlayer viewer, RecordView record) =>
        record.Status == PunishmentStatus.Lifted
            ? T(viewer, $"menu.time.lifted_{Key(record.Kind)}", Durations.Format(TimeSpan.FromMilliseconds(Math.Max(0, AdminsDatabase.NowMs - record.ExpiresAt))))
            : Remaining(viewer, record.ExpiresAt);

    private string Ends(IPlayer viewer, RecordView record)
    {
        if (record.Status == PunishmentStatus.Lifted) return T(viewer, $"menu.record.lifted_{Key(record.Kind)}", _messenger.Timestamp(viewer, record.ExpiresAt));

        var expiresAt = record.ExpiresAt;
        if (expiresAt == 0) return T(viewer, "menu.record.permanent");

        var now = AdminsDatabase.NowMs;
        return expiresAt > now
            ? T(viewer, "menu.record.expires", _messenger.Timestamp(viewer, expiresAt), Durations.Format(TimeSpan.FromMilliseconds(expiresAt - now)))
            : T(viewer, "menu.record.ended", _messenger.Timestamp(viewer, expiresAt));
    }

    private void SummarizeRecord(IPlayer viewer, RecordView record)
    {
        _messenger.Chat(viewer, "menu.summary.record", KindName(viewer, record.Kind), record.Id, record.Subject, ReasonText(viewer, record.Reason), record.Admin, Scope(viewer, record.Global));
        _messenger.Chat(viewer, "menu.summary.record_time", _messenger.Timestamp(viewer, record.CreatedAt), Readable(viewer, TimeSpan.FromMilliseconds(record.Length)), Ends(viewer, record));
    }

    // The list was loaded a while ago; lifting a record that has since ended would rewrite when it ended.
    private void LiftBan(IPlayer viewer, Ban ban, Action reload)
    {
        var issuer = Issuer.From(viewer);
        long? steamId = ban.SteamId64 != 0 ? ban.SteamId64 : null;
        var ip = ban.SteamId64 == 0 ? ban.PlayerIp : null;

        _ = Task.Run(async () =>
        {
            try
            {
                var active = await _bans.FindBansAsync(steamId, ip, status: RecordStatus.Active).ConfigureAwait(false);
                var stillActive = active.Any(b => b.Id == ban.Id);

                _core.Scheduler.NextTick(() =>
                {
                    if (stillActive)
                    {
                        _bans.Unban(issuer, ban, reload);
                        return;
                    }

                    _messenger.Reply(issuer, "menu.record.stale");
                    reload();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checking ban #{Id} before lifting it failed.", ban.Id);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }

    private void LiftSanction(IPlayer viewer, Sanction sanction, Action reload)
    {
        var issuer = Issuer.From(viewer);
        long? steamId = sanction.SteamId64 != 0 ? sanction.SteamId64 : null;
        var ip = sanction.SteamId64 == 0 ? sanction.PlayerIp : null;

        _ = Task.Run(async () =>
        {
            try
            {
                var active = await _comms.FindSanctionsAsync(steamId, ip, sanction.SanctionKind, status: RecordStatus.Active).ConfigureAwait(false);
                var stillActive = active.Any(s => s.Id == sanction.Id);

                _core.Scheduler.NextTick(() =>
                {
                    if (stillActive)
                    {
                        _comms.LiftOne(issuer, sanction, reload);
                        return;
                    }

                    _messenger.Reply(issuer, "menu.record.stale");
                    reload();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checking sanction #{Id} before lifting it failed.", sanction.Id);
                _messenger.ReplyLater(issuer, "command.database_error");
            }
        });
    }
}
