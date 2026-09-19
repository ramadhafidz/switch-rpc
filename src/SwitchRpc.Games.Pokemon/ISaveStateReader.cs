using PKHeX.Core;
using SwitchRpc.Core;

namespace SwitchRpc.Games.Pokemon;

/// <summary>
/// A save reader that turns an identified PKHeX save into a normalized
/// GameState.
/// </summary>
public interface ISaveStateReader
{
	bool CanRead(SaveFile save);

	GameState Read(SaveFile save, string gameId);
}
