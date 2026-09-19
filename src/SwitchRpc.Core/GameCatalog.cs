namespace SwitchRpc.Core;

/// <summary>
/// Temporary catalog, ported from the POC. Batch B of the Phase 2
/// migration replaces this with config.json-driven definitions so display
/// names and artwork are no longer hardcoded.
/// </summary>
public static class GameCatalog
{
	public static readonly IReadOnlyList<GameDefinition> Games =
	[
		new("pokemon_legends_arceus", "Pokémon Legends: Arceus", "01001F5010DFA000", "arceus"),
		new("pokemon_scarlet", "Pokémon Scarlet", "0100A3D008C5C000", "scarlet"),
		new("pokemon_violet", "Pokémon Violet", "01008F6008C5E000", "violet"),
		new("pokemon_legends_za", "Pokémon Legends: Z-A", "0100F43008C44000", "za"),
	];

	public static GameDefinition? FindByWindowTitle(string title)
	{
		foreach (var game in Games)
		{
			if (title.Contains(game.DisplayName, StringComparison.Ordinal))
				return game;
		}

		return null;
	}
}
