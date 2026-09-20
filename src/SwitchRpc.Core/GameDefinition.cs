namespace SwitchRpc.Core;

/// <summary>
/// A supported game: identification data plus Rich Presence presentation
/// values. Instances are built from config.json by the application host;
/// display names and artwork are configuration, not code. Emulator names
/// the adapter that hosts this game (matched against
/// IEmulatorAdapter.Name).
/// </summary>
public sealed record GameDefinition(
	string Id,
	string DisplayName,
	string Emulator,
	string TitleId,
	string ImageKey,
	string Region = "",
	string ImageText = ""
);
