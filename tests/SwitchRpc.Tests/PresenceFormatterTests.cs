using SwitchRpc.Core;

namespace SwitchRpc.Tests;

public class PresenceFormatterTests
{
	[Fact]
	public void FormatPokedexPages_EmptyPokedex_ReturnsPlaceholder()
	{
		var state = new GameState(
			"pokemon_scarlet",
			null,
			null,
			null,
			new Dictionary<string, DexStats>()
		);

		var pages = PresenceFormatter.FormatPokedexPages(state);

		var page = Assert.Single(pages);
		Assert.Equal("Pokédex: —", page);
	}

	[Fact]
	public void FormatPokedexPages_CapitalizesDexName()
	{
		var state = new GameState(
			"pokemon_scarlet",
			null,
			null,
			null,
			new Dictionary<string, DexStats>
			{
				["paldea"] = new(33, 22, 400),
			}
		);

		var pages = PresenceFormatter.FormatPokedexPages(state);

		var page = Assert.Single(pages);
		Assert.Equal("Paldea: 22/400", page);
	}

	[Fact]
	public void FormatPokedexPages_PreservesDexOrder()
	{
		var state = new GameState(
			"pokemon_scarlet",
			null,
			null,
			null,
			new Dictionary<string, DexStats>
			{
				["paldea"] = new(33, 22, 400),
				["kitakami"] = new(8, 3, 200),
				["blueberry"] = new(0, 0, 243),
			}
		);

		var pages = PresenceFormatter.FormatPokedexPages(state);

		Assert.Equal(3, pages.Count);
		Assert.Equal("Paldea: 22/400", pages[0]);
		Assert.Equal("Kitakami: 3/200", pages[1]);
		Assert.Equal("Blueberry: 0/243", pages[2]);
	}
}
