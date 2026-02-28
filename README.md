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

## Table of Contents

- [Building](#building)
- [Publishing](#publishing)
- [Database Connection Key](#database-connection-key)
- [Commands & Permissions](#commands--permissions)
- [Server Console Admin Management](#server-console-admin-management)

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.

## Database Connection Key

The database connection is using the key `admins`. It supports SQLite, MySQL, MariaDB and PostgreSQL.

## Commands & Permissions

### Core Commands

| Command | Permission | Description |
|---------|-----------|-------------|
| `!admins <give/edit/remove/list>` | `admins.command.admins` | Manage server admins |
| `!groups <give/edit/remove/list>` | `admins.command.groups` | Manage admin groups |
| `!admin` | `admins.commands.admin` | Open admin menu |

### Ban Commands (Admins.Bans)

| Command | Permission | Description |
|---------|-----------|-------------|
| `!ban <player> <duration> [reason]` | `admins.commands.ban` | Ban a player by SteamID |
| `!globalban <player> <duration> [reason]` | `admins.commands.globalban` | Ban a player globally across all servers |
| `!banip <player> <duration> [reason]` | `admins.commands.ban` | Ban a player by IP address |
| `!globalbanip <player> <duration> [reason]` | `admins.commands.globalban` | Ban a player's IP globally |
| `!bano <steamid64> <duration> [reason]` | `admins.commands.ban` | Ban offline player by SteamID64 |
| `!globalbano <steamid64> <duration> [reason]` | `admins.commands.globalban` | Ban offline player globally by SteamID64 |
| `!banipo <ip_address> <duration> [reason]` | `admins.commands.ban` | Ban offline player by IP address |
| `!globalbanipo <ip_address> <duration> [reason]` | `admins.commands.globalban` | Ban offline player's IP globally |
| `!unban <steamid64>` | `admins.commands.unban` | Unban a player by SteamID64 |
| `!unbanip <ip_address>` | `admins.commands.unban` | Unban an IP address |

### Communication Commands (Admins.Comms)

| Command | Permission | Description |
|---------|-----------|-------------|
| `!gag <player> <duration> [reason]` | `admins.commands.gag` | Prevent player from using voice chat |
| `!globalgag <player> <duration> [reason]` | `admins.commands.globalgag` | Gag player globally across all servers |
| `!mute <player> <duration> [reason]` | `admins.commands.mute` | Prevent player from using text chat |
| `!globalmute <player> <duration> [reason]` | `admins.commands.globalmute` | Mute player globally across all servers |
| `!silence <player> <duration> [reason]` | `admins.commands.silence` | Prevent player from using both voice and text chat |
| `!globalsilence <player> <duration> [reason]` | `admins.commands.globalsilence` | Silence player globally across all servers |
| `!ungag <player>` | `admins.commands.ungag` | Remove gag from player |
| `!unmute <player>` | `admins.commands.unmute` | Remove mute from player |
| `!unsilence <player>` | `admins.commands.unsilence` | Remove silence from player |
| `!gago <steamid64> <duration> [reason]` | `admins.commands.gag` | Gag offline player by SteamID64 |
| `!muteo <steamid64> <duration> [reason]` | `admins.commands.mute` | Mute offline player by SteamID64 |
| `!silenceo <steamid64> <duration> [reason]` | `admins.commands.silence` | Silence offline player by SteamID64 |
| `!globalgago <steamid64> <duration> [reason]` | `admins.commands.globalgag` | Gag offline player globally |
| `!globalmuteo <steamid64> <duration> [reason]` | `admins.commands.globalmute` | Mute offline player globally |
| `!globalsilenceo <steamid64> <duration> [reason]` | `admins.commands.globalsilence` | Silence offline player globally |
| `!gagip <player> <duration> [reason]` | `admins.commands.gag` | Gag player by IP address |
| `!muteip <player> <duration> [reason]` | `admins.commands.mute` | Mute player by IP address |
| `!silenceip <player> <duration> [reason]` | `admins.commands.silence` | Silence player by IP address |
| `!globalgagip <player> <duration> [reason]` | `admins.commands.globalgag` | Gag IP globally |
| `!globalmuteip <player> <duration> [reason]` | `admins.commands.globalmute` | Mute IP globally |
| `!globalsilenceip <player> <duration> [reason]` | `admins.commands.globalsilence` | Silence IP globally |
| `!gagipo <ip_address> <duration> [reason]` | `admins.commands.gag` | Gag offline player by IP |
| `!muteipo <ip_address> <duration> [reason]` | `admins.commands.mute` | Mute offline player by IP |
| `!silenceipo <ip_address> <duration> [reason]` | `admins.commands.silence` | Silence offline player by IP |
| `!globalgagipo <ip_address> <duration> [reason]` | `admins.commands.globalgag` | Gag offline player's IP globally |
| `!globalmuteipo <ip_address> <duration> [reason]` | `admins.commands.globalmute` | Mute offline player's IP globally |
| `!globalsilenceipo <ip_address> <duration> [reason]` | `admins.commands.globalsilence` | Silence offline player's IP globally |

### Player Management Commands (Admins.SuperCommands)

| Command | Permission | Description |
|---------|-----------|-------------|
| `!hp <player> <amount>` | `admins.commands.hp` | Set player health |
| `!freeze <player>` | `admins.commands.freeze` | Freeze a player |
| `!unfreeze <player>` | `admins.commands.unfreeze` | Unfreeze a player |
| `!noclip <player>` | `admins.commands.noclip` | Toggle noclip for a player |
| `!setspeed <player> <speed>` | `admins.commands.setspeed` | Set player movement speed |
| `!setgravity <player> <gravity>` | `admins.commands.setgravity` | Set player gravity |
| `!slay <player>` | `admins.commands.slay` | Kill a player |
| `!slap <player> [damage]` | `admins.commands.slap` | Slap a player (optional damage) |
| `!rename <player> <new_name>` | `admins.commands.rename` | Rename a player |
| `!givemoney <player> <amount>` | `admins.commands.givemoney` | Give money to a player |
| `!setmoney <player> <amount>` | `admins.commands.setmoney` | Set player's money |

### Weapon Commands (Admins.SuperCommands)

| Command | Permission | Description |
|---------|-----------|-------------|
| `!giveitem <player> <item_name>` | `admins.commands.giveitem` | Give an item/weapon to a player |
| `!melee <player>` | `admins.commands.melee` | Strip weapons and give knife only |
| `!disarm <player>` | `admins.commands.disarm` | Remove all weapons from a player |

### Server Commands (Admins.SuperCommands)

| Command | Permission | Description |
|---------|-----------|-------------|
| `!restartround [delay]` | `admins.commands.restartround` | Restart the current round |
| `!csay <message>` | `admins.commands.csay` | Send center message to all players |
| `!rcon <command>` | `admins.commands.rcon` | Execute server console command |
| `!map <map_name>` | `admins.commands.map` | Change map (workshop or official) |

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
sw_groups give owner 100 "admins.commands.*"
sw_groups give admin 90 "admins.commands.*"
sw_groups give moderator 50 "admins.commands.ban,admins.commands.kick,admins.commands.mute,admins.commands.gag"
sw_groups give vip 10 "admins.commands.noclip"

# Add yourself as owner
sw_groups give 76561198123456789 "YourName" 100 "" "owner"

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

You can use wildcards in permissions:
- `admins.commands.*` - All command permissions
- `admins.commands.ban*` - All ban-related permissions (ban, banip, globalban, etc.)
- `admins.commands.mute*` - All mute-related permissions
