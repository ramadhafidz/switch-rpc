# AGENTS.md

## Project Overview

SWITCH RPC is a native .NET 10 application that provides Discord Rich Presence for Nintendo Switch games running through emulators.

The application is designed to:

- Detect the Eden emulator process.
- Identify the currently running Pokémon game from the Eden window.
- Locate the corresponding local save file through the configured title ID.
- Read supported Pokémon save data directly through PKHeX.Core.
- Convert save data into a common application state (GameState).
- Display relevant information through Discord Rich Presence.
- Clear the Rich Presence when the game closes.

The project currently uses:

- .NET 10 (C#) for the entire application.
- PKHeX.Core for Pokémon save parsing (local source checkout, referenced as a project).
- DiscordRichPresence for Discord Rich Presence communication.
- xUnit for automated testing.

The long-term direction is a modular Nintendo Switch gaming platform —
see `docs/ROADMAP.md`. Pokémon support through PKHeX.Core is the first
fully implemented part of that platform.

---

## Solution Architecture

Solution file: `SwitchRpc.slnx` (the .NET 10 solution format). All application code lives under `src/`, tests under `tests/`.

Runtime flow:

```text
Eden
  ↓
SwitchRpc.Emulators.Eden (EdenDetector.Poll — single process scan)
  ↓
SwitchRpc.Core (GameDefinition, loaded from config.json)
  ↓
SwitchRpc.Emulators.Eden (EdenSaveLocator — title ID → save path)
  ↓
SwitchRpc.Games.Pokemon (PokemonSaveReader facade → PKHeX.Core)
  ↓
SwitchRpc.Core (GameState)
  ↓
SwitchRpc.Core (PresenceFormatter)
  ↓
SwitchRpc.Discord (PresenceClient)
```

### Projects

| Project | Responsibility |
|---|---|
| `SwitchRpc.Core` | Normalized `GameState`, `GameDefinition`, presence formatting — no dependencies |
| `SwitchRpc.Games.Pokemon` | PKHeX-based save readers behind the `PokemonSaveReader` facade |
| `SwitchRpc.Emulators.Eden` | Eden process/window detection and save location |
| `SwitchRpc.Discord` | Discord Rich Presence wrapper |
| `SwitchRpc.App` | Console host, monitoring loop, configuration, diagnostics |

### Dependency Rules (enforced physically)

1. `SwitchRpc.Core` references nothing — PKHeX, Discord, and Eden types cannot leak into the normalized state.
2. Only `SwitchRpc.Games.Pokemon` touches PKHeX.Core.
3. Only `SwitchRpc.Discord` touches the Discord library.
4. Game definitions come from `config.json` at runtime; display names, regions, title IDs, and artwork are configuration, not code.

Keep these responsibilities separated.

---

## General Rules

### 1. Preserve the Architecture

Do not unnecessarily merge projects or move responsibilities between them.

Before introducing a new abstraction, check whether an existing project or type already provides the required responsibility.

### 2. Keep the Project Modular

Game-specific logic should remain isolated in `SwitchRpc.Games.Pokemon`, and emulator-specific logic in `SwitchRpc.Emulators.Eden`.

Adding a new Pokémon game should not require rewriting the core application.

### 3. Do Not Hardcode User-Specific Paths

Never introduce paths such as:

```text
C:\Users\Hafidz\...
D:\Games\Eden\...
```

into production code.

Use dynamic paths such as `Environment.GetFolderPath` and configuration where appropriate.

User-specific paths may only appear in temporary debugging scripts or documentation examples when explicitly necessary.

### 4. Keep Save Access Read-Only

The application is a save-data reader.

Never add functionality that:

- Modifies save files.
- Writes Pokémon data.
- Injects data into saves.
- Deletes save files.
- Automatically creates or rewrites emulator save data.
- Changes emulator save data.

Reading save data is allowed.

Writing save data is outside the scope of this project.

---

## Pokémon Save Data Rules

### Never Guess Save Offsets

This is one of the most important project rules.

Do not invent or estimate:

- Save offsets.
- Field locations.
- Block sizes.
- Pointer locations.
- Pokémon structure locations.
- Pokédex offsets.
- Location offsets.
- Playtime offsets.

Save structures must be based on:

1. Verified PKHeX implementations.
2. The local PKHeX source for the exact version being built.
3. Official or reliable technical documentation.

If the structure is unknown, leave the feature unimplemented rather than guessing.

### Prefer PKHeX Implementations

When PKHeX.Core already exposes the required data, use the existing PKHeX API instead of manually parsing the underlying bytes.

Prefer:

```text
save.MyStatus
save.Played
save.Zukan
save.Blocks.TryGetBlock(...)
```

over manually calculating offsets.

### Preserve Read-Only Behavior

Do not call APIs that modify save data.

Do not introduce write operations into the readers.

---

## PKHeX Rules

PKHeX is a third-party dependency used through a local source checkout.

Expected local structure:

```text
bridge/
└── PKHeX/          # untracked local checkout
```

`SwitchRpc.Games.Pokemon` references `bridge/PKHeX/PKHeX.Core/PKHeX.Core.csproj` through a ProjectReference. The checkout is intentionally ignored by Git.

The checkout must match the pinned upstream revision (`kwsch/PKHeX` @ `8ad201e80244f630ab5a46922ab72fb79c5ad4f4`) — CI checks out the same revision automatically. Clone command: `docs/DEVELOPMENT.md`, section "PKHeX Local Setup".

Do not:

- Commit PKHeX source.
- Copy PKHeX source into another project directory.
- Modify PKHeX source to implement project-specific features.
- Vendor PKHeX into this repository.

When working with PKHeX:

1. Check the local PKHeX source first when available.
2. Verify APIs against the actual local version.
3. Prefer stable public APIs and existing abstractions.
4. Never fabricate APIs, offsets, block layouts, or field locations.

### Known Identification Quirk

`SaveUtil.GetSaveFile` identifies real save files partly through on-disk size fingerprints (the gen9 size ranges accept "tons of optional blocks"). A blank in-memory save (`new SAV9SV()`, `new SAV8LA()`) therefore cannot round-trip through `GetSaveFile` when written to disk.

Consequences for testing:

- Reader tests use in-memory `SaveFile` objects directly.
- File-based identification is covered by garbage-file tests and real-save integration runs.

---

## Save Reader Rules (src/SwitchRpc.Games.Pokemon)

The save readers:

1. Implement `ISaveStateReader` (`CanRead` + `Read`).
2. Keep every PKHeX interaction inside this project, behind the `PokemonSaveReader` facade.
3. Return a normalized `GameState`; never leak PKHeX types across the boundary.
4. Report unsupported formats through `SaveReadResult` (`Supported = false`, with the identified save type name) so the host can degrade to identity-only presence.
5. Stay read-only.

The facade should not:

- Modify the input save.
- Print non-diagnostic output.
- Depend on the application host.
- Contain Discord RPC logic.

---

## GameState

`GameState` (in `SwitchRpc.Core`) is the normalized representation consumed by the presentation layer.

Current shape:

```csharp
public sealed record GameState(
	string GameId,
	long? PlaytimeSeconds,
	string? LocationName,
	int? LocationId,
	IReadOnlyDictionary<string, DexStats> Pokedex
);
```

The Discord layer must consume application state rather than PKHeX objects.

---

## Game Support

Detection support and save-reader support are independent.

Currently verified save-reader support:

- Pokémon Legends: Arceus.
- Pokémon Scarlet.

Configured but not fully verified:

- Pokémon Violet.
- Pokémon Legends: Z-A.

When adding a game:

1. Add its game definition to `config.json` (including `title_id`).
2. Verify detection through the Eden window title.
3. Implement a verified save reader if the format is supported.
4. Test the reader independently.
5. Connect the resulting data to `GameState`.
6. Connect the resulting state to Discord RPC.
7. Update documentation.

Never mark a game as fully supported merely because it is detectable.

---

## Game IDs

Use the existing internal game ID convention:

```text
pokemon_legends_arceus
pokemon_scarlet
pokemon_violet
pokemon_legends_za
```

Do not rename existing IDs without a clear migration reason.

Game IDs should be stable and machine-oriented. Display names belong in `config.json`.

---

## Configuration

Game configuration and Discord settings belong in `config.json`.

Do not hardcode:

- Discord Application IDs.
- Artwork names.
- Game display names.
- Regions.
- Title IDs.
- User-configurable update intervals.

Never commit secrets, authentication tokens, private keys, or credentials.

A Discord Application ID is not a secret, but use a placeholder in documentation examples.

---

## Discord RPC Rules

Discord Rich Presence is handled by:

```text
src/SwitchRpc.Discord/PresenceClient.cs
```

Keep Discord-specific behavior inside this wrapper.

The wrapper handles:

- Connecting to Discord.
- Tracking connection state through the client lifecycle events (`OnReady`, `OnClose`, `OnConnectionFailed`, `OnError`).
- Updating the Rich Presence.
- Clearing the presence.
- Disposing the client.
- Reinitializing on reconnection (the library keeps its initialized flag after the pipe dies; call `Deinitialize()` before `Initialize()` again).

The session timer is intentionally retained in RPC state and preserved across reconnections.

When the detected game changes:

1. Clear the previous presence when necessary.
2. Load the new game definition.
3. Build the new state.
4. Update Discord.

When the game closes:

```text
Game detected
      ↓
Eden/game no longer running
      ↓
Clear RPC
```

Do not leave stale Rich Presence active after the game closes.

---

## Error Handling

The application should fail gracefully.

Do not crash the entire application because:

- Eden is not running.
- Discord is unavailable.
- A save file cannot be found.
- A save format is unsupported.
- PKHeX cannot identify a save.
- Discord RPC disconnects.

Prefer:

```text
Log error
↓
Return null / failure state
↓
Continue monitoring
```

The monitoring loop catches per-tick exceptions and keeps polling; programming errors will resurface every tick instead of being hidden.

Use exceptions for genuinely unexpected failures.

---

## Logging

Keep console output useful and concise.

Good:

```text
Eden: running
Game detected: Pokémon Scarlet
Save data refreshed.
Rich Presence updated: Pokédex | Paldea: 22/400
```

For errors, provide enough information to diagnose the issue.

Avoid logging:

- Save file contents.
- Sensitive information.
- Large binary dumps.
- Credentials.

---

## .NET Tooling

```powershell
dotnet build SwitchRpc.slnx
dotnet test SwitchRpc.slnx
dotnet run --project src/SwitchRpc.App
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
dotnet publish src/SwitchRpc.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

The solution uses the `.slnx` format (the .NET 10 default). Tests use xUnit and live in `tests/SwitchRpc.Tests`.

CI (`.github/workflows/ci.yml`) builds and tests every push to `main` and every pull request; it checks out the pinned PKHeX revision itself.

The `--diagnose` mode runs the whole pipeline once without Eden or Discord and prints pipeline metrics for `docs/BENCHMARKS.md`.

Do not introduce additional analyzers, test frameworks, or build tools unless there is a concrete reason to change the tooling strategy.

---

## RTK (Command Wrapper)

`rtk` (installed locally) wraps shell commands to compress their output — typically 60–90% fewer tokens on common operations. The project's RTK config is `.rtk/filters.toml`.

### Golden Rule

**Prefix shell commands with `rtk`.** Commands with a dedicated RTK filter get compressed output; everything else passes through unchanged — so prefixing is always safe. Inside `&&` chains, prefix each command:

```text
rtk git add . && rtk git commit -m "msg" && rtk git push
```

### Most useful in this repository

```text
rtk git status              # compact status
rtk git log / diff / show   # compact git output (59–80%)
rtk gh run list             # compact CI run list (82%)
rtk gh pr view / checks     # compact PR output
rtk err <cmd>               # errors only, from any command
rtk summary <cmd>           # smart summary of any command output
rtk gain                    # token-savings statistics
rtk proxy <cmd>             # bypass filtering (debugging RTK itself)
```

There is no `dotnet`-specific filter (as of RTK 0.42): `rtk dotnet ...` passes through unchanged, which is fine.

The full generic cheat sheet (other ecosystems) was removed from `CLAUDE.md` on purpose; it is recoverable with `git show 751f888:CLAUDE.md`. Note that running `rtk init` re-creates that cheat sheet in `CLAUDE.md` — this repo intentionally keeps RTK guidance here instead, with `CLAUDE.md` as a pointer to `AGENTS.md`.

---

## C# Code Style

Use:

- PascalCase for types and members, camelCase for locals and parameters.
- Tabs for indentation (matching the existing code).
- Records for immutable data such as `GameState` and `GameDefinition`.
- Modern C# features when appropriate (collection expressions, pattern matching).
- Explicit types when they improve readability.
- Small focused classes.

Do not replace the project's indentation style.

---

## Dependencies

Avoid adding dependencies without a clear reason.

Before adding a dependency:

1. Check whether the base class library can solve the problem.
2. Check whether an existing dependency already provides the functionality.
3. Consider maintenance and compatibility.
4. Update the relevant project file.
5. Update documentation if the dependency affects setup.

Do not introduce large frameworks for small problems.

---

## File Organization

Keep the existing structure unless there is a strong reason to change it:

```text
switch-rpc/
├── SwitchRpc.slnx
├── config.json
├── src/
│   ├── SwitchRpc.App/
│   ├── SwitchRpc.Core/
│   ├── SwitchRpc.Discord/
│   ├── SwitchRpc.Emulators.Eden/
│   └── SwitchRpc.Games.Pokemon/
├── tests/
│   └── SwitchRpc.Tests/
├── assets/
├── bridge/
│   └── PKHeX/            # untracked local checkout
└── docs/
```

Do not create random utility directories.

If a new project has a clear responsibility, place it in the appropriate existing layer.

---

## Git Rules

Never commit:

```text
bin/
obj/
bridge/PKHeX/
.env
config.local.json
*.sav
*.bin
```

Do not commit:

- Emulator save files.
- Local emulator data.
- Personal configuration.
- Secrets.
- PKHeX source.
- Build artifacts.

Before committing, check:

```powershell
git status
git status --ignored
```

---

## Documentation Rules

When behavior changes, update the relevant documentation.

Important documentation files:

```text
README.md
AGENTS.md
CHANGELOG.md

docs/
├── ARCHITECTURE.md
├── BENCHMARKS.md
├── DEVELOPMENT.md
├── CONFIGURATION.md
├── SAVE-READER.md
├── DISCORD-RPC.md
├── GAME-SUPPORT.md
├── TROUBLESHOOTING.md
├── PKHeX.md
└── ROADMAP.md
```

Do not document planned functionality as if it already exists.

Clearly distinguish:

```text
Implemented
In Progress
Planned
Unsupported
```

---

## Adding a New Pokémon Game

When adding a new game:

1. **Game Definition** — add the game to `config.json` (with `title_id`).
2. **Detection** — verify the Eden window title matches the configured name.
3. **Save Path** — verify the save is located through the title ID.
4. **Save Reader** — implement only after format verification.
5. **Tests** — add or update tests.
6. **GameState** — map data into the common state model.
7. **Discord RPC** — add appropriate behavior.
8. **Documentation** — update README, roadmap, and relevant docs.

---

## What Not To Do

Do not:

- Guess Pokémon save offsets.
- Modify save files.
- Commit emulator saves.
- Commit PKHeX source.
- Hardcode personal filesystem paths.
- Put Discord RPC logic into save readers.
- Put PKHeX-specific objects outside `SwitchRpc.Games.Pokemon`.
- Add unnecessary dependencies.
- Claim unsupported features are implemented.
- Remove existing functionality without checking its consumers.
- Rewrite working modules without a concrete reason.
- Change project architecture merely for stylistic preference.
- Silence type-checking or test failures just to hide fixable problems.

---

## Development Philosophy

Prefer:

```text
Simple
Modular
Verifiable
Read-only
Testable
Maintainable
```

over:

```text
Complex
Monolithic
Guess-based
Write-capable
Hardcoded
Over-engineered
```

When uncertain about a Pokémon save structure, do not guess.

When uncertain about an architectural change, inspect the existing implementation and its consumers before changing it.

When a feature cannot be safely verified, leave it unimplemented and document the limitation.

---

## Priority Order

When making implementation decisions, prioritize:

1. Correctness.
2. Save-data safety.
3. Existing architecture.
4. Maintainability.
5. Testability.
6. User experience.
7. Performance.
8. Convenience.

Never sacrifice save-data safety or correctness for convenience.

---

## Documentation and External APIs

Use current, authoritative documentation when working with external libraries, SDKs, APIs, or developer tools.

For PKHeX specifically:

1. Check the local PKHeX source first when it is available.
2. Verify the relevant API against the actual local version.
3. Prefer existing PKHeX abstractions over manual binary parsing.
4. Verify behavior against the actual game/save version.
5. Test with a valid save.
6. Never invent APIs, offsets, block layouts, or field locations.

If external documentation and the local source disagree, treat the actual local source version as authoritative for the code being built and investigate the discrepancy before proceeding.

Never assume that a class, method, property, parameter, or API exists.

If an API cannot be verified, treat it as an investigation task.

---

## Final Checklist

Before considering a change complete:

- [ ] Existing functionality still works.
- [ ] `dotnet build SwitchRpc.slnx` passes.
- [ ] `dotnet test SwitchRpc.slnx` passes.
- [ ] No user-specific paths were introduced.
- [ ] No save files were added to Git.
- [ ] No PKHeX source was added to Git.
- [ ] No secrets were added.
- [ ] Save access remains read-only.
- [ ] Save structures are based on verified information.
- [ ] Documentation reflects the actual implementation.
- [ ] New functionality is placed in the appropriate project.
