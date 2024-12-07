# Gamble Plugin for Rust

## Overview
This plugin creates a gambling system in a Rust server, allowing players to participate in rounds by gambling a specified amount of in-game currency (`scrap`). The winner of each round is randomly selected, and winnings are distributed accordingly.

## Details

### Plugin Info
- **Name:** Gamble
- **Author:** Ghosty
- **Version:** 1.1.0

### Features
1. **Gambling Rounds:** Players can join rounds by using the `/gamble <amount>` command.
2. **Countdown Timer:** Each round has a countdown timer before a winner is selected.
3. **Auto-Start:** Rounds can be set to auto-start at specific intervals.
4. **Broadcast Messages:** Game events are broadcast to all players or logged in the server console based on configuration.
5. **Dynamic Winner Selection:** Randomly selects a winner based on the total pot.

### Configuration
- **CountdownTime:** Time (in seconds) before a round ends. Default is `600`.
- **CurrencyShortName:** The in-game currency used for gambling. Default is `"scrap"`.
- **BroadcastToAll:** Determines whether events are broadcast to all players. Default is `true`.
- **BroadcastPrefix:** Prefix for broadcast messages. Default includes the plugin's branding.
- **AutoStartInterval:** Interval (in seconds) to automatically start a round if no round is active. Default is `1800`.

### Commands
- **/gamble <amount>:** Enter a gamble with the specified amount of currency.

### Events
1. **Round Start:** Notifies players of a new round and countdown.
2. **Countdown Updates:** Periodic reminders of time remaining in the round.
3. **Round End:** Declares a winner or indicates no participants if applicable.

### Winner Selection
- A participant is chosen randomly, weighted by their contribution to the total pot.
- The winner receives all the accumulated currency.

### Extras
- Ensures players have sufficient funds before entering a gamble.
- Handles inventory item removal and addition with fallbacks to drop items if inventory is full.

### Licensing
This plugin is **exclusively licensed** to Enchanted.gg and may not be edited or sold without explicit permission.


© 2024 Ghosty & Enchanted.gg
