# DEVELOPMENT.md

Development guide for **SWITCH RPC**.

This document describes the development workflow, code structure, how to run components, testing, debugging, and practical rules when adding or changing features.

---

## 1. Purpose of This Document

Use this document as a guide when:

- setting up the development environment;
- running the application locally;
- developing the detector, save readers, or Discord RPC;
- adding support for a new game;
- running tests and debugging;
- updating dependencies;
- reviewing changes before a commit.

For rules that AI coding agents must follow, see `AGENTS.md`.

Architecture documentation is available in:

- `docs/ARCHITECTURE.md`
- `docs/SAVE-READER.md`
- `docs/GAME-SUPPORT.md`
- `docs/CONFIGURATION.md`
- `docs/BENCHMARKS.md`

---

## 2. Prerequisites

The project's primary environment is currently:

- Windows 11
- .NET SDK 10+
- Discord Desktop
- Nintendo Switch Eden emulator
- Git

Components that require a Windows environment:

- Eden process detection;
- window title detection;
- Discord IPC;
- runtime testing with a running game.

---

## 3. Repository Setup

Clone the repository:

```powershell
git clone <repository-url>
cd switch-rpc
```

---

## 4. PKHeX Local Setup

The PKHeX source is used as a local dependency and is referenced directly by the `SwitchRpc.Games.Pokemon` project.

Expected structure:

```text
third_party/
└── PKHeX/
    └── PKHeX.Core/
```

PKHeX **is not stored as part of the main repository** and must remain ignored by Git.

Place the PKHeX source in `third_party/PKHeX/` so the following ProjectReference resolves:

```text
src/SwitchRpc.Games.Pokemon/SwitchRpc.Games.Pokemon.csproj
  → ..\..\third_party\PKHeX\PKHeX.Core\PKHeX.Core.csproj
```

Clone PKHeX at the pinned revision — CI uses the same revision:

```powershell
git clone https://github.com/kwsch/PKHeX.git third_party/PKHeX
git -C third_party/PKHeX checkout 8ad201e80244f630ab5a46922ab72fb79c5ad4f4
```

---

## 5. Dependency Documentation

When documentation for a library, framework, SDK, or external tool is needed:

1. check the local source that is actually built;
2. official documentation;
3. other trusted technical sources.

For PKHeX, the local source code is the key reference. If external documentation differs from the version in use, prioritize the API that is actually available in the local source.

**Do not invent APIs, properties, save offsets, or data structures.**

---

## 6. Building and Running the Application

Build the entire solution:

```powershell
dotnet build SwitchRpc.slnx
```

Run the application:

```powershell
dotnet run --project src/SwitchRpc.App
```

Normal output looks roughly like:

```text
SWITCH RPC started.
Connecting to Discord...
Discord RPC connected.
Eden: running
Game detected: Pokémon Scarlet
Save data refreshed.
Rich Presence updated: Pokédex | Paldea: 22/400
```

The application runs as a polling loop and checks Eden's status periodically. The interval is configured through `config.json`.

### Diagnose Mode

Runs the whole pipeline once without requiring Eden or Discord, and prints pipeline metrics (used for `docs/BENCHMARKS.md`):

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

### Distributable Build (Single-File)

Builds a single executable for distribution (self-contained — no .NET installation required on the target machine):

```powershell
dotnet publish src/SwitchRpc.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

Output lands in `src/SwitchRpc.App/bin/Release/net10.0/win-x64/publish/SwitchRpc.App.exe`.

Before distributing, place `config.json` next to the executable (or in one of its parent folders) — the application searches the working directory and the application directory. The currently targeted platform is `win-x64` only.

---

## 7. Development Workflow

Recommended workflow:

```text
Understand
   ↓
Inspect existing code
   ↓
Verify external APIs against local source
   ↓
Implement smallest change
   ↓
Run focused test
   ↓
Run full test suite
   ↓
Review diff
   ↓
Update documentation
   ↓
Commit
```

Do not jump into a large refactor when a small change can solve the problem.

---

## 8. Project Structure

| Project | Responsibility |
|---|---|
| `src/SwitchRpc.Core` | `GameState`, `DexStats`, `GameDefinition`, presence formatting — no dependencies |
| `src/SwitchRpc.Games.Pokemon` | Pokémon (PKHeX) save readers behind the `PokemonSaveReader` facade |
| `src/SwitchRpc.Emulators.Eden` | Eden process/window detection and save location |
| `src/SwitchRpc.Discord` | Discord Rich Presence wrapper |
| `src/SwitchRpc.App` | Console host: loop, configuration, diagnostics |
| `tests/SwitchRpc.Tests` | xUnit tests |

Enforced dependency rules:

- `SwitchRpc.Core` references nothing;
- only `SwitchRpc.Games.Pokemon` touches PKHeX;
- only `SwitchRpc.Discord` touches the Discord library;
- game definitions are loaded from `config.json`.

---

## 9. C# Coding Rules

### Indentation

Use **tabs** for indentation (matching the existing code).

### General Rules

- PascalCase for types and members, camelCase for locals;
- records for immutable data (`GameState`, `GameDefinition`);
- modern C# features when appropriate (collection expressions, pattern matching);
- avoid hardcoded developer-specific paths;
- exception handling at external boundaries;
- do not silently swallow errors that matter for debugging;
- keep projects small and focused.

---

## 10. Save Reader Development

Save readers are the part most sensitive to game format changes.

Core rules:

- read-only;
- never write the save back;
- never modify the save;
- never guess offsets;
- never guess binary structures;
- use PKHeX abstractions when available;
- verify against real saves;
- document verification results.

All PKHeX interaction must stay inside `SwitchRpc.Games.Pokemon`, behind the `PokemonSaveReader` facade. The end result is a normalized `GameState` — PKHeX types must not cross out of this project.

For details, see `docs/SAVE-READER.md`.

---

## 11. Adding New Save Data

To add a new field:

### Step 1 — Look for an abstraction

Check whether PKHeX already provides a matching property, save block, accessor, helper, enum, or method.

### Step 2 — Verify the source

Make sure the API exists in the PKHeX version in use (`third_party/PKHeX/`).

### Step 3 — Add it to the reader

Add the extraction in `SvSaveReader` / `PlaSaveReader` (or another game's reader), then map it into `GameState`.

### Step 4 — Build and Test

```powershell
dotnet build SwitchRpc.slnx
dotnet test SwitchRpc.slnx
```

### Step 5 — Verify with a real save

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

---

## 12. Game Detection Development

The detector uses:

1. process detection (`Process.GetProcessesByName("eden")`);
2. the Eden window title (Win32 `EnumWindows`);
3. matching the window title against game names from `config.json`.

When adding a new game:

- add the game configuration (`name`, `title_id`, artwork);
- use a stable internal ID;
- verify with Eden actually running that game.

Example IDs:

```text
pokemon_legends_arceus
pokemon_scarlet
pokemon_violet
pokemon_legends_za
```

---

## 13. Discord RPC Development

Discord RPC must remain a separate layer.

`PresenceClient` is responsible for:

- connecting;
- updating;
- clearing;
- disposing;
- tracking connection state through the library's events (`OnReady`, `OnClose`, `OnConnectionFailed`, `OnError`);
- deinitializing and reinitializing on reconnect.

Game logic must not depend directly on Discord library types.

If the library changes:

1. check the documentation for the version in use;
2. check the source/package;
3. change the `PresenceClient` wrapper;
4. avoid spreading the library API across other projects.

---

## 14. Testing

```powershell
dotnet test SwitchRpc.slnx
```

The xUnit tests live in `tests/SwitchRpc.Tests`.

Current coverage:

- `PresenceFormatterTests` — Pokédex page formatting;
- `SvSaveReaderTests` / `PlaSaveReaderTests` — readers against blank PKHeX saves (in-memory);
- `PokemonSaveReaderTests` — file identification and results for unsupported formats;
- `EdenSaveLocatorTests` — save path resolution with an injected root.

Important note: blank PKHeX saves **cannot** be round-tripped through `SaveUtil.GetSaveFile` because gen9 identification includes on-disk file size fingerprints. Reader tests therefore use in-memory `SaveFile` objects, while file identification is covered by garbage-file tests and real-save verification.

### Integration Test

Run:

```powershell
dotnet run --project src/SwitchRpc.App
```

then:

1. open Discord;
2. start Eden;
3. start one of the games;
4. confirm the game is detected;
5. confirm the RPC appears and rotates;
6. close the game;
7. confirm the RPC is cleared.

---

## 15. Reads Without Writing Saves

Test saves must always remain read-only.

Use a save copy if an experiment needs extra inspection.

Never run code that:

- calls a save writer;
- saves changes;
- modifies bytes;
- changes Pokémon;
- changes the Pokédex;
- changes progress.

The project's purpose is to read game state for Rich Presence.

---

## 16. Debugging

Debug in stages.

### Eden not detected

Check:

```powershell
Get-Process eden -ErrorAction SilentlyContinue
```

### Game not detected

Make sure:

- Eden is running;
- the game is actually active;
- the window title contains the configured game name;
- the game configuration in `config.json` is correct.

### Discord not connecting

Check:

- Discord Desktop is running;
- the Client/Application ID is correct;
- Discord IPC is available.

Do not create a new RPC connection every polling cycle — the wrapper handles reconnection.

### Save not found

Check:

- the Eden save root;
- `title_id` in `config.json`;
- the game folder;
- the `main` file;
- permissions;
- whether the game has ever created a save.

### Save fails to read

Run diagnostics:

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

Split the problem into:

```text
Path
 ↓
File
 ↓
PKHeX identification
 ↓
Save parsing
 ↓
GameState
```

---

## 17. Dependency Changes

Before changing a dependency:

1. check the current version;
2. check the documentation;
3. check breaking changes;
4. check compatibility;
5. make a small change;
6. run the tests;
7. update documentation if needed.

Do not upgrade every dependency at once without a reason.

Use `dotnet --info` to help reproduce the environment.

---

## 18. Git Workflow

Before committing:

```powershell
git status
```

Review changes:

```powershell
git diff
git diff --cached
git diff --cached --name-only
```

Do not commit:

- `bin/`, `obj/`;
- save files;
- emulator data;
- `third_party/PKHeX/`;
- secrets;
- local configuration;
- caches.

Use `.gitignore` as extra protection, but still review `git status`.

---

## 19. Commits

Write commits that explain the change.

Example:

```text
feat(save-reader): support Legends Z-A saves
```

Commit types that may be used:

```text
feat:
fix:
refactor:
docs:
test:
chore:
```

Avoid commits like:

```text
update
fix
changes
test
asdf
```

---

## 20. Adding a New Game

Workflow:

```text
1. Decide the stable game ID
        ↓
2. Add the game configuration (name, title_id, artwork)
        ↓
3. Verify window title detection
        ↓
4. Verify the save location through the title ID
        ↓
5. Verify the save format
        ↓
6. Implement the save reader (if PKHeX supports the format)
        ↓
7. Map into GameState
        ↓
8. Test the save
        ↓
9. Test Eden + Discord RPC
        ↓
10. Update documentation
```

Never declare a game "supported" merely because the detector recognizes its name.

Detection support and save-reader support are two different things.

---

## 21. Performance

Polling must stay lightweight.

Principles:

- do not read the save every second;
- do not spawn processes without need;
- do not reconnect to Discord continuously;
- use configured intervals;
- avoid excessive filesystem scans;
- one process scan per tick (`EdenAdapter.Poll()`).

If the save reader takes a long time, do not run it more often than needed to keep the Rich Presence up to date.

---

## 22. Privacy and Security

The project reads local data from the emulator.

Do not log or expose sensitive data without need.

Avoid putting into the repository:

- save files;
- trainer IDs unless required;
- user-specific absolute paths;
- Discord credentials;
- API keys;
- tokens;
- environment secrets.

If an example needs sensitive data, use a placeholder.

---

## 23. Pre-Pull Request Checklist

### Code

- [ ] Changes match the project's responsibility.
- [ ] No hardcoded user paths.
- [ ] No invented APIs.
- [ ] Error handling is sufficient.
- [ ] No save modification.

### Save Reader

- [ ] Save format verified.
- [ ] PKHeX APIs verified.
- [ ] No guessed offsets.
- [ ] Save access remains read-only.
- [ ] PKHeX types stay inside `SwitchRpc.Games.Pokemon`.

### Runtime

- [ ] Eden detection works.
- [ ] Game detection works.
- [ ] Discord RPC works.
- [ ] RPC is cleared when the game stops.

### Git

- [ ] `git status` is clean of local files that should not be committed.
- [ ] No save files.
- [ ] No PKHeX source.
- [ ] No build output.
- [ ] No secrets.

### Documentation

- [ ] README is still accurate.
- [ ] Related documentation updated.
- [ ] Game support status matches the actual implementation.

---

## 24. Development Principles

Priority order when making changes:

1. **Correctness**
2. **Read-only safety**
3. **Maintainability**
4. **Modularity**
5. **Performance**
6. **User experience**

For save parsing, correctness matters more than implementation speed.

A field not being available yet is better than displaying an unverified number.

---

## 25. Related Documentation

- `README.md`
- `AGENTS.md`
- `docs/ARCHITECTURE.md`
- `docs/CONFIGURATION.md`
- `docs/BENCHMARKS.md`
- `docs/SAVE-READER.md`
- `docs/GAME-SUPPORT.md`
- `docs/DISCORD-RPC.md`
- `docs/TROUBLESHOOTING.md`
- `docs/ROADMAP.md`
