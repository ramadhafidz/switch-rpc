using System.Diagnostics;
using SwitchRpc.Poc.Detection;
using SwitchRpc.Poc.Discord;
using SwitchRpc.Poc.Saves;
using SwitchRpc.Poc.State;

namespace SwitchRpc.Poc;

/// <summary>
/// The live RPC loop — the .NET counterpart of Python main.py. Polls Eden,
/// refreshes save data on an interval, rotates Pokédex pages, and updates
/// Discord only when displayed state changes.
/// </summary>
public sealed class AppLoop
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

	private readonly EdenDetector _detector = new();
	private readonly EdenSaveLocator _locator = new();
	private readonly ISaveStateReader[] _readers =
	[
		new SvSaveReader(),
		new PlaSaveReader(),
	];
	private readonly PresenceClient _rpc;

	private readonly TimeSpan _saveRefreshInterval;
	private readonly TimeSpan _dexRotationInterval;

	private GameDefinition? _currentGame;
	private GameState? _currentState;
	private IReadOnlyList<string> _pokedexPages = [];
	private int _currentPage;
	private (string, string, string, string, string)? _previousPresence;
	private bool _cancelRequested;

	public AppLoop(
		string clientId,
		TimeSpan saveRefreshInterval,
		TimeSpan dexRotationInterval
	)
	{
		_rpc = new PresenceClient(clientId);
		_saveRefreshInterval = saveRefreshInterval;
		_dexRotationInterval = dexRotationInterval;
	}

	public int Run()
	{
		Console.CancelKeyPress += (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			_cancelRequested = true;
		};

		Console.WriteLine("SWITCH RPC POC started.");
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

				UpdatePresence(game, _pokedexPages);

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

		var save = PKHeX.Core.SaveUtil.GetSaveFile(savePath);

		if (save is null)
		{
			Console.WriteLine("PKHeX could not identify the save file.");
			return null;
		}

		var reader = _readers.FirstOrDefault(x => x.CanRead(save));

		if (reader is null)
		{
			Console.WriteLine(
				$"Save format {save.GetType().Name} is not implemented in the POC;"
				+ " falling back to identity-only presence."
			);

			return new GameState(game.Id, null, null, null, new Dictionary<string, DexStats>());
		}

		return reader.Read(save, game.Id);
	}

	private void UpdatePresence(GameDefinition game, IReadOnlyList<string> pages)
	{
		if (_currentState is null || pages.Count == 0)
			return;

		var page = pages[_currentPage];
		var details = "Pokédex";

		var presence = (game.DisplayName, details, page, game.ImageKey, game.DisplayName);

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

		if (_rpc.Update(details, page, game.ImageKey, game.DisplayName))
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
