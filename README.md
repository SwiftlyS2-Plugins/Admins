<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>Admins</strong></h2>
  <h3>Base admin system for your server.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/SwiftlyS2-Plugins/Admins/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/SwiftlyS2-Plugins/Admins?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/SwiftlyS2-Plugins/Admins" alt="License">
</p>

> [!warning]
> This plugin is still in beta.

One plugin for everything admin related: admins and groups, bans, gags and mutes, player and server commands, votes, ESP, hiding from players and a single `!admin` menu. It replaces the former `Admins.Core`, `Admins.Bans`, `Admins.Comms`, `Admins.Menu` and `Admins.SuperCommands` plugins and uses the same database tables, so existing data keeps working.

## Table of Contents

- [Installation](#installation)
- [Upgrading from the split plugins](#upgrading-from-the-split-plugins)
- [Building](#building)
- [Database](#database)
- [Configuration](#configuration)
- [Immunity Mode](#immunity-mode)
- [Admin Menu](#admin-menu)
- [Hiding, ESP & Status](#hiding-esp--status)
- [Targeting Players](#targeting-players)
- [Commands & Permissions](#commands--permissions)
- [Server Console Admin Management](#server-console-admin-management)
- [Developer API](#developer-api)

## Installation

1. Extract the release zip so the `Admins` folder lands in your SwiftlyS2 `plugins` directory.
2. Add a database connection named `admins` (see [Database](#database)).
3. Start the server. The tables are created on first load and `config.jsonc` is generated in the plugin's config folder.

## Upgrading from the split plugins

1. Install `Admins` as above.
2. **Delete** the `Admins.Core`, `Admins.Bans`, `Admins.Comms`, `Admins.Menu` and `Admins.SuperCommands` plugin folders. Every command now lives in `Admins`; an old plugin left in place registers its commands a second time.
3. Start the server. On the first load the plugin:
   - imports the server GUID from the `Admins.Core` data folder (`server_id.txt`). Admins, groups, bans and sanctions are scoped to that GUID, so keeping it is what keeps them applying to this server;
   - if it has no `config.jsonc` yet, builds one from the `Admins.Core`, `Admins.Bans` and `Admins.Comms` configs. The old files are not modified; remove them once you have checked the result.

Plugins that used the old shared interfaces (`Admins.Admins.V1`, `Admins.Bans.V1`, ...) need to move to the [Developer API](#developer-api).

## Building

- Build `Admins.csproj` with the .NET 10 SDK. The plugin and its resources are written to `build/`.
- `dotnet publish -c Release` produces the deployable plugin in `build/publish/Admins/` and a zip of it.

## Database

The plugin uses the SwiftlyS2 database connection named `admins` and supports SQLite, MySQL, MariaDB and PostgreSQL. The schema is the one the split plugins created; migrations run on load.

With `UseDatabase` set to `false` nothing is persisted, and admins can only be supplied by other plugins through the API.

## Configuration

`config.jsonc` documents every key in a comment. Edits are applied while the server runs.

| Key | Default | Description |
|-----|---------|-------------|
| `Prefix` | `[[blue]SwiftlyS2[default]]` | Prefix of every chat message. Supports color tags. |
| `UseDatabase` | `true` | Store everything in the `admins` connection. |
| `TimeZone` | `UTC` | Time zone of displayed dates, as an IANA or Windows id. |
| `ImmunityMode` | `1` | See [Immunity Mode](#immunity-mode). |
| `Admins.DatabaseSyncIntervalSeconds` | `60` | How often admins and groups are reloaded, so changes made elsewhere apply. `0` disables it (use `!reloadadmins`). |
| `Bans.DatabaseSyncIntervalSeconds` | `30` | How often online players are re-checked against the bans table, so bans issued on other servers take effect. `0` disables it. |
| `Bans.Reasons` | list | Reasons offered by the ban form. |
| `Bans.DurationsInSeconds` | list | Durations offered by the ban form. `0` is permanent. |
| `Bans.KickOnSteamAuthorizationFailure` | `true` | Kick players whose Steam authorization fails. Turning it off lets players in during Steam outages, but their bans and admin rights cannot be checked until they authorize. |
| `Comms.DatabaseSyncIntervalSeconds` | `30` | How often the gags and mutes of online players are reloaded. `0` disables it. |
| `Comms.Reasons` | list | Reasons offered by the gag/mute form. |
| `Comms.DurationsInSeconds` | list | Durations offered by the gag/mute form. `0` is permanent. |
| `Comms.EnableAdminChat` | `true` | Send team chat starting with `AdminChatStartCharacter` to admin chat instead of the team. |
| `Comms.AdminChatStartCharacter` | `@` | The admin chat prefix. |
| `Presence.ConnectMessages` | `true` | Replace the game's join and leave messages with the plugin's. `!hide` fakes these, so with `false` it announces nothing. |
| `Status.Enabled` | `true` | Answer a player's `status` with the lines below, without hidden admins. |
| `Status.Header` | list | Lines before the player rows. Placeholders: `{hostname}`, `{map}`, `{humans}`, `{bots}`, `{maxplayers}`, `{date}`, `{time}`. |
| `Status.Row` | see config | One line per player. Placeholders: `{userid}`, `{name}`, `{steamid}`, `{duration}`, `{ping}`. |
| `Status.Footer` | `["#end"]` | Lines after the player rows, with the header's placeholders. |
| `Esp.AllowWhileAlive` | `false` | Show ESP outlines to admins who are alive, not only to dead and spectating ones. |

A placeholder takes a width like `{name,-24}`: the value is padded to 24 characters, on the right for a negative width and on the left for a positive one, and cut when longer.

## Immunity Mode

The Immunity Mode controls how admins can act on other admins based on their immunity levels. Set it with the `ImmunityMode` key in `config.jsonc`. An admin's immunity is the highest of their own and their groups' immunity.

### Available Modes

| Value | Mode | An admin can target another admin when |
|-------|------|----------------------------------------|
| `0` | `IgnoreImmunity` | always |
| `1` | `ProtectFromLowerAccess` (default) | their immunity is **higher than or equal to** the target's |
| `2` | `ProtectFromEqualOrLowerAccess` | their immunity is **strictly higher** than the target's |
| `3` | `ProtectWithNoImmunityBypass` | like `2`, but two admins with immunity `0` can target each other |

Players without an admin entry on this server can always be targeted, and an admin can always target themselves. Commands run from the server console ignore immunity.

### Configuration Example

```jsonc
{
  "Main": {
    "ImmunityMode": 1
  }
}
```

## Admin Menu

`!admin` opens a single menu built for the admin who opens it. Every entry is filtered by that admin's permissions, so whatever is shown can be used.

- **Players**: pick a player (immune players are marked), then act on them from one page:
  - **Ban…** and **Gag / Mute…** open a one-page form: by SteamID or IP, the action for gags and mutes (gag, mute or silence), a preset or typed reason, a preset or typed duration (`30m`, `12h`, `7d`), an *All servers* toggle, then **Confirm**.
  - **Remove gag** / **Remove mute**, shown only while one is active.
  - Kick, Slay, Slap, the Freeze / God mode / Noclip / Beacon / Drug toggles, Respawn, Respawn where they died, Move to team…, Swap team, Go to player and Bring player.
- **Bans**: ban a SteamID or IP that is not on the server, or browse the active bans and unban one.
- **Gags & Mutes**: the same for gags, mutes and silences.
- **Server**: restart the round, remove dropped weapons, change the map.
- **ESP** and **Hidden**: switch your own ESP and hiding on and off.

Typing a ban, gag, mute or silence command in chat without arguments opens the player picker and then the matching form.

The permissions `admins.menu.bans` and `admins.menu.comms` from the old menu plugin still allow banning, gagging and muting on this server from the menu. Other plugins can add their own entries through `IAdminMenuAPI` (see [Developer API](#developer-api)).

## Hiding, ESP & Status

### Join and leave messages

With `Presence.ConnectMessages` on, the game's own join and leave messages are replaced by "*name* joined the game", once the player has loaded in, and "*name* left the game (*reason*)", the reason being Disconnect, Kicked, Banned or Timed out in each reader's language. These are the messages `!hide` and `!unhide` fake.

### Hiding

`!hide` moves you off your team to no team, which takes you off the scoreboard, and announces that you left. While hidden:

- `status` leaves you out, except for admins holding `admins.commands.hide`, who see you marked *(hidden)*;
- your chat messages and voice are blocked so they cannot give you away. Chat commands and admin chat still work;
- you can spectate and use ESP;
- the team menu stays closed, also after a map change.

`!unhide` announces that you joined and moves you to the spectators. Joining a team in any other way ends the hide the same way, and disconnecting while hidden shows no second leave message.

Hiding does not reach outside the game's own lists: the server browser and other plugins (a website server list, for example) still count you, and the messages of the actions you take as an admin still name you. If the server forces a team on players who have not picked one (`mp_force_pick_time`), that ends the hide.

### ESP

`!esp` outlines every living player through walls, T in red and CT in blue. The outlines are sent only to admins with ESP on who are dead or spectating (or alive too with `Esp.AllowWhileAlive`), so other players never receive them. The outline of a player you spectate in first person is left out, as it would cover your screen. On a full server the outlines appear over a few ticks, one player per tick.

### Status

A player typing `status` in the console gets the lines configured under `Status` instead of the game's output, so hidden admins stay hidden there as well. The server console's own `status` is unchanged.

## Targeting Players

Wherever a command takes `<player>`:

| Form | Matches |
|------|---------|
| `name` | The exact name, otherwise a partial name matching exactly one player. A partial name matching several players is refused. |
| `#userid` | The player with that user id. |
| `76561198000000000` | The player with that SteamID64. |
| `@all`, `@t`, `@ct`, `@me`, `@aim`, ... | The SwiftlyS2 target selectors. |

Bans, gags and mutes, `!kick`, `!bring` and `!votekick` leave the admin running them out of group selectors such as `@all`.

The ban, gag, mute and silence commands also take the SteamID64 of a player who is not on the server, or for the `ip` variants an IP address.

## Commands & Permissions

Commands work in chat (`!command`) and, unless marked as player only, from the server console (`sw_command`). `<argument>` is required, `[argument]` optional. Durations are written like `30m`, `12h` or `7d`; `0` is permanent.

### Admin Management

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!admin` | | `admins.commands.admin` | Open the admin menu. Player only. |
| `!admins` | `<give\|edit\|remove\|list> ...` | `admins.command.admins` | Manage admins. See [Server Console Admin Management](#server-console-admin-management). |
| `!groups` | `<give\|edit\|remove\|list> ...` | `admins.command.groups` | Manage groups. |
| `!reloadadmins` | | `admins.command.reloadadmins` | Reload admins and groups from the database. |

The last three keep their singular `admins.command.` prefix, so `admins.commands.*` does not grant them.

### Bans

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!ban` | `<player\|steamid64> <time> <reason>` | `admins.commands.ban` | Ban by SteamID. |
| `!globalban` | `<player\|steamid64> <time> <reason>` | `admins.commands.globalban` | Ban by SteamID on every server. |
| `!banip` | `<player\|ip> <time> <reason>` | `admins.commands.ban` | Ban by IP address. |
| `!globalbanip` | `<player\|ip> <time> <reason>` | `admins.commands.globalban` | Ban by IP address on every server. |
| `!unban` | `<steamid64>` | `admins.commands.unban` | Lift the active bans of a SteamID that apply to this server: its own and global ones. |
| `!unbanip` | `<ip>` | `admins.commands.unban` | Lift the active bans of an IP address that apply to this server. |

### Gags & Mutes

A **gag** blocks text chat, a **mute** blocks voice chat and a **silence** does both.

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!gag` | `<player\|steamid64> <time> <reason>` | `admins.commands.gag` | Block text chat. |
| `!mute` | `<player\|steamid64> <time> <reason>` | `admins.commands.mute` | Block voice chat. |
| `!silence` | `<player\|steamid64> <time> <reason>` | `admins.commands.silence` | Block text and voice chat. |
| `!globalgag` | `<player\|steamid64> <time> <reason>` | `admins.commands.globalgag` | Block text chat on every server. |
| `!globalmute` | `<player\|steamid64> <time> <reason>` | `admins.commands.globalmute` | Block voice chat on every server. |
| `!globalsilence` | `<player\|steamid64> <time> <reason>` | `admins.commands.globalsilence` | Block both on every server. |
| `!gagip`, `!muteip`, `!silenceip` | `<player\|ip> <time> <reason>` | `admins.commands.gag`, `.mute`, `.silence` | The same, by IP address. |
| `!globalgagip`, `!globalmuteip`, `!globalsilenceip` | `<player\|ip> <time> <reason>` | `admins.commands.globalgag`, `.globalmute`, `.globalsilence` | The same, by IP address on every server. |
| `!ungag` | `<player\|steamid64>` | `admins.commands.ungag` | Lift the active gags that apply to this server. |
| `!unmute` | `<player\|steamid64>` | `admins.commands.unmute` | Lift the active mutes that apply to this server. |
| `!unsilence` | `<player\|steamid64>` | `admins.commands.unsilence` | Lift both. |

The `o` variants of the old plugin still work and behave like the commands above, since those now accept offline players too: `!gago`, `!muteo`, `!silenceo` and their `global` forms take a SteamID64; `!gagipo`, `!muteipo`, `!silenceipo` and their `global` forms take an IP address.

### Chat & Notifications

| Permission | Effect |
|------------|--------|
| `admins.chat` | Receive admin chat: team chat starting with `@` (configurable) from anyone goes to the holders of this permission instead of the team. A gagged player can still use it. |
| `admins.notify` | Be told when a player with bans, gags or mutes on record connects. |

### Player Commands

Every player command except `!goto`, which only moves you, respects immunity.

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!kick` | `<player> [reason]` | `admins.commands.kick` | Kick a player. |
| `!slay` | `<player>` | `admins.commands.slay` | Kill a player. |
| `!slap` | `<player> [damage]` | `admins.commands.slap` | Slap a player, optionally dealing 0-100 damage. |
| `!freeze` / `!unfreeze` | `<player>` | `admins.commands.freeze` / `admins.commands.unfreeze` | Stop / restore a player's movement. |
| `!bury` / `!unbury` | `<player> [depth]` | `admins.commands.bury` / `admins.commands.unbury` | Move a player down (default 10 units) / up (default 15 units). |
| `!noclip` | `[player]` | `admins.commands.noclip` | Toggle noclip. Without a player, toggles it for yourself. |
| `!god` | `<player>` | `admins.commands.god` | Toggle god mode. |
| `!beacon` | `[player]` | `admins.commands.beacon` | Toggle a beacon. Without a player, toggles it for yourself. |
| `!drug` | `<player>` | `admins.commands.drug` | Toggle a drugged screen: a shifting color tint and a shake, until it is toggled off or the player dies. |
| `!setspeed`, `!speed` | `<player> [multiplier]` | `admins.commands.setspeed` | Set the speed multiplier (0.1-10, default 1). |
| `!setgravity`, `!gravity` | `<player> [multiplier]` | `admins.commands.setgravity` | Set the gravity multiplier (0.1-10, default 1). |
| `!hp`, `!health` | `<player> <health> [armor] [helmet 0\|1]` | `admins.commands.hp` | Set health (0-100) and armor (0-100), optionally with a helmet. |
| `!blind` / `!unblind` | `<player> <hold_seconds>` / `<player>` | `admins.commands.blind` / `admins.commands.unblind` | Fade a player's screen to black for 0.1-60 seconds / clear it. |
| `!shake` | `<player>` | `admins.commands.shake` | Shake a player's view. |
| `!color` | `<player> <r> <g> <b> [a]` | `admins.commands.color` | Tint a player (0-255 per channel). |
| `!glow` | `<player> <r> <g> <b> [a]` | `admins.commands.glow` | Give a player a colored glow (0-255 per channel). |
| `!rename` | `<player> <new_name>` | `admins.commands.rename` | Rename a player. |
| `!givemoney`, `!money` | `<player> <amount>` | `admins.commands.givemoney` | Add 1-16000 money. |
| `!setmoney` | `<player> <amount>` | `admins.commands.setmoney` | Set money to 0-16000. |
| `!respawn` | `<player>` | `admins.commands.respawn` | Respawn a player who is on a team. |
| `!hrespawn`, `!1up` | `<player>` | `admins.commands.hrespawn` | Respawn a player where they last died. |
| `!team` | `<player> <ct\|t\|spec\|none>` | `admins.commands.team` | Move a player to a team. |
| `!swap` | `<player>` | `admins.commands.swap` | Move a player to the other team. |
| `!goto` | `<player>` | `admins.commands.goto` | Teleport yourself behind a player. Player only. |
| `!bring` | `<player>` | `admins.commands.bring` | Teleport a player to you. Player only. |
| `!giveitem`, `!give` | `<player> <item_name>` | `admins.commands.giveitem` | Give an item by its name, e.g. `weapon_ak47`. |
| `!melee` | `<player>` | `admins.commands.melee` | Strip a player's weapons and give a knife. |
| `!disarm` | `<player>` | `admins.commands.disarm` | Strip all of a player's weapons. |

### Server Commands

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!restartround`, `!rr` | `[delay]` | `admins.commands.restartround` | Restart the round after 0-300 seconds (default 1). |
| `!say` | `<message>` | `admins.commands.say` | Send a chat message to everyone. |
| `!csay` | `<message>` | `admins.commands.csay` | Send a centered message to everyone. |
| `!rcon` | `<command>` | `admins.commands.rcon` | Run a server console command. |
| `!map`, `!changemap` | `<map_name\|ws:workshop_id>` | `admins.commands.map` | Change the map after 3 seconds. |
| `!clean` | | `admins.commands.clean` | Remove every dropped weapon from the map. |

### Admin Tools

See [Hiding, ESP & Status](#hiding-esp--status).

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!esp`, `!wh`, `!xray` | | `admins.commands.esp` | Toggle outlines of every living player through walls. Player only. |
| `!hide` | | `admins.commands.hide` | Leave your team for no team, out of sight, with a faked leave message. Player only. |
| `!unhide` | | `admins.commands.hide` | Come back as a spectator, with a faked join message. Player only. |

### Votes

Votes run for 30 seconds in a menu shown to every player. The option with the most votes wins; a tie or no votes ends without a result.

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!vote` | `<question> <option1> <option2> [option3...]` | `admins.commands.vote` | Start a custom vote. |
| `!votekick` | `<player>` | `admins.commands.votekick` | Vote to kick a player. *Yes* has to beat *No*. |
| `!votemap` | `<map1> <map2> [map3...]` | `admins.commands.votemap` | Vote on a map change. The winning map is loaded when the vote ends. |

## Server Console Admin Management

You can manage admins and groups directly from the server console using the `sw_admins` and `sw_groups` commands. These commands are essential for initial setup and when you don't have access to in-game commands.

### Admin Management via Console (`sw_admins`)

The `sw_admins` command allows you to manage administrators from the server console.

#### Give Admin Privileges

**Syntax:**
```
sw_admins give <steamid64> <username> <immunity> [permissions] [groups] [server_guids]
```

**Parameters:**
- `<steamid64>` - Player's SteamID64 (required)
- `<username>` - Display name for the admin (required)
- `<immunity>` - Immunity level (0-100, higher = more immunity) (required)
- `[permissions]` - Comma-separated list of permissions (optional)
- `[groups]` - Comma-separated list of group names (optional)
- `[server_guids]` - Comma-separated list of additional server GUIDs (optional)

**Examples:**
```
# Basic admin with immunity 50
sw_admins give 76561198123456789 "PlayerName" 50

# Admin with specific permissions
sw_admins give 76561198123456789 "PlayerName" 75 "admins.commands.ban,admins.commands.kick"

# Admin with group membership
sw_admins give 76561198123456789 "PlayerName" 80 "" "moderators,vip"

# Admin with full permissions and multiple servers
sw_admins give 76561198123456789 "PlayerName" 100 "admins.commands.*" "admin" "guid-server-1,guid-server-2"
```

#### Edit Admin Properties

**Syntax:**
```
sw_admins edit <steamid64> <field> <value>
```

**Fields:**
- `username` - Change admin's display name
- `immunity` - Change immunity level
- `permissions` - Update permissions (comma-separated)
- `groups` - Update group memberships (comma-separated)
- `servers` - Update server GUIDs (comma-separated)

**Examples:**
```
# Change username
sw_admins edit 76561198123456789 username "NewName"

# Update immunity
sw_admins edit 76561198123456789 immunity 90

# Change permissions
sw_admins edit 76561198123456789 permissions "admins.commands.ban,admins.commands.mute"

# Update groups
sw_admins edit 76561198123456789 groups "admin,moderator"

# Update server access
sw_admins edit 76561198123456789 servers "guid-1,guid-2,guid-3"
```

#### Remove Admin

**Syntax:**
```
sw_admins remove <steamid64>
```

**Example:**
```
sw_admins remove 76561198123456789
```

**Note:** This removes the admin from the current server. If the admin has access to other servers, they will only be removed from the database when removed from all servers.

#### List All Admins

**Syntax:**
```
sw_admins list
```

This displays all admins configured for the current server, showing:
- Username
- SteamID64
- Immunity level
- Groups
- Permissions
- Number of servers they have access to

---

### Group Management via Console (`sw_groups`)

The `sw_groups` command allows you to manage permission groups from the server console.

#### Create/Give Group

**Syntax:**
```
sw_groups give <group_name> <immunity> [permissions] [server_guids]
```

**Parameters:**
- `<group_name>` - Unique name for the group (required)
- `<immunity>` - Immunity level for group members (required)
- `[permissions]` - Comma-separated list of permissions (optional)
- `[server_guids]` - Comma-separated list of additional server GUIDs (optional)

**Examples:**
```
# Basic moderator group
sw_groups give moderators 50

# VIP group with permissions
sw_groups give vip 25 "admins.commands.noclip,admins.commands.setspeed"

# Admin group with full permissions
sw_groups give admin 100 "admins.commands.*"

# Multi-server group
sw_groups give global-admins 90 "admins.commands.*" "guid-1,guid-2,guid-3"
```

**Note:** If a group with the same name already exists, this command will update it and add the current server to its server list.

#### Edit Group

**Syntax:**
```
sw_groups edit <group_name> <immunity> [permissions] [server_guids]
```

**Parameters:**
- `<group_name>` - Name of the group to edit (required)
- `<immunity>` - New immunity level (required)
- `[permissions]` - New comma-separated permissions list (optional)
- `[server_guids]` - Additional server GUIDs to add (optional)

**Examples:**
```
# Update immunity level
sw_groups edit moderators 60

# Update permissions
sw_groups edit moderators 60 "admins.commands.ban,admins.commands.kick,admins.commands.mute"

# Add servers to group
sw_groups edit moderators 60 "admins.commands.ban,admins.commands.kick" "new-server-guid"
```

#### Remove Group

**Syntax:**
```
sw_groups remove <group_name>
```

**Example:**
```
sw_groups remove moderators
```

**Note:** This removes the group from the current server. If the group is used on other servers, it will only be deleted from the database when removed from all servers.

#### List All Groups

**Syntax:**
```
sw_groups list
```

This displays all groups configured for the current server, showing:
- Group name
- Immunity level
- Permissions
- Number of servers using this group

---

### Common Console Admin Setup Examples

#### Example 1: Initial Server Setup

```bash
# Create admin groups
sw_groups give owner 100 "admins.*"
sw_groups give admin 90 "admins.commands.*,admins.chat,admins.notify"
sw_groups give moderator 50 "admins.commands.ban,admins.commands.kick,admins.commands.mute,admins.commands.gag"
sw_groups give vip 10 "admins.commands.noclip"

# Add yourself as owner
sw_admins give 76561198123456789 "YourName" 100 "" "owner"

# Add other admins
sw_admins give 76561198987654321 "AdminName" 90 "" "admin"
sw_admins give 76561198111111111 "ModeratorName" 50 "" "moderator"
```

#### Example 2: Grant Temporary Full Access

```bash
# Give someone full access temporarily (you can adjust later)
sw_admins give 76561198123456789 "TempAdmin" 100 "admins.commands.*"
```

#### Example 3: Fix Admin Permissions

```bash
# If an admin can't use certain commands, update their permissions
sw_admins edit 76561198123456789 permissions "admins.commands.*"

# Or add them to a proper group
sw_admins edit 76561198123456789 groups "admin"
```

### Permission Wildcards

A permission ending in `.*` grants every permission that starts with the part before it:
- `admins.*` - every permission of this plugin
- `admins.commands.*` - every `admins.commands.` permission. It does not include `admins.command.admins`, `admins.command.groups`, `admins.command.reloadadmins`, `admins.chat` or `admins.notify`.

## Developer API

Reference `Admins.Contract.dll` (shipped in `resources/exports`) as a compile-time dependency without copying it into your plugin, then fetch the interfaces in `UseSharedInterface`:

| Key (`AdminsInterfaceKeys`) | Interface | Purpose |
|-----------------------------|-----------|---------|
| `Admins.Admins.V2` | `IAdminsManager` | Look up admins and their immunity, check `CanTarget`, add, update and remove admins, `OnAdminLoad`. |
| `Admins.Groups.V2` | `IGroupsManager` | Look up, add, update and remove groups. |
| `Admins.Server.V2` | `IServerManager` | The server GUID that admins, groups, bans and sanctions are scoped to. |
| `Admins.Bans.V2` | `IBansManager` | Find, add, update and remove bans, and the ban events. |
| `Admins.Comms.V2` | `ICommsManager` | Find, add, update and remove gags and mutes, the active sanction of an online player, and the sanction events. |
| `Admins.Menu.V2` | `IAdminMenuAPI` | Add an entry to the `!admin` menu, or build and open the menu. |

```csharp
public override void UseSharedInterface(IInterfaceManager interfaceManager)
{
    if (!interfaceManager.HasSharedInterface(AdminsInterfaceKeys.Menu)) return;

    var adminMenu = interfaceManager.GetSharedInterface<IAdminMenuAPI>(AdminsInterfaceKeys.Menu);
    adminMenu.RegisterSubmenu("myplugin.tools", ["myplugin.admin"], player => "My tools", player => BuildMenu(player));
}
```

The entry is shown to admins holding any of the listed permissions, or to everyone who can open `!admin` when the list is empty, and closing your menu returns to the admin menu. Call `UnregisterSubmenu` when your plugin unloads.

Changes from the split plugins' API: every contract lives in the single `Admins.Contract` assembly, the keys moved from `V1` to `V2`, the database methods are async, `SetBans` / `ClearBans` / `SetSanctions` / `ClearSanctions` were removed, and `IGamePlayer` / `IConfigurationManager` are gone.
