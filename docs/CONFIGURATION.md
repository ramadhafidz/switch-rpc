# Configuration

## Overview

SWITCH RPC uses `config.json` as its primary configuration file.

Configuration is intentionally kept separate from the application logic so that game-specific Rich Presence settings and runtime behavior can be changed without modifying Python source code.

The current configuration has two main sections:

```text
config.json
├── discord
└── games
```

---

## Configuration File

The default structure is:

```json
{
	"discord": {
		"client_id": "YOUR_DISCORD_APPLICATION_ID",
		"update_interval": 15
	},
	"games": {
		"pokemon_legends_arceus": {
			"name": "Pokémon Legends: Arceus",
			"region": "Hisui",
			"large_image": "arceus",
			"large_text": "Pokémon Legends: Arceus"
		},
		"pokemon_scarlet": {
			"name": "Pokémon Scarlet",
			"region": "Paldea",
			"large_image": "scarlet",
			"large_text": "Pokémon Scarlet"
		},
		"pokemon_violet": {
			"name": "Pokémon Violet",
			"region": "Paldea",
			"large_image": "violet",
			"large_text": "Pokémon Violet"
		},
		"pokemon_legends_za": {
			"name": "Pokémon Legends: Z-A",
			"region": "Kalos",
			"large_image": "za",
			"large_text": "Pokémon Legends: Z-A"
		}
	}
}
```

---

## Discord Configuration

The `discord` section contains settings related to Discord Rich Presence.

```json
{
	"discord": {
		"client_id": "YOUR_DISCORD_APPLICATION_ID",
		"update_interval": 15
	}
}
```

### `client_id`

The Discord Application ID used by the Rich Presence integration.

Example:

```json
"client_id": "YOUR_DISCORD_APPLICATION_ID"
```

The value should correspond to the Discord application created for this project.

A Discord Application ID is not a secret credential. However, never place Discord tokens, authentication credentials, or private keys in `config.json`.

---

### `update_interval`

Controls how often the main monitoring loop checks the current state.

Example:

```json
"update_interval": 15
```

The value is specified in seconds.

A lower value makes the application check more frequently, while a higher value reduces the frequency of checks.

The current project uses:

```text
15 seconds
```

as the configured interval.

The interval affects the monitoring loop and should be kept reasonable to avoid unnecessary process, filesystem, and RPC activity.

---

## Game Configuration

The `games` section contains the configuration for each supported game.

Each game is identified by a stable internal game ID.

Example:

```json
{
	"pokemon_scarlet": {
		"name": "Pokémon Scarlet",
		"region": "Paldea",
		"large_image": "scarlet",
		"large_text": "Pokémon Scarlet"
	}
}
```

The internal ID is:

```text
pokemon_scarlet
```

The ID is used by the application internally and should remain stable.

---

## Game IDs

Current game IDs are:

```text
pokemon_legends_arceus
pokemon_scarlet
pokemon_violet
pokemon_legends_za
```

These IDs are referenced by the game detector and registry.

Do not rename an existing ID casually because other components may depend on it.

---

## Game Fields

### `name`

The display name shown in Discord Rich Presence.

Example:

```json
"name": "Pokémon Scarlet"
```

This is a presentation value and can be changed without changing the internal game ID.

---

### `region`

The Pokémon region associated with the game.

Example:

```json
"region": "Paldea"
```

The current configuration uses:

```text
Pokémon Legends: Arceus → Hisui
Pokémon Scarlet         → Paldea
Pokémon Violet          → Paldea
Pokémon Legends: Z-A   → Kalos
```

This value is currently used when constructing Rich Presence details.

---

### `large_image`

The Discord Rich Presence large image asset key.

Example:

```json
"large_image": "scarlet"
```

The value must correspond to an asset configured in the Discord application.

The asset key is not necessarily the same as the game's display name.

---

### `large_text`

The tooltip shown when hovering over the large Rich Presence image.

Example:

```json
"large_text": "Pokémon Scarlet"
```

This is a presentation value.

---

## Adding a New Game

Adding a game configuration does not automatically provide complete game support.

A new entry can be added like:

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

However, complete support may also require changes to:

```text
games/detector.py
games/save_paths.py
bridge/PokemonSaveReader/Program.cs
games/state.py
```

and corresponding tests and documentation.

See [Game Support](GAME-SUPPORT.md).

---

## Discord Artwork

Discord artwork is configured through the Discord Developer Portal.

The value in `large_image` must match the asset key configured for the Discord application.

Conceptually:

```text
Discord Developer Portal
        │
        ▼
Rich Presence Assets
        │
        ▼
Asset Key
        │
        ▼
config.json
        │
        ▼
DiscordRPC.update()
```

For example:

```json
"large_image": "arceus"
```

requires a corresponding Discord Rich Presence asset key.

---

## Local Configuration

Do not store personal or environment-specific configuration directly in the repository when it should remain local.

The project ignores:

```text
config.local.json
.env
.env.*
```

If future configuration requirements become user-specific, prefer introducing a local configuration mechanism rather than hardcoding values into source files.

---

## Secrets

Do not put secrets in:

```text
config.json
config.local.json
.env
source code
documentation
```

unless the value is intentionally an example placeholder.

Never commit:

- Discord tokens.
- API keys.
- Private keys.
- Passwords.
- Authentication cookies.
- Emulator credentials.

Use environment variables or another appropriate secret-management mechanism if the project ever requires actual secrets.

---

## Configuration and Source Code

Configuration should contain values that are intended to be changed without modifying application logic.

Good candidates:

```text
Discord Application ID
Update interval
Game display name
Game region
Artwork asset key
Artwork tooltip
```

Avoid moving application logic into configuration.

For example, configuration should not contain arbitrary Python code or executable commands.

---

## Validation

The application currently loads `config.json` directly.

When extending the configuration system, prefer explicit validation for:

- Missing required sections.
- Missing game fields.
- Invalid update intervals.
- Invalid data types.
- Unknown configuration values where appropriate.

Configuration errors should produce a clear error message rather than an obscure runtime failure.

---

## Versioning Configuration

When changing the configuration schema in a breaking way:

1. Update the application.
2. Update `README.md`.
3. Update this document.
4. Update relevant tests.
5. Consider providing a migration path.

Do not silently change the meaning of an existing configuration field.

---

## Configuration Design Principles

### Stable IDs

Internal game IDs should remain stable.

### Human-Readable Values

Display names and presentation values should remain easy to understand.

### No Secrets

Configuration files should never contain sensitive credentials.

### Minimal Configuration

Only values that benefit from being configurable should be exposed.

### Backward Compatibility

Avoid unnecessary breaking changes to the configuration schema.

### Separation of Concerns

Configuration controls values; application code controls behavior.

---

## Related Documentation

- [Architecture](ARCHITECTURE.md)
- [Game Support](GAME-SUPPORT.md)
- [Save Reader](SAVE-READER.md)
- [Development Setup](DEVELOPMENT.md)
- [Discord RPC](DISCORD-RPC.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Roadmap](ROADMAP.md)
