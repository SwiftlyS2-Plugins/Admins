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
| `Task<IBan?> FindActiveBanAsync(long steamId64, string? playerIp)` | The active ban that applies to this server for the SteamID or the IP. A SteamID ban comes before an IP ban |
| `Task<IBan> AddBanAsync(IBan ban)` | Store the ban, kick the online players it matches and raise `OnAdminBanAdded`. Returns the stored copy |
| `Task UpdateBanAsync(IBan ban)` | Overwrite the row with the same `Id` and raise `OnAdminBanUpdated`. While the ban is active, it kicks the players it matches |
| `Task RemoveBanAsync(IBan ban)` | Lift the ban by expiring it now. The row stays as history |

| Event | Raised when |
|-------|-------------|
| `event Action<IBan>? OnAdminBanAdded` | A ban was stored, by a command, the menu or `AddBanAsync` |
| `event Action<IBan>? OnAdminBanUpdated` | `UpdateBanAsync` ran; the menu's *Change length* uses it |
| `event Action<IBan>? OnAdminBanRemoved` | A ban was lifted, by `!unban`, the menu or `RemoveBanAsync` |

```csharp
public interface IBan
{
    long Id { get; set; }              // set when stored
    long SteamId64 { get; set; }
    string PlayerName { get; set; }
    string PlayerIp { get; set; }      // stored without a port
    BanType BanType { get; set; }      // what it matches: the SteamID or the IP
    long ExpiresAt { get; set; }       // when it ends; 0 = permanent
    long Length { get; set; }          // the length players and admins are shown; 0 = permanent
    string Reason { get; set; }
    long AdminSteamId64 { get; set; }  // 0 = the server console
    string AdminName { get; set; }
    string Server { get; set; }        // GUID of the issuing server; empty when added = this server
    bool GlobalBan { get; set; }       // applies on every server
    long CreatedAt { get; set; }       // 0 when added = now
    long UpdatedAt { get; set; }
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
| `Task RemoveSanctionAsync(ISanction sanction)` | Lift the sanction by expiring it now. The row stays as history |

| Event | Raised when |
|-------|-------------|
| `event Action<ISanction>? OnAdminSanctionAdded` | A sanction was stored, by a command, the menu or `AddSanctionAsync` |
| `event Action<ISanction>? OnAdminSanctionUpdated` | `UpdateSanctionAsync` ran; the menu's *Change length* uses it |
| `event Action<ISanction>? OnAdminSanctionRemoved` | A sanction was lifted, by `!ungag`, `!unmute`, `!unsilence`, the menu or `RemoveSanctionAsync` |

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

| `RegisterSubmenu` parameter | Meaning |
|-----------------------------|---------|
| `id` | Stable and unique, such as `myplugin.tools` |
| `permissions` | The entry shows to players holding any of them. Empty shows it to everyone who can open `!admin` |
| `getTitle` | The entry's label for the viewer, called when the admin menu is built. If it throws, the entry is left out |
| `createMenu` | Your menu for the viewer, called when the entry is picked. If it throws, the viewer sees "This menu could not be opened." |

Entries follow the built-in ones, in registration order; registering an `id` again moves it to the end. A menu returned without a `Parent` is adopted by the entry, so closing it returns to the admin menu. `IMenu` is SwiftlyS2's `Core.Menu` menu.

---

## Enums

| Enum | Values |
|------|--------|
| `ImmunityMode` | `IgnoreImmunity` = 0, `ProtectFromLowerAccess` = 1, `ProtectFromEqualOrLowerAccess` = 2, `ProtectWithNoImmunityBypass` = 3. See [Immunity](../README.md#immunity) |
| `RecordStatus` | `Active`, `Expired`, `All` |
| `BanType` | `SteamID` = 1, `IP` = 2 |
| `SanctionType` | `SteamID` = 1, `IP` = 2 |
| `SanctionKind` | `Gag` = 1 (text chat), `Mute` = 2 (voice) |
