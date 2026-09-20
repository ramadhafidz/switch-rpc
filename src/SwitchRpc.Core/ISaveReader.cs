namespace SwitchRpc.Core;

/// <summary>
/// The outcome of one save read: a normalized state when the format is
/// supported, plus identification details when it is known but
/// unsupported.
/// </summary>
public sealed record SaveReadResult(
	GameState? State,
	string? SaveTypeName,
	bool Supported
);

/// <summary>
/// Reads a save file into a normalized GameState. Implemented by
/// game-specific reader facades; consumers never see game-specific
/// types.
/// </summary>
public interface ISaveReader
{
	SaveReadResult Read(string savePath, string gameId);
}
