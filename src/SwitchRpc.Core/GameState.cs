namespace SwitchRpc.Core;

public sealed record DexStats(int Seen, int Caught, int Total);

/// <summary>
/// Normalized game state — the single boundary between save readers and
/// consumers. PKHeX types never cross this type.
/// </summary>
public sealed record GameState(
	string GameId,
	long? PlaytimeSeconds,
	string? LocationName,
	int? LocationId,
	IReadOnlyDictionary<string, DexStats> Pokedex
);
