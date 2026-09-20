using SwitchRpc.App;
using SwitchRpc.Core;
using SwitchRpc.Discord;
using SwitchRpc.Emulators.Eden;
using SwitchRpc.Games.Pokemon;

if (args.Contains("--diagnose"))
{
	return Diagnose.Run();
}

var configPath = ConfigLocator.Find();

if (configPath is null)
{
	Console.Error.WriteLine(
		"config.json not found (searched upward from the current"
		+ " directory and the application directory)."
	);

	return 1;
}

var config = ConfigLocator.Load(configPath);

// Composition root: one adapter per supported emulator, each fed the
// games its name matches in config.json.
var adapters = new List<IEmulatorAdapter>();

var edenGames = config.Games
	.Where(game => game.Emulator == "eden")
	.ToList();

if (edenGames.Count > 0)
{
	adapters.Add(new EdenAdapter(edenGames));
}

foreach (var game in config.Games.Where(
	game => adapters.All(adapter => adapter.Name != game.Emulator)
))
{
	Console.Error.WriteLine(
		$"No emulator adapter for game '{game.Id}'"
		+ $" (emulator: '{game.Emulator}'); it will be ignored."
	);
}

if (adapters.Count == 0)
{
	Console.Error.WriteLine("No emulator adapters were configured; nothing to monitor.");
	return 1;
}

return new AppLoop(
	config,
	adapters,
	new PokemonSaveReader(),
	new PresenceClient(config.ClientId)
).Run();
