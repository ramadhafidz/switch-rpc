using PKHeX.Core;
using SwitchRpc.Poc.State;

namespace SwitchRpc.Poc.Saves;

/// <summary>
/// A save reader that turns an identified PKHeX save into a normalized
/// GameState. Mirrors the bridge's ISaveReader dispatch pattern.
/// </summary>
public interface ISaveStateReader
{
	bool CanRead(SaveFile save);

	GameState Read(SaveFile save, string gameId);
}
