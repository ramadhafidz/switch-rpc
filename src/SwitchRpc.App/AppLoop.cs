using System.Diagnostics;
using SwitchRpc.Core;
using SwitchRpc.Discord;
using SwitchRpc.Emulators.Eden;
using SwitchRpc.Games.Pokemon;

namespace SwitchRpc.App;

/// <summary>
/// The live RPC loop. Polls Eden, refreshes save data on an interval,
/// rotates Pokédex pages, and updates Discord only when displayed state
/// changes.
/// </summary>
public sealed class AppLoop
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

	private readonly EdenDetector _detector;
	private readonly EdenSaveLocator _locator = new();
	private readonly PokemonSaveReader _saveReader = new();
	private readonly PresenceClient _rpc;

	private readonly TimeSpan _saveRefreshInterval;
	private readonly TimeSpan _dexRotationInterval;

	private GameDefinition? _currentGame;
	private GameState? _currentState;
	private IReadOnlyList<string> _pokedexPages = [];
	private int _currentPage;
	private (string, string, string, string, string)? _previousPresence;
	private bool _cancelRequested;

	public AppLoop(AppConfig config)
	{
		_detector = new EdenDetector(config.Games);
		_rpc = new PresenceClient(config.ClientId);
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

		TimeSpan? lastSaveRefresh = null;
		TimeSpan? lastRotation = null;
		var stopwatch = Stopwatch.StartNew();

		try
		{
			while (!_cancelRequested)
			{
				var now = stopwatch.Elapsed;

				var game = _detector.DetectGame();

				if (game is null)
				{
					if (_currentGame is not null)
					{
						Console.WriteLine("Game: none");
						ResetPresence();
					}

					SleepPoll();
					continue;
				}

				if (_currentGame is null || game.Id != _currentGame.Id)
				{
					Console.WriteLine($"Game detected: {game.DisplayName}");

					_currentGame = game;
					_currentState = null;
					_previousPresence = null;
					_pokedexPages = [];
					_currentPage = 0;
					lastSaveRefresh = null;
					lastRotation = now;
				}

				if (lastSaveRefresh is null
					|| now - lastSaveRefresh.Value >= _saveRefreshInterval)
				{
					var state = ReadGameState(game);

					if (state is not null)
					{
						_currentState = state;
						_pokedexPages = PresenceFormatter.FormatPokedexPages(state);

						if (_currentPage >= _pokedexPages.Count)
							_currentPage = 0;

						Console.WriteLine("Save data refreshed.");
					}

					lastSaveRefresh = now;
				}

				if (_pokedexPages.Count > 0
					&& (lastRotation is null
						|| now - lastRotation.Value >= _dexRotationInterval))
				{
					_currentPage = (_currentPage + 1) % _pokedexPages.Count;
					lastRotation = now;
				}

				UpdatePresence(_pokedexPages);

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

	private GameState? ReadGameState(GameDefinition game)
	{
		var savePath = _locator.Locate(game.TitleId);

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

		Console.WriteLine("PKHeX could not identify the save file.");
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
				SleepPoll();
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
