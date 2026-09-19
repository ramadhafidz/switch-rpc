using System.Diagnostics;
using SwitchRpc.Core;
using SwitchRpc.Emulators.Eden;
using SwitchRpc.Games.Pokemon;

namespace SwitchRpc.App;

/// <summary>
/// One-shot diagnostic run that exercises the whole pipeline without
/// requiring Eden or Discord to be running, printing the metrics recorded
/// in poc/BENCHMARKS.md.
/// </summary>
public static class Diagnose
{
	public static int Run()
	{
		Console.WriteLine("SWITCH RPC — diagnose");
		Console.WriteLine();

		PrintProcessMetrics();

		Console.WriteLine();

		PrintGameDetectionMetrics();

		Console.WriteLine();

		PrintSaveMetrics();

		Console.WriteLine();
		Console.WriteLine("Diagnose complete.");
		return 0;
	}

	private static void PrintProcessMetrics()
	{
		var process = Process.GetCurrentProcess();

		Console.WriteLine($"PID: {process.Id}");
		Console.WriteLine($"Working set: {MiB(process.WorkingSet64):F1} MiB");
		Console.WriteLine($"Private memory: {MiB(process.PrivateMemorySize64):F1} MiB");
		Console.WriteLine($"Managed heap: {GC.GetTotalMemory(false) / 1024.0:F0} KiB");
		Console.WriteLine($"Threads: {process.Threads.Count}");
	}

	private static void PrintGameDetectionMetrics()
	{
		var detector = new EdenDetector();

		var stopwatch = Stopwatch.StartNew();
		var game = detector.DetectGame();
		stopwatch.Stop();

		Console.WriteLine(game is null
			? "Game detection: Eden not running or no supported game window"
			: $"Game detection: {game.DisplayName}"
		);

		Console.WriteLine(
			$"Game detection time: {stopwatch.Elapsed.TotalMilliseconds:F1} ms"
		);
	}

	private static void PrintSaveMetrics()
	{
		var locator = new EdenSaveLocator();
		var reader = new PokemonSaveReader();

		foreach (var definition in GameCatalog.Games)
		{
			var savePath = locator.Locate(definition.TitleId);

			if (savePath is null)
			{
				Console.WriteLine($"{definition.DisplayName}: no save found");
				continue;
			}

			Console.WriteLine(
				$"{definition.DisplayName}: save found ({savePath})"
			);

			var stopwatch = Stopwatch.StartNew();
			var typeName = reader.Identify(savePath);
			stopwatch.Stop();

			if (typeName is null)
			{
				Console.WriteLine("  PKHeX could not identify the save file.");
				continue;
			}

			Console.WriteLine(
				$"  PKHeX identification: {typeName}"
				+ $" in {stopwatch.Elapsed.TotalMilliseconds:F1} ms"
			);

			stopwatch.Restart();
			var result = reader.Read(savePath, definition.Id);
			stopwatch.Stop();

			Console.WriteLine(
				$"  Save parse: {stopwatch.Elapsed.TotalMilliseconds:F2} ms"
			);

			var state = result.State;

			if (state is null)
			{
				Console.WriteLine("  Save reading: no reader for this format yet.");
				continue;
			}

			Console.WriteLine($"  Game: {state.GameId}");
			Console.WriteLine($"  Playtime: {state.PlaytimeSeconds} s");
			Console.WriteLine(
				$"  Location: {state.LocationName ?? "—"} (id {state.LocationId})"
			);

			foreach (var (name, stats) in state.Pokedex)
			{
				Console.WriteLine(
					$"  Pokédex {name}: {stats.Seen} seen /"
					+ $" {stats.Caught} caught / {stats.Total} total"
				);
			}
		}
	}

	private static double MiB(long bytes)
	{
		return bytes / 1024.0 / 1024.0;
	}
}
