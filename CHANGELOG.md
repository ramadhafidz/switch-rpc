# Changelog

All notable changes to SWITCH RPC are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning](https://semver.org/) where applicable.

## [Unreleased]

### Added

- CI workflow (GitHub Actions): builds the solution and runs the xUnit suite on every push to `main` and every pull request, checking out the pinned PKHeX revision into `third_party/PKHeX/`.
- Packaging documentation for distributable single-file Windows builds.

## [0.2.0] - 2026-09-20

The native .NET 10 migration (Phase 2) is complete; the repository is now .NET-only. Tag: `v0.2.0-dotnet-core`.

### Added

- .NET 10 implementation of the RPC core under `src/` (Phase 2 migration): layered `SwitchRpc.*` solution with direct PKHeX.Core save reading for Pokémon Scarlet/Violet and Legends: Arceus, config-driven game definitions, and Discord Rich Presence with reconnection hardening.
- .NET test suite (xUnit) covering the core state, save readers, and the Eden save locator.
- Diagnostic mode (`--diagnose`) for the .NET application, plus benchmark documentation in `docs/BENCHMARKS.md`.

### Removed

- Python baseline (`main.py`, `dev.py`, `games/`, `rpc/`, `test/`, `pyproject.toml`, `requirements*.txt`) and the `bridge/PokemonSaveReader` JSON bridge — superseded by the native .NET implementation. Their history is preserved under the `v0.1.0-python-baseline` tag, including verified extractors (trainer, party, boxes, items, progress) not yet ported to the .NET readers.

### Technical Notes

- The project targets Windows.
- Save parsing is read-only.
- Unsupported save formats remain unsupported until their format and required data have been verified.
- Raw save offsets are not introduced without documented verification.

## [0.1.0] - 2026-09-20

The Python baseline, stabilized and tagged `v0.1.0-python-baseline`. Retired in 0.2.0.

### Added

- Modular game definitions through `GameRegistry`.
- Normalized `GameState` model for game data.
- Eden process detection.
- Game detection from the Eden window title.
- Discord Rich Presence integration through PyPresence.
- Configurable Discord update interval.
- Configurable game artwork and region information.
- Elapsed session time in Discord Rich Presence.
- Eden save-path resolution for supported games.
- Python save-reader wrapper for the .NET bridge.
- C# `PokemonSaveReader` bridge.
- PKHeX.Core integration for save-file parsing.
- Pokémon Legends: Arceus save parsing.
- Pokémon Scarlet save parsing.
- Pokémon Violet game configuration placeholder.
- Pokémon Legends: Z-A game configuration placeholder.
- Trainer information extraction.
- Playtime extraction.
- Pokédex seen/caught data extraction where supported.
- Documentation covering architecture, development, configuration, save reading, game support, Discord RPC, troubleshooting, roadmap, and PKHeX.

### Changed

- Refactored the application into separate game, save-reader, and RPC modules.
- Moved game metadata out of the main application loop and into configuration.
- Changed save parsing from experimental direct inspection to PKHeX.Core-backed reading.
- Kept the PKHeX source tree local and excluded it from the public repository.
- Clarified `dev.py` error messages: a failing command now echoes the failed command, a missing executable reports `Executable not found` with a PATH hint, and a failing Python tool suggests installing `requirements-dev.txt`.
- Normalized repository line endings to LF through `.gitattributes`, so Ruff format checks behave consistently on Windows checkouts regardless of `core.autocrlf`.
- Configured Pyright to resolve the project virtual environment explicitly, fixing a false-positive import error for `pypresence` and missing-source warnings for `psutil` and `pywin32`.

### Fixed

- Eden game detection now works from the emulator window title when the process command line does not expose the running game.
- Discord Rich Presence can reconnect after a connection/update failure.
- Discord Rich Presence is cleared when the detected game closes.
- Save-reader failures are handled without terminating the main Python application.

## Versioning Guidelines

Use the following general categories when preparing release notes:

- **Added** — new functionality.
- **Changed** — modifications to existing functionality.
- **Deprecated** — functionality that will be removed in a future release.
- **Removed** — functionality that has been removed.
- **Fixed** — bug fixes.
- **Security** — security-related changes.

Version numbers should follow:

```text
MAJOR.MINOR.PATCH
```

For example:

```text
0.1.0
0.2.0
0.2.1
1.0.0
```

Until the first stable release, breaking changes may be reflected through minor-version increments according to the project's release policy.

## Unreleased Changes

Changes should remain under `[Unreleased]` during development. When a release is prepared:

1. Review all entries.
2. Remove obsolete or duplicate entries.
3. Group entries by category.
4. Create the new version section with its release date.
5. Start a fresh `[Unreleased]` section.
6. Ensure the changelog matches the actual repository state.

[unreleased]: https://github.com/ramadhafidz/switch-rpc/compare/v0.2.0-dotnet-core...HEAD
[0.2.0]: https://github.com/ramadhafidz/switch-rpc/compare/v0.1.0-python-baseline...v0.2.0-dotnet-core
[0.1.0]: https://github.com/ramadhafidz/switch-rpc/releases/tag/v0.1.0-python-baseline
