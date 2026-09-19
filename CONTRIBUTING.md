# Contributing

It's awesome that you are interested in contributing, thanks :heart:! This guide explains our internal processes and how we can work together to the best of our ability.

SWITCH RPC is a modular, read-only Discord Rich Presence application. Contributions are welcome, especially improvements to game detection, save reading, Discord Rich Presence, documentation, and testing.

## 📑 How to contribute

There are several ways to contribute to the project:

- [Reporting bugs](#reporting-bugs)
- [Requesting features](#requesting-features)
- [Making pull requests](#making-pull-requests)
- [Development Setup](#development-setup)
- [Architecture Guidelines](#architecture-guidelines)
- [Save Reader Rules (Important)](#save-reader-rules)
- [Other ways of contributing](#other-ways-of-contributing)

---

### Reporting bugs

If you have found a bug, you can report it using the GitHub issues. Before reporting your bug, please do the following checks:

1. Update your local repository to the latest `main` branch. Maybe your bug has already been fixed.
2. Verify if the bug has not been previously reported by someone else (search the existing issues).

If the bug has not been resolved, create a new issue. In the description, please include:
- Operating system and .NET SDK version.
- Eden version and Pokémon game (with update version).
- Relevant application logs (from the console).
- Steps to reproduce the issue.

> **⚠️ IMPORTANT:** Do **not** upload or attach personal save files (`.sav`, `.bin`) unless they have been strictly sanitized and contain no sensitive information. 

### Requesting features

Time is a limited resource, so some features have higher priority than others. To suggest a new feature or improvement:

1. Make sure your idea is not already being addressed in our [ROADMAP.md](docs/ROADMAP.md).
2. Make sure the idea is not already listed in the issues.
3. If it's a new Pokémon game support, ensure it's possible to verify the save structure (we **never** guess offsets).

Create a new issue describing the enhancements. Use the label **enhancement** or **feature**.

### Making pull requests

Once you have a relatively clear plan of action, you can contribute code. 

Before you open your PR (pull request) make sure that:
- Your PR solves a single issue. If you want to do more than one thing, split it into multiple PRs.
- Your code is functional and passes local checks (`dotnet build`, `dotnet test`).
- You have followed the **Save Reader Rules** (see below).
- The messages from your commits are clear (we prefer Conventional Commits, e.g., `feat(save-reader): add Scarlet save support`).

**Pull Request Checklist:**
- [ ] The project builds and `dotnet test` passes.
- [ ] Save-reader changes were tested against a compatible save when possible.
- [ ] No save files or personal data were committed.
- [ ] No guessed save offsets were introduced.
- [ ] Save parsing remains strictly **read-only**.
- [ ] `third_party/PKHeX/` remains untracked/ignored.

---

## Development Setup

### Requirements

The project currently uses:

- Windows
- .NET 10 SDK
- Git
- Eden emulator
- Discord desktop application for Rich Presence testing

The application references `PKHeX.Core` for Pokémon save parsing.

### Clone the Repository

```powershell
git clone <repository-url>
cd switch-rpc
```

### Build the Solution

```powershell
dotnet build SwitchRpc.slnx
```

### PKHeX Setup

PKHeX is used as the save-format implementation layer through `PKHeX.Core`.

The PKHeX source is intentionally kept outside the public repository. See [docs/PKHeX.md](docs/PKHeX.md) for the architecture, supported formats, and setup details.

The local PKHeX checkout is located at:

```text
third_party/PKHeX/
```

It is referenced by `SwitchRpc.Games.Pokemon` through a ProjectReference:

```text
third_party/PKHeX/PKHeX.Core/PKHeX.Core.csproj
```

Do not commit the local `third_party/PKHeX/` source tree.

## Project Structure

```text
switch-rpc/
├── SwitchRpc.slnx
├── config.json
├── .gitignore
├── src/
│   ├── SwitchRpc.App/            # console host, monitoring loop
│   ├── SwitchRpc.Core/           # normalized state, game definitions
│   ├── SwitchRpc.Discord/        # Discord Rich Presence wrapper
│   ├── SwitchRpc.Emulators.Eden/ # Eden detection + save location
│   └── SwitchRpc.Games.Pokemon/  # PKHeX save readers
├── tests/
│   └── SwitchRpc.Tests/          # xUnit tests
├── third_party/
│   └── PKHeX/                    # Local only, ignored by Git
└── docs/
```

The main responsibilities are:

- `SwitchRpc.App` — application loop, configuration, and orchestration.
- `SwitchRpc.Core` — normalized `GameState`, game definitions, presence formatting.
- `SwitchRpc.Games.Pokemon` — PKHeX-based save readers behind a facade.
- `SwitchRpc.Emulators.Eden` — Eden process/window detection and save location.
- `SwitchRpc.Discord` — Discord Rich Presence integration.
- `docs/` — project architecture and development documentation.

## Code Style

### C#

Use clear, small, focused types and methods.

Follow the existing style:

- **Tabs for indentation**
- PascalCase for types and members, camelCase for locals.
- Records for immutable data.
- Prefer explicit names over abbreviations.
- Avoid unnecessary abstractions.
- Handle expected runtime failures without crashing the application.

### Architecture Guidelines

The project follows this general flow:

```text
Eden
  ↓
SwitchRpc.Emulators.Eden (detection + save location)
  ↓
SwitchRpc.Games.Pokemon (PKHeX save readers)
  ↓
SwitchRpc.Core (GameState)
  ↓
SwitchRpc.Discord (RPC)
```

When adding functionality:

1. Determine which project owns the responsibility.
2. Keep game-specific behavior in `SwitchRpc.Games.Pokemon`.
3. Keep Discord-specific behavior in `SwitchRpc.Discord`.
4. Keep `SwitchRpc.Core` free of external dependencies.
5. Keep the application loop focused on orchestration.

## Adding a New Game

When adding support for a new Pokémon game:

1. Add the game definition to `config.json` (with `title_id`).
2. Verify that the Eden window title matches the configured display name.
3. Verify that PKHeX supports the corresponding save format.
4. Add a save reader in `SwitchRpc.Games.Pokemon` when the format is verified.
5. Map the save data into the normalized `GameState`.
6. Add tests using a valid save file when possible.
7. Update [docs/GAME-SUPPORT.md](docs/GAME-SUPPORT.md).
8. Update [docs/SAVE-READER.md](docs/SAVE-READER.md) if save-reading behavior changes.
9. Update the relevant roadmap or documentation when appropriate.

Do not add guessed save offsets.

If a save format has not been verified, leave the implementation unsupported rather than returning potentially incorrect data.

## Save Reader Rules

Save parsing is one of the most important parts of this project.

### Read-only

The application must never modify a user's save file.

Do not:

- write to save files;
- patch save data;
- alter Pokémon data;
- modify inventory or progression;
- modify Pokédex data;
- create or inject Pokémon;
- overwrite emulator saves.

### Prefer PKHeX.Core

When PKHeX already provides access to the required data, use PKHeX.Core rather than implementing raw binary parsing.

This keeps format-specific knowledge inside the established save implementation and reduces the risk of incorrect offsets.

### Verify Data

Before exposing a field through the application:

- verify that the field exists in the supported save format;
- verify its meaning;
- verify the relevant PKHeX API;
- test against a real compatible save when possible.

If the meaning of a field is uncertain, do not present it as authoritative.

### Avoid Hardcoded Offsets

Do not introduce raw offsets simply because a value can be found at a particular byte position in one save.

Game updates and save-format differences can invalidate offsets.

If direct binary access is genuinely required, document:

- the game/version;
- the save format;
- the source of the offset;
- the expected data type;
- known version limitations;
- validation performed against real saves.

## Testing

Tests should be added when behavior changes.

Useful areas to test include:

- Eden process detection.
- Game detection from the Eden window title.
- Save-path resolution.
- Save-format detection.
- Save parsing.
- Game state normalization.
- Presence formatting.
- Discord RPC behavior.

Run the suite with `dotnet test SwitchRpc.slnx`.

Do not commit real personal save files.

For local testing, keep emulator save data outside Git-tracked paths or use the ignored save patterns already defined in `.gitignore`.

## Discord RPC

Discord Rich Presence should remain isolated in `src/SwitchRpc.Discord`.

When changing RPC behavior:

- preserve graceful reconnect behavior;
- clear the presence when the game closes;
- avoid updating more often than necessary;
- keep artwork identifiers configurable;
- do not make Discord credentials or client IDs part of source code unnecessarily.

If a change affects user-visible Rich Presence fields, update [docs/DISCORD-RPC.md](docs/DISCORD-RPC.md).

## Configuration

User-specific configuration should not be hardcoded into source code.

Use `config.json` for project configuration such as:

- Discord application settings;
- update interval;
- supported game definitions;
- artwork identifiers;
- displayed game/region information.

Do not commit private credentials, tokens, personal save files, or local machine-specific secrets.

See [docs/CONFIGURATION.md](docs/CONFIGURATION.md).

## Documentation

All repository documentation should be written in **English**.

Documentation should explain not only what the code does, but also why the architecture works that way.

When changing a system component, check whether the following documents need updates:

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)
- [docs/SAVE-READER.md](docs/SAVE-READER.md)
- [docs/GAME-SUPPORT.md](docs/GAME-SUPPORT.md)
- [docs/CONFIGURATION.md](docs/CONFIGURATION.md)
- [docs/DISCORD-RPC.md](docs/DISCORD-RPC.md)
- [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)
- [docs/ROADMAP.md](docs/ROADMAP.md)
- [docs/PKHeX.md](docs/PKHeX.md)

## Git Guidelines

Keep commits focused on one logical change.

Recommended commit format:

```text
type(scope): short description
```

Examples:

```text
feat(save-reader): add Scarlet save support
fix(detector): handle missing Eden window
refactor(rpc): isolate reconnect handling
docs(pkhex): document supported save formats
test(detector): add game title detection cases
```

Avoid commits that combine unrelated features, refactors, and documentation changes unless they are part of the same logical change.

## Pull Requests

A pull request should explain:

1. What changed.
2. Why it changed.
3. Which games or components are affected.
4. How the change was tested.
5. Whether documentation was updated.
6. Whether the change depends on a specific PKHeX version or save format.

For save-reader changes, include enough technical information for another developer to reproduce and verify the behavior.

### Pull Request Checklist

Before submitting:

- [ ] The project builds successfully.
- [ ] The project builds and `dotnet test` passes.
- [ ] Relevant tests pass.
- [ ] Save-reader changes were tested against a compatible save when possible.
- [ ] No save files or personal data were committed.
- [ ] No guessed save offsets were introduced.
- [ ] Save parsing remains read-only.
- [ ] Discord RPC behavior was checked when relevant.
- [ ] Documentation was updated when needed.
- [ ] `third_party/PKHeX/` remains untracked/ignored.
- [ ] The change is focused and clearly described.

## Reporting Bugs

When reporting a bug, include:

- Operating system.
- .NET SDK version.
- Eden version.
- Pokémon game and version/update.
- Relevant application logs.
- Steps to reproduce.
- Expected behavior.
- Actual behavior.

Do not upload or attach personal save files unless they have been sanitized and you have verified that they contain no sensitive information.

See [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) for common problems.

## Security and Privacy

This project interacts with local emulator processes and save files.

Contributors must not add functionality that:

- accesses Nintendo online services;
- bypasses DRM or platform security;
- circumvents emulator security mechanisms;
- uploads save files without explicit user action;
- collects unnecessary personal information;
- modifies user saves without explicit project requirements and review.

For security-sensitive issues, avoid publishing private data or credentials in public issues or pull requests.

## Scope of Contributions

Useful contributions include:

- New supported Pokémon games.
- Improved Eden detection.
- Better save-path resolution.
- Additional verified save data.
- Improved Discord Rich Presence.
- Tests.
- Documentation.
- Performance improvements.
- Error handling and reliability improvements.
- Packaging and developer tooling.

Large architectural changes should be discussed before implementation so they can be evaluated against the project's current design.

## License

By contributing, you agree that your contribution may be distributed under the project's license.

If the repository does not yet contain a finalized license, confirm the intended license before publishing contribution terms that depend on it.
