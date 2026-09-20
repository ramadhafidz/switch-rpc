using PKHeX.Core;
using SwitchRpc.Core;

namespace SwitchRpc.Games.Pokemon;

/// <summary>
/// Facade over the Pokémon save readers. Loads a save with PKHeX, picks the
/// matching reader, and returns a normalized result — callers never touch
/// PKHeX types directly.
///
/// The base interface is fully qualified because PKHeX.Core also declares
/// an ISaveReader; the Core contract is the one implemented here.
/// </summary>
public sealed class PokemonSaveReader : SwitchRpc.Core.ISaveReader
{
	private readonly ISaveStateReader[] _readers =
	[
		new SvSaveReader(),
		new PlaSaveReader(),
	];

	/// <summary>
	/// Returns the PKHeX save type name for a save file, or null when the
	/// file cannot be identified.
	/// </summary>
	public string? Identify(string savePath)
	{
		return SaveUtil.GetSaveFile(savePath)?.GetType().Name;
	}

	public SaveReadResult Read(string savePath, string gameId)
	{
		var save = SaveUtil.GetSaveFile(savePath);

		if (save is null)
			return new SaveReadResult(null, null, Supported: false);

		var reader = _readers.FirstOrDefault(x => x.CanRead(save));

		if (reader is null)
			return new SaveReadResult(null, save.GetType().Name, Supported: false);

		return new SaveReadResult(reader.Read(save, gameId), save.GetType().Name, Supported: true);
	}
}
