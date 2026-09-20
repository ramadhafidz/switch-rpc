using System.Text.Json;
using SwitchRpc.Core;

namespace SwitchRpc.App;

public sealed record AppConfig(
	string ClientId,
	TimeSpan SaveRefreshInterval,
	TimeSpan DexRotationInterval,
	IReadOnlyList<GameDefinition> Games
);

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

	public static AppConfig Load(string configPath)
	{
		using var document = JsonDocument.Parse(
			File.ReadAllText(configPath)
		);

		var root = document.RootElement;

		var discord = root.GetProperty("discord");

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

		var games = new List<GameDefinition>();

		if (root.TryGetProperty("games", out var gamesElement))
		{
			foreach (var game in gamesElement.EnumerateObject())
			{
				if (game.Value.ValueKind != JsonValueKind.Object)
				{
					Console.WriteLine(
						$"config.json: skipping invalid game entry '{game.Name}'."
					);

					continue;
				}

				var displayName = game.Value.TryGetProperty("name", out var name)
					? name.GetString()
					: null;

				var titleId = game.Value.TryGetProperty("title_id", out var title)
					? title.GetString()
					: null;

				var emulator = game.Value.TryGetProperty("emulator", out var emulatorElement)
					? emulatorElement.GetString()
					: null;

				var imageKey = game.Value.TryGetProperty("large_image", out var image)
					? image.GetString()
					: null;

				if (string.IsNullOrWhiteSpace(displayName)
					|| string.IsNullOrWhiteSpace(titleId)
					|| string.IsNullOrWhiteSpace(imageKey)
					|| string.IsNullOrWhiteSpace(emulator))
				{
					Console.WriteLine(
						$"config.json: skipping game '{game.Name}' —"
						+ " 'name', 'title_id', 'large_image' and 'emulator'"
						+ " are required."
					);

					continue;
				}

				var region = game.Value.TryGetProperty("region", out var regionElement)
					? regionElement.GetString() ?? ""
					: "";

				var imageText = game.Value.TryGetProperty("large_text", out var textElement)
					? textElement.GetString() ?? ""
					: "";

				games.Add(new GameDefinition(
					game.Name,
					displayName,
					emulator,
					titleId,
					imageKey,
					region,
					imageText
				));
			}
		}

		if (games.Count == 0)
		{
			throw new InvalidOperationException(
				"config.json: no usable game definitions found."
			);
		}

		return new AppConfig(clientId, saveRefresh, dexRotation, games);
	}
}
