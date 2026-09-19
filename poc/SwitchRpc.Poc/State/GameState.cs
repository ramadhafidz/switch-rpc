namespace SwitchRpc.Poc.State;

public sealed record DexStats(int Seen, int Caught, int Total);

/// <summary>
/// Normalized game state for the POC — the .NET counterpart of the Python
/// GameState. PKHeX types never cross this boundary.
/// </summary>
public sealed record GameState(
	string GameId,
	long? PlaytimeSeconds,
	string? LocationName,
	int? LocationId,
	IReadOnlyDictionary<string, DexStats> Pokedex
);
