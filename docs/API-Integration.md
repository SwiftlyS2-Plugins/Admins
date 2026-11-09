# API Integration

How to use Admins from another plugin. Every member is described in the [API Reference](API-Reference.md).

---

## 1. Reference the contract

`Admins.Contract.dll` is in the Admins release, under `resources/exports/`.

```xml
<Reference Include="Admins.Contract">
  <HintPath>path\to\Admins.Contract.dll</HintPath>
  <Private>false</Private>
</Reference>
```

`Private` must be `false`. SwiftlyS2 loads the copy in Admins' `resources/exports/`, and a second copy next to your plugin gives the contract types a second identity: the interfaces stop matching, with no build error.

---

## 2. Get the interfaces

Shared interfaces do not exist yet in `Load`. Get them in both handshake overrides, which SwiftlyS2 runs again whenever any plugin loads or unloads, and assign on every path, so a reference to an unloaded Admins never lingers.

```csharp
private IBansManager? _bans;

public override void UseSharedInterface(IInterfaceManager im) => Bind(im);
public override void OnSharedInterfaceInjected(IInterfaceManager im) => Bind(im);

private void Bind(IInterfaceManager im)
{
    _bans = im.HasSharedInterface(AdminsInterfaceKeys.Bans)
        ? im.GetSharedInterface<IBansManager>(AdminsInterfaceKeys.Bans)
        : null;
}
```

Report a missing Admins from `OnAllPluginsLoaded`, which runs once per load, not from `Bind`, which runs on every rebuild.

### When Admins is optional

On a server without Admins the contract DLL is missing, and the runtime throws as soon as it prepares a method that names a contract type. The exception lands in the calling method, where a `try` inside the failing method cannot catch it, and one escaping the handshake brings down the SwiftlyS2 host.

1. Name contract types in one file only, and mark every member there that touches one `[MethodImpl(MethodImplOptions.NoInlining)]`.
2. Keep contract instances in `object?` fields, and give the rest of the plugin a `bool` to check.
3. Put the `try`/`catch` around the calls into that file.

```csharp
// AdminsBridge.cs: the only file naming Admins.Contract types.
internal sealed class AdminsBridge
{
    private object? _bans;

    public bool IsAvailable { get; private set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Bind(IInterfaceManager im)
    {
        _bans = im.HasSharedInterface(AdminsInterfaceKeys.Bans)
            ? im.GetSharedInterface<IBansManager>(AdminsInterfaceKeys.Bans)
            : null;
        IsAvailable = _bans is not null;
    }
}
```

```csharp
// The plugin
private void Bind(IInterfaceManager im)
{
    try
    {
        _admins.Bind(im);
    }
    catch (Exception ex)
    {
        Core.Logger.LogDebug(ex, "Admins is not installed.");
    }
}
```

---

## 3. Threads

| What | Thread |
|------|--------|
| Events | Raised on the game thread |
| `IAdminMenuAPI`, `ICommsManager.GetActiveSanction` and members taking an `IPlayer` | Call from the game thread |
| `…Async` methods | Complete off the game thread. Return to it with `Core.Scheduler.NextTick` before touching a player |

```csharp
var bans = await _bans.FindBansAsync(steamId64: (long)steamId, status: RecordStatus.Active);

Core.Scheduler.NextTick(() =>
{
    if (Core.PlayerManager.GetPlayer(slot) is { } player) player.SendChat($"{bans.Count} active bans");
});
```

---

## 4. Events

Because the handshake runs again, subscribe in `Bind`: take the handler off the previous instance before adding it to the new one, and take it off in `Unload`.

```csharp
private void Bind(IInterfaceManager im)
{
    if (_bans is not null) _bans.OnAdminBanAdded -= OnBanAdded;

    _bans = im.HasSharedInterface(AdminsInterfaceKeys.Bans)
        ? im.GetSharedInterface<IBansManager>(AdminsInterfaceKeys.Bans)
        : null;

    if (_bans is not null) _bans.OnAdminBanAdded += OnBanAdded;
}

public override void Unload()
{
    if (_bans is not null) _bans.OnAdminBanAdded -= OnBanAdded;
}

private void OnBanAdded(IBan ban)
{
    // Game thread.
}
```

Do not throw from a handler. Admins logs the exception, and the handlers after yours are not called.

---

## 5. Add an entry to `!admin`

```csharp
_menu.RegisterSubmenu(
    "myplugin.tools",
    ["myplugin.admin"],
    player => Core.Translation.GetPlayerLocalizer(player)["menu.tools"],
    player => BuildToolsMenu(player));
```

- An empty permission list shows the entry to everyone who can open `!admin`.
- Build the menu with your own `Core.Menu` and leave its parent unset. The entry adopts it, so closing it returns to the admin menu.
- Registering an id again replaces the entry, so registering from `Bind` is safe.
- Call `UnregisterSubmenu("myplugin.tools")` in `Unload`. An entry left behind points into an unloaded plugin.

---

## 6. Issue a ban or a gag

The contract holds interfaces only, so implement `IBan` or `ISanction` with a plain class:

```csharp
internal sealed class Ban : IBan
{
    public long Id { get; set; }
    public long SteamId64 { get; set; }
    public string PlayerName { get; set; } = "";
    public string PlayerIp { get; set; } = "";
    public BanType BanType { get; set; } = BanType.SteamID;
    public long ExpiresAt { get; set; }
    public long Length { get; set; }
    public string Reason { get; set; } = "";
    public long AdminSteamId64 { get; set; }
    public string AdminName { get; set; } = "";
    public string Server { get; set; } = "";
    public bool GlobalBan { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}
```

```csharp
var length = (long)TimeSpan.FromDays(1).TotalMilliseconds;

await _bans.AddBanAsync(new Ban
{
    SteamId64 = (long)steamId,
    PlayerName = name,
    ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + length,
    Length = length,
    Reason = "Cheating",
    AdminName = "MyPlugin",
});
```

- Set both `ExpiresAt` and `Length`: `ExpiresAt` decides when it ends, `Length` is what players and admins are shown. `0` in both is permanent.
- Leave `Server` empty for this server, or set `GlobalBan` for every server.
- `AddBanAsync` fills in `Id`, `CreatedAt` and `UpdatedAt` and kicks the player if they are online.
- A gag or mute is the same with `ISanction` and `SanctionKind`. For a silence, add one of each.

---

## 7. Check who may act on whom

```csharp
if (!_admins.CanTarget(caller, target.SteamID))
{
    return;
}
```

`CanTarget` applies the server's `ImmunityMode`. `GetAdmin` is `null`, and `GetImmunity` `0`, for anyone without an admin entry on this server. Permissions go through SwiftlyS2: `Core.Permission.PlayerHasPermission(steamId, "admins.commands.ban")`.

---

## Upgrading from V1

| V1 | V2 |
|----|----|
| `Admins.Core.Contract`, `Admins.Bans.Contract`, `Admins.Comms.Contract`, `Admins.Menu.Contract` | One `Admins.Contract` |
| Key strings such as `Admins.Admins.V1` | `AdminsInterfaceKeys`, `Admins.*.V2` |
| `Admins.Configuration.V1` (`IConfigurationManager`), `Admins.GamePlayer.V1` (`IGamePlayer`) | Removed |
| `FindBans`, `AddBan`, `UpdateBan`, `RemoveBan` | `FindBansAsync`, `AddBanAsync`, `UpdateBanAsync`, `RemoveBanAsync`, plus `FindActiveBanAsync` |
| `FindSanctions`, `AddSanction`, `UpdateSanction`, `RemoveSanction` | Their `…Async` forms, plus `GetActiveSanction` |
| `SetBans`, `ClearBans`, `SetSanctions`, `ClearSanctions` | Removed |
| `AddAdmin`, `UpdateAdminAsync` | `AddOrUpdateAdminAsync` |
| `AssignAdmin`, `UnassignAdmin` | Removed; Admins applies permissions itself |
| `RemoveAdmin`, `RefreshAdmins`, `GetAdminBySteamId64Async` | `RemoveAdminAsync`, `RefreshAsync`, `GetAdminFromDatabaseAsync` |
| `GetGroupByNameAsync`, `UpdateGroupAsync`, `RefreshGroups` | `GetGroupFromDatabaseAsync`, `AddOrUpdateGroupAsync`, `RefreshAsync` |
| | New: `GetImmunity`, `CanTarget` |
| `List<T>` results | `IReadOnlyList<T>` |
| `RegisterSubmenu(translationKey, permission, Func<IPlayer, string, string>, Func<IPlayer, IMenuAPI>)` | `RegisterSubmenu(id, permissions, Func<IPlayer, string>, Func<IPlayer, IMenu>)`, with `Core.Menu` menus |
| `CreateAdminMenu` returning `IMenuAPI`, `GetMenuColor` | `CreateAdminMenu` returning `IMenu`, and `OpenAdminMenu` |
