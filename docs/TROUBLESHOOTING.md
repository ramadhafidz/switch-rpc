# TROUBLESHOOTING.md

Troubleshooting guide for **SWITCH RPC**.

This document covers common problems involving the Python application, Eden detection, save reading, the C# bridge, PKHeX, and Discord Rich Presence.

---

## 1. General Troubleshooting Workflow

When something does not work, isolate the problem by layer:

```text
Environment
    ↓
Python
    ↓
Eden Detection
    ↓
Game Detection
    ↓
Save Path
    ↓
Save Reader Bridge
    ↓
PKHeX
    ↓
GameState
    ↓
Discord RPC
```

Do not change multiple unrelated components at once.

Start with the smallest failing layer, verify it independently, then continue upward.

---

## 2. Application Does Not Start

### Symptoms

The application exits immediately or Python reports an import/module error.

### Check Python

```powershell
python --version
```

If using the virtual environment:

```powershell
.venv\Scripts\python.exe --version
```

### Check dependencies

```powershell
python -m pip install -r requirements.txt
```

Verify PyPresence:

```powershell
python -m pip show pypresence
```

Verify other dependencies:

```powershell
python -m pip show psutil
python -m pip show pywin32
```

### Check project directory

Run the application from the repository root:

```powershell
cd switch-rpc
python main.py
```

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

The detector currently checks the process name rather than the installation path.

### Important

The application does not require Eden to be installed in a particular directory.

For example, the following is an installation detail and should not be hardcoded:

```text
D:\Games\Eden
```

The detector looks for the running process.

---

## 4. Eden Is Running but the Game Is Not Detected

### Symptoms

Eden is detected, but:

```text
Game: none
```

or no game is reported.

### Check the Eden window title

The current detector uses the visible Eden window title.

The title should contain a supported Pokémon game name, for example:

```text
Pokémon Legends: Arceus
Pokémon Scarlet
Pokémon Violet
Pokémon Legends: Z-A
```

### Test the detector

Run:

```powershell
python test/game_detection.py
```

If the title is different from the expected format, inspect the detector mapping in:

```text
games/detector.py
```

Do not immediately add broad substring matching without verifying the actual Eden title.

---

## 5. Game Is Detected but Save Is Not Found

### Symptoms

The application detects the game, but save reading reports:

```text
Save not found for game: ...
```

### Check the save root

The resolver expects the Eden user save directory under:

```text
%APPDATA%\eden\nand\user\save\0000000000000000
```

On Windows this normally corresponds to:

```text
C:\Users\<user>\AppData\Roaming\eden\nand\user\save\0000000000000000
```

The application uses:

```python
Path.home()
```

instead of hardcoding a user name.

### Check the title ID

Game-specific title IDs are defined in:

```text
games/save_paths.py
```

A missing title ID means save resolution is intentionally unavailable for that game.

Do not invent a title ID.

---

## 6. Save File Exists but Is Not Read

### Symptoms

The save directory exists and contains a `main` file, but the bridge fails.

Test the bridge directly:

```powershell
dotnet run --project bridge/PokemonSaveReader -- "<save-path>"
```

This separates Python problems from C# / PKHeX problems.

If the bridge succeeds, test the Python wrapper:

```powershell
python test/game_save_reader.py
```

---

## 7. PKHeX Cannot Identify the Save

### Symptoms

The bridge reports that PKHeX could not identify the save.

Possible causes include:

- unsupported save format;
- invalid or incomplete save;
- wrong file selected;
- incompatible PKHeX version;
- damaged save;
- incorrect bridge build.

### Debugging

First verify the exact file:

```powershell
Get-Item "<save-path>"
```

Then run the bridge directly.

Do not create a custom parser as a workaround until the save format and PKHeX support have been verified.

---

## 8. Bridge Does Not Build

### Check .NET SDK

```powershell
dotnet --version
```

List installed SDKs:

```powershell
dotnet --list-sdks
```

### Restore and build

```powershell
dotnet restore bridge/PokemonSaveReader/PokemonSaveReader.csproj
dotnet build bridge/PokemonSaveReader/PokemonSaveReader.csproj
```

If a configured NuGet source is unavailable but the required packages are already available locally:

```powershell
dotnet restore bridge/PokemonSaveReader/PokemonSaveReader.csproj --ignore-failed-sources
dotnet build bridge/PokemonSaveReader/PokemonSaveReader.csproj --no-restore
```

Do not permanently ignore package sources without understanding the environment.

---

## 9. Bridge Runs but Python Cannot Start It

### Symptoms

Python reports:

```text
Save reader failed to start
```

Check that the bridge path exists.

The Python save reader expects a configured path to the bridge executable/project output.

Also verify that `dotnet` is available from the current shell:

```powershell
dotnet --version
```

If the bridge is being executed from a built DLL, verify the output exists under the project's `bin` directory.

---

## 10. Bridge Returns Invalid JSON

### Symptoms

Python reports:

```text
Invalid JSON from save reader
```

The bridge must keep stdout machine-readable.

Expected stdout:

```json
{
	"success": true
}
```

Diagnostic messages should be written to stderr.

Run the bridge directly:

```powershell
dotnet run --project bridge/PokemonSaveReader -- "<save-path>"
```

If additional text is printed before or after the JSON, fix the bridge output rather than making the Python parser strip arbitrary text.

---

## 11. Save Data Looks Incorrect

Do not assume the parser is correct just because it returns a number.

For any suspicious field:

1. identify the PKHeX API used;
2. inspect the relevant PKHeX source;
3. verify the save format;
4. compare against known in-game data;
5. test with another valid save if available;
6. document the result.

For save structure research:

> Never guess offsets or binary structures.

Prefer verified PKHeX abstractions.

---

## 12. Pokédex Count Is Unexpected

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

## 13. Discord RPC Does Not Connect

### Symptoms

```text
Failed to connect to Discord.
```

Check:

- Discord Desktop is running;
- the user is logged in;
- the configured Application ID is correct;
- PyPresence is installed;
- Discord IPC is available.

Check PyPresence:

```powershell
python -m pip show pypresence
```

The application should not require a Discord bot token for Rich Presence.

---

## 14. Discord RPC Connects but Presence Does Not Appear

Check the application log.

Expected flow:

```text
Discord RPC connected.
Game detected: Pokémon Scarlet
Rich Presence updated.
```

If the game is not detected, the RPC update may never happen.

If:

```text
Rich Presence updated.
```

appears but the artwork or display is wrong, investigate the Discord Application configuration and payload.

---

## 15. Artwork Does Not Appear

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

## 16. RPC Does Not Clear After Closing the Game

Check game state transitions.

The application should detect:

```text
Game running
    ↓
Game closes
    ↓
game_id = None
    ↓
rpc.clear()
```

If the application itself is stopped, shutdown cleanup should also execute through the `finally` block.

Use:

```text
games/detector.py
main.py
rpc/discord_rpc.py
```

to isolate the failure.

---

## 17. RPC Connection Drops

If Discord disconnects during runtime, the wrapper marks itself disconnected.

The next appropriate update can reconnect.

Expected behavior:

```text
Connected
    ↓
Connection failure
    ↓
Disconnected
    ↓
Reconnect
    ↓
Connected
```

The application should not repeatedly create connections during every polling cycle.

---

## 18. Timer Is Not What You Expect

The RPC session start timestamp is created when the Discord RPC connection is established.

Therefore:

```text
Application starts
    ↓
Discord connects
    ↓
start_time
```

does not necessarily equal:

```text
Game starts
    ↓
start_time
```

If accurate per-game session timing is required, session lifecycle should be handled at the application/game-state layer.

---

## 19. High CPU Usage

The application is designed to use periodic polling.

Check:

```json
{
	"discord": {
		"update_interval": 15
	}
}
```

Avoid unnecessarily small intervals.

Potential expensive operations include:

- filesystem scans;
- save parsing;
- launching the C# bridge;
- Discord updates.

Do not parse the entire save every second unless there is a demonstrated need.

---

## 20. Game Switch Is Not Reflected

If switching from one game to another does not update the RPC:

1. verify the first game is no longer detected;
2. verify the second game is detected;
3. check `current_game_id` handling;
4. check RPC clear/update behavior;
5. verify both games have valid registry entries.

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

## 21. Configuration Problems

If the application reports a missing configuration:

Check:

```text
config.json
```

Verify:

```json
{
	"discord": {
		"client_id": "YOUR_DISCORD_APPLICATION_ID",
		"update_interval": 15
	}
}
```

and that each supported game has a corresponding entry under:

```text
games
```

Stable internal IDs must match the detector output.

Example:

```text
pokemon_scarlet
```

must exist consistently across:

```text
detector
registry
save resolver
GameState
```

---

## 22. Python and C# Version Problems

The project contains two runtime environments:

```text
Python
  ↓
C# / .NET
```

When debugging version issues, identify which side fails first.

### Python

```powershell
python --version
```

### .NET

```powershell
dotnet --version
```

### Bridge

```powershell
dotnet build bridge/PokemonSaveReader/PokemonSaveReader.csproj
```

Do not assume a Python dependency error is related to .NET, or vice versa.

---

## 23. Context7 and Documentation Issues

When unsure about an external API:

1. check Context7;
2. check the official documentation;
3. check the installed dependency version;
4. inspect source code if necessary;
5. reproduce the behavior locally.

Do not rely on examples written for unrelated versions.

This is particularly important for:

- PyPresence;
- Discord RPC;
- Python packages;
- .NET APIs;
- PKHeX;
- Eden.

---

## 24. Git / Local Files Accidentally Appear

Check:

```powershell
git status --ignored --short
```

Expected ignored development files include:

```text
.venv/
bridge/PKHeX/
bridge/PokemonSaveReader/bin/
bridge/PokemonSaveReader/obj/
__pycache__/
```

Save files and emulator data must not be committed.

If a file was already tracked before adding it to `.gitignore`, `.gitignore` alone will not untrack it.

Review the Git history before removing or rewriting tracked files.

---

## 25. Safe Debugging Rules

Never use debugging as a reason to modify the user's save.

Safe debugging means:

- reading files;
- printing metadata;
- inspecting JSON;
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

## 26. Minimal Diagnostic Checklist

When reporting a bug, collect:

```text
OS:
Python version:
.NET SDK version:
PyPresence version:
Eden version:
Game:
Game version:
Detected process:
Detected window title:
Save reader result:
Bridge result:
Discord connection result:
Relevant error:
```

Do not include private credentials or unnecessary personal data.

---

## 27. When to Stop and Investigate

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

## 28. Related Documentation

- `README.md`
- `AGENTS.md`
- `docs/ARCHITECTURE.md`
- `docs/DEVELOPMENT.md`
- `docs/CONFIGURATION.md`
- `docs/SAVE-READER.md`
- `docs/GAME-SUPPORT.md`
- `docs/DISCORD-RPC.md`
- `docs/ROADMAP.md`
