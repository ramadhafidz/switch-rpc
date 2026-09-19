# AGENTS.md

## Project Overview

SWITCH RPC is a modular Python application that provides Discord
Rich Presence for Pokémon games running through the Eden Nintendo Switch
emulator.

The long-term direction is a modular Nintendo Switch gaming platform —
see `docs/ROADMAP.md`. Pokémon support through PKHeX.Core is the first
fully implemented part of that platform.

The application is designed to:

-   Detect the Eden emulator process.
-   Identify the currently running Pokémon game.
-   Locate the corresponding local save file.
-   Read supported Pokémon save data through PKHeX.Core.
-   Convert save data into a common application state.
-   Display relevant information through Discord Rich Presence.
-   Clear the Rich Presence when the game closes.

The project currently uses:

-   Python for the main application.
-   C# / .NET for the PKHeX.Core bridge.
-   PKHeX.Core for Pokémon save parsing.
-   PyPresence for Discord Rich Presence.
-   psutil and pywin32 for Windows process and window detection.
-   Ruff for Python formatting and linting.
-   Pyright for Python static type checking.
-   pytest for automated testing.

------------------------------------------------------------------------

## Core Architecture

The intended runtime flow is:

``` text
Eden
  ↓
Game Detector
  ↓
Game Registry
  ↓
Save Path Resolver
  ↓
Python Save Reader
  ↓
PokemonSaveReader (.NET / C#)
  ↓
PKHeX.Core
  ↓
JSON
  ↓
GameState Parser
  ↓
GameState
  ↓
GameState Formatter
  ↓
Discord RPC
```

`GameRegistry` is primarily a configuration/lookup layer. It should not
become a mandatory processing stage when the existing architecture does
not require it.

Keep these responsibilities separated.

### Python

Python is responsible for:

-   Application lifecycle.
-   Configuration loading.
-   Eden process detection.
-   Game detection.
-   Game registry.
-   Save path resolution.
-   Invoking the C# save reader.
-   Parsing returned JSON.
-   Building `GameState`.
-   Formatting state for presentation.
-   Discord RPC integration.

### C# / .NET

C# is responsible for:

-   Loading Pokémon save files through PKHeX.Core.
-   Detecting the save format through PKHeX.
-   Extracting verified save data.
-   Resolving game-specific save information where appropriate.
-   Returning structured JSON to the Python application.

Do not move Pokémon save parsing into Python unless there is a specific
architectural reason to do so.

------------------------------------------------------------------------

## General Rules

### 1. Preserve the Architecture

Do not unnecessarily merge modules or move responsibilities between
components.

Prefer small, focused modules over large files containing unrelated
logic.

Before introducing a new abstraction, check whether an existing class or
module already provides the required responsibility.

### 2. Keep the Project Modular

Game-specific logic should remain isolated.

Adding a new Pokémon game should not require rewriting the core
application loop.

Prefer:

``` text
Game Definition
Game Detection
Save Resolution
Save Reader
GameState
```

as independent concepts.

### 3. Do Not Hardcode User-Specific Paths

Never introduce paths such as:

``` text
C:\Users\Hafidz\...
D:\Games\Eden\...
```

into production code.

Use dynamic paths such as:

``` python
Path.home()
```

and configuration where appropriate.

User-specific paths may only appear in temporary debugging scripts or
documentation examples when explicitly necessary.

### 4. Keep Save Access Read-Only

The application is a save-data reader.

Never add functionality that:

-   Modifies save files.
-   Writes Pokémon data.
-   Injects data into saves.
-   Deletes save files.
-   Automatically creates or rewrites emulator save data.
-   Changes emulator save data.

Reading save data is allowed.

Writing save data is outside the scope of this project.

------------------------------------------------------------------------

## Pokémon Save Data Rules

### Never Guess Save Offsets

This is one of the most important project rules.

Do not invent or estimate:

-   Save offsets.
-   Field locations.
-   Block sizes.
-   Pointer locations.
-   Pokémon structure locations.
-   Pokédex offsets.
-   Location offsets.
-   Playtime offsets.

Save structures must be based on:

1.  Verified PKHeX implementations.
2.  Official or reliable technical documentation.
3.  Reproducible analysis of known save formats.

If the structure is unknown, leave the feature unimplemented rather than
guessing.

### Prefer PKHeX Implementations

When PKHeX.Core already exposes the required data, use the existing
PKHeX API instead of manually parsing the underlying bytes.

Prefer:

``` text
save.MyStatus
save.Played
save.Zukan
```

over manually calculating offsets.

### Preserve Read-Only Behavior

Do not call APIs that modify save data.

Do not introduce write operations into the bridge.

------------------------------------------------------------------------

## PKHeX Rules

PKHeX is a third-party dependency.

The PKHeX source tree must remain outside the tracked repository.

Expected local structure:

``` text
bridge/
├── PKHeX/
└── PokemonSaveReader/
```

`bridge/PKHeX/` is intentionally ignored by Git.

Do not:

-   Commit PKHeX source.
-   Copy PKHeX source into another project directory.
-   Modify PKHeX source to implement project-specific features.
-   Vendor PKHeX into this repository.

When working with PKHeX:

1.  Check the local PKHeX source first when available.
2.  Verify APIs against the actual local version.
3.  Prefer stable public APIs and existing abstractions.
4.  Never fabricate APIs, offsets, block layouts, or field locations.

If PKHeX functionality is missing, implement project-specific logic in
the bridge only when the required data can be verified.

------------------------------------------------------------------------

## C# Bridge Rules

The C# project is located at:

``` text
bridge/PokemonSaveReader/
```

The bridge should:

1.  Receive a save file path.
2.  Validate that the file exists.
3.  Load the save using PKHeX.
4.  Identify the supported save type.
5.  Extract the required data.
6.  Return structured JSON.
7.  Return a non-zero exit code on failure.

The bridge should not:

-   Modify the input save.
-   Print non-JSON data to stdout when returning successful results.
-   Depend on Python internals.
-   Contain Discord RPC logic.

Use `stderr` for errors when appropriate so stdout remains
machine-readable JSON.

------------------------------------------------------------------------

## JSON Communication

Python and C# communicate through JSON.

The JSON output should be:

-   Structured.
-   Predictable.
-   Machine-readable.
-   Backward-compatible where practical.

Current top-level structure includes:

``` json
{
    "success": true,
    "game": {},
    "trainer": {},
    "playtime": {},
    "pokedex": {},
    "party": {},
    "boxes": {},
    "items": {},
    "location": {},
    "progress": {}
}
```

Current location data can include:

``` json
{
    "name": "Artazon",
    "fieldID": 0,
    "locationID": 86,
    "x": 3709.828369140625,
    "y": 154.77886962890625,
    "z": -1743.365966796875
}
```

Do not silently change the JSON schema when existing consumers depend on
it.

If a breaking change is necessary, update the Python consumer and
documentation together.

------------------------------------------------------------------------

## GameState

`GameState` is the common state representation used by the Python
application.

Current model:

``` python
@dataclass
class GameState:
	game_id: str
	playtime_seconds: int | None = None
	location: LocationState | None = None
	pokedex: dict[str, PokedexStats] | None = None
	party: PartyState | None = None
	boxes: BoxesState | None = None
```

Current supporting state models include:

``` text
PokedexStats
PokemonState
PartyState
BoxSlotState
BoxState
BoxesState
LocationState
```

`LocationState` contains:

``` python
@dataclass
class LocationState:
	name: str | None = None
	field_id: int | None = None
	location_id: int | None = None
	x: float | None = None
	y: float | None = None
	z: float | None = None
```

Keep `GameState` independent from PKHeX-specific classes.

Do not expose PKHeX objects directly to the Discord RPC layer.

The intended flow is:

``` text
PKHeX
  ↓
JSON
  ↓
GameState Parser
  ↓
GameState
  ↓
Discord RPC
```

------------------------------------------------------------------------

## Game Support

Detection support and save-reader support are independent.

Currently verified save-reader support:

-   Pokémon Legends: Arceus.
-   Pokémon Scarlet.

Configured but not fully verified:

-   Pokémon Violet.
-   Pokémon Legends: Z-A.

When adding a game:

1.  Add its game definition.
2.  Add or update detection logic if required.
3.  Add save path information.
4.  Implement a verified save reader if supported.
5.  Test the save reader independently.
6.  Connect the resulting data to `GameState`.
7.  Connect the resulting state to Discord RPC.
8.  Update documentation.

Never mark a game as fully supported merely because it is detectable.

------------------------------------------------------------------------

## Game IDs

Use the existing internal game ID convention:

``` text
pokemon_legends_arceus
pokemon_scarlet
pokemon_violet
pokemon_legends_za
```

Do not rename existing IDs without a clear migration reason.

Game IDs should be stable and machine-oriented.

Display names belong in the game configuration.

------------------------------------------------------------------------

## Configuration

Game configuration belongs in `config.json`.

Do not hardcode:

-   Discord Application IDs.
-   Artwork names.
-   Game display names.
-   Regions.
-   User-configurable update intervals.

Never commit secrets, authentication tokens, private keys, or
credentials.

A Discord Application ID is not a secret, but use a placeholder in
documentation examples.

------------------------------------------------------------------------

## Discord RPC Rules

Discord RPC is handled by:

``` text
rpc/discord_rpc.py
```

Keep Discord-specific behavior inside the RPC layer.

The rest of the application should not directly depend on PyPresence
APIs when avoidable.

The RPC layer should handle:

-   Connection.
-   Reconnection.
-   Presence updates.
-   Clearing presence.
-   Closing the connection.
-   Connection failures.

The session timer is intentionally retained in RPC state.

When the detected game changes:

1.  Clear the previous presence when necessary.
2.  Load the new game definition.
3.  Build the new state.
4.  Update Discord.

When the game closes:

``` text
Game detected
      ↓
Eden/game no longer running
      ↓
Clear RPC
```

Do not leave stale Rich Presence active after the game closes.

------------------------------------------------------------------------

## Error Handling

The application should fail gracefully.

Do not crash the entire application because:

-   Eden is not running.
-   Discord is unavailable.
-   A save file cannot be found.
-   A save format is unsupported.
-   PKHeX cannot identify a save.
-   The C# bridge fails.
-   Discord RPC disconnects.

Prefer:

``` text
Log error
↓
Return None / failure state
↓
Continue monitoring
```

Use exceptions for genuinely unexpected failures.

Do not use broad exception handling to silently hide programming errors.

------------------------------------------------------------------------

## Logging

Keep console output useful and concise.

Good:

``` text
Eden: running
Game detected: Pokémon Scarlet
Save data refreshed.
Rich Presence updated.
```

For errors, provide enough information to diagnose the issue.

Avoid logging:

-   Save file contents.
-   Sensitive information.
-   Large binary dumps.
-   Credentials.
-   Authentication tokens.

------------------------------------------------------------------------

## Python Development Tooling

The project uses:

``` text
Ruff
├── Formatter
├── Linter
└── Import sorting

Pyright
└── Static type checking

pytest
└── Automated testing
```

Runtime dependencies belong in:

``` text
requirements.txt
```

Development dependencies belong in:

``` text
requirements-dev.txt
```

The current development dependency file contains:

``` text
ruff
pyright
pytest
```

Tool configuration belongs in:

``` text
pyproject.toml
```

Do not introduce Black, isort, Flake8, or another formatter/linter
unless there is a concrete reason to change the tooling strategy.

### Python Formatting

Use Ruff as the canonical formatter.

Current project formatting rules include:

-   Line length: 80.
-   Python target: 3.12.
-   Double quotes.
-   Tabs for indentation.
-   LF line endings.
-   Two tab indentation convention for nested Python blocks.

Run:

``` powershell
ruff check --fix .
ruff format .
```

### Python Type Checking

Run:

``` powershell
pyright
```

Do not silence a type error merely to obtain a clean output when the
underlying code can be corrected.

### Testing

Run:

``` powershell
pytest
```

Integration tests that require local emulator saves or PKHeX should be
clearly separated from portable unit tests.

------------------------------------------------------------------------

## Developer CLI

The project provides a developer command entry point:

``` text
dev.py
```

Available commands:

``` text
check
format
lint
typecheck
test
build
run
save
clean
all
```

The CLI:

-   Uses subprocesses with correct exit codes.
-   Avoids hardcoded user-specific paths.
-   Works from the repository root.
-   Keeps Python and C# commands in one predictable interface.
-   Has no additional dependencies beyond the standard library.

Run `python dev.py --help` to list available commands.

------------------------------------------------------------------------

## Testing

Test individual components independently when possible.

Automated pytest tests:

``` text
test/test_dev.py
```

Current manual/integration test scripts:

``` text
test/game_detection.py
test/save_path.py
test/game_save_reader.py
test/command_line.py
test/windows.py
```

The project is building out pytest-based automated tests alongside
existing manual integration scripts.

For save-reader changes, test with actual supported save files when
available.

Do not commit real emulator save files to the repository.

Tests should cover both successful reads and failure handling.

------------------------------------------------------------------------

## Code Style

### Python

Use:

-   Type hints where practical.
-   `pathlib.Path` for filesystem paths.
-   Small focused functions.
-   Clear class and variable names.
-   Explicit error handling.
-   Ruff for formatting and linting.
-   Pyright-compatible typing.

Use 2 tabs for indentation.

Example:

``` python
class Example:
	def method(self):
		if condition:
			return True

		return False
```

Do not replace the project's indentation style with 4 spaces.

### C

Follow standard C# conventions.

Use:

-   PascalCase for classes and public members.
-   camelCase for local variables and parameters.
-   Explicit types when they improve readability.
-   Modern C# features when appropriate.

The Python project's indentation rule does not need to be forced onto C#
code.

------------------------------------------------------------------------

## Dependencies

Avoid adding dependencies without a clear reason.

Before adding a dependency:

1.  Check whether the standard library can solve the problem.
2.  Check whether an existing dependency already provides the
    functionality.
3.  Consider maintenance and compatibility.
4.  Update `requirements.txt`, `requirements-dev.txt`, or the relevant
    .NET project file.
5.  Update documentation if the dependency affects setup.

Do not introduce large frameworks for small problems.

------------------------------------------------------------------------

## File Organization

Keep the existing structure unless there is a strong reason to change
it:

``` text
switch-rpc/
├── main.py
├── dev.py
├── config.json
├── pyproject.toml
├── requirements.txt
├── requirements-dev.txt
├── CHANGELOG.md
├── CONTRIBUTING.md
├── LICENSE
├── assets/
├── games/
├── rpc/
├── test/
├── bridge/
└── docs/
```

Do not create random utility directories.

If a new module has a clear responsibility, place it in the appropriate
existing package.

------------------------------------------------------------------------

## Git Rules

Never commit:

``` text
.venv/
__pycache__/
*.pyc
bridge/PKHeX/
bridge/PokemonSaveReader/bin/
bridge/PokemonSaveReader/obj/
config.local.json
.env
eden/
main
main2
backup
poke_trade
*.sav
*.dsv
*.dat
*.bin
```

Do not commit:

-   Emulator save files.
-   Local emulator data.
-   Personal configuration.
-   Secrets.
-   PKHeX source.
-   Build artifacts.

Before committing, check:

``` powershell
git status
git status --ignored
```

------------------------------------------------------------------------

## Documentation Rules

When behavior changes, update the relevant documentation.

Important documentation files:

``` text
README.md
AGENTS.md
CHANGELOG.md

docs/
├── ARCHITECTURE.md
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

``` text
Implemented
In Progress
Planned
Unsupported
```

------------------------------------------------------------------------

## Adding a New Pokémon Game

When adding a new game:

1.  **Game Definition** --- add the game to `config.json`.
2.  **Detection** --- add required Eden identification.
3.  **Save Path** --- add title ID or save resolution.
4.  **Save Reader** --- implement only after format verification.
5.  **JSON** --- return a consistent structure.
6.  **GameState** --- map data into the common state model.
7.  **Discord RPC** --- add appropriate behavior.
8.  **Tests** --- add or update tests.
9.  **Documentation** --- update README, roadmap, and relevant docs.

------------------------------------------------------------------------

## What Not To Do

Do not:

-   Guess Pokémon save offsets.
-   Modify save files.
-   Commit emulator saves.
-   Commit PKHeX source.
-   Hardcode personal filesystem paths.
-   Put Discord RPC logic into save readers.
-   Put PKHeX-specific objects into `GameState`.
-   Add unnecessary dependencies.
-   Claim unsupported features are implemented.
-   Remove existing functionality without checking its consumers.
-   Rewrite working modules without a concrete reason.
-   Change project architecture merely for stylistic preference.
-   Disable lint/type-checking rules just to hide fixable problems.

------------------------------------------------------------------------

## Development Philosophy

Prefer:

``` text
Simple
Modular
Verifiable
Read-only
Testable
Maintainable
```

over:

``` text
Complex
Monolithic
Guess-based
Write-capable
Hardcoded
Over-engineered
```

When uncertain about a Pokémon save structure, do not guess.

When uncertain about an architectural change, inspect the existing
implementation and its consumers before changing it.

When a feature cannot be safely verified, leave it unimplemented and
document the limitation.

------------------------------------------------------------------------

## Priority Order

When making implementation decisions, prioritize:

1.  Correctness.
2.  Save-data safety.
3.  Existing architecture.
4.  Maintainability.
5.  Testability.
6.  User experience.
7.  Performance.
8.  Convenience.

Never sacrifice save-data safety or correctness for convenience.

------------------------------------------------------------------------

## Documentation and External APIs

Use current, authoritative documentation when working with external
libraries, SDKs, APIs, or developer tools.

For PKHeX specifically:

1.  Check the local PKHeX source first when it is available.
2.  Verify the relevant API against the actual local version.
3.  Prefer existing PKHeX abstractions over manual binary parsing.
4.  Verify behavior against the actual game/save version.
5.  Test with a valid save.
6.  Never invent APIs, offsets, block layouts, or field locations.

If external documentation and the local source disagree, treat the
actual local source version as authoritative for the code being built
and investigate the discrepancy before proceeding.

Never assume that a class, method, property, parameter, or API exists.

If an API cannot be verified, treat it as an investigation task.

------------------------------------------------------------------------

## Final Checklist

Before considering a change complete:

-   [ ] Existing functionality still works.
-   [ ] Relevant tests pass.
-   [ ] Ruff passes.
-   [ ] Pyright passes.
-   [ ] No user-specific paths were introduced.
-   [ ] No save files were added to Git.
-   [ ] No PKHeX source was added to Git.
-   [ ] No secrets were added.
-   [ ] Save access remains read-only.
-   [ ] Save structures are based on verified information.
-   [ ] Documentation reflects the actual implementation.
-   [ ] Python code follows the 2-tab indentation convention.
-   [ ] New functionality is placed in the appropriate module.
