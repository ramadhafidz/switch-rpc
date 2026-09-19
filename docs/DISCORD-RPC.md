# DISCORD-RPC.md

Documentation of the **Discord Rich Presence** integration in SWITCH RPC.

This document explains how the application communicates with Discord, the RPC wrapper structure, the connection lifecycle, the Rich Presence payload, artwork, troubleshooting, and development rules.

---

## 1. Overview

SWITCH RPC uses Discord Rich Presence to display the status of the game currently being played.

The simplified flow:

```text
Eden
  ↓
Game Detection
  ↓
Game Definition
  ↓
GameState
  ↓
PresenceClient
  ↓
DiscordRichPresence library
  ↓
Discord IPC
  ↓
Discord Desktop
```

The application does not call Discord library objects from every project. The Discord integration is wrapped by:

```text
src/SwitchRpc.Discord/PresenceClient.cs
```

The goal is to keep the Discord dependency isolated — only this project touches the Discord library types.

---

## 2. Technology

Current RPC components:

- Discord Desktop
- Discord Rich Presence / RPC
- **DiscordRichPresence** library (NuGet, by Lachee)
- .NET 10
- Discord IPC

Important note: the correct NuGet package is **`DiscordRichPresence`**; the package named `DiscordRPC` on NuGet is a different library. The code namespace remains `DiscordRPC`.

Dependency and API documentation must be verified against the version actually in use (XML documentation ships with the package).

---

## 3. Discord Application

Rich Presence requires a Discord Application.

The application provides:

- Client/Application ID;
- Rich Presence assets;
- the Discord application configuration.

The Client ID is used by the application to open the RPC connection and is loaded from `config.json`.

Do not put Discord tokens or private credentials into the repository configuration.

The Client/Application ID itself is not a secret like a password or token, but it should still be configured deliberately.

---

## 4. RPC Wrapper

Main file:

```text
src/SwitchRpc.Discord/PresenceClient.cs
```

Class:

```csharp
PresenceClient
```

The wrapper handles:

```text
Connect()
Update()
Clear()
Dispose()
```

Lifecycle:

```text
Disconnected
     ↓
Connect()
     ↓
Connected
     ↓
Update()
     ↓
Connected
     ↓
Clear()
     ↓
Dispose()
     ↓
Disconnected
```

Connection state is tracked through the library's events (`OnReady`, `OnClose`, `OnConnectionFailed`, `OnError`), not only from the `Connect()` result.

---

## 5. Connection Lifecycle

### Connect

The application does not create a new connection on every polling cycle.

A connection is created when needed, and a failed connection does not stop the application:

```text
Application starts
        ↓
Discord unavailable
        ↓
Connect() returns false
        ↓
Application keeps running
        ↓
next required update
        ↓
reconnect attempt
```

### Reconnect

After the IPC pipe dies (for example, Discord is closed mid-session), the library still marks itself as initialized. `Connect()` therefore calls `Deinitialize()` before `Initialize()` again.

`SetPresence` is fire-and-forget — it only enqueues a message. Pipe failure is detected asynchronously through events, so there is a delay of a few seconds between Discord closing and the wrapper's status changing.

---

## 6. Elapsed Time

The RPC uses a start timestamp to display elapsed session time.

The timestamp is stored when the first connect succeeds and is **preserved** across reconnects — the timer in Discord does not reset when the connection drops.

The timestamp is sent on every update through `Timestamps`.

Notes:

- the timestamp comes from application runtime;
- restarting the application creates a new session timestamp;
- switching games may require a separate decision about whether the timer should reset.

---

## 7. Rich Presence Payload

The current payload concepts:

```text
details
state
large_image
large_text
timestamps
```

Example display:

```text
Details:
Pokédex

State:
Paldea: 22/400

Artwork:
scarlet (tooltip "Pokémon Scarlet")
```

Meaning:

```text
details      → activity context
state        → additional information (Pokédex page)
large_image  → game artwork
large_text   → artwork tooltip
timestamps   → elapsed session time
```

---

## 8. Data Source

The displayed data comes from `GameState`, shaped by `PresenceFormatter` in `SwitchRpc.Core`:

```text
Save Reader
    ↓
GameState
    ↓
PresenceFormatter
    ↓
RPC payload
```

Do not hardcode game names, artwork, or save data inside the RPC wrapper.

The RPC wrapper should receive the data to display, not decide which game is being played.

---

## 9. Artwork

Artwork is stored as Discord Rich Presence assets.

The game configuration determines the artwork key:

```json
{
	"pokemon_scarlet": {
		"large_image": "scarlet",
		"large_text": "Pokémon Scarlet"
	}
}
```

Example keys:

```text
arceus
scarlet
violet
za
```

The keys must match the assets available on the Discord Application.

If an asset key does not match, the artwork will not display as expected.

### Artwork Naming

Use asset names that are:

- simple;
- stable;
- lowercase when possible;
- independent of local filenames;
- unchanged by display name changes.

Do not use local paths — Discord artwork is stored on the Discord Application, not in the project's filesystem.

---

## 10. Update Interval

Two intervals are configured through `config.json`:

```json
{
	"discord": {
		"save_refresh_interval": 15,
		"pokedex_rotation_interval": 5
	}
}
```

- `save_refresh_interval` — how often the save is re-read;
- `pokedex_rotation_interval` — how often the Pokédex page rotates.

Each tick, the loop:

```text
detects Eden + game
    ↓
refreshes the save when due
    ↓
rotates the dex page when due
    ↓
updates the RPC when the display changed
    ↓
waits
```

Intervals that are too small can cause excessive filesystem scans, overly frequent save parsing, and overly frequent RPC updates.

---

## 11. Update Only When Needed

The application should not update Discord without a reason.

Examples of meaningful changes:

```text
No game  →  Scarlet    → RPC needs an update
Scarlet  →  No game    → RPC needs clearing
Paldea: 22/400  →  Kitakami: 3/200    → RPC needs an update
```

The application compares the next display against the last one sent; updates are only sent when there is a difference.

---

## 12. Clearing RPC

When the game stops, the RPC must be cleared.

```csharp
_rpc.Clear();
```

This matters so Discord does not keep showing the game after it has been closed.

When the application itself shuts down, the `finally` block must also clear the RPC before `Dispose()`.

### Clear vs Dispose

They have different purposes.

### `Clear()`

Removes the active Rich Presence.

### `Dispose()`

Closes the RPC connection and releases resources.

Shutdown order:

```text
Clear()
  ↓
Dispose()
```

---

## 13. Error Handling

The RPC wrapper must handle external errors.

Examples of failures:

- Discord not running;
- IPC unavailable;
- connection dropped;
- payload rejected;
- Discord RPC error.

The application should keep running if the RPC fails — the failure is logged, the connection status is updated, and the next update attempts to reconnect.

However, do not use exception handling to hide programming errors without logging.

---

## 14. Discord IPC

Discord Desktop provides an IPC endpoint (named pipe) used by the RPC library.

Developers do not need to create their own named pipe/IPC protocol as long as the library provides the required abstraction.

If IPC troubleshooting is needed, check the Discord environment and the library documentation before writing a custom implementation.

---

## 15. Testing RPC

Minimal testing:

### Test 1 — Discord available

1. Open Discord Desktop.
2. Run the application.
3. Confirm the connection succeeds.
4. Start Eden.
5. Start a game.
6. Check the Rich Presence.

### Test 2 — Game stops

1. RPC is active.
2. Close the game.
3. Confirm the detector no longer finds the game.
4. Confirm the RPC is cleared.

### Test 3 — Discord restart

1. Run the application.
2. Confirm the RPC is active.
3. Close Discord (quit, not minimize).
4. Observe `Discord RPC disconnected. Reconnecting...` and temporary failures.
5. Reopen Discord.
6. Confirm the application reconnects and the presence returns with an unreset timer.

### Test 4 — Switching games

1. Run game A.
2. Confirm the RPC shows game A.
3. Close game A.
4. Run game B.
5. Confirm the RPC switches to game B.

---

## 16. Debugging Checklist

If the RPC does not appear:

### Discord

- [ ] Discord Desktop is running.
- [ ] The user is logged in to Discord.
- [ ] The Discord Application exists.
- [ ] The Client ID is correct.

### Application

- [ ] `Connect()` succeeded.
- [ ] `IsConnected == true`.
- [ ] The game is detected.
- [ ] `Update()` succeeded.

### Artwork

- [ ] The asset exists on the Discord Application.
- [ ] The asset key matches `large_image`.
- [ ] Discord has finished processing the artwork.

---

## 17. Common Failure: RPC Does Not Connect

Symptom:

```text
Failed to connect to Discord.
```

Check:

1. Discord Desktop;
2. Client ID;
3. IPC;
4. application configuration.

Do not change the connection code before confirming the Discord environment is healthy.

---

## 18. Common Failure: RPC Connects But Does Not Display

Possibilities:

- the game has not been detected;
- an update has not been sent;
- the payload failed;
- the Discord application is wrong;
- the artwork key is wrong.

Look at the log:

```text
Discord RPC connected.
Game detected: Pokémon Scarlet
Rich Presence updated: Pokédex | Paldea: 22/400
```

If `Rich Presence updated.` appears but the artwork is broken, focus debugging on the asset configuration, not the connection.

---

## 19. Common Failure: Timer Does Not Match

The timer uses the timestamp from the first RPC connection.

The timestamp is preserved across reconnects, so the displayed duration is the duration of the running application session.

If this behavior should change (for example, a per-game timer), that decision belongs to the application/session state in `AppLoop`, not to the Discord wrapper.

---

## 20. Keep Game Logic Out of RPC

Avoid methods or branches like:

```csharp
public void UpdateScarlet() { ... }

if (gameId == "pokemon_scarlet") { ... }
```

inside the RPC wrapper.

Use:

```text
Game detection
      ↓
Game definition
      ↓
Game state
      ↓
RPC payload
```

The RPC's only job is to send data.

---

## 21. API Isolation

The DiscordRichPresence library should only be used in:

```text
src/SwitchRpc.Discord/
```

Other projects should never do:

```csharp
using DiscordRPC;
```

With this isolation, if the library is replaced in the future, the changes can be focused on a single project.

---

## 22. Configuration Rules

Do not hardcode:

- Client ID;
- intervals;
- game display names;
- regions;
- artwork keys.

That data lives in `config.json` — see `docs/CONFIGURATION.md`.

---

## 23. Development Rules

When changing Discord RPC:

- use the existing wrapper;
- do not spread the Discord library into other projects;
- check the documentation for the dependency version in use;
- preserve the reconnect behavior;
- preserve the shutdown cleanup;
- do not create a connection on every polling cycle;
- do not put game-specific logic into the RPC wrapper;
- test against a real Discord Desktop.

---

## 24. Security

Do not store:

- Discord tokens;
- user tokens;
- private credentials;
- secret API keys;

in source code or the repository.

The Client/Application ID is not a Discord login credential, but still do not add other credentials carelessly.

---

## 25. Future RPC Features

Features that could be added:

- party Pokémon;
- current map;
- badges/progression;
- game-specific details;
- dynamic artwork;
- trainer name.

Every feature must:

1. have a clear data source;
2. come from a verified save reader or runtime source;
3. be added to `GameState`;
4. then be mapped to RPC.

---

## 26. Related Documentation

- `README.md`
- `AGENTS.md`
- `docs/ARCHITECTURE.md`
- `docs/DEVELOPMENT.md`
- `docs/CONFIGURATION.md`
- `docs/SAVE-READER.md`
- `docs/GAME-SUPPORT.md`
- `docs/TROUBLESHOOTING.md`
- `docs/ROADMAP.md`

---

## 27. Reference

Relevant external documentation:

- Discord Rich Presence documentation
- DiscordRichPresence library documentation (XML docs ship with the package)
- Discord IPC documentation

Always use documentation matching the version of the dependency in use.
