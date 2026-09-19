namespace SwitchRpc.Core;

/// <summary>
/// A supported game: identification data plus Rich Presence presentation
/// values. Instances are built from config.json by the application host;
/// display names and artwork are configuration, not code.
/// </summary>
public sealed record GameDefinition(
	string Id,
	string DisplayName,
	string TitleId,
	string ImageKey,
	string Region = "",
	string ImageText = ""
);
