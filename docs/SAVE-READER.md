# Save Reader

## Overview

The Save Reader subsystem is responsible for locating supported Pokémon save files and extracting game data from them.

SWITCH RPC does not parse Pokémon save files directly in Python. Instead, the project uses a small C#/.NET bridge that relies on **PKHeX.Core** for save-format handling.

The overall flow is:

```text
Eden Save Directory
        │
        ▼
EdenSavePathResolver
        │
        │ save path
        ▼
Python SaveReader
        │
        │ subprocess
        ▼
PokemonSaveReader
        │
        ▼
PKHeX.Core
        │
        ▼
Game-specific save implementation
        │
        ▼
JSON
        │
        ▼
Python application
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
4. Return structured data to Python.
5. Fail gracefully when the save cannot be read.
6. Avoid modifying the original save file.

The subsystem should **not** modify save data or act as a save editor.

---

## Components

The current save-reading system consists of:

```text
games/
├── save_paths.py
├── save_reader.py
└── game_save_reader.py

bridge/
└── PokemonSaveReader/
    ├── PokemonSaveReader.csproj
    └── Program.cs
```

### `games/save_paths.py`

Contains `EdenSavePathResolver`.

Responsible for:

- Finding Eden's save root.
- Mapping internal game IDs to known title IDs.
- Locating the game's save directory.
- Resolving the `main` save file.

It does not parse save contents.

---

### `games/save_reader.py`

Contains `SaveReader`.

Responsible for:

- Receiving a save path.
- Executing the .NET bridge.
- Passing the save path to the bridge.
- Capturing stdout and stderr.
- Handling process failures.
- Parsing JSON output.

It does not know the internal structure of Pokémon save formats.

---

### `games/game_save_reader.py`

Contains `GameSaveReader`.

This class coordinates the path resolver and generic save reader:

```text
Game ID
   │
   ▼
EdenSavePathResolver
   │
   ▼
Save Path
   │
   ▼
SaveReader
   │
   ▼
Save Data
```

This provides the Python application with a simple game-oriented interface.

---

### `bridge/PokemonSaveReader/`

The C# bridge is responsible for interacting with PKHeX.Core.

It:

1. Receives a save file path.
2. Checks that the file exists.
3. Uses PKHeX to identify the save format.
4. Dispatches to the appropriate game-specific reader.
5. Extracts verified data.
6. Serializes the result as JSON.

---

## Eden Save Location

Eden stores game save data under its local user save directory.

The Python resolver uses:

```python
Path.home()
```

as the basis for locating the Windows user profile.

The current save root is:

```text
%USERPROFILE%\AppData\Roaming\eden\nand\user\save\0000000000000000\
```

The resolver then searches for the known game title ID.

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

Use `Path.home()` or another configurable mechanism.

---

## Game Title IDs

The resolver currently contains known title IDs for supported save formats.

Current entries include:

```text
pokemon_legends_arceus → 01001F5010DFA000
pokemon_scarlet        → 0100A3D008C5C000
pokemon_violet         → not implemented
pokemon_legends_za     → not implemented
```

A title ID should only be added after it has been verified.

Do not guess a title ID from another release, region, or game.

---

## Save File Selection

For supported games, the resolver looks for:

```text
<title-id>/main
```

The resolver returns `None` when:

- The game ID is unknown.
- No title ID is configured.
- The game directory cannot be found.
- `main` does not exist.

This allows the application to continue monitoring instead of crashing.

---

## PKHeX Integration

PKHeX.Core provides the save-format implementations used by the bridge.

The bridge uses PKHeX's save detection:

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

The bridge should prefer existing PKHeX abstractions over manual binary parsing.

For example, if PKHeX exposes a property for playtime, Pokédex data, trainer information, or another field, use that API instead of manually calculating an offset.

---

## Supported Save Data

### Pokémon Legends: Arceus

The current bridge extracts:

```text
Game version
Generation
Trainer name
Trainer ID
Playtime
Pokédex seen count
Pokédex total
```

The PLA Pokédex is read through PKHeX's `PokedexSave8a` implementation.

The current implementation uses the Hisui Pokédex:

```csharp
save.PokedexSave.GetDexGetCount(PokedexType8a.Hisui)
```

and:

```csharp
PokedexSave8a.GetDexTotalCount(PokedexType8a.Hisui)
```

---

### Pokémon Scarlet / Violet

The current bridge extracts:

```text
Game version
Generation
Trainer name
Trainer ID
Playtime
Pokédex seen count
Pokédex caught count
Pokédex total
```

The SV Pokédex is accessed through:

```csharp
save.Zukan
```

The current implementation uses:

```csharp
save.Zukan.SeenCount
save.Zukan.CaughtCount
```

The exact definition of the total count should remain aligned with the actual PKHeX implementation rather than being treated as a universal Pokédex size.

---

## JSON Boundary

The C# bridge communicates with Python using JSON over stdout.

A successful response has the general shape:

```json
{
	"success": true,
	"game": {
		"version": "SL",
		"generation": 9,
		"type": "scarlet_violet"
	},
	"trainer": {
		"name": "Trainer",
		"id": 123456789
	},
	"playtime": {
		"hours": 10,
		"minutes": 51,
		"seconds": 28
	},
	"pokedex": {
		"seen": 41,
		"caught": 25,
		"total": 1025
	}
}
```

The exact fields can evolve as more save data is supported.

### stdout

Successful JSON output should be written to stdout.

Do not print debugging messages to stdout during a successful machine-readable response.

### stderr

Errors and diagnostic messages should be written to stderr when appropriate.

This keeps stdout safe for JSON parsing.

---

## Python Save Reader

The Python `SaveReader` invokes the bridge as a subprocess.

Conceptually:

```text
SaveReader
    │
    │ dotnet PokemonSaveReader <save>
    ▼
PokemonSaveReader
    │
    ▼
JSON stdout
    │
    ▼
json.loads()
    │
    ▼
Python dict
```

The reader handles:

- Missing save files.
- Missing bridge executable/project.
- Process startup failures.
- Timeouts.
- Non-zero exit codes.
- Empty output.
- Invalid JSON.

A save-reader failure should not terminate the entire monitoring application.

---

## C# Bridge Execution

The bridge can be tested directly with:

```powershell
dotnet run --project bridge/PokemonSaveReader -- "<path-to-save>"
```

After building, the resulting application can also be executed directly.

Example:

```powershell
dotnet build bridge/PokemonSaveReader/PokemonSaveReader.csproj
```

The bridge accepts exactly one argument:

```text
PokemonSaveReader.exe <save-file>
```

If the argument is missing or the file does not exist, the bridge should return a non-zero exit code.

---

## Save Type Detection

The bridge currently uses:

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
   │     └── ReadArceus()
   │
   └── SAV9SV
         └── ReadScarletViolet()
```

---

## Read-Only Policy

The Save Reader must remain strictly read-only.

It may:

- Open save files.
- Read save blocks.
- Extract values.
- Parse Pokémon structures.
- Return JSON data.

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

The local PKHeX source is especially important because it represents the exact PKHeX version being referenced by the bridge.

Look for:

- Save classes.
- Save block accessors.
- Data structures.
- Properties exposing the required value.
- Existing helper methods.

### 2. Use Context7

When relevant documentation is available through Context7, use it to check current APIs and documentation for the dependency being used.

Context7 should be particularly useful for:

- .NET APIs.
- C# APIs.
- Python dependencies.
- PyPresence.
- Discord RPC.
- Other external libraries.

Always consider the actual dependency version.

### 3. Check Official Sources

Use official documentation or official source repositories when additional verification is required.

### 4. Verify Before Implementing

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

Add the game's title ID or another verified save-location mechanism to `save_paths.py`.

### Step 3 — Add the C# Reader

Add a game-specific branch to the bridge.

For example:

```csharp
return save switch
{
	SAV8LA pla => ReadArceus(pla, savePath),
	SAV9SV sv => ReadScarletViolet(sv, savePath),
	_ => throw new InvalidOperationException(
		$"Unsupported save type: {save.GetType().Name}"
	)
};
```

### Step 4 — Extract Verified Fields

Start with a small set of reliable fields:

```text
Trainer
Playtime
Pokédex
Location
Party
```

Only add fields when their source is verified.

### Step 5 — Define JSON Output

Add the fields to the JSON response without exposing PKHeX-specific objects.

### Step 6 — Map to GameState

Convert the relevant data into the common Python state model.

### Step 7 — Test

Test the reader directly with a valid save.

### Step 8 — Document

Update:

- `README.md`
- `docs/GAME-SUPPORT.md`
- `docs/ROADMAP.md`
- This document when necessary

---

## Testing

The save reader can be tested at multiple levels.

### Save Path

```powershell
python test/save_path.py
```

This verifies that the configured game can resolve its local save path.

### Full Python Save Reader

```powershell
python test/game_save_reader.py
```

This verifies:

```text
Game ID
   ↓
Save Path
   ↓
C# Bridge
   ↓
JSON
```

### C# Bridge

```powershell
dotnet run --project bridge/PokemonSaveReader -- "<path-to-save>"
```

This isolates PKHeX and C# behavior from the Python application.

---

## Troubleshooting

### Save Not Found

Check:

1. Eden has created the save directory.
2. The expected title ID is correct.
3. The game directory exists.
4. The `main` file exists.
5. The current user profile is being resolved correctly.

### PKHeX Cannot Identify the Save

Check:

1. The save is a supported format.
2. The save file is not corrupted.
3. The local PKHeX version supports the format.
4. The bridge references the intended PKHeX.Core project.

### Bridge Fails to Build

Check:

```powershell
dotnet --info
```

and:

```powershell
dotnet build bridge/PokemonSaveReader/PokemonSaveReader.csproj
```

If NuGet source configuration causes a restore failure, inspect the configured package sources and use `--ignore-failed-sources` only when appropriate.

### Invalid JSON

Check that the C# bridge does not write diagnostic output to stdout.

Debugging output should go to stderr.

---

## Security and Privacy

Save data is processed locally.

The application should not:

- Upload save files.
- Send save contents to third-party APIs.
- Store unnecessary copies of save data.
- Log complete save contents.

Only the extracted information required by the application should be passed to the Python layer.

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

### Minimal

Only the required data should cross the C# → Python boundary.

### Maintainable

Prefer stable PKHeX abstractions over custom binary parsing.

### Fail Gracefully

An unreadable save should not crash the main monitoring application.

---

## Related Documentation

- [Architecture](ARCHITECTURE.md)
- [Game Support](GAME-SUPPORT.md)
- [Development Setup](DEVELOPMENT.md)
- [Configuration](CONFIGURATION.md)
- [Discord RPC](DISCORD-RPC.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Roadmap](ROADMAP.md)
