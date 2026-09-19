using SwitchRpc.App;

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

return new AppLoop(ConfigLocator.Load(configPath)).Run();
