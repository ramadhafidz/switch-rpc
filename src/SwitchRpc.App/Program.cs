using SwitchRpc.App;
using SwitchRpc.Core;

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

var (clientId, saveRefresh, dexRotation) = ConfigLocator.Load(configPath);

return new AppLoop(clientId, saveRefresh, dexRotation).Run();
