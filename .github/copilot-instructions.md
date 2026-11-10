# Admins — Copilot Instructions

> A SwiftlyS2 plugin (C#, .NET 10) for Counter-Strike 2: admins and groups, bans, gags and
> mutes, player and server commands, votes, hiding, ESP and the `!admin` menu. It merges the
> former `Admins.Core`, `Admins.Bans`, `Admins.Comms`, `Admins.Menu` and `Admins.SuperCommands`
> plugins. Upstream is `SwiftlyS2-Plugins/Admins`.

## Where things are documented

| Topic | Lives in |
|---|---|
| What server owners and admins use: features, install, config, commands, permissions, the menu | The wiki (`Admins.wiki`, its own repository); `README.md` is its Home page with links rewritten |
| The shared contract, for plugin authors | `docs/API-Reference.md` (every member), `docs/API-Integration.md` (how to use it) |
| How the code works and what must not break | this file |

The wiki says what a feature does for the person using it, never how it is built — mechanics,
invariants and rationale go here. Keep each fact in one place and link to it.

## Build

```powershell
dotnet build Admins.csproj -c Release
dotnet publish Admins.csproj -c Release
```

`build` also builds `Admins.Contract` and writes to `build/net10.0/`, the contract to its
`resources/exports/`. `publish` writes `build/publish/Admins/` and zips it to `build/Admins.zip`.
The tree builds with zero warnings; a new one is a regression. There are no tests: verify by
building, then on a server. `SwiftlyS2.CS2` floats (`*-*`), so a restore can pick up a newer beta.

## Layout

```
Admins.csproj                  the plugin; removes Admins.Contract/** from its own compile items
Admins.Contract/               the public contract, its own project, namespace Admins.Contract
src/
  Entrypoint.cs                PluginMetadata (the only version), DI, shared interfaces
  LegacyConfigImport.cs        seeds config.jsonc from the split plugins' configs, once
  Database/                    AdminsDatabase, MigrationRunner, Migrations/, Models/ (Dommel)
  Models/Configuration.cs      section "Main"; each service owns a *Configuration class
  Services/
    Access/                    admins, groups, permissions, immunity, sw_admins / sw_groups
    Bans/, Comms/              records, enforcement, sync timers and their commands
    Menus/                     AdminMenuService (one partial per area), RecentPlayers
    PlayerControl/             player actions shared by the commands and the menu
    Presence/                  join and leave messages, !hide, the status override
    Sessions/                  player_sessions: one row per connection, and !sessions
    Esp/, Votes/, ServerControl/, Server/, Notifications/
  Shared/                      Messenger + Issuer, TargetResolver, PlayerRef + MenuShortcuts, Formatting, Timers
resources/translations/        en.jsonc (the reference), pt-BR.jsonc
docs/                          API docs
```

## Composition

- `Load` builds a private `ServiceCollection` of singletons and resolves every one. Constructors
  register commands, listeners and timers (`core.Registrator.Register(this)`), so **a service that
  is registered but never resolved does nothing** — a new one goes in both lists in `Entrypoint.cs`.
- `ConfigureSharedInterface` publishes the six interfaces; `AccessService` backs both admins and
  groups. `Unload` disposes the provider, so a service with timers or per-player game state is
  `IDisposable` — `CommsService` unmutes everyone it muted.
- The ban, comms and kick commands open the menu through `MenuShortcuts`, because the menu depends
  on those services and not the other way round.
- `PlayerControlService` holds the player actions. Each checks immunity itself, replies to the
  issuer on failure and announces success, whether a command or the menu called it.
- `ServerControlService.ChangeMap` and `IsKnownMap`, which `!votemap` checks its maps with, refuse a
  `ws:` name holding `;`, `"`, whitespace or a control character: it goes into
  `ds_workshop_changelevel`, a server command, where `;` would start another — `!rcon` for anyone
  with `!map`.
- `TargetResolver` tries `@` selectors, `#userid`, then any SteamID form, then a name typed in full
  or a bare user id, then a part of a name, all lowercased. Two matches at one step are ambiguous
  and target nobody. `@all`, `@t`, `@ct`, `@spec`, `@alive` and `@dead` are read from the controller:
  SwiftlyS2's own selectors match case-sensitively, skip players with no pawn and find no team for a
  caller without one. Punishments and their removals check for an IP address first, so a name and an
  address are never mistaken for each other, then take one player through `FindOne`, never a group.
  Anything by IP, those commands and the menu alike, is refused while anyone connected from the
  address is immune (`TargetResolver.OnIp`). A SteamID with nobody online behind it is punished
  offline, under the name of its latest session; a removal that finds nobody looks the name up on
  the punishments.
- With `Bans.GlobalBansMatchIp`, a global SteamID ban also matches the IP recorded on it.
  `BansService.Matches` and the two queries that pre-filter for it (`FindActiveBanAsync`,
  `CheckOnlinePlayers`) must agree. The queries spell out one predicate per setting, the way
  `CommsService.Here` does for its nullable kind. The unban lookup (`FindApplicableActiveAsync`)
  does not depend on it: an address finds every ban recorded with it.
- A SteamID in `ip_ban_exclusions` is held only by bans on its own SteamID. `Matches` reads the
  cached set (`_ipBanExcluded`), which the sync tick reloads once a minute and again before it kicks
  anyone it found, and `!excludeipban` after a change. `FindActiveBanAsync` counts the table, so only
  the commands that read the set (`!banip`, the menu's immunity check) can be up to a minute behind.
  Gags and mutes ignore it.
- Entities kept past the call that spawned them are held as a `CHandle`: round cleanup removes
  plugin entities, and a held wrapper checks only its address, which a new entity can take. The
  beacon's beams are also marked changed (`*Updated()`) after `DispatchSpawn`, as every beam known
  to draw is.

## Configuration

- `config.jsonc`, section `Main`, bound to `Models/Configuration.cs`. `[Description]` text becomes
  the comment above each key, and `addMissingKeys` adds new keys to existing files.
- **Every list with a non-empty default is cleared in the `.Configure(...)` block in
  `Entrypoint.cs`** before binding: the binder appends a file's entries to the default list, so a
  list left out of that block shows every default twice. A new list setting goes there too.
- Services hold `IOptionsMonitor<Configuration>` and follow `OnChange`; sync timers restart when
  their interval changes. Migrations run only at load, so `UseDatabase` turned on at runtime needs
  a restart.
- `LegacyConfigImport` runs only while `config.jsonc` does not exist, and never touches the old files.

## Database

- One connection, `admins`. `AdminsDatabase.Enabled` reads `UseDatabase` live, and every database
  path checks it: reads return empty, updates and removes return, adds still act in memory.
- Tables `servers`, `groups`, `admins`, `bans`, `sanctions` keep the split plugins' schema, so
  existing databases and web panels keep working. Never rename or retype a column.
  `player_sessions` is this plugin's own (`20270215000000`); its name avoids the `sessions` table
  web frameworks sharing the database tend to own. So is `ip_ban_exclusions` (`20270216000001`), and
  the `Status` column added to `bans` and `sanctions` (`20270216000000`), and their `SteamId64` and
  `PlayerIp` indexes (`20270217000000`): every enforcement query finds rows through them, and without
  them each sync tick scans both tables whole. `servers.LastSeenAt` (`20270218000000`) too.
- Migrations `20270214103911`–`…918` reuse the split plugins' ids in the default `VersionInfo`
  ledger, which is what makes existing databases skip them. Never renumber or edit one. A new
  migration takes a new id, guards its create with `Schema.Table(...).Exists()`, and must run on
  MySQL/MariaDB, PostgreSQL and SQLite.
- `MigrationRunner` reads the connection string from an **unopened** connection — an opened
  `MySqlConnection` drops the password — and dispatches on the configured driver name. The plugin
  ships MySqlConnector, Npgsql and System.Data.SQLite because FluentMigrator loads the driver from
  the plugin's own folder. A failed run is logged and `Load` goes on: SwiftlyS2 unloads a plugin
  whose `Load` throws, and every other database path recovers once the database is back; pending
  migrations then wait for a restart.
- Dommel over Dapper, with `[Table]`, `[Column]`, `[Key]` and `[Ignore]`. List columns are JSON
  strings behind `*Json` properties; `JsonList.Parse` reads a malformed cell as an empty list rather
  than making every admin unloadable, and drops `null` entries, which every reader would trim. Paged queries need `.Select()` before `.Where(...)`,
  `CountAsync` returns `long`, and a nullable enum folded into one predicate does not translate —
  spell out each case (`CommsService.Here`). Dapper floats (`2.*`) because `SwiftlyS2.CS2` raises
  its floor.
- Times are Unix milliseconds (`AdminsDatabase.NowMs`); `ExpiresAt = 0` is permanent. Nothing
  deletes a ban or sanction: lifting marks it `Lifted` and sets `ExpiresAt` to now. A typed length
  stops at 1000 years (`Durations.TryParse`), and `Messenger.Timestamp` clamps what a web panel wrote
  to the years a date can hold: kicks and gags format the expiry before they act.
- In force is decided by `ExpiresAt` alone, in every query and in `Status`. The `Status` column maps
  to `StoredStatus` (the one property named apart from its column; Dommel's type map reads it back by
  `[Column]`) and only tells a lifted record from one that ran out. It lags: the sync ticks run
  `AdminsDatabase.MarkExpiredAsync` at most once a minute (`UpkeepDue`), which marks ended `Active`
  rows `Expired` for outside readers.
- Rows a web panel writes are enforced like another server's. The sync matches
  `PlayerIp` exactly; only the join check also takes the `ip:port` form older rows have, and that
  prefix match is why the join lookups still scan on PostgreSQL and SQLite.

## Servers and scope

- Each server is the GUID in `data/Admins/server_id.txt`, imported once from `data/Admins.Core/`.
  **A new GUID detaches the server from every admin, group, ban and sanction** — never regenerate
  it to fix something.
- `ServerService.Report` runs at Steam activation, on a GUID change and every 60 seconds, single-flight:
  it writes this server's `servers` row (IP and port only once Steam has given the address, which
  reads `0.0.0.0` before) and `LastSeenAt`, or inserts the row. A GUID generated this run has no row
  and may stand for a lost file: `ClaimAddressAsync` takes over the row last seen at the same
  IP and port only once it has gone `RecentlySeenMs` (10 min) without a report, and registers anew if
  it moves meanwhile. Docker servers can all report the same in-container port, which is why a live
  server's row is never taken. A row whose address changes between two of this server's own writes
  means two servers share the GUID (`WarnIfShared`, once).
- `sw_serverid` lists `servers` by `Id`; `use <Id>` calls `SetServerGUID`. The management commands
  accept that `Id` wherever they take a server and store the GUID (`ToGuidsAsync`).
- Admins and groups apply where their `Servers` list holds the GUID; bans and sanctions where
  `Server` is it or the global flag is set (`AppliesHere` on each service).
- The contract's `Find…Async` and the menu's player history are not limited to this server, so the
  menu offers the lift and Change length only when `AppliesHere`.
- `unban`, `ungag`, `unmute` and `unsilence` lift only this server's own records and their
  `globalun…` forms only global ones (`LiftScope`), each under its own permission; when only the
  other scope matched, the reply names the other command. The menu lifts a record in its own scope,
  so `CanLift(viewer, kind, global)` checks the matching permission.
- `sw_admins remove` and `sw_groups remove` take this server off the list and delete the row only
  when none is left; the API's `Remove…Async` deletes outright.

## Threading

- Game thread only: `AccessService._grants`, `CommsService`'s cache, `SessionsService._tracked`, the
  menu's `_viewers` and `_open`, ESP, presence and votes. Per-player dictionaries are keyed by
  **slot** and checked against `SessionId`, because the SteamID already reads 0 when the disconnect
  event fires.
- Database work runs off the game thread (`Task.Run`, `ConfigureAwait(false)`) and comes back
  through `Core.Scheduler.NextTick`. Capture an `Issuer` on the game thread; after an await, check
  `Messenger.IsConnected(issuer)` before touching its player, or reply with `ReplyLater`.
- `Core.Menu` handlers stay on the game thread while `Validating` and the body complete
  synchronously; a handler that awaits continues off it.
- Repeating timers use `DelayAndRepeatGuarded` (`Shared/Timers.cs`), never `RepeatBySeconds`, which
  fires at once and doubles the startup load. SwiftlyS2 re-arms a repeating timer only after its
  callback returns, so one throw stopped it for good — the voice timer is all that mutes a player who
  joins muted. The beacon's is left bare: a pulse that threw part-way would leave its beams behind on
  every retry.
- `NextTickAsync` callbacks outlive an unload, unlike `NextTick`'s, so `ReconcileAll` and `Forget`,
  which reloads and lifts reach through it, return once their service is disposed.

## Enforcement

- **Access** swaps a `volatile` snapshot of admins and groups, then `ReconcileAll` applies only the
  difference in each player's permissions, so a reload never leaves an online admin without them.
  `OnAdminLoad` fires when an admin's id, immunity or permissions change.
- **Bans** are checked on Steam authorization and by the sync timer, in one query for the whole
  server. A ban kicks even when storing it fails.
- A sync tick that finds the last one still querying does nothing, in Access, Bans and Comms alike,
  so a short interval cannot pile queries onto a slow database.
- **Comms** are cached per slot and enforced on chat and voice. A sanction issued or lifted while a
  reload is querying bumps the slot's version, and the stale result is queried again. A sanction
  applies in memory even when storing fails, and reloads keep it (`Id` 0) until the player
  reconnects. A gag a reload finds that the cache did not hold was issued elsewhere, and the player is
  told as for one issued here. Admin chat is checked before the gag, so a gagged player can still
  reach the admins.
- Contract events are raised through `Raise`, which logs a throwing handler; the handlers after it
  in the delegate are skipped.

## Sessions

- `SessionsService` inserts a `player_sessions` row when Steam authorizes a player, backdated to
  the connection, refreshes `LastSeenAt` and the name every 60 seconds, and sets `DisconnectedAt`
  on disconnect. `DisconnectedAt = 0` is an open session.
- All its writes go through one chain (`Enqueue`), in the order the game thread asked for them, so
  an update never lands before its insert. A disposed instance drops what is still queued: after a
  hot reload a late full-row update would reopen a row the new instance just closed.
- On load it closes this server's open rows at their `LastSeenAt` (a crash or an unload left them),
  so a hot reload splits a session with a gap of up to a minute. Retention deletes only this
  server's rows; servers sharing the database may keep theirs longer.
- The heartbeat also starts rows for players nobody tracks (a hot reload, recording turned on), and
  closes every tracked row when recording is turned off.
- `!sessions` reads off the game thread and prints on it. A row open with no heartbeat for 150
  seconds is one a crash left; it shows as ended at `LastSeenAt`, not as online.
- A player's Sessions page in the menu loads through `LoadAccountAsync`, every server's rows at
  once: playtime per calendar period in `TimeZone` (`Messenger.UnixMs` gives the period starts),
  addresses by session count, connections newest first. It takes `admins.commands.sessions`, as
  `!sessions` does, because it shows IP addresses.

## Hiding, status and ESP

- `!hide` moves the admin to team none, which takes them off the scoreboard, and fakes a leave with
  the `presence.*` messages, so with `Presence.ConnectMessages` off it is silent. Any team change
  ends it, handled in Pre so the rejoin is announced before the team message; a disconnect while
  hidden prints no second leave.
- While hidden, chat lines are dropped (commands have already run by then), voice is muted, and the
  team picker is held closed with `sv_disable_teamselect_menu`, replicated as `"1"` and sent again
  after every map change, which would otherwise bring the picker back.
- `StatusService` answers a client's `status` — it reaches the server as a message the console's
  own `status` never passes through — with the configured lines, leaving hidden admins out for
  whoever cannot see them. It answers each player at most once a second: a client can send `status`
  as fast as it likes, and every answer is a console line per player.
- `EspService` gives each player a pair of bone-merged props, transmit-blocked for every slot except
  allowed viewers; a new connection is blocked before its first snapshot. Entity work runs in
  `OnTick`, one new pair per tick and none in the first ticks of a map, because building every pair
  inside the command once took long enough for the server to drop the admin. Blocks are released
  before a prop despawns, since the engine reuses entity indices. An alive viewer is sent them only
  with `Esp.AllowWhileAlive` or `admins.esp.alive`, which stays outside `admins.commands.*`.

## Votes

`VotesService` runs one vote at a time for 30 seconds. The electorate is fixed when it starts, a
ballot that opens unasked ignores input for 750 ms, a tie keeps the player on a votekick, and a map
change cancels the vote. An admin interrupted mid-menu goes back to the same page after voting.
The ballot's title is the question in the voter's language, under a `vote.time_left` header; its
menu `Tag` is the running vote (`IAdminVote`) and each answer's `AdminVoteOptionRow`, so a skin
can draw who called it and the tally.

## Menus

`AdminMenuService` is split by area: `Pages` (row types, page state, helpers, permission checks),
`Players`, `Flow` (giving a punishment), `Records` (ban and comms lists, single records), `Sessions`
(a player's sessions page) and `Tools` (My tools, Server). It uses `Core.Menu`, never the legacy `MenusAPI`.

| Rule | Why |
|------|-----|
| Every grouping is a submenu (`Link`); the body has no non-selectable label or section rows | Keyboard navigation; inline labels were rejected in-game |
| The header holds only notices: empty, loading, failed, left the server, immune, warnings (`AddHeader(Line(...))`) | Same |
| No label ends in `...` | Same |
| No descriptive detail: team, alive, SteamID, K/D, ping, immunity, status lists, step counter, picks so far, record details, footer hints, SteamIDs beside names | Owner's rule: CustomHUD's Admins pages draw it from the API and the tags below |
| Nothing is typed into a menu; a free value prints the chat command (`PrintUsage`). In a punishment it is a step like any other (`Draft.InChat`): the flow goes on and its last step prints the command filled in with every pick (`FilledCommands`) | Chat plugins that take messages first swallow typed input; a command printed before the rest is picked leaves the admin to type it all |
| A printed punishment names the player by SteamID64 (or IP when matched by IP), never `#userid`, and its length as one token (`CommandLength`). Live-only hints (rename, health, money) keep the shorter `#userid` | `TargetResolver` falls back from a stale `#userid` to a slot match, someone else, and a punishment must still reach a player who left; `Durations.Format` gives `1d 6h`, two arguments |
| Rows that act at once take `Validating = page.Guard` | The second press of a double-tap must not pick on the page it opened |
| Rows do not handle A / D (`NavigateLeft` / `NavigateRight`); only sliders and selectors may | They are the strafe keys: a five-row jump on them moved the cursor whenever an admin walked with the menu open |
| Page ids are `AdminMenuIds` constants; every row carries an `Admins.Contract` record as its component `Tag` (`AdminMenuRows.cs`) and every page its subject as the menu `Tag` (`AdminMenuViews.cs`) | CustomHUD's `LockerProfiles.Admins()` picks its mouse views by id, and its Admins integration draws each row from its tag; a row without one draws plain |

- Pages are built per viewer, filtered by permission, and opened as submenus of the page they were
  picked from, so Tab always goes back one page. `!admin <player>` builds the same parent chain.
- `Cached(...)` builds a linked page once per parent. `Build(builder, page, rows)` rebuilds the
  dynamic rows when the page is shown and holds them still while it stays open (`ReplaceRows` /
  `ShowRows` at `PageState.RowsAt`), so a joining player cannot move the cursor; focus is restored
  by row `Tag`.
- Open sessions are invalidated every second so live state (alive, time left, toggles) refreshes.
- `ViewerState` (slot and session) remembers the last reason and length per punishment kind;
  `RecentPlayers` keeps up to 50 leavers for 15 minutes, refilled from `player_sessions` on load.
- Punishment rights are `CanPunish` / `CanLift`; `admins.menu.bans` and `admins.menu.comms` from the
  old menu plugin still grant local bans and comms.
- `IAdminMenuAPI` entries come after the built-in ones. A throwing title drops the entry; a
  throwing build shows `menu.entry_failed`.
- Root links are static: fixed labels, an `AdminMenuIds` id each, and no `TextProvider`,
  `VisibleWhen` or `EnabledWhen`. CustomHUD's sidebar mirrors only static submenu rows, so a root
  entry with any of them drops out of it — which is why the root's live counts were removed.

## Shared contract

- `Admins.Contract` is referenced with `Private="false"`, and the `CopyContract` /
  `CopyContractToPublish` targets copy its DLL into `resources/exports/` — from a target, because an
  item glob misses it on a clean checkout. A copy next to `Admins.dll` would give its types a second
  identity, and consumers would stop matching.
- Keys are `AdminsInterfaceKeys`, `Admins.*.V2`. Change members additively; a breaking change takes
  new keys. SwiftlyS2 binds consumers to the exports copy it read at server start, so a contract
  change needs a server restart.
- `docs/API-Reference.md` and the wiki's `API-Reference` page list every member; update both in the
  same change.

## Translations

- `en.jsonc` is the reference; `pt-BR.jsonc` holds exactly the same keys.
- Many keys are composed at runtime — `menu.kind.{kind}`, `menu.flow.title_{kind}`,
  `menu.punish.un{kind}`, `menu.players.tag_{tag}`, `menu.len.{unit}s`, `team.{team}` — and
  single-word keys such as `none`, `yes`, `gag` and `global_gag` are looked up directly. An
  unused-key scan that misses either flags live keys.
- Labels are plain text without a trailing `...`. Chat strings use SwiftlyS2 colour tags.
- `Messenger.Text` never throws: a translation that does not fit its arguments (a stray `{` or `}`, a
  missing placeholder) is shown unformatted and logged once per key, since kicks and gags build their
  message before they act.
