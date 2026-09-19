# TROUBLESHOOTING.md

Troubleshooting guide for **SWITCH RPC**.

This document covers common problems involving the .NET application, Eden detection, save reading, PKHeX, and Discord Rich Presence.

---

## 1. General Troubleshooting Workflow

When something does not work, isolate the problem by layer:

```text
Environment
    ↓
Build
    ↓
Eden Detection
    ↓
Game Detection
    ↓
Save Path
    ↓
PKHeX Identification
    ↓
Save Parsing
    ↓
GameState
    ↓
Discord RPC
```

Do not change multiple unrelated components at once.

Start with the smallest failing layer, verify it independently, then continue upward.

The `--diagnose` mode exercises most of this chain in one command:

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

---

## 2. Application Does Not Start

### Symptoms

The application exits immediately or `dotnet run` reports a build error.

### Check the .NET SDK

```powershell
dotnet --version
dotnet --list-sdks
```

The project requires the .NET 10 SDK.

### Build the solution

```powershell
dotnet build SwitchRpc.slnx
```

### Common build failures

- **PKHeX.Core project not found** — the local PKHeX checkout is missing at `bridge/PKHeX/`. See `docs/DEVELOPMENT.md` for the expected layout.
- **File locks (`MSB3021`/`MSB3027`)** — a previous application instance is still running and holding its DLLs. Stop it before rebuilding.
- **NuGet restore failure** — inspect configured package sources; use `--ignore-failed-sources` only when appropriate.

---

## 3. Eden Is Not Detected

### Symptoms

The application prints:

```text
Eden: not running
```

even though Eden is open.

### Check the process

```powershell
Get-Process eden -ErrorAction SilentlyContinue
```

If no process is returned, verify that the emulator executable is actually named:

```text
eden.exe
```

The detector checks the process name rather than the installation path.

### Important

The application does not require Eden to be installed in a particular directory.

For example, the following is an installation detail and should not be hardcoded:

```text
D:\Games\Eden
```

---

## 4. Eden Is Running but the Game Is Not Detected

### Symptoms

Eden is detected, but:

```text
Game: none
```

or no game is reported.

### Check the Eden window title

The detector matches visible Eden window titles against the game display names configured in `config.json`.

The title should contain a configured game name, for example:

```text
Pokémon Legends: Arceus
Pokémon Scarlet
Pokémon Violet
Pokémon Legends: Z-A
```

If the title is different from the expected format, verify the actual Eden title and adjust the `name` in `config.json` — display names are configuration, not code.

---

## 5. Game Is Detected but Save Is Not Found

### Symptoms

The application detects the game, but save reading reports:

```text
Save not found for game: ...
```

### Check the save root

The locator expects the Eden user save directory under:

```text
%APPDATA%\eden\nand\user\save\
```

On Windows this normally corresponds to:

```text
C:\Users\<user>\AppData\Roaming\eden\nand\user\save\
```

The application uses `Environment.GetFolderPath` instead of hardcoding a user name.

### Check the title ID

Title IDs are configured per game in `config.json`.

A missing or wrong `title_id` means save resolution is intentionally unavailable for that game.

Do not invent a title ID.

---

## 6. Save File Exists but Is Not Read

### Symptoms

The save directory exists and contains a `main` file, but reading fails.

Run diagnose to isolate the layer:

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

It reports whether the save was found, identified by PKHeX, and parsed.

---

## 7. PKHeX Cannot Identify the Save

### Symptoms

The application reports that PKHeX could not identify the save.

Possible causes include:

- unsupported save format;
- invalid or incomplete save;
- wrong file selected;
- incompatible PKHeX version;
- damaged save.

### Debugging

First verify the exact file:

```powershell
Get-Item "<save-path>"
```

Do not create a custom parser as a workaround until the save format and PKHeX support have been verified.

---

## 8. Save Data Looks Incorrect

Do not assume the parser is correct just because it returns a number.

For any suspicious field:

1. identify the PKHeX API used;
2. inspect the relevant PKHeX source (`bridge/PKHeX/`);
3. verify the save format;
4. compare against known in-game data;
5. test with another valid save if available;
6. document the result.

For save structure research:

> Never guess offsets or binary structures.

Prefer verified PKHeX abstractions.

---

## 9. Pokédex Count Is Unexpected

Different Pokémon games have different Pokédex structures.

Do not assume:

```text
total = MaxSpeciesID
```

is the final representation for every game.

For example, a game may have:

- multiple regional dexes;
- DLC dexes;
- separate local dex blocks;
- species that exist in the save but are not part of the target dex.

If the displayed total needs refinement, inspect the game's actual PKHeX Pokédex abstraction and verify which entries should count.

---

## 10. Discord RPC Does Not Connect

### Symptoms

```text
Failed to connect to Discord.
```

Check:

- Discord Desktop is running;
- the user is logged in;
- the configured Application ID is correct;
- Discord IPC is available.

The application does not require a Discord bot token for Rich Presence.

---

## 11. Discord RPC Connects but Presence Does Not Appear

Check the application log.

Expected flow:

```text
Discord RPC connected.
Game detected: Pokémon Scarlet
Save data refreshed.
Rich Presence updated: Pokédex | Paldea: 22/400
```

If the game is not detected, the RPC update may never happen.

If `Rich Presence updated.` appears but the artwork or display is wrong, investigate the Discord Application configuration and payload.

---

## 12. Artwork Does Not Appear

Check the Discord Application assets.

The configured asset key:

```json
"large_image": "scarlet"
```

must match the asset configured in Discord.

Common mistakes:

- wrong asset key;
- asset was renamed;
- asset is missing;
- configuration was changed but application was not restarted.

The application does not load artwork files from the local filesystem for Rich Presence.

---

## 13. RPC Does Not Clear After Closing the Game

Check game state transitions.

The application should detect:

```text
Game running
    ↓
Game closes
    ↓
Game: none
    ↓
Clear RPC
```

If the application itself is stopped, shutdown cleanup should also execute through the `finally` block.

---

## 14. RPC Connection Drops

If Discord disconnects during runtime, the wrapper marks itself disconnected through the library events.

The next appropriate update can reconnect.

Expected behavior:

```text
Connected
    ↓
Connection failure (asynchronous)
    ↓
Disconnected
    ↓
Deinitialize + Initialize
    ↓
Connected
```

Note that disconnect detection is asynchronous — `SetPresence` only queues a message, so there can be a short delay between Discord quitting and the wrapper noticing.

The application should not repeatedly create connections during every polling cycle.

---

## 15. Timer Is Not What You Expect

The RPC session start timestamp is created when the Discord RPC connection is first established, and it is preserved across reconnections.

Therefore:

```text
Application starts
    ↓
Discord connects
    ↓
session start
```

does not necessarily equal:

```text
Game starts
    ↓
session start
```

If accurate per-game session timing is required, session lifecycle should be handled at the application/game-state layer.

---

## 16. High CPU Usage

The application is designed to use periodic polling with a single process scan per tick.

Check the configured intervals:

```json
{
	"discord": {
		"save_refresh_interval": 15,
		"pokedex_rotation_interval": 5
	}
}
```

Avoid unnecessarily small intervals.

Potential expensive operations include:

- filesystem scans;
- save parsing;
- Discord updates.

Do not parse the entire save every second unless there is a demonstrated need.

---

## 17. Game Switch Is Not Reflected

If switching from one game to another does not update the RPC:

1. verify the first game is no longer detected;
2. verify the second game is detected;
3. check current-game handling in the loop;
4. check RPC clear/update behavior;
5. verify both games have valid `config.json` entries.

Expected flow:

```text
Game A
  ↓
None
  ↓
Game B
```

The detector should not rely on a stale game ID.

---

## 18. Configuration Problems

If the application reports a missing configuration:

Check `config.json` and verify:

```json
{
	"discord": {
		"client_id": "YOUR_DISCORD_APPLICATION_ID",
		"save_refresh_interval": 15,
		"pokedex_rotation_interval": 5
	}
}
```

and that each supported game has a corresponding entry under `games` with `name`, `title_id`, and `large_image`.

Stable internal IDs must match across:

```text
detection
configuration
save locator
GameState
```

Entries that are missing required fields are skipped with a warning at startup.

---

## 19. Context7 and Documentation Issues

When unsure about an external API:

1. check the local dependency source;
2. check the official documentation;
3. check the installed dependency version;
4. inspect source code if necessary;
5. reproduce the behavior locally.

Do not rely on examples written for unrelated versions.

This is particularly important for:

- PKHeX;
- DiscordRichPresence;
- .NET APIs;
- Eden.

---

## 20. Git / Local Files Accidentally Appear

Check:

```powershell
git status --ignored --short
```

Expected ignored development files include:

```text
bin/
obj/
bridge/PKHeX/
```

Save files and emulator data must not be committed.

If a file was already tracked before adding it to `.gitignore`, `.gitignore` alone will not untrack it.

Review the Git history before removing or rewriting tracked files.

---

## 21. Safe Debugging Rules

Never use debugging as a reason to modify the user's save.

Safe debugging means:

- reading files;
- printing metadata;
- inspecting PKHeX source;
- comparing read-only outputs;
- testing with copies.

Avoid operations that:

- write to `main`;
- overwrite save data;
- modify Pokémon;
- modify Pokédex;
- alter progress;
- create edited save files.

---

## 22. Minimal Diagnostic Checklist

When reporting a bug, collect:

```text
OS:
.NET SDK version:
Eden version:
Game:
Game version:
Detected process:
Detected window title:
Diagnose output:
Discord connection result:
Relevant error:
```

Do not include private credentials or unnecessary personal data.

---

## 23. When to Stop and Investigate

Stop implementation and investigate when:

- a save offset is unknown;
- PKHeX behavior is unclear;
- a game format is not verified;
- the save type is ambiguous;
- a dependency API differs from documentation;
- Discord RPC behavior is inconsistent;
- the same error persists after an apparently correct fix.

A verified incomplete implementation is preferable to an unverified implementation that silently reports incorrect data.

---

## 24. Related Documentation

- `README.md`
- `AGENTS.md`
- `docs/ARCHITECTURE.md`
- `docs/DEVELOPMENT.md`
- `docs/CONFIGURATION.md`
- `docs/SAVE-READER.md`
- `docs/GAME-SUPPORT.md`
- `docs/DISCORD-RPC.md`
- `docs/ROADMAP.md`
