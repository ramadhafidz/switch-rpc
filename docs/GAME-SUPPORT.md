# Game Support

## Overview

SWITCH RPC is designed so that emulator detection and save-data support are implemented independently for each game.

A game can be detected by Eden before its save reader is implemented.

This document tracks the current implementation status and the requirements for adding support for additional Pokémon games.

---

## Support Status

| Game | Internal ID | Eden Detection | Save Reader | Pokédex | Playtime | Trainer |
|---|---|:---:|:---:|:---:|:---:|:---:|
| Pokémon Legends: Arceus | `pokemon_legends_arceus` | ✅ | ✅ | ✅ | ✅ | ✅ |
| Pokémon Scarlet | `pokemon_scarlet` | ✅ | ✅ | ✅ | ✅ | ✅ |
| Pokémon Violet | `pokemon_violet` | 🚧 | 🚧 | 🚧 | 🚧 | 🚧 |
| Pokémon Legends: Z-A | `pokemon_legends_za` | 🚧 | 🚧 | 🚧 | 🚧 | 🚧 |

### Status Legend

| Symbol | Meaning |
|---|---|
| ✅ | Implemented and tested |
| 🚧 | Planned or currently being implemented |
| ❌ | Not supported |
| — | Not applicable |

---

## Pokémon Legends: Arceus

### Game Definition

```text
Internal ID: pokemon_legends_arceus
Region: Hisui
Save Type: SAV8LA
```

### Current Support

| Feature | Status |
|---|:---:|
| Eden detection | ✅ |
| Window title detection | ✅ |
| Save path resolution | ✅ |
| Save format detection | ✅ |
| Trainer information | ✅ |
| Playtime | ✅ |
| Pokédex | ✅ |
| Discord RPC | ✅ |
| Location | 🚧 |
| Party Pokémon | 🚧 |

### Save Reader

The C# bridge uses the PKHeX `SAV8LA` implementation.

The current reader extracts:

```text
Game version
Generation
Trainer name
Trainer ID
Playtime
Pokédex seen count
Pokédex total
```

The PLA Pokédex is read through the PKHeX Pokédex implementation using the Hisui dex:

```csharp
save.PokedexSave.GetDexGetCount(PokedexType8a.Hisui)
```

and:

```csharp
PokedexSave8a.GetDexTotalCount(PokedexType8a.Hisui)
```

### Known Save Location

The current resolver uses the verified Eden title ID:

```text
01001F5010DFA000
```

and resolves:

```text
<title-id>/main
```

---

## Pokémon Scarlet

### Game Definition

```text
Internal ID: pokemon_scarlet
Region: Paldea
Save Type: SAV9SV
```

### Current Support

| Feature | Status |
|---|:---:|
| Eden detection | ✅ |
| Window title detection | ✅ |
| Save path resolution | ✅ |
| Save format detection | ✅ |
| Trainer information | ✅ |
| Playtime | ✅ |
| Pokédex | ✅ |
| Discord RPC | ✅ |
| Location | ✅ |
| Party Pokémon | 🚧 |

### Save Reader

The C# bridge uses the PKHeX `SAV9SV` implementation.

The current reader extracts:

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

with:

```csharp
save.Zukan.SeenCount
save.Zukan.CaughtCount
```

### Known Save Location

The current resolver uses the verified Eden title ID:

```text
0100A3D008C5C000
```

and resolves:

```text
<title-id>/main
```

---

## Pokémon Violet

### Game Definition

```text
Internal ID: pokemon_violet
Region: Paldea
Save Type: SAV9SV
```

### Current Support

| Feature | Status |
|---|:---:|
| Eden detection | ✅ |
| Window title detection | ✅ |
| Save path resolution | 🚧 |
| Save format detection | 🚧 |
| Trainer information | 🚧 |
| Playtime | 🚧 |
| Pokédex | 🚧 |
| Discord RPC | 🚧 |
| Location | 🚧 |
| Party Pokémon | 🚧 |

### Planned Implementation

Pokémon Violet uses the same general generation of save format as Pokémon Scarlet, but support should still be verified against an actual Violet save and the exact PKHeX implementation being used.

Before enabling Violet save reading:

1. Verify the Violet title ID.
2. Verify Eden's save directory.
3. Test the save with `SaveUtil.GetSaveFile()`.
4. Confirm the detected save type.
5. Verify the required fields through PKHeX.
6. Add tests using an actual Violet save.

Do not assume that Scarlet-specific identifiers or paths can simply be copied without verification.

---

## Pokémon Legends: Z-A

### Game Definition

```text
Internal ID: pokemon_legends_za
Region: Kalos
Save Type: Not yet implemented
```

### Current Support

| Feature | Status |
|---|:---:|
| Eden detection | 🚧 |
| Window title detection | 🚧 |
| Save path resolution | 🚧 |
| Save format detection | 🚧 |
| Trainer information | 🚧 |
| Playtime | 🚧 |
| Pokédex | 🚧 |
| Discord RPC | 🚧 |
| Location | 🚧 |
| Party Pokémon | 🚧 |

### Planned Implementation

Z-A support must begin with verification of:

1. Eden game detection.
2. Title ID.
3. Save directory structure.
4. Save file name.
5. PKHeX support for the game's save format.
6. Relevant save structures.
7. Required fields.

Do not implement save parsing based on assumptions from Legends: Arceus.

Even when two games appear conceptually similar, their save formats must be independently verified.

---

## Feature Categories

Game support is tracked by individual capabilities rather than as a single supported/unsupported flag.

### Detection

Detection determines whether the application can identify the game running inside Eden.

Detection currently relies on:

```text
Eden process
     ↓
Eden window
     ↓
Window title
     ↓
Game ID
```

### Save Reader

Save Reader support means the application can locate and parse the game's save file through the C# bridge and PKHeX.Core.

### Pokédex

Pokédex support means the save reader can extract verified Pokédex information.

The exact meaning of:

```text
seen
caught
total
```

depends on the game's save implementation.

Do not assume that the Pokédex structure is identical across generations.

### Playtime

Playtime support means the save reader can extract the player's stored playtime.

The saved playtime is separate from the Discord session timer.

```text
Save Playtime
    ≠
Discord Session Time
```

The save playtime represents time stored in the save.

The Discord session timer represents the current RPC session.

### Trainer Information

Trainer information currently refers to data such as:

```text
Trainer name
Trainer ID
```

Additional trainer fields may be added later.

---

## Detection vs Save Support

These two capabilities should remain separate.

For example:

```text
Game detected
      │
      ▼
Save reader available?
      │
   ┌──┴──┐
  Yes    No
   │      │
   ▼      ▼
Read     Continue
save     without
data     save data
```

This allows the Rich Presence system to work with a newly detected game even before complete save support is available.

---

## Adding Game Support

When adding a new Pokémon game, follow this order.

### 1. Identify the Game

Add a stable internal game ID.

Example:

```text
pokemon_example
```

### 2. Add Game Configuration

Add the display information to `config.json`.

```json
{
	"pokemon_example": {
		"name": "Pokémon Example",
		"region": "Example Region",
		"large_image": "example",
		"large_text": "Pokémon Example"
	}
}
```

### 3. Verify Eden Detection

Determine how Eden exposes the game.

Prefer a stable identifier.

The current implementation uses the Eden window title.

### 4. Verify Save Location

Determine:

- Title ID
- Save directory
- Save file name

Do not guess these values.

### 5. Verify Save Format

Check whether PKHeX.Core supports the save format.

Prefer:

```csharp
SaveUtil.GetSaveFile(savePath)
```

over custom format detection.

### 6. Implement Save Reading

Add a game-specific reader only after the save format has been verified.

### 7. Extract Minimal Required Data

Start with reliable fields such as:

```text
Trainer
Playtime
Pokédex
```

Additional fields can be added later.

### 8. Map to GameState

Convert the extracted data into the common application representation.

### 9. Test

Test:

- Game detection.
- Save path resolution.
- Save format detection.
- Save data extraction.
- output.
- GameState integration.

### 10. Update Documentation

Update:

- `README.md`
- `docs/GAME-SUPPORT.md`
- `docs/SAVE-READER.md`
- `docs/ROADMAP.md`

---

## Verification Requirements

A game should not be marked as fully supported until its implementation has been verified.

At minimum, verify:

```text
[ ] Eden detection
[ ] Save path
[ ] Save format
[ ] Save reader
[ ] GameState mapping
[ ] GameState mapping
[ ] Discord integration
[ ] Tests
```

For save fields, verify each field independently.

---

## Research Workflow

When implementing support for a new game or save field:

```text
Identify requirement
        │
        ▼
Inspect local project
        │
        ▼
Inspect local PKHeX source
        │
        ▼
Check Context7
        │
        ▼
Check official / reliable sources
        │
        ▼
Verify against actual dependency version
        │
        ▼
Implement
        │
        ▼
Test
        │
        ▼
Document
```

### Important Rule

Never invent:

- Title IDs
- Save paths
- Save offsets
- Block layouts
- Pointer addresses
- Pokédex structures
- Location structures
- API methods

If information cannot be verified, keep the feature marked as `🚧`.

---

## Version Awareness

Game support can depend on multiple versions:

```text
Game version
     +
Save revision
     +
PKHeX version
     +
Eden version
```

When investigating save behavior, record relevant versions when they affect compatibility.

Do not assume that a save implementation from one revision automatically applies to another revision.

---

## Testing Strategy

### Unit-Level

Test individual components:

```text
Game Detector
Save Path Resolver
Save Reader
```

### Bridge-Level

Test:

```text
PokemonSaveReader
       ↓
PKHeX.Core
       ↓
JSON
```

### Integration-Level

Test:

```text
Eden
 ↓
Game Detection
 ↓
Save Reading
 ↓
GameState
 ↓
Discord RPC
```

A game should only be marked fully supported after the relevant integration path has been tested.

---

## Current Development Priorities

The current project priorities are:

1. Connect save data to `GameState`.
2. Display actual Pokédex progress through Discord RPC.
3. Display saved playtime through Discord RPC.
4. Add location support.
5. Add current party information.
6. Complete Pokémon Violet save support.
7. Investigate Pokémon Legends: Z-A support.

These priorities may change as the project develops.

---

## Related Documentation

- [Architecture](ARCHITECTURE.md)
- [Save Reader](SAVE-READER.md)
- [Development Setup](DEVELOPMENT.md)
- [Configuration](CONFIGURATION.md)
- [Discord RPC](DISCORD-RPC.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Roadmap](ROADMAP.md)
