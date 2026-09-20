using System.Diagnostics;
using SwitchRpc.Core;

namespace SwitchRpc.App;

/// <summary>
/// The live RPC loop. Polls the configured emulator adapters, refreshes
/// save data on an interval, rotates Pokédex pages, and updates Discord
/// only when displayed state changes. All collaborators are injected so
/// the loop is testable with fakes.
/// </summary>
public sealed class AppLoop
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

	private readonly IReadOnlyList<IEmulatorAdapter> _adapters;
	private readonly ISaveReader _saveReader;
	private readonly IPresenceClient _rpc;

	private readonly TimeSpan _saveRefreshInterval;
	private readonly TimeSpan _dexRotationInterval;

	private readonly Dictionary<string, bool> _previousRunning = [];

	private GameDefinition? _currentGame;
	private GameState? _currentState;
	private IReadOnlyList<string> _pokedexPages = [];
	private int _currentPage;
	private (string, string, string, string, string)? _previousPresence;

	private TimeSpan? _lastSaveRefresh;
	private TimeSpan? _lastRotation;

	private bool _cancelRequested;

	public AppLoop(
		AppConfig config,
		IReadOnlyList<IEmulatorAdapter> adapters,
		ISaveReader saveReader,
		IPresenceClient rpc
	)
	{
		_adapters = adapters;
		_saveReader = saveReader;
		_rpc = rpc;
		_saveRefreshInterval = config.SaveRefreshInterval;
		_dexRotationInterval = config.DexRotationInterval;
	}

	public int Run()
	{
		Console.CancelKeyPress += (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			_cancelRequested = true;
		};

		Console.WriteLine("SWITCH RPC started.");
		Console.WriteLine("Connecting to Discord...");

		if (_rpc.Connect())
			Console.WriteLine("Discord RPC connected.");
		else
			Console.WriteLine("Discord not available yet; will retry on update.");

		var stopwatch = Stopwatch.StartNew();

		try
		{
			while (!_cancelRequested)
			{
				try
				{
					Tick(stopwatch.Elapsed);
				}
				catch (Exception error)
				{
					// A monitoring loop must survive transient failures from
					// the emulator, the filesystem, and IPC. Log and keep
					// polling; programming errors will resurface every tick
					// instead of being hidden.
					Console.WriteLine($"Monitoring loop error: {error.Message}");
				}

				SleepPoll();
			}
		}
		finally
		{
			_rpc.Clear();
			_rpc.Dispose();

			Console.WriteLine("RPC cleared. Goodbye.");
		}

		return 0;
	}

	/// <summary>
	/// One monitoring cycle: poll the adapters, refresh the save when due,
	/// rotate the Pokédex page when due, and update presence when the
	/// displayed state changed. Public so tests can drive it with
	/// synthetic timestamps.
	/// </summary>
	public void Tick(TimeSpan now)
	{
		var detected = PollAdapters();

		if (detected is null)
		{
			if (_currentGame is not null)
			{
				Console.WriteLine("Game: none");
				ResetPresence();
			}

			return;
		}

		var (adapter, game) = detected.Value;

		if (_currentGame is null || game.Id != _currentGame.Id)
		{
			Console.WriteLine($"Game detected: {game.DisplayName}");

			_currentGame = game;
			_currentState = null;
			_previousPresence = null;
			_pokedexPages = [];
			_currentPage = 0;
			_lastSaveRefresh = null;
			_lastRotation = now;
		}

		if (_lastSaveRefresh is null
			|| now - _lastSaveRefresh.Value >= _saveRefreshInterval)
		{
			RefreshSave(adapter, game);

			_lastSaveRefresh = now;
		}

		if (_pokedexPages.Count > 0
			&& (_lastRotation is null
				|| now - _lastRotation.Value >= _dexRotationInterval))
		{
			_currentPage = (_currentPage + 1) % _pokedexPages.Count;
			_lastRotation = now;
		}

		UpdatePresence(_pokedexPages);
	}

	/// <summary>
	/// Polls every adapter once, logging running-state transitions. Returns
	/// the first running adapter with a detected game, or null when no
	/// adapter reports one.
	/// </summary>
	private (IEmulatorAdapter Adapter, GameDefinition Game)? PollAdapters()
	{
		IEmulatorAdapter? activeAdapter = null;
		GameDefinition? game = null;

		foreach (var adapter in _adapters)
		{
			var state = adapter.Poll();

			if (_previousRunning.TryGetValue(adapter.Name, out var wasRunning)
				&& wasRunning != state.Running)
			{
				Console.WriteLine(
					$"{adapter.Name}: {(state.Running ? "running" : "not running")}"
				);
			}

			_previousRunning[adapter.Name] = state.Running;

			if (activeAdapter is null
				&& state.Running
				&& state.Game is not null)
			{
				activeAdapter = adapter;
				game = state.Game;
			}
		}

		return activeAdapter is null || game is null
			? null
			: (activeAdapter, game);
	}

	private void RefreshSave(IEmulatorAdapter adapter, GameDefinition game)
	{
		var state = ReadGameState(adapter, game);

		if (state is null)
			return;

		_currentState = state;
		_pokedexPages = PresenceFormatter.FormatPokedexPages(state);

		if (_currentPage >= _pokedexPages.Count)
			_currentPage = 0;

		Console.WriteLine("Save data refreshed.");
	}

	private GameState? ReadGameState(IEmulatorAdapter adapter, GameDefinition game)
	{
		var savePath = adapter.LocateSave(game);

		if (savePath is null)
		{
			Console.WriteLine($"Save not found for game: {game.Id}");
			return null;
		}

		var result = _saveReader.Read(savePath, game.Id);

		if (result.State is not null)
			return result.State;

		if (result.SaveTypeName is not null)
		{
			Console.WriteLine(
				$"Save format {result.SaveTypeName} has no reader yet;"
				+ " falling back to identity-only presence."
			);

			return new GameState(
				game.Id,
				null,
				null,
				null,
				new Dictionary<string, DexStats>()
			);
		}

		Console.WriteLine("The save reader could not identify the save file.");
		return null;
	}

	private void UpdatePresence(IReadOnlyList<string> pages)
	{
		var game = _currentGame;

		if (_currentState is null || game is null || pages.Count == 0)
			return;

		var page = pages[_currentPage];
		var details = "Pokédex";

		var presence = (game.DisplayName, details, page, game.ImageKey, game.ImageText);

		if (presence == _previousPresence)
			return;

		if (!_rpc.IsConnected)
		{
			Console.WriteLine("Discord RPC disconnected. Reconnecting...");

			if (!_rpc.Connect())
			{
				Console.WriteLine("Failed to connect to Discord.");
				return;
			}
		}

		if (_rpc.Update(details, page, game.ImageKey, game.ImageText))
		{
			Console.WriteLine($"Rich Presence updated: {details} | {page}");
			_previousPresence = presence;
		}
	}

	private void ResetPresence()
	{
		_rpc.Clear();

		_currentGame = null;
		_currentState = null;
		_previousPresence = null;
		_pokedexPages = [];
		_currentPage = 0;
	}

	private static void SleepPoll()
	{
		Thread.Sleep(PollInterval);
	}
}
