using Admins.Contract;
using Admins.Database;
using Admins.Services.Comms;
using Admins.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;

namespace Admins.Services.Menus;

public sealed partial class AdminMenuService
{
    private const int PageJump = 5;
    private const long DoubleTapMs = 300;

    private static readonly MenuTextStyle InfoStyle = MenuTextStyle.Default.WithSize(MenuTextSize.Small).WithColor("#C8C8C8");
    private static readonly MenuTextStyle WarnStyle = MenuTextStyle.Default.WithSize(MenuTextSize.Small).WithColor("#FFB74D");
    private static readonly MenuTextStyle DangerStyle = MenuTextStyle.Default.WithSize(MenuTextSize.SmallMedium).WithColor("#FF6B6B");

    private enum Punishment
    {
        Gag,
        Mute,
        Silence,
        Kick,
        Ban,
    }

    /// <summary>Identifies a list entry across rebuilds, so the cursor returns to the same player or record.</summary>
    private readonly record struct RowKey(string Kind, long Id);

    private sealed class PageState
    {
        public PageState() => Guard = _ => ValueTask.FromResult(Environment.TickCount64 - OpenedAt >= DoubleTapMs);

        public int? Focus { get; set; }

        public object? FocusKey { get; set; }

        public IMenuComponent? Start { get; set; }

        public long OpenedAt { get; set; }

        public List<IMenuComponent> Rows { get; } = [];

        /// <summary>The body index the rebuilt rows go at; the page's fixed rows before it stay put.</summary>
        public int RowsAt { get; set; }

        // On rows that act right away: the second press of a double-tap must not also pick on the page it opened.
        public Func<MenuActionContext, ValueTask<bool>> Guard { get; }
    }

    private sealed class Row(string text, Action<IMenuSession> onSelect) : ButtonComponent(text, ctx =>
    {
        onSelect(ctx.Session);
        return ValueTask.CompletedTask;
    })
    {
        public override ValueTask<bool> HandleActionAsync(MenuActionContext context) =>
            Jump(context) ? ValueTask.FromResult(true) : base.HandleActionAsync(context);
    }

    /// <summary>A row that opens a page; returning null stays here. The page goes back here when it closes.</summary>
    private sealed class Link(string text, Func<IMenuSession, IMenu?> next) : SubmenuComponent(text, next)
    {
        public override ValueTask<bool> HandleActionAsync(MenuActionContext context) =>
            Jump(context) ? ValueTask.FromResult(true) : base.HandleActionAsync(context);
    }

    // A / D move a screen at a time; the framework binds them but gives plain rows no use for them.
    private static bool Jump(MenuActionContext context)
    {
        int delta;
        if (string.Equals(context.Action.Name, MenuActions.NavigateLeft.Name, StringComparison.OrdinalIgnoreCase)) delta = -PageJump;
        else if (string.Equals(context.Action.Name, MenuActions.NavigateRight.Name, StringComparison.OrdinalIgnoreCase)) delta = PageJump;
        else return false;

        var session = context.Session;
        var current = session.FocusedIndex;

        if (delta < 0)
        {
            session.SetFocus(Math.Max(0, current + delta));
            return true;
        }

        for (var index = current + delta; index > current; index--)
        {
            if (session.SetFocus(index)) break;
        }

        return true;
    }

    private (IMenuBuilder Builder, PageState Page) NewPage(string id, string title) => (_core.Menu.CreateMenu(id).WithTitle(title), new PageState());

    /// <param name="rows">
    /// Rebuilt each time the page is shown and left alone while it stays open, so a player joining or leaving cannot move
    /// the cursor onto someone else. They go at <see cref="PageState.RowsAt"/>.
    /// </param>
    private IMenu Build(IMenuBuilder builder, PageState page, Func<IReadOnlyList<IMenuComponent>>? rows = null)
    {
        var menu = builder.Build();

        menu.Opened += session =>
        {
            page.OpenedAt = Environment.TickCount64;
            _open.Add(session);

            if (rows is not null) ReplaceRows(menu, page, rows());
            Restore(session, page);
        };

        menu.Closed += session => _open.Remove(session);

        menu.FocusChanged += session =>
        {
            page.Focus = session.FocusedIndex;
            page.FocusKey = (session.FocusedComponent as MenuComponentBase)?.Tag;
        };

        return menu;
    }

    private static void ReplaceRows(IMenu menu, PageState page, IReadOnlyList<IMenuComponent> rows)
    {
        foreach (var row in page.Rows) menu.Remove(row);
        page.Rows.Clear();

        for (var i = 0; i < rows.Count; i++)
        {
            menu.Insert(MenuRegion.Body, page.RowsAt + i, rows[i]);
            page.Rows.Add(rows[i]);
        }
    }

    // For rows that arrive later, from the database, while someone may be looking at the page.
    private void ShowRows(IMenu menu, PageState page, IReadOnlyList<IMenuComponent> rows)
    {
        ReplaceRows(menu, page, rows);

        foreach (var session in _open.Where(s => s.IsOpen && ReferenceEquals(s.Menu, menu)).ToList())
        {
            Restore(session, page);
            session.Invalidate();
        }
    }

    // Opening a page gives it a new session at the top, including going back to it.
    private static void Restore(IMenuSession session, PageState page)
    {
        if (page.FocusKey is { } key && FocusWhere(session, c => c is MenuComponentBase { Tag: { } tag } && tag.Equals(key))) return;

        if (page.Focus is { } focus)
        {
            for (var i = focus; i > 0; i--)
            {
                if (session.FocusedIndex == i || session.SetFocus(i)) return;
            }

            return;
        }

        if (page.Start is { } start) FocusWhere(session, c => ReferenceEquals(c, start));
    }

    private static bool FocusWhere(IMenuSession session, Func<IMenuComponent, bool> match)
    {
        var index = 0;
        foreach (var component in session.Menu.GetComponents(MenuRegion.Body))
        {
            if (!component.IsFocusable || !component.IsVisible(session)) continue;

            if (match(component))
            {
                if (session.FocusedIndex != index) session.SetFocus(index);
                return true;
            }

            index++;
        }

        return false;
    }

    private Link Cached(string text, Func<IMenu> build)
    {
        IMenu? menu = null;
        return new Link(text, _ => menu ??= build());
    }

    private static TextComponent Line(Func<IMenuSession, string?> text, Func<IMenuSession, bool>? visible = null, MenuTextStyle? style = null) => new()
    {
        TextProvider = text,
        VisibleWhen = visible,
        Style = style ?? InfoStyle,
        MaxWidth = 34f,
    };

    private Row Toggle(IPlayer viewer, string key, Func<bool> isOn, Action flip) => new(string.Empty, session =>
    {
        flip();
        session.Invalidate();
    })
    {
        TextProvider = _ => T(viewer, key, T(viewer, isOn() ? "menu.on" : "menu.off")),
    };

    /// <summary>Closes the whole menu rather than going back a page.</summary>
    private static void CloseMenu(IMenuSession session)
    {
        var menu = session.Menu;
        var parent = menu.Parent;
        menu.Parent = null;
        session.Close();
        menu.Parent = parent;
    }

    // Anything typed is entered as a whole chat command: chat plugins that stop chat messages would swallow captured input.
    private void PrintUsage(IMenuSession session, IEnumerable<string> commands, string? hintKey = null)
    {
        foreach (var command in commands) _messenger.Chat(session.Player, "menu.usage", command);
        if (hintKey is not null) _messenger.Chat(session.Player, hintKey);
        CloseMenu(session);
    }

    #region Text

    private string T(IPlayer? viewer, string key, params object[] args) => _messenger.Text(viewer, key, args);

    private static string Key(Punishment kind) => kind.ToString().ToLowerInvariant();

    private string Remaining(IPlayer viewer, long expiresAt)
    {
        if (expiresAt == 0) return T(viewer, "menu.time.forever");

        var now = AdminsDatabase.NowMs;
        return expiresAt > now
            ? T(viewer, "menu.time.left", Durations.Format(TimeSpan.FromMilliseconds(expiresAt - now)))
            : T(viewer, "menu.time.ended", Durations.Format(TimeSpan.FromMilliseconds(now - expiresAt)));
    }

    // "2 weeks" rather than "14d": what the admin reads, not what they type.
    private string Readable(IPlayer viewer, TimeSpan length)
    {
        if (length <= TimeSpan.Zero) return T(viewer, "menu.flow.permanent");

        var seconds = (long)length.TotalSeconds;
        var (count, unit) = seconds % 604800 == 0 ? (seconds / 604800, "week")
            : seconds % 86400 == 0 ? (seconds / 86400, "day")
            : seconds % 3600 == 0 ? (seconds / 3600, "hour")
            : seconds % 60 == 0 ? (seconds / 60, "minute")
            : (seconds, "second");

        return T(viewer, count == 1 ? $"menu.len.{unit}" : $"menu.len.{unit}s", count);
    }

    private string ReasonText(IPlayer viewer, string? reason) => string.IsNullOrWhiteSpace(reason) ? T(viewer, "menu.flow.no_reason") : reason;

    private string KindName(IPlayer viewer, Punishment kind) => T(viewer, $"menu.kind.{Key(kind)}");

    private string Scope(IPlayer viewer, bool global) => T(viewer, global ? "menu.scope.global" : "menu.scope.local");

    private static string Ago(DateTime utc) => Durations.Format(DateTime.UtcNow - utc);

    private static string Subject(string name, long steamId64, string ip, bool byIp)
    {
        var id = byIp || steamId64 == 0 ? ip : steamId64.ToString();
        return string.IsNullOrWhiteSpace(name) || name == "Unknown" ? id : $"{name} ({id})";
    }

    // The <time> argument of the chat commands: a single token in the largest unit that divides evenly.
    private static string DurationToken(TimeSpan length)
    {
        if (length <= TimeSpan.Zero) return "0";

        var seconds = (long)length.TotalSeconds;
        if (seconds % 86400 == 0) return $"{seconds / 86400}d";
        if (seconds % 3600 == 0) return $"{seconds / 3600}h";
        return seconds % 60 == 0 ? $"{seconds / 60}m" : $"{seconds}s";
    }

    #endregion

    #region Permissions

    private bool Has(IPlayer viewer, string permission) => _core.Permission.PlayerHasPermission(viewer.SteamID, permission);

    // admins.menu.bans / admins.menu.comms predate the per-command checks; they keep granting local actions only.
    private bool CanBan(IPlayer viewer, bool global) =>
        global ? Has(viewer, "admins.commands.globalban") : Has(viewer, "admins.commands.ban") || Has(viewer, "admins.menu.bans");

    private bool CanComms(IPlayer viewer, CommsAction action, bool global)
    {
        var name = action.ToString().ToLowerInvariant();
        return global ? Has(viewer, $"admins.commands.global{name}") : Has(viewer, $"admins.commands.{name}") || Has(viewer, "admins.menu.comms");
    }

    private bool CanPunish(IPlayer viewer, Punishment kind, bool global) => kind switch
    {
        Punishment.Kick => !global && Has(viewer, "admins.commands.kick"),
        Punishment.Ban => CanBan(viewer, global),
        _ => CanComms(viewer, ActionOf(kind), global),
    };

    private bool CanPunishAny(IPlayer viewer, Punishment kind) => CanPunish(viewer, kind, false) || CanPunish(viewer, kind, true);

    private bool CanLift(IPlayer viewer, Punishment kind) => kind switch
    {
        Punishment.Ban => Has(viewer, "admins.commands.unban"),
        Punishment.Gag => Has(viewer, "admins.commands.ungag"),
        Punishment.Mute => Has(viewer, "admins.commands.unmute"),
        Punishment.Silence => Has(viewer, "admins.commands.unsilence"),
        _ => false,
    };

    private static readonly Punishment[] CommsKinds = [Punishment.Gag, Punishment.Mute, Punishment.Silence];

    private static CommsAction ActionOf(Punishment kind) => kind switch
    {
        Punishment.Gag => CommsAction.Gag,
        Punishment.Mute => CommsAction.Mute,
        _ => CommsAction.Silence,
    };

    private static Punishment KindOf(CommsAction action) => action switch
    {
        CommsAction.Gag => Punishment.Gag,
        CommsAction.Mute => Punishment.Mute,
        _ => Punishment.Silence,
    };

    private static Punishment KindOf(SanctionKind kind) => kind == SanctionKind.Gag ? Punishment.Gag : Punishment.Mute;

    #endregion
}
