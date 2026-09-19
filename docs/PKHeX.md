# PKHeX.md

Technical documentation for the role of **PKHeX** and **PKHeX.Core** in SWITCH RPC.

This document exists so that developers and AI coding agents understand what PKHeX is, how it works, what it supports, what it can do, and — most importantly — how this project is allowed to use it.

---

## 1. What Is PKHeX?

**PKHeX** is a C# application for working with Pokémon save data.

The official project describes it as a Pokémon core-series save editor. It can load supported save files, display their data, edit that data, and save the result. It also supports individual Pokémon files, Mystery Gift files, certain other Pokémon-related formats, and cross-generation conversions. citeturn0search0turn1search5

The important distinction for this project is:

```text
PKHeX
├── PKHeX.WinForms
│   └── Graphical save editor
│
└── PKHeX.Core
    └── Reusable C# library containing
        save formats, data models, parsers,
        legality logic, and related abstractions
```

SWITCH RPC does **not** need the graphical PKHeX application.

It uses:

```text
PKHeX.Core
```

as a library from the local PKHeX source tree.

---

## 2. Why This Project Uses PKHeX

Pokémon save files are not simple generic binary files.

Different Pokémon games use different:

- layouts;
- block structures;
- checksums;
- cryptographic integrity mechanisms;
- Pokémon data formats;
- Pokédex structures;
- game-specific save systems.

The PKHeX developer has explicitly explained that each set of games has its own format and quirks, including checksums and cryptographic signatures, and that PKHeX provides abstractions for each format. citeturn1search6

Therefore, SWITCH RPC should not reinvent save parsing unless there is a specific, verified reason to do so.

Instead:

```text
Pokémon Save
      ↓
PKHeX.Core
      ↓
Verified game-specific abstraction
      ↓
Requested data
      ↓
JSON
      ↓
Python
```

This gives the project access to existing, maintained knowledge about Pokémon save formats.

---

## 3. PKHeX vs PKHeX.Core

These terms must not be treated as interchangeable.

### PKHeX

The full application.

It provides:

- graphical interface;
- save loading;
- save editing;
- Pokémon editing;
- legality analysis;
- import/export workflows;
- other user-facing tools.

### PKHeX.Core

The reusable library inside the PKHeX project.

It provides the underlying programmatic functionality used to understand Pokémon data.

For this project, the important component is:

```text
bridge/PokemonSaveReader/
        ↓
PKHeX.Core
```

The GUI is not required.

---

## 4. What PKHeX Can Work With

The official PKHeX repository currently lists support for:

### Main-series save formats

- Generation I:
  - Red / Blue / Yellow
- Generation II:
  - Gold / Silver / Crystal
- Generation III:
  - Ruby / Sapphire
  - Emerald
  - FireRed / LeafGreen
- Generation IV:
  - Diamond / Pearl
  - Platinum
  - HeartGold / SoulSilver
- Generation V:
  - Black / White
  - Black 2 / White 2
- Generation VI:
  - X / Y
  - Omega Ruby / Alpha Sapphire
- Generation VII:
  - Sun / Moon
  - Ultra Sun / Ultra Moon
  - Let's Go, Pikachu! / Let's Go, Eevee!
- Generation VIII:
  - Sword / Shield
  - Brilliant Diamond / Shining Pearl
  - Legends: Arceus
- Generation IX:
  - Scarlet / Violet
  - Legends: Z-A

The current `PKHeX.Core` `SaveUtil` source contains save detection and constructors for these main-game save types, including `SAV8LA`, `SAV9SV`, and `SAV9ZA`. citeturn1search1

### Side and related formats

The current source also contains support for several related Pokémon titles and systems, including:

- Pokémon Colosseum;
- Pokémon XD: Gale of Darkness;
- Pokémon Ruby/Sapphire Box;
- Pokémon Battle Revolution;
- Pokémon Stadium;
- Pokémon Bank-related data;
- PokéStock-related formats;
- Pokémon Ranch-related data.

These are not automatically relevant to SWITCH RPC. They are listed here to clarify the broader scope of PKHeX.Core. citeturn1search1turn1search2

---

## 5. Supported File Types

The official PKHeX documentation lists support for multiple kinds of Pokémon data, including:

### Save files

Examples include:

```text
main
*.sav
*.dsv
*.dat
*.gci
*.bin
```

### Individual Pokémon files

Examples include:

```text
.pk*
.ck3
.xk3
.pb7
.sk2
.bk4
.rk4
```

### Other supported data

PKHeX also documents support for:

- GameCube memory card files;
- Mystery Gift files;
- GO Park entity imports;
- teams from decrypted 3DS Battle Videos;
- cross-generation Pokémon transfers/conversions;
- Pokémon Showdown sets and QR-code workflows. citeturn0search0turn1search5

Not every supported format is relevant to this project.

---

## 6. Important: PKHeX Does Not Automatically Decrypt Console Saves

PKHeX expects save files that are accessible in a usable, unencrypted form rather than console-specific encrypted data.

The official README states that save data should be imported/exported using an appropriate save-data manager when necessary. citeturn0search0

For this project, Eden already exposes the local save data in a form that our current bridge can pass to PKHeX.Core.

The project should therefore distinguish:

```text
Save extraction / decryption
```

from:

```text
Save parsing
```

PKHeX is primarily being used here for the second part.

---

## 7. How PKHeX Detects a Save

The current PKHeX.Core implementation uses `SaveUtil` to identify supported save formats.

Conceptually:

```text
Raw save bytes
      ↓
SaveUtil.GetTypeInfo(...)
      ↓
Determine save type
      ↓
Create matching SAV* class
      ↓
Expose game-specific data
```

For example, the current source maps:

```text
LA  → SAV8LA
SV  → SAV9SV
ZA  → SAV9ZA
```

and similarly maps older save types to their corresponding `SAV*` classes. citeturn1search1

Our bridge uses:

```csharp
SaveFile? save = SaveUtil.GetSaveFile(savePath);
```

This means the bridge does not need to manually determine whether a file is PLA, Scarlet, Violet, etc.

---

## 8. Game-Specific Save Classes

PKHeX uses game-specific save classes.

Examples:

```text
SAV8LA
SAV9SV
SAV9ZA
```

These classes expose data through abstractions appropriate to each game's format.

This is important because:

```text
Pokémon Legends: Arceus
```

does not store its data in exactly the same way as:

```text
Pokémon Scarlet / Violet
```

and Scarlet/Violet are not necessarily identical to:

```text
Pokémon Legends: Z-A
```

Do not assume that a property, offset, block, or structure from one game exists in another game.

---

## 9. Save Blocks

Many modern Pokémon saves are organized into game-specific blocks.

PKHeX provides accessors for these blocks.

For example, the Scarlet/Violet save abstraction exposes concepts such as:

```text
BoxInfo
PartyInfo
MyStatus
Played
Zukan
BoxLayout
```

This allows application code to ask for meaningful data rather than manually reading arbitrary byte offsets.

Conceptually:

```text
Raw Save
   ↓
Save Block Accessor
   ↓
Game-specific structure
   ↓
Meaningful property
```

This is one of the main reasons this project should use PKHeX.Core instead of implementing its own binary parser.

---

## 10. Pokémon Data Abstraction

PKHeX also has abstractions for individual Pokémon data.

The core library can enumerate Pokémon stored in supported save structures, including box and party data. Its current extensions expose functionality such as `GetAllPKM()` and game-specific extra Pokémon slots. citeturn1search3

Conceptually:

```text
SaveFile
   ├── Boxes
   │    └── Pokémon
   │
   ├── Party
   │    └── Pokémon
   │
   └── Extra game-specific slots
```

This can eventually allow SWITCH RPC to expose information such as:

- party Pokémon;
- box contents;
- species;
- forms;
- levels;
- other verified Pokémon properties.

These features are future possibilities, not current RPC requirements.

---

## 11. Pokédex Support

PKHeX has game-specific Pokédex abstractions.

For example, the current Legends: Arceus implementation contains a dedicated:

```text
PokedexSave8a
```

implementation.

It handles the Legends: Arceus Pokédex structure and exposes concepts such as:

- Pokédex indexes;
- species/form mappings;
- completion state;
- research information;
- different dex categories. citeturn1search7

This is exactly the kind of abstraction SWITCH RPC should consume.

Instead of:

```text
Read byte at offset X
→ assume it means caught Pokémon
```

prefer:

```text
SAV8LA
→ PokedexSave8a
→ verified Pokédex API
→ count
```

---

## 12. Example: Legends: Arceus

Our current bridge uses:

```csharp
SAV8LA
```

and reads:

```text
save.MyStatus
save.Played
save.PokedexSave
```

For example:

```csharp
var playtime = save.Played;

var seen = save.PokedexSave.GetDexGetCount(
    PokedexType8a.Hisui
);
```

This is preferable to reverse-engineering the raw save again.

The bridge can then normalize the result:

```json
{
    "success": true,
    "game": {
        "type": "legends_arceus"
    },
    "playtime": {
        "hours": 6,
        "minutes": 50,
        "seconds": 34
    },
    "pokedex": {
        "seen": 8,
        "total": 242
    }
}
```

---

## 13. Example: Scarlet / Violet

Our current bridge uses:

```csharp
SAV9SV
```

and can access:

```text
save.MyStatus
save.Played
save.Zukan
```

For example:

```csharp
var playtime = save.Played;

var seen = save.Zukan.SeenCount;
var caught = save.Zukan.CaughtCount;
```

This gives the Python application meaningful data without requiring it to understand the underlying save block layout.

---

## 14. Why We Should Not Use Raw Offsets

A raw-offset parser might look like:

```text
Read bytes at 0x123456
→ interpret as integer
→ call it Pokédex count
```

This is fragile.

Different games and game versions can change:

- save sizes;
- block locations;
- checksums;
- structures;
- optional blocks;
- version-specific data;
- encryption/integrity mechanisms.

PKHeX already contains game-specific abstractions for these formats.

Therefore:

> Do not invent or guess save offsets when a verified PKHeX abstraction exists.

This rule is mandatory for this project.

---

## 15. What PKHeX Can Do

Depending on the supported game and data type, PKHeX can provide functionality for:

### Save data

- load saves;
- inspect save metadata;
- edit save data;
- save modified data;
- manage game-specific save structures.

### Pokémon

- inspect Pokémon data;
- create/edit Pokémon data;
- move Pokémon between supported storage locations;
- work with individual Pokémon files;
- perform legality analysis where supported.

### Other Pokémon data

- Mystery Gifts;
- Pokémon Showdown sets;
- QR-related workflows;
- some game-specific storage;
- cross-generation conversions.

The exact functionality varies by generation and game.

The official project explicitly describes PKHeX as a save editor and supports multiple Pokémon data formats and conversion workflows. citeturn0search0turn1search5

---

## 16. What PKHeX Does Not Mean

Using PKHeX does **not** automatically mean the project:

- can access Nintendo Online;
- can access a Nintendo Switch directly;
- can decrypt every console save;
- can read every Pokémon-related game;
- can understand undocumented future formats;
- can provide every field for every game;
- can modify a save safely without understanding its format.

Support is game- and format-specific.

Always verify the actual `PKHeX.Core` version being used.

---

## 17. PKHeX in This Project

The intended architecture is:

```text
Eden
  ↓
Game Detector
  ↓
Game Definition
  ↓
Save Path Resolver
  ↓
Python Save Reader
  ↓
PokemonSaveReader (.NET)
  ↓
PKHeX.Core
  ↓
Game-specific SaveFile
  ↓
Game-specific data abstraction
  ↓
JSON
  ↓
Python GameState
  ↓
Discord RPC
```

PKHeX is therefore a **data parsing dependency**, not the application itself.

---

## 18. Why We Use a C# Bridge

PKHeX.Core is written in C#.

The main application is written in Python.

Instead of rewriting PKHeX functionality in Python, the project uses:

```text
Python
   ↓
subprocess
   ↓
.NET executable
   ↓
PKHeX.Core
   ↓
JSON stdout
   ↓
Python
```

The bridge is:

```text
bridge/PokemonSaveReader/
```

Its responsibility is deliberately narrow.

---

## 19. Responsibilities of the Bridge

The C# bridge should:

1. accept a save path;
2. load the save through PKHeX.Core;
3. identify the save type;
4. read verified data;
5. normalize the result;
6. serialize the result to JSON;
7. write diagnostics to stderr;
8. exit with an appropriate status code.

The bridge should **not**:

- communicate with Discord;
- detect Eden;
- implement the main application loop;
- modify saves;
- contain UI logic;
- contain arbitrary game detection logic.

---

## 20. Read-Only Rule

This project uses PKHeX.Core strictly as a **read-only save data source**.

Even though PKHeX itself is capable of editing and saving data, SWITCH RPC must not use those capabilities.

Allowed:

```text
Load save
   ↓
Inspect
   ↓
Extract data
   ↓
Return JSON
```

Not allowed:

```text
Load save
   ↓
Modify
   ↓
Write save
```

This distinction must remain clear to both human developers and AI coding agents.

---

## 21. No Save Mutation

Do not add code that:

- changes Pokémon;
- changes items;
- changes trainer information;
- changes Pokédex state;
- changes story progression;
- writes modified save files;
- replaces the original save;
- creates edited save files as part of normal operation.

If a future feature requires writing saves, it must be treated as a separate project-level design decision rather than silently extending the current reader.

---

## 22. Version Awareness

PKHeX is actively developed.

Its supported formats and APIs can change over time.

Therefore:

```text
PKHeX documentation
```

and:

```text
Local PKHeX source
```

must always be interpreted together.

For this project, the local source actually referenced by:

```text
PokemonSaveReader.csproj
```

is authoritative for the APIs available during a build.

When an API is unclear:

1. inspect the local PKHeX source;
2. check Context7 when available;
3. check the official PKHeX repository;
4. verify the exact dependency version;
5. build and test.

Never assume an API exists because an old tutorial mentions it.

---

## 23. Local PKHeX Source

The project intentionally keeps the PKHeX source outside the public repository.

Expected local structure:

```text
bridge/
├── PKHeX/
└── PokemonSaveReader/
```

The repository ignores:

```text
bridge/PKHeX/
```

This means a new developer must obtain PKHeX separately before building the bridge.

This is an intentional repository/dependency decision.

---

## 24. Why PKHeX Source Is Useful During Development

The local source is not only a dependency.

It is also a reference for understanding save structures.

For example, if an AI needs to implement Pokédex support, it should first look for:

```text
Pokedex...
Zukan...
SAV...
SaveBlockAccessor...
MyStatus...
PlayTime...
```

rather than immediately searching for raw byte offsets.

The source often reveals:

```text
SaveFile
  ↓
Save block
  ↓
Game-specific abstraction
  ↓
Meaningful property
```

This is the preferred research path.

---

## 25. How an AI Should Research PKHeX

When asked to add a new save field:

```text
1. Identify the target game
        ↓
2. Identify the PKHeX SAV* class
        ↓
3. Search for an existing property/block
        ↓
4. Search for game-specific abstractions
        ↓
5. Check whether the value already has a helper
        ↓
6. Verify the implementation in local source
        ↓
7. Add the smallest bridge change
        ↓
8. Build
        ↓
9. Test against a real save
```

Example:

```text
Need playtime
    ↓
SAV9SV
    ↓
Played
    ↓
PlayTime9
    ↓
PlayedHours / PlayedMinutes / PlayedSeconds
```

Do not jump directly to binary offsets.

---

## 26. How an AI Should Handle Unsupported Data

If PKHeX does not expose a required field:

Do **not** immediately implement a custom parser.

Instead:

1. verify that the target game is supported;
2. verify the local PKHeX version;
3. inspect the relevant save class;
4. inspect save blocks;
5. inspect related abstractions;
6. check official PKHeX discussions/source;
7. determine whether the field is actually available;
8. document the limitation if it cannot be safely obtained.

A missing field is acceptable.

Incorrect data is not.

---

## 27. PKHeX Support vs Project Support

These are different concepts.

### PKHeX support

Means PKHeX.Core can identify/read a particular save format.

### Project save-reader support

Means SWITCH RPC has implemented and verified the fields it needs.

### Project runtime support

Means those fields are integrated into:

```text
GameState
```

and then:

```text
Discord RPC
```

Therefore:

```text
PKHeX supports game
        ≠
SWITCH RPC fully supports game
```

---

## 28. Example Support Matrix

| Game | PKHeX Core | Project Save Reader | RPC Integration |
|---|---|---|---|
| Pokémon Legends: Arceus | Supported | Implemented | Partial |
| Pokémon Scarlet | Supported | Implemented | Partial |
| Pokémon Violet | Supported | Verification pending | Pending |
| Pokémon Legends: Z-A | Supported in current source | Verification pending | Pending |

PKHeX's current source contains `SAV8LA`, `SAV9SV`, and `SAV9ZA` save implementations. citeturn1search1turn1search2

The project status must still be determined independently.

---

## 29. What PKHeX Is Best Used For in This Project

PKHeX.Core is particularly useful for:

- save format detection;
- trainer information;
- playtime;
- Pokémon storage;
- party data;
- Pokédex data;
- game-specific save blocks;
- location-related save data where exposed;
- other structured save metadata.

The project should prefer these high-level abstractions over raw binary parsing.

---

## 30. What PKHeX Should Not Be Used For in This Project

Do not use PKHeX as:

- a game emulator;
- a Nintendo Online client;
- a network client;
- a cheat engine;
- a live memory editor;
- a save writer;
- a mechanism for bypassing console security.

Its role here is:

```text
Read verified local save data
```

and nothing more.

---

## 31. Common Developer Mistakes

### Mistake 1 — Treating PKHeX as a magic decoder

PKHeX does not make arbitrary encrypted or unsupported data readable.

### Mistake 2 — Assuming all Pokémon games share one format

They do not.

### Mistake 3 — Copying offsets from another game

An offset valid for one game/version may be meaningless elsewhere.

### Mistake 4 — Using GUI APIs in the bridge

The bridge should use:

```text
PKHeX.Core
```

not the WinForms application.

### Mistake 5 — Assuming support means every field is available

A supported save format does not mean every possible game value has a convenient API.

### Mistake 6 — Using editing APIs accidentally

The bridge must remain read-only.

---

## 32. Practical Mental Model

Developers should think of PKHeX.Core as:

> A large, game-aware Pokémon data model and save-format library.

Not:

> A generic binary parser.

And not:

> An emulator.

The intended relationship is:

```text
Pokémon Save
     ↓
PKHeX understands the format
     ↓
PKHeX exposes structured data
     ↓
Our bridge selects what we need
     ↓
Our Python application consumes the data
```

---

## 33. Recommended Development Pattern

When adding support for a field:

```text
Question:
"Where is the player's current location?"
        ↓
Find target SAV class
        ↓
Find related save block/accessor
        ↓
Find existing property/helper
        ↓
Verify implementation
        ↓
Expose through bridge
        ↓
Normalize JSON
        ↓
Map into GameState
        ↓
Display through RPC
```

This pattern should be followed for:

- Pokédex;
- playtime;
- trainer data;
- party;
- boxes;
- location;
- progression;
- other game state.

---

## 34. References

Primary sources:

- Official PKHeX repository: https://github.com/kwsch/PKHeX
- PKHeX README: https://github.com/kwsch/PKHeX/blob/master/README.md
- PKHeX `SaveUtil`: https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Saves/Util/SaveUtil.cs
- PKHeX Legends: Arceus Pokédex implementation: https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Saves/Substructures/Gen8/LA/Pokedex/PokedexSave8a.cs

For this project, always prefer the **local PKHeX source version actually referenced by the bridge** when resolving API questions.
