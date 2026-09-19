# Save Reader

## Overview

The Save Reader subsystem is responsible for locating supported Pokémon save files and extracting game data from them.

SWITCH RPC reads Pokémon save files directly through **PKHeX.Core** inside `SwitchRpc.Games.Pokemon`. There is no subprocess or JSON boundary — the save data flows in memory into the normalized `GameState`.

The overall flow is:

```text
Eden Save Directory
        │
        ▼
EdenSaveLocator (SwitchRpc.Emulators.Eden)
        │
        │ save path
        ▼
PokemonSaveReader facade (SwitchRpc.Games.Pokemon)
        │
        ▼
PKHeX.Core
        │
        ▼
Game-specific save reader
        │
        ▼
GameState (SwitchRpc.Core)
        │
        ▼
Discord RPC / other consumers
```

The save reader is designed to be:

- Read-only
- Local
- Modular
- Verifiable
- Independent from Discord RPC

---

## Goals

The Save Reader subsystem should:

1. Locate the correct save file for the detected game.
2. Load the save through a verified save implementation.
3. Extract useful game information.
4. Return a normalized `GameState`.
5. Fail gracefully when the save cannot be read.
6. Avoid modifying the original save file.

The subsystem should **not** modify save data or act as a save editor.

---

## Components

The current save-reading system consists of:

```text
src/
├── SwitchRpc.Emulators.Eden/
│   └── EdenSaveLocator.cs
└── SwitchRpc.Games.Pokemon/
    ├── ISaveStateReader.cs
    ├── PokemonSaveReader.cs
    ├── SvSaveReader.cs
    └── PlaSaveReader.cs
```

### `EdenSaveLocator`

Responsible for:

- Finding Eden's save root.
- Locating the game's save directory through its configured title ID.
- Resolving the `main` save file.

It does not parse save contents.

### `ISaveStateReader`

The contract implemented by every game-specific reader:

```csharp
bool CanRead(SaveFile save);
GameState Read(SaveFile save, string gameId);
```

### `PokemonSaveReader`

The facade over the game-specific readers.

It:

1. Receives a save file path.
2. Uses PKHeX to identify the save format.
3. Dispatches to the appropriate game-specific reader.
4. Returns a normalized `SaveReadResult` — a `GameState`, the identified save type name, and whether the format is supported.

Callers never touch PKHeX types directly; only this project does.

---

## Eden Save Location

Eden stores game save data under its local user save directory.

The locator uses:

```csharp
Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
```

as the basis for locating the Windows user profile.

The current save root is:

```text
%USERPROFILE%\AppData\Roaming\eden\nand\user\save\
```

The locator then searches for the configured game title ID.

Conceptually:

```text
save/
└── 0000000000000000/
    └── <title-id>/
        ├── main
        └── other files
```

The project currently reads the `main` file.

### Important

Do not hardcode a user-specific path such as:

```text
C:\Users\<username>\...
```

Use `Environment.GetFolderPath` or another configurable mechanism.

---

## Game Title IDs

Title IDs are configured per game in `config.json`:

```text
pokemon_legends_arceus → 01001F5010DFA000
pokemon_scarlet        → 0100A3D008C5C000
pokemon_violet         → 01008F6008C5E000
pokemon_legends_za     → 0100F43008C44000
```

A title ID should only be added after it has been verified.

Do not guess a title ID from another release, region, or game.

---

## Save File Selection

For supported games, the locator looks for:

```text
<title-id>/main
```

The locator returns `null` when:

- The save root does not exist.
- The game directory cannot be found.
- `main` does not exist.

This allows the application to continue monitoring instead of crashing.

---

## PKHeX Integration

PKHeX.Core provides the save-format implementations used by the readers.

The facade uses PKHeX's save detection:

```csharp
SaveFile? save = SaveUtil.GetSaveFile(savePath);
```

The detected save type is then dispatched to the appropriate reader.

Current supported save classes are:

```text
SAV8LA
└── Pokémon Legends: Arceus

SAV9SV
└── Pokémon Scarlet / Violet
```

The readers should prefer existing PKHeX abstractions over manual binary parsing.

For example, if PKHeX exposes a property for playtime, Pokédex data, or another field, use that API instead of manually calculating an offset.

### Known Identification Quirk

`SaveUtil.GetSaveFile` identifies real save files partly through on-disk size fingerprints (the gen9 size ranges accept "tons of optional blocks"). A blank in-memory save (`new SAV9SV()`, `new SAV8LA()`) therefore cannot round-trip through `GetSaveFile` when written to disk.

Consequences:

- Reader tests use in-memory `SaveFile` objects directly.
- File-based identification is covered by garbage-file tests and real-save integration runs (`--diagnose`).

---

## Supported Save Data

### Pokémon Legends: Arceus

The current reader extracts:

```text
Playtime
Hisui Pokédex (seen counted as caught, mirroring the verified bridge implementation)
```

The PLA Pokédex is read through PKHeX's `PokedexSave8a` implementation:

```csharp
save.Blocks.PokedexSave.GetDexGetCount(PokedexType8a.Hisui)
PokedexSave8a.GetDexTotalCount(PokedexType8a.Hisui)
```

### Pokémon Scarlet / Violet

The current reader extracts:

```text
Playtime
Location (location ID, human-readable name)
Pokédex per dex group (Paldea, Kitakami, Blueberry): seen / caught / total
```

The SV Pokédex is accessed through:

```csharp
save.Zukan
```

with per-group seen/caught checks through:

```csharp
save.Zukan.DexPaldea.GetSeen(species)
save.Zukan.DexPaldea.GetCaught(species)
```

Dex group membership comes from the personal table entries:

```csharp
save.Personal.GetFormEntry(species, form)
```

(`DexPaldea`, `DexKitakami`, `DexBlueberry`).

Current totals: Paldea 400, Kitakami 200, Blueberry 243.

The location is read from the save block storage:

```csharp
save.Blocks.TryGetBlock(key, out var block)
```

and the human-readable location name is resolved through PKHeX's game
string/location data rather than a project-specific hardcoded location
dictionary.

---

## Save Type Detection

The facade uses:

```csharp
SaveUtil.GetSaveFile(savePath)
```

rather than identifying a save solely by:

- File size.
- Filename.
- Arbitrary byte patterns.
- User-provided assumptions.

This is important because save files may change between game versions and revisions.

The detected `SaveFile` type determines which reader is used:

```text
SaveFile
   │
   ├── SAV8LA
   │     └── PlaSaveReader
   │
   └── SAV9SV
         └── SvSaveReader
```

Save types without a reader produce a `SaveReadResult` with
`Supported = false`, and the application degrades to identity-only
presence.

---

## Read-Only Policy

The Save Reader must remain strictly read-only.

It may:

- Open save files.
- Read save blocks.
- Extract values.
- Parse Pokémon structures.
- Return normalized state.

It must not:

- Modify save files.
- Save changes back to disk.
- Write Pokémon data.
- Change trainer information.
- Change Pokédex data.
- Modify party or boxes.
- Delete saves.
- Upload saves to external services.

The input save should be treated as immutable application input.

---

## Save Structure Research

Save formats are version-sensitive and should not be reverse-engineered through guesswork.

Before implementing a new field:

### 1. Inspect the Local PKHeX Source

The local PKHeX source is especially important because it represents the exact PKHeX version being referenced by the project.

Look for:

- Save classes.
- Save block accessors.
- Data structures.
- Properties exposing the required value.
- Existing helper methods.

### 2. Check Official Sources

Use official documentation or official source repositories when additional verification is required.

### 3. Verify Before Implementing

Only implement a field when its structure or API can be verified.

If the required information cannot be verified:

```text
Do not guess.
Do not invent an offset.
Do not invent an API.
Leave the feature unimplemented.
```

---

## Adding a New Save Reader

When adding support for a new Pokémon game:

### Step 1 — Verify the Save Format

Determine the correct PKHeX save class or verified save implementation.

### Step 2 — Add Save Resolution

Add the game's `title_id` to `config.json`.

### Step 3 — Add the Reader

Implement `ISaveStateReader` in `SwitchRpc.Games.Pokemon` and register it in the `PokemonSaveReader` facade.

### Step 4 — Extract Verified Fields

Start with a small set of reliable fields:

```text
Playtime
Pokédex
Location
```

Only add fields when their source is verified.

### Step 5 — Map to GameState

Convert the relevant data into the normalized `GameState`.

### Step 6 — Test

Add xUnit tests (in-memory `SaveFile` objects work for reader tests), then verify with a real save through `--diagnose`.

### Step 7 — Document

Update:

- `README.md`
- `docs/GAME-SUPPORT.md`
- `docs/ROADMAP.md`
- This document when necessary

---

## Testing

The save reader is tested at multiple levels.

### Unit tests (xUnit)

```powershell
dotnet test SwitchRpc.slnx
```

Covers the presence formatter, the game readers against blank PKHeX saves (in memory), the facade behavior for unidentifiable and unsupported files, and the save locator.

### Diagnose (real saves)

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

This exercises the full pipeline — save location, PKHeX identification, and parsing — against real local saves and prints the timings.

---

## Troubleshooting

### Save Not Found

Check:

1. Eden has created the save directory.
2. The configured `title_id` is correct.
3. The game directory exists.
4. The `main` file exists.
5. The current user profile is being resolved correctly.

### PKHeX Cannot Identify the Save

Check:

1. The save is a supported format.
2. The save file is not corrupted.
3. The local PKHeX version supports the format.
4. `SwitchRpc.Games.Pokemon` references the intended PKHeX.Core project.

### Build Fails

Check:

```powershell
dotnet --info
```

and:

```powershell
dotnet build SwitchRpc.slnx
```

If NuGet source configuration causes a restore failure, inspect the configured package sources and use `--ignore-failed-sources` only when appropriate.

---

## Security and Privacy

Save data is processed locally.

The application should not:

- Upload save files.
- Send save contents to third-party APIs.
- Store unnecessary copies of save data.
- Log complete save contents.

Only the extracted information required by the application should be used.

---

## Design Principles

### Read-Only

The save file is an immutable input.

### Verified

Every save field should have a verifiable source.

### Modular

Game-specific save implementations remain isolated.

### Local

Save processing happens locally.

### Normalized

PKHeX types stop at the `SwitchRpc.Games.Pokemon` boundary; consumers see `GameState`.

### Maintainable

Prefer stable PKHeX abstractions over custom binary parsing.

### Fail Gracefully

An unreadable save should not crash the monitoring application.

---

## Related Documentation

- [Architecture](ARCHITECTURE.md)
- [Game Support](GAME-SUPPORT.md)
- [Development Setup](DEVELOPMENT.md)
- [Configuration](CONFIGURATION.md)
- [Discord RPC](DISCORD-RPC.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Roadmap](ROADMAP.md)
