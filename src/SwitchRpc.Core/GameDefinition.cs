namespace SwitchRpc.Core;

public sealed record GameDefinition(
	string Id,
	string DisplayName,
	string TitleId,
	string ImageKey
);
