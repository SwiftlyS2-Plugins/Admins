# API Reference

Every type in `Admins.Contract` (namespace `Admins.Contract`). For referencing the contract and getting the interfaces, see [API Integration](API-Integration.md).

| Key | `AdminsInterfaceKeys` | Interface |
|-----|-----------------------|-----------|
| `Admins.Admins.V2` | `Admins` | [`IAdminsManager`](#iadminsmanager) |
| `Admins.Groups.V2` | `Groups` | [`IGroupsManager`](#igroupsmanager) |
| `Admins.Server.V2` | `Server` | [`IServerManager`](#iservermanager) |
| `Admins.Bans.V2` | `Bans` | [`IBansManager`](#ibansmanager) |
| `Admins.Comms.V2` | `Comms` | [`ICommsManager`](#icommsmanager) |
| `Admins.Menu.V2` | `Menu` | [`IAdminMenuAPI`](#iadminmenuapi) |

- Events are raised on the game thread. `…Async` methods use the database and complete off it.
- Times are Unix milliseconds. An `ExpiresAt` or `Length` of `0` is permanent.
- *This server* is the GUID `IServerManager.GetServerGUID()` returns. Admins and groups apply where their `Servers` list holds it; bans and sanctions where `Server` is it, or everywhere when they are global.
- With `UseDatabase` off nothing is stored: finds and database reads return nothing, updates and removes do nothing, and adds still act on the players online.

---

## IAdminsManager

| Member | Description |
|--------|-------------|
| `IAdmin? GetAdmin(ulong steamId64)` | The admin entry that applies to this server, whether the player is online or not |
| `IAdmin? GetAdmin(IPlayer player)` | The same, for an online player |
| `IAdmin? GetAdmin(int playerId)` | The same, by player id |
| `IReadOnlyList<IAdmin> GetAllAdmins()` | Every loaded admin, other servers' included |
| `int GetImmunity(ulong steamId64)` | The highest of the admin's own and their groups' immunity on this server. `0` for anyone else |
| `bool CanTarget(IPlayer? admin, ulong targetSteamId64)` | Whether `admin` may act on the target under the configured [`ImmunityMode`](#enums). `null` is the server console, which always may |
| `Task RefreshAsync()` | Reload admins and groups, then re-apply the permissions of the players online |
| `Task<IAdmin?> GetAdminFromDatabaseAsync(ulong steamId64)` | One admin, read from the database instead of the loaded list |
| `Task AddOrUpdateAdminAsync(IAdmin admin)` | Insert the admin, or update the entry with the same `SteamId64`, then refresh |
| `Task RemoveAdminAsync(IAdmin admin)` | Delete the entry with the same `SteamId64`, for every server, then refresh |
| `void SetAdmins(IEnumerable<IAdmin> admins)` | Replace the loaded list. Meant for `UseDatabase` off; with a database the next reload overwrites it |
| `event Action<IPlayer, IAdmin>? OnAdminLoad` | An admin's permissions were applied to a player: when they join, and again when their immunity or permissions change |

The loaded list is reloaded every `Admins.DatabaseSyncIntervalSeconds`, on `!reloadadmins` and after every write.

```csharp
public interface IAdmin
{
    long Id { get; set; }
    long SteamId64 { get; set; }
    string Username { get; set; }           // unique across the database
    int Immunity { get; set; }              // their own; their groups can raise it
    List<string> Permissions { get; set; }  // granted directly, on top of their groups'
    List<string> Groups { get; set; }       // group names
    List<string> Servers { get; set; }      // GUIDs of the servers the entry applies on
}
```

---

## IGroupsManager

| Member | Description |
|--------|-------------|
| `IGroup? GetGroup(string groupName)` | A loaded group by name, case-insensitive |
| `IReadOnlyList<IGroup> GetAllGroups()` | Every loaded group, other servers' included |
| `IReadOnlyList<IGroup> GetAdminGroups(IAdmin admin)` | The admin's groups that apply to this server |
| `IReadOnlyList<IGroup> GetPlayerGroups(IPlayer player)` | The same, for an online player |
| `Task<IGroup?> GetGroupFromDatabaseAsync(string groupName)` | One group, read from the database instead of the loaded list |
| `Task AddOrUpdateGroupAsync(IGroup group)` | Insert the group, or update the one with the same name, then refresh |
| `Task RemoveGroupAsync(IGroup group)` | Delete the group with that name, for every server, then refresh |
| `void SetGroups(IEnumerable<IGroup> groups)` | Replace the loaded list. Meant for `UseDatabase` off |
| `Task RefreshAsync()` | The same as `IAdminsManager.RefreshAsync` |

```csharp
public interface IGroup
{
    long Id { get; set; }
    string Name { get; set; }               // matched case-insensitively
    int Immunity { get; set; }
    List<string> Permissions { get; set; }
    List<string> Servers { get; set; }      // GUIDs of the servers the group applies on
}
```

---

## IServerManager

| Member | Description |
|--------|-------------|
| `string GetServerGUID()` | This server's GUID |
| `void SetServerGUID(string serverGUID)` | Move this server to another GUID and save it to `server_id.txt`. An invalid GUID is ignored. The permissions of the players online are re-applied for the new GUID |

`IServer` is a row of the `servers` table, where each server records its `IP`, `Port`, `Hostname` and `GUID`. No member returns it.

---

## IBansManager

| Member | Description |
|--------|-------------|
| `Task<IReadOnlyList<IBan>> FindBansAsync(long? steamId64 = null, string? playerIp = null, BanType? banType = null, RecordStatus status = RecordStatus.All)` | Bans matching the SteamID or the IP, narrowed by type and status. With neither given, every ban. Not limited to this server |
| `Task<IBan?> FindActiveBanAsync(long steamId64, string? playerIp)` | The active ban that applies to this server for the SteamID or the IP. With `Bans.GlobalBansMatchIp`, a global SteamID ban also matches the IP it was recorded with. A SteamID excluded with `!excludeipban` matches by SteamID only. The SteamID's own ban comes first, then an IP ban |
| `Task<IBan> AddBanAsync(IBan ban)` | Store the ban, kick the online players it matches and raise `OnAdminBanAdded`. Returns the stored copy |
| `Task UpdateBanAsync(IBan ban)` | Overwrite the row with the same `Id` and raise `OnAdminBanUpdated`. While the ban is active, it kicks the players it matches |
| `Task RemoveBanAsync(IBan ban)` | Lift the ban: mark it `Lifted` and end it now. The row stays as history |
| `Task<AdminRecordCounts> CountBansAsync()` | The bans that apply to this server, active, expired and lifted, as the Bans page counts them. Zero without a database |

| Event | Raised when |
|-------|-------------|
| `event Action<IBan>? OnAdminBanAdded` | A ban was stored, by a command, the menu or `AddBanAsync` |
| `event Action<IBan>? OnAdminBanUpdated` | `UpdateBanAsync` ran; the menu's *Change length* uses it |
| `event Action<IBan>? OnAdminBanRemoved` | A ban was lifted, by `!unban`, `!globalunban`, the menu or `RemoveBanAsync` |

```csharp
public interface IBan
{
    long Id { get; set; }              // set when stored
    long SteamId64 { get; set; }
    string PlayerName { get; set; }
    string PlayerIp { get; set; }      // stored without a port
    BanType BanType { get; set; }      // what it matches: the SteamID or the IP; a global SteamID ban also its IP, with Bans.GlobalBansMatchIp
    long ExpiresAt { get; set; }       // when it ends; 0 = permanent
    long Length { get; set; }          // the length players and admins are shown; 0 = permanent
    string Reason { get; set; }
    long AdminSteamId64 { get; set; }  // 0 = the server console
    string AdminName { get; set; }
    string Server { get; set; }        // GUID of the issuing server; empty when added = this server
    bool GlobalBan { get; set; }       // applies on every server
    long CreatedAt { get; set; }       // 0 when added = now
    long UpdatedAt { get; set; }
    PunishmentStatus Status { get; }   // Active, Lifted or Expired; your own implementation reports Active or Expired by ExpiresAt
}
```

---

## ICommsManager

A gag blocks text chat and a mute blocks voice. A silence is stored as one of each.

| Member | Description |
|--------|-------------|
| `Task<IReadOnlyList<ISanction>> FindSanctionsAsync(long? steamId64 = null, string? playerIp = null, SanctionKind? sanctionKind = null, SanctionType? sanctionType = null, RecordStatus status = RecordStatus.All)` | Sanctions matching the SteamID or the IP, narrowed by kind, type and status. With neither given, every sanction. Not limited to this server |
| `ISanction? GetActiveSanction(IPlayer player, SanctionKind kind)` | The active gag or mute that applies to an online player on this server; the longest-running one when there are several. Reads memory, on the game thread |
| `Task<ISanction> AddSanctionAsync(ISanction sanction)` | Store the sanction, apply it to the online players it matches and raise `OnAdminSanctionAdded`. Returns the stored copy |
| `Task UpdateSanctionAsync(ISanction sanction)` | Overwrite the row with the same `Id`, reload the sanctions of the players online and raise `OnAdminSanctionUpdated` |
| `Task RemoveSanctionAsync(ISanction sanction)` | Lift the sanction: mark it `Lifted` and end it now. The row stays as history |
| `Task<AdminRecordCounts> CountSanctionsAsync(SanctionKind? sanctionKind = null)` | The sanctions of a kind (`null`: both) that apply to this server, active, expired and lifted, as the Comms page counts them. Zero without a database |

| Event | Raised when |
|-------|-------------|
| `event Action<ISanction>? OnAdminSanctionAdded` | A sanction was stored, by a command, the menu or `AddSanctionAsync` |
| `event Action<ISanction>? OnAdminSanctionUpdated` | `UpdateSanctionAsync` ran; the menu's *Change length* uses it |
| `event Action<ISanction>? OnAdminSanctionRemoved` | A sanction was lifted, by `!ungag`, `!unmute`, `!unsilence`, their `global` forms, the menu or `RemoveSanctionAsync` |

A silence raises each event twice, once for the gag and once for the mute.

```csharp
public interface ISanction
{
    long Id { get; set; }                     // set when stored
    long SteamId64 { get; set; }
    string PlayerName { get; set; }
    string PlayerIp { get; set; }             // stored without a port
    SanctionType SanctionType { get; set; }   // what it matches: the SteamID or the IP
    SanctionKind SanctionKind { get; set; }   // Gag or Mute
    long ExpiresAt { get; set; }              // when it ends; 0 = permanent
    long Length { get; set; }                 // the length players and admins are shown; 0 = permanent
    string Reason { get; set; }
    long AdminSteamId64 { get; set; }         // 0 = the server console
    string AdminName { get; set; }
    string Server { get; set; }               // GUID of the issuing server; empty when added = this server
    bool GlobalSanction { get; set; }         // applies on every server
    long CreatedAt { get; set; }              // 0 when added = now
    long UpdatedAt { get; set; }
    PunishmentStatus Status { get; }          // Active, Lifted or Expired; your own implementation reports Active or Expired by ExpiresAt
}
```

---

## IAdminMenuAPI

Call from the game thread.

| Member | Description |
|--------|-------------|
| `void RegisterSubmenu(string id, string[] permissions, Func<IPlayer, string> getTitle, Func<IPlayer, IMenu> createMenu)` | Add an entry to `!admin`, or replace the one with this `id` |
| `void UnregisterSubmenu(string id)` | Remove the entry |
| `IMenu CreateAdminMenu(IPlayer player)` | Build the admin menu for a player without opening it |
| `void OpenAdminMenu(IPlayer player)` | Build the admin menu and open it |
| `IReadOnlyList<IPlayer> GetListedPlayers(IPlayer viewer)` | The players the viewer's menu lists: everyone but the server's TV and hidden admins the viewer may not see |
| `AdminPlayerState GetPlayerState(IPlayer player)` | What the admin tools have set on a player (hidden, frozen, noclip, god, beacon, drugged, ESP) |
| `IReadOnlyList<AdminRecentRow> GetRecentPlayers()` | Humans who left this server in the last 15 minutes, newest first, less anyone who came back. With the database on, the list survives a restart |
| `bool IsMapChangePending()` | True for the few seconds between ordering a map change and the change |

| `RegisterSubmenu` parameter | Meaning |
|-----------------------------|---------|
| `id` | Stable and unique, such as `myplugin.tools` |
| `permissions` | The entry shows to players holding any of them. Empty shows it to everyone who can open `!admin` |
| `getTitle` | The entry's label for the viewer, called when the admin menu is built. If it throws, the entry is left out |
| `createMenu` | Your menu for the viewer, called when the entry is picked. If it throws, the viewer sees "This menu could not be opened." |

Entries follow the built-in ones, in registration order; registering an `id` again moves it to the end. A menu returned without a `Parent` is adopted by the entry, so closing it returns to the admin menu. `IMenu` is SwiftlyS2's `Core.Menu` menu.

---

## Menu skins

For a menu renderer that draws `!admin` from data instead of parsing its text, as CustomHUD does. Every page has a stable id in `AdminMenuIds`; every row carries what it stands for as its component's `Tag`, and every page what it is about as the menu's `Tag`. A row's `Tag` is fixed when the page is built; what changes while a page is open (frozen, muted, the picks so far, the vote's tally) is read through the API when drawn. Admins' own rows stay plain text for every other renderer.

| Page | Id | Menu `Tag` |
|------|----|------------|
| The admin menu | `admins.main` | - |
| Players | `admins.players` | `IAdminPlayerList` |
| A player, and their Punish, Control, Move, History and Sessions pages | `admins.players.player`, `…player.punish`, `…player.control`, `…player.move`, `…player.history`, `…player.sessions` | `AdminPlayerRow` |
| A player's team, health and money pages | `admins.players.team`, `admins.players.health`, `admins.players.money` | `AdminPlayerRow` |
| Bans, Comms | `admins.bans`, `admins.comms` | `IAdminRecordList` |
| A record | `admins.bans.record`, `admins.comms.record` | `AdminRecordRow` |
| A punishment's steps | `admins.bans.give.<step>`, `admins.comms.give.<step>`, `admins.kick.<step>`, the step from `AdminFlowSteps` | `AdminFlowPage` |
| My tools, Server and its pages | `admins.tools`, `admins.server`, `admins.server.restart`, `admins.server.map`, `admins.server.map_confirm` | the map confirm page: `AdminMapRow` |
| A vote's ballot | `admins.vote` | `IAdminVote` |
| A registered entry that failed to build | `admins.notice` | - |

| Row `Tag` | Stands for |
|-----------|------------|
| `AdminPlayerRow(PlayerId, SessionId, SteamId64, Name)` | A player. `SessionId` identifies the connection, so a reconnect is a new row; `SteamId64` is 0 for a bot |
| `AdminRecentRow(SteamId64, Name, LeftAt)` | Someone who left in the last few minutes, when picking who to punish |
| `AdminPlayerGroupRow(AdminPlayerGroup)` | A link on a player's page: Punish, Control, Move, History or Sessions |
| `AdminPlayerActionRow(AdminPlayerAction)` | Something to do to the player. Which way a two-way one goes (mute or unmute) is read when drawn |
| `AdminTeamRow(Team)`, `AdminHealthRow(Health, Armor, Helmet)`, `AdminMoneyRow(Amount, Add)` | A team, a health preset, a money preset |
| `AdminRecordRow` | A ban (`Ban`) or a gag or mute (`Sanction`). Two are equal when they are the same record |
| `AdminPlaytimeRow(AdminPlaytimePeriod, Played)` | A player's time on every server in a period, on their Sessions page. Today, the week (from Monday), the month and the year are calendar periods in `TimeZone`; `Total` covers the sessions still kept |
| `AdminSessionIpRow(Ip, Sessions, LastSeen)` | An address they connected from, how many sessions came from it, and when the newest ended (now while open). Picking it prints the other accounts that used it |
| `AdminSessionRow(Id, Ip, Server, ConnectedAt, Played, Online)` | One of their connections, newest first. `Server` is another server's hostname (empty when unknown), `null` for this one; while `Online`, `Played` runs to now |
| `AdminKindRow(AdminPunishment)`, `AdminReasonRow(Reason)`, `AdminLengthRow(Length)`, `AdminScopeRow(Global)` | A punishment step's choices. A `null` reason is a kick's "no reason"; `TimeSpan.Zero` is permanent |
| `AdminRestartRow(DelaySeconds)`, `AdminMapRow(Map)` | A restart delay, a map |
| `AdminVoteOptionRow(Index)` | An answer on a ballot, indexing `IAdminVote.Options` |
| `AdminActionRow(AdminAction)` | A control that is not one of the page's items: a filter, `Give`, `Retry`, `Confirm`, `PrintCommand`, a tool |

| Page `Tag` | Members |
|------------|---------|
| `IAdminPlayerList` | `Team?` the team shown, `null` for everyone |
| `IAdminRecordList` | `Comms`, the `Status` and `Kind` shown, `Counts` (`null` until counted), `Loading`, `Failed` |
| `AdminFlowPage(Step, Draft)` | The step shown, and the `IAdminDraft`: `Kind`, `Target`, `ByIp`, `HasReason`, `Reason`, `Length`, `Global`, the `Record` whose length is being changed, the `Steps` in order, `TypedInChat`, and `Preview(length)`, what the player will be shown. `TypedInChat` holds the steps no listed choice fitted (the target, the reason, the length): they read as not picked, and the last step offers `PrintCommand`, which prints the command filled in with the rest, instead of `Confirm` |
| `IAdminVote` | `Kind` (`Poll`, `Kick`, `Map`), `StartedBy`, the kick's `Target`, `Question(viewer)` (the ballot's title), `Options(viewer)` (a kick's are yes, then no), `Votes(option)`, `Voters`, `Voted`, `SecondsLeft` |

`AdminPlayerState` and `AdminRecordCounts` (`Active`, `Expired`, `Lifted`, `All`) are value types: hold them in locals, not in fields, if your plugin must load without Admins.

---

## Enums

| Enum | Values |
|------|--------|
| `ImmunityMode` | `IgnoreImmunity` = 0, `ProtectFromLowerAccess` = 1, `ProtectFromEqualOrLowerAccess` = 2, `ProtectWithNoImmunityBypass` = 3. See [Immunity](../README.md#immunity) |
| `RecordStatus` | `Active`, `Expired` (ran out), `All`, `Lifted` (lifted by an admin) |
| `PunishmentStatus` | `Active` = 0, `Lifted` = 1, `Expired` = 2. In force is decided by `ExpiresAt`: lifting also ends a punishment now |
| `BanType` | `SteamID` = 1, `IP` = 2 |
| `SanctionType` | `SteamID` = 1, `IP` = 2 |
| `SanctionKind` | `Gag` = 1 (text chat), `Mute` = 2 (voice) |
| `AdminPunishment` | `Gag`, `Mute`, `Silence` (both), `Kick`, `Ban` |
| `AdminFlowStep` | `Target`, `Kind`, `Reason`, `Length`, `Scope`, `Confirm` |
| `AdminPlayerGroup` | `Punish`, `Control`, `Move`, `History`, `Sessions` |
| `AdminPlaytimePeriod` | `Today`, `Week`, `Month`, `Year`, `Total` |
| `AdminPlayerAction` | `Kick`, `Ban`, `Mute`, `Gag`, `Silence`, `Slay`, `Freeze`, `Slap`, `Respawn`, `RespawnWhereDied`, `Team`, `Swap`, `Beacon`, `Drug`, `God`, `Noclip`, `Goto`, `Bring`, `Rename`, `Health`, `Money` |
| `AdminVoteKind` | `Poll`, `Kick` (a tie keeps the player), `Map` |
| `AdminAction` | The controls an `AdminActionRow` stands for; each value's doc comment names its page |
