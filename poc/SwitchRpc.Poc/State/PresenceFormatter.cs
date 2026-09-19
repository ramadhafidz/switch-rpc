namespace SwitchRpc.Poc.State;

public static class PresenceFormatter
{
	public static IReadOnlyList<string> FormatPokedexPages(GameState state)
	{
		if (state.Pokedex.Count == 0)
			return ["Pokédex: —"];

		var pages = new List<string>();

		foreach (var (name, stats) in state.Pokedex)
		{
			var display = char.ToUpperInvariant(name[0]) + name[1..];

			pages.Add($"{display}: {stats.Caught}/{stats.Total}");
		}

		return pages;
	}
}
