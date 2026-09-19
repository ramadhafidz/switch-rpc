namespace SwitchRpc.Emulators.Eden;

/// <summary>
/// Resolves Eden save files under
/// %APPDATA%/eden/nand/user/save/&lt;title-id&gt;/main, mirroring the
/// Python save-path resolver.
/// </summary>
public sealed class EdenSaveLocator
{
	private readonly string _saveRoot;

	public EdenSaveLocator()
		: this(DefaultSaveRoot())
	{
	}

	/// <summary>
	/// Allows tests and alternative hosts to point the locator at a
	/// different save root.
	/// </summary>
	public EdenSaveLocator(string saveRoot)
	{
		_saveRoot = saveRoot;
	}

	private static string DefaultSaveRoot()
	{
		return Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"eden",
			"nand",
			"user",
			"save"
		);
	}

	public string? Locate(string titleId)
	{
		if (!Directory.Exists(_saveRoot))
			return null;

		foreach (var directory in Directory.EnumerateDirectories(
			_saveRoot,
			titleId,
			SearchOption.AllDirectories
		))
		{
			var saveFile = Path.Combine(directory, "main");

			if (File.Exists(saveFile))
				return saveFile;
		}

		return null;
	}
}
