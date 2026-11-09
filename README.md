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

One plugin for everything admin related. It replaces `Admins.Core`, `Admins.Bans`, `Admins.Comms`, `Admins.Menu` and `Admins.SuperCommands` and keeps their database, so existing admins, bans and gags carry over.

## Features

- 🛡️ **Admins and groups** — permissions, immunity levels, groups - all configured in a database
- 🔨 **Local and Global Bans** — punish players by Name, StatusID, SteamID or IP, on one server or all of them. Works when players are online and offline
- 🔇 **Gags, mutes and silences** — block text chat, voice or both
- 📋 **One `!admin` menu** — comperhensive and easy to navigate menu for all administrative actions
- 🎮 **Player commands** — slay, slap, freeze, teleport, health, money, teams, items and effects
- 🖥️ **Server commands** — restart the round, change the map, `say`, `rcon`, remove dropped weapons
- 👻 **Hiding** — drop off the scoreboard and `status`, with a faked leave message
- 👁️ **ESP** — outlines players through walls while you are dead or spectating
- 🗳️ **Votes** — any question, vote kick and vote map
- 💬 **Admin chat** — team chat starting with `@` reaches only the admins
- 🔔 **Record alerts** — admins are told when a player with bans or gags on record joins
- 🗄️ **MySQL, MariaDB, PostgreSQL or SQLite** — one `admins` connection, which servers can share
- 🧩 **Developer API** — other plugins read admins, issue bans and add entries to `!admin`

## Contents

- [Installation](#installation)
- [Configuration](#configuration)
- [Immunity](#immunity)
- [Admin Menu](#admin-menu)
- [Hiding, ESP and Status](#hiding-esp-and-status)
- [Commands](#commands)
- [Permissions](#permissions)
- [Managing Admins from the Console](#managing-admins-from-the-console)
- [Developer API](#developer-api)
- [Building](#building)

## Installation

**Requirements:** SwiftlyS2, and a database connection named `admins` in SwiftlyS2's `configs/database.jsonc` (MySQL, MariaDB, PostgreSQL or SQLite). To run without one, set `UseDatabase` to `false`.

1. Unzip the release into `addons/swiftlys2/plugins/`.
2. Restart the server. The tables and `config.jsonc` are created on the first start.
3. Make yourself an admin from the server console, see [Managing Admins from the Console](#managing-admins-from-the-console).

| What | Where |
|------|-------|
| Plugin | `addons/swiftlys2/plugins/Admins/` |
| Config | `addons/swiftlys2/configs/plugins/Admins/config.jsonc` |
| Server GUID | `addons/swiftlys2/data/Admins/server_id.txt` |
| Translations | `addons/swiftlys2/plugins/Admins/resources/translations/` |
| Tables | `admins`, `groups`, `servers`, `bans`, `sanctions` and the migration ledger `VersionInfo` |

Keep `server_id.txt` when you reinstall. Admins, groups, bans and gags are tied to the server by that GUID, and a new one detaches the server from all of them.

### Upgrading from the split plugins

1. Install Admins as above.
2. Delete the `Admins.Core`, `Admins.Bans`, `Admins.Comms`, `Admins.Menu` and `Admins.SuperCommands` plugin folders. Left in place, they register every command a second time.
3. Restart. Admins takes over the server GUID from `data/Admins.Core/server_id.txt` and, if it has no `config.jsonc` yet, builds one from the three old configs. The old files are left as they are; delete them once the result looks right.

Plugins built against the old `V1` interfaces need updating, see [API Integration](docs/API-Integration.md#upgrading-from-v1).

## Configuration

`addons/swiftlys2/configs/plugins/Admins/config.jsonc`. Every key sits under `Main` and is described by a comment in the file. Changes apply on save, except that turning `UseDatabase` on needs a restart: the tables are created when the plugin loads.

| Key | Default | Description |
|-----|---------|-------------|
| `Prefix` | `[[blue]SwiftlyS2[default]]` | In front of every chat message. Color tags work |
| `UseDatabase` | `true` | Store everything in the `admins` connection. With `false` nothing is stored, and admins can only come from other plugins |
| `TimeZone` | `UTC` | Time zone of displayed dates, as an IANA or Windows id |
| `ImmunityMode` | `1` | See [Immunity](#immunity) |
| `Admins.DatabaseSyncIntervalSeconds` | `60` | How often admins and groups are reloaded, so changes made elsewhere apply. `0` = only on `!reloadadmins` |
| `Bans.DatabaseSyncIntervalSeconds` | `30` | How often online players are checked against the bans, so bans from other servers apply. `0` = off |
| `Bans.Reasons` | list | Reasons the menu offers for a ban |
| `Bans.DurationsInSeconds` | `[1800, 3600, 21600, 86400, 604800, 2592000, 0]` | Lengths the menu offers for a ban. `0` = permanent |
| `Bans.KickOnSteamAuthorizationFailure` | `true` | Kick players Steam cannot authorize. `false` lets them in during Steam outages, but their bans and admin rights go unchecked until they authorize |
| `Comms.DatabaseSyncIntervalSeconds` | `30` | How often online players' gags and mutes are reloaded. `0` = off |
| `Comms.Reasons` | list | Reasons the menu offers for a gag or mute |
| `Comms.DurationsInSeconds` | as `Bans` | Lengths the menu offers for a gag or mute |
| `Comms.EnableAdminChat` | `true` | Send team chat starting with `AdminChatStartCharacter` to the admins instead of the team |
| `Comms.AdminChatStartCharacter` | `@` | The admin chat prefix |
| `Kick.Reasons` | `["AFK", "Mic spam", "Toxic", "Bad name", "Other"]` | Reasons the menu offers for a kick, after *No reason* |
| `Server.Maps` | `[]` | Maps on the menu's *Change map* page, e.g. `de_dust2` or `ws:3070284539` |
| `Presence.ConnectMessages` | `true` | Use the plugin's join and leave messages instead of the game's. `!hide` fakes these, so with `false` it announces nothing |
| `Status.Enabled` | `true` | Answer players' `status` with the lines below, without hidden admins |
| `Status.Header` | see file | Lines above the players. Placeholders: `{hostname}`, `{map}`, `{humans}`, `{bots}`, `{maxplayers}`, `{date}`, `{time}` |
| `Status.Row` | see file | One line per player. Placeholders: `{userid}`, `{name}`, `{steamid}`, `{duration}`, `{ping}` |
| `Status.Footer` | `["#end"]` | Lines below the players, with the header's placeholders |
| `Esp.AllowWhileAlive` | `false` | Show ESP to admins who are alive too, not only to dead and spectating ones |

A width pads a placeholder and cuts it when longer: `{name,-24}` is left-aligned in 24 columns, `{ping,4}` right-aligned in 4.

Default lists only fill a new file; an existing file keeps its own.

## Immunity

An admin's immunity is the highest of their own and their groups'. `ImmunityMode` decides who may act on whom:

| Value | Mode | An admin can target another admin when |
|-------|------|----------------------------------------|
| `0` | `IgnoreImmunity` | always |
| `1` | `ProtectFromLowerAccess` (default) | their immunity is **higher than or equal to** the target's |
| `2` | `ProtectFromEqualOrLowerAccess` | their immunity is **strictly higher** than the target's |
| `3` | `ProtectWithNoImmunityBypass` | like `2`, but two admins with immunity `0` can target each other |

Players with no admin entry on this server can always be targeted, and anyone can target themselves. The server console ignores immunity.

## Admin Menu

`!admin` opens the menu and `!admin <player>` a player's page. Each admin sees only what their permissions allow.

```
Admin Menu                  !admin
├─ Players (n)              Show: All, CT, T or Spectators
│  └─ <player>              !admin <player>
│     ├─ Punish             kick, ban, gag, mute, silence, slay, ungag, unmute
│     ├─ Control            freeze, slap, respawn, team, beacon, drug, god mode, noclip
│     ├─ Move and edit      go to, bring, rename, health, money
│     └─ History (n)        their bans, gags and mutes on every server
├─ Bans (n active)          Show: Active, Expired or All
│  ├─ Give ban
│  └─ <ban>                 summary, remove, change length, their player page
├─ Comms (n active)         the same for gags and mutes, with Type: All, Gags or Mutes
├─ My tools                 ESP, hide, noclip, god mode, respawn me
├─ Server                   restart round, remove dropped weapons, change map
└─ <other plugins' entries>
```

Bans and Comms list the punishments that apply to this server, and need the database. A player's page offers only what applies right now, such as respawn while they are dead; once they leave, it keeps ban, gag, mute and silence, which then act on their SteamID. Remove and change length are offered for active punishments of this server.

### Giving a punishment

A page per step. Steps that do not apply are skipped.

| Step | Offers |
|------|--------|
| Player | The players on the server, **Recently left (n)** with everyone who left in the last 15 minutes, and **Someone else, by SteamID or IP** |
| Type | Gag, mute or silence, when you came from **Give gag or mute** |
| Reason | The configured reasons and **Other reason**. A kick is issued here |
| Length | The configured lengths and **Other length** |
| Scope | **This server** or **All servers**, when you may do both |
| Confirm | **Confirm**, **Summary in chat**, and **Match by** SteamID or IP |

- `!ban`, `!gag`, `!mute`, `!silence` and `!kick` without arguments open these steps. `!ban` is this server and `!globalban` every server; the `ip` forms start matched by IP.
- Nothing is typed into the menu. **Other reason**, **Other length**, **Other amount**, **Other map** and **Someone else** print the chat command to type, with what is known filled in.
- Reason and length start on what you picked last.
- A player who leaves halfway is still punished, by SteamID.
- Gagging or muting someone who already is replaces theirs.

### Keys

| Key | Action |
|-----|--------|
| **Shift** / **F** | Up / down |
| **E** | Pick |
| **Tab** | Back one page |
| **A** / **D** | Five rows up / down |

The bindings are SwiftlyS2's, set in its `configs/menus/menus.jsonc`.

## Hiding, ESP and Status

**`!hide`** takes you off your team and the scoreboard and tells everyone you left. While you are hidden:

| Where | While hidden |
|-------|--------------|
| `status` | You are left out. Other admins still see you, marked *(hidden)* |
| Chat and voice | Blocked, so they cannot give you away. Chat commands and admin chat still work |
| Spectating and ESP | Work as usual |
| Team menu | Stays closed, also after a map change |

`!unhide` brings you back as a spectator, announced with a "joined the game" message. Joining a team any other way also ends the hide, with the same message.

Hiding covers the game's own lists only. The server browser and outside tools such as a website's server list still count you, messages about the actions you take still name you, and `mp_force_pick_time` forcing you onto a team ends the hide.

**`!esp`** outlines every living player through walls, T in red and CT in blue, while you are dead or spectating, or always with `Esp.AllowWhileAlive`. No one else receives the outlines.

**Join and leave messages**: with `Presence.ConnectMessages`, "*name* joined the game" once a player has loaded in, and "*name* left the game (*reason*)" with Disconnect, Kicked, Banned or Timed out. These are what `!hide` and `!unhide` fake.

**`status`** typed by a player shows the lines under `Status` instead of the game's output, so hidden admins stay hidden there too. The server console's own `status` is unchanged.

## Commands

Commands work in chat (`!command`) and, unless marked player only, from the server console (`sw_command`). `<argument>` is required, `[argument]` optional. Lengths are written like `30m`, `12h` or `7d`; `0` is permanent.

Wherever a command takes `<player>`:

| Form | Matches |
|------|---------|
| `name` | The exact name, otherwise a part of a name that matches exactly one player |
| `#userid` | The player with that user id |
| `76561198000000000` | The player with that SteamID64 |
| `@all`, `@t`, `@ct`, `@me`, `@aim`, ... | SwiftlyS2's target selectors |

Bans, gags, mutes, `!kick`, `!bring` and `!votekick` leave the admin running them out of group selectors such as `@all`. The ban, gag, mute and silence commands also take the SteamID64 of a player who is not on the server, or an IP address for the `ip` forms.

### Admin Management

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!admin` | `[player]` | `admins.commands.admin` | Open the admin menu, or a player's page in it. Player only. |
| `!admins` | `<give\|edit\|remove\|list> ...` | `admins.command.admins` | Manage admins, see [Managing Admins from the Console](#managing-admins-from-the-console). |
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

The old plugin's `o` forms still work, the same as the commands above: `!gago`, `!muteo`, `!silenceo` and their `global` forms take a SteamID64, and `!gagipo`, `!muteipo`, `!silenceipo` and their `global` forms an IP address.

### Player Commands

Every player command except `!goto`, which only moves you, respects immunity.

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!kick` | `<player> [reason]` | `admins.commands.kick` | Kick a player. Without arguments, opens a player picker. |
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
| `!map`, `!changemap` | `<map_name\|ws:workshop_id>` | `admins.commands.map` | Change the map after 3 seconds. Refused while another change is on its way. |
| `!clean` | | `admins.commands.clean` | Remove every dropped weapon from the map. |

### Admin Tools

See [Hiding, ESP and Status](#hiding-esp-and-status).

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!esp`, `!wh`, `!xray` | | `admins.commands.esp` | Toggle outlines of every living player through walls. Player only. |
| `!hide` | | `admins.commands.hide` | Leave your team for no team, out of sight, with a faked leave message. Player only. |
| `!unhide` | | `admins.commands.hide` | Come back as a spectator, with a faked join message. Player only. |

### Votes

Votes run for 30 seconds in a menu shown to every player; players who join later cannot vote. The option with the most votes wins, and a tie or no votes ends without a result. A closed ballot reopens with `!vote`, which needs no permission. A vote still running at a map change is cancelled.

| Command | Arguments | Permission | Description |
|---------|-----------|------------|-------------|
| `!vote` | `<question> <option1> <option2> [option3...]` | `admins.commands.vote` | Start a custom vote. Without arguments, reopens your ballot (anyone). |
| `!votekick` | `<player>` | `admins.commands.votekick` | Vote to kick a player. *Yes* has to beat *No*. |
| `!votemap` | `<map1> <map2> [map3...]` | `admins.commands.votemap` | Vote on a map change. The winning map is loaded when the vote ends. |

## Permissions

Each command's permission is in its table. Besides those:

| Permission | Grants |
|------------|--------|
| `admins.chat` | Admin chat: team chat starting with `@` from anyone goes to the holders instead of the team. A gagged player can still send it |
| `admins.notify` | A message when a player with bans, gags or mutes on record connects |
| `admins.menu.bans`, `admins.menu.comms` | From the old menu plugin: banning, gagging and muting on this server from the menu |

A permission ending in `.*` grants every permission that starts with the part before it. `admins.*` covers this whole plugin. `admins.commands.*` covers every `admins.commands.` permission, so not `admins.command.admins`, `admins.command.groups`, `admins.command.reloadadmins`, `admins.chat`, `admins.notify` or the `admins.menu.` ones.

## Managing Admins from the Console

`sw_admins` and `sw_groups` in the server console, or `!admins` and `!groups` in chat. Lists are comma-separated; quote a value that contains spaces.

| Command | Does |
|---------|------|
| `sw_admins give <steamid64> <username> <immunity> [permissions] [groups] [servers]` | Make someone an admin on this server. An existing entry is overwritten, keeping its other servers |
| `sw_admins edit <steamid64> <field> <value>` | Change one field: `username`, `immunity`, `permissions`, `groups` or `servers`. `servers` replaces the whole list |
| `sw_admins remove <steamid64>` | Take the admin off this server. The entry is deleted once no server is left |
| `sw_admins list` | The admins of this server |
| `sw_groups give <name> <immunity> [permissions] [servers]` | Create a group on this server. An existing group is overwritten, keeping its other servers |
| `sw_groups edit <name> <immunity> [permissions] [servers]` | Change an existing group. Its permissions are replaced, so list all it keeps; the servers listed are added |
| `sw_groups remove <name>` | Take the group off this server. It is deleted once no server is left |
| `sw_groups list` | The groups of this server |

`[servers]` are the GUIDs of other servers, from their `server_id.txt`; `give` always adds this server. Groups must exist before admins are put in them. Usernames are unique, and group names match regardless of case.

A first setup, with groups for staff and yourself as the owner:

```
sw_groups give owner 100 "admins.*"
sw_groups give admin 90 "admins.commands.*,admins.chat,admins.notify"
sw_groups give moderator 50 "admins.commands.admin,admins.commands.kick,admins.commands.ban,admins.commands.gag,admins.commands.mute,admins.chat"
sw_admins give 76561198000000000 "YourName" 100 "" "owner"
```

## Developer API

Other plugins use Admins through `Admins.Contract.dll`, found in the plugin's `resources/exports/`.

| Guide | Contents |
|-------|----------|
| [API Integration](docs/API-Integration.md) | Referencing the contract, getting the interfaces, adding entries to `!admin`, upgrading from `V1` |
| [API Reference](docs/API-Reference.md) | Every interface, member, event and enum |

## Building

Build `Admins.csproj` with the .NET 10 SDK. `dotnet publish -c Release` writes the plugin to `build/publish/Admins/` and a zip of it to `build/Admins.zip`.
