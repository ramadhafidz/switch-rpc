namespace SwitchRpc.Core;

/// <summary>
/// The result of one emulator poll: whether the emulator process is
/// running and which configured game its window is showing, if any.
/// </summary>
public sealed record EmulatorState(
	bool Running,
	GameDefinition? Game
);

/// <summary>
/// One emulator integration: process and window detection, running-game
/// identification, and save location. Implemented per emulator (for
/// example EdenAdapter); the host loop only knows this contract.
/// </summary>
public interface IEmulatorAdapter
{
	/// <summary>
	/// Stable adapter identifier, matched against GameDefinition.Emulator.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Performs a single process scan and reports the running state and
	/// the detected game, if any.
	/// </summary>
	EmulatorState Poll();

	/// <summary>
	/// Resolves the save file path for a game this adapter hosts, or null
	/// when no save file exists.
	/// </summary>
	string? LocateSave(GameDefinition game);
}
