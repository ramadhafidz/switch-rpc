using System.Text.Json;

namespace SwitchRpc.App;

public static class ConfigLocator
{
	/// <summary>
	/// Finds the repository config.json by walking upward from the current
	/// directory and the application directory.
	/// </summary>
	public static string? Find()
	{
		foreach (var root in new[]
		{
			Directory.GetCurrentDirectory(),
			AppContext.BaseDirectory
		})
		{
			var directory = new DirectoryInfo(root);

			while (directory is not null)
			{
				var candidate = Path.Combine(
					directory.FullName,
					"config.json"
				);

				if (File.Exists(candidate))
					return candidate;

				directory = directory.Parent;
			}
		}

		return null;
	}

	public static (string ClientId, TimeSpan SaveRefresh, TimeSpan DexRotation) Load(
		string configPath
	)
	{
		using var document = JsonDocument.Parse(
			File.ReadAllText(configPath)
		);

		var discord = document.RootElement.GetProperty("discord");

		var clientId = discord
			.GetProperty("client_id")
			.GetString() ?? throw new InvalidOperationException(
			"config.json: discord.client_id is missing."
		);

		var saveRefresh = TimeSpan.FromSeconds(
			discord.TryGetProperty("save_refresh_interval", out var save)
				? save.GetDouble()
				: 15
		);

		var dexRotation = TimeSpan.FromSeconds(
			discord.TryGetProperty("pokedex_rotation_interval", out var dex)
				? dex.GetDouble()
				: 5
		);

		return (clientId, saveRefresh, dexRotation);
	}
}
