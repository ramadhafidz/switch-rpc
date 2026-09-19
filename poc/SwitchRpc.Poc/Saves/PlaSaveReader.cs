using PKHeX.Core;
using SwitchRpc.Poc.State;

namespace SwitchRpc.Poc.Saves;

/// <summary>
/// Reads Legends: Arceus saves directly through PKHeX.Core, mirroring the
/// verified bridge implementation (LegendsArceusReader + extractors).
/// Current-location extraction for PLA is intentionally absent: the Python
/// side has not verified it either (see TODO.md).
/// </summary>
public sealed class PlaSaveReader : ISaveStateReader
{
	public bool CanRead(SaveFile save)
	{
		return save is SAV8LA;
	}

	public GameState Read(SaveFile save, string gameId)
	{
		if (save is not SAV8LA pla)
			throw new ArgumentException(
				"Save file is not a Pokémon Legends: Arceus save.",
				nameof(save)
			);

		// The bridge reports PLA seen counts as caught counts; see
		// bridge/PokemonSaveReader/Extractors/PokedexExtractor.cs.
		PokedexSave8a pokedex = pla.Blocks.PokedexSave;
		var caught = pokedex.GetDexGetCount(PokedexType8a.Hisui);

		var stats = new Dictionary<string, DexStats>
		{
			["hisui"] = new(
				caught,
				caught,
				PokedexSave8a.GetDexTotalCount(PokedexType8a.Hisui)
			),
		};

		return new GameState(
			gameId,
			(pla.PlayedHours * 3600L)
				+ (pla.PlayedMinutes * 60L)
				+ pla.PlayedSeconds,
			null,
			null,
			stats
		);
	}
}
