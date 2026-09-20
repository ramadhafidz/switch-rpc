using SwitchRpc.App;
using SwitchRpc.Core;
using Xunit;

namespace SwitchRpc.Tests;

/// <summary>
/// Behavioral tests for the host loop, driven through Tick with synthetic
/// timestamps and fake collaborators — no emulator, filesystem, or Discord.
/// </summary>
public sealed class AppLoopTests
{
	private static readonly GameDefinition Scarlet = new(
		"pokemon_scarlet",
		"Pokémon Scarlet",
		"eden",
		"0100A3D008C5C000",
		"scarlet",
		"Paldea",
		"Pokémon Scarlet"
	);

	[Fact]
	public void GameDetected_RefreshesSaveAndUpdatesPresence()
	{
		var adapter = new FakeEmulatorAdapter("eden");
		var reader = new FakeSaveReader();
		var rpc = new FakePresenceClient();
		var loop = CreateLoop(adapter, reader, rpc);

		adapter.SetRunning(true, Scarlet);
		adapter.NextSavePath = @"C:\saves\main";
		reader.NextResult = new SaveReadResult(PaldeaState(), null, true);

		loop.Tick(TimeSpan.Zero);

		Assert.Equal(1, reader.ReadCount);
		Assert.Equal(Scarlet, adapter.LastLocateGame);
		Assert.Equal(1, rpc.UpdateCount);
		Assert.Equal(("Pokédex", "Paldea: 22/400"), rpc.Updates[0]);
	}

	[Fact]
	public void UnchangedState_DoesNotUpdatePresenceAgain()
	{
		var (loop, adapter, reader, rpc) = CreateRunningLoop();

		loop.Tick(TimeSpan.Zero);
		loop.Tick(TimeSpan.FromSeconds(1));

		Assert.Equal(1, reader.ReadCount);
		Assert.Equal(1, rpc.UpdateCount);
	}

	[Fact]
	public void SaveRefreshesOnlyAfterTheConfiguredInterval()
	{
		var (loop, _, reader, _) = CreateRunningLoop();

		loop.Tick(TimeSpan.Zero);
		loop.Tick(TimeSpan.FromSeconds(14));
		Assert.Equal(1, reader.ReadCount);

		loop.Tick(TimeSpan.FromSeconds(15));
		Assert.Equal(2, reader.ReadCount);
	}

	[Fact]
	public void PokedexPagesRotateOnTheConfiguredInterval()
	{
		var adapter = new FakeEmulatorAdapter("eden");
		var reader = new FakeSaveReader();
		var rpc = new FakePresenceClient();
		var loop = CreateLoop(adapter, reader, rpc);

		adapter.SetRunning(true, Scarlet);
		adapter.NextSavePath = @"C:\saves\main";
		reader.NextResult = new SaveReadResult(ThreeRegionState(), null, true);

		loop.Tick(TimeSpan.Zero);
		loop.Tick(TimeSpan.FromSeconds(5));
		loop.Tick(TimeSpan.FromSeconds(10));

		Assert.Equal(
			[
				("Pokédex", "Paldea: 22/400"),
				("Pokédex", "Kitakami: 3/200"),
				("Pokédex", "Blueberry: 0/243"),
			],
			rpc.Updates
		);
	}

	[Fact]
	public void GameExit_ClearsPresenceAndStopsUpdating()
	{
		var (loop, adapter, _, rpc) = CreateRunningLoop();

		loop.Tick(TimeSpan.Zero);

		adapter.SetRunning(false, null);
		loop.Tick(TimeSpan.FromSeconds(1));
		loop.Tick(TimeSpan.FromSeconds(2));

		Assert.Equal(1, rpc.ClearCount);
		Assert.Equal(1, rpc.UpdateCount);
	}

	[Fact]
	public void UnsupportedFormat_FallsBackToIdentityOnlyPresence()
	{
		var adapter = new FakeEmulatorAdapter("eden");
		var reader = new FakeSaveReader();
		var rpc = new FakePresenceClient();
		var loop = CreateLoop(adapter, reader, rpc);

		adapter.SetRunning(true, Scarlet);
		adapter.NextSavePath = @"C:\saves\main";
		reader.NextResult = new SaveReadResult(null, "SAV9ZA", false);

		loop.Tick(TimeSpan.Zero);

		Assert.Equal(1, rpc.UpdateCount);
		Assert.Equal(("Pokédex", "Pokédex: —"), rpc.Updates[0]);
	}

	[Fact]
	public void UnidentifiableSave_DoesNotUpdatePresence()
	{
		var adapter = new FakeEmulatorAdapter("eden");
		var reader = new FakeSaveReader();
		var rpc = new FakePresenceClient();
		var loop = CreateLoop(adapter, reader, rpc);

		adapter.SetRunning(true, Scarlet);
		adapter.NextSavePath = @"C:\saves\main";
		reader.NextResult = new SaveReadResult(null, null, false);

		loop.Tick(TimeSpan.Zero);

		Assert.Equal(0, rpc.UpdateCount);
	}

	[Fact]
	public void SaveNotFound_DoesNotUpdatePresence()
	{
		var adapter = new FakeEmulatorAdapter("eden");
		var reader = new FakeSaveReader();
		var rpc = new FakePresenceClient();
		var loop = CreateLoop(adapter, reader, rpc);

		adapter.SetRunning(true, Scarlet);
		adapter.NextSavePath = null;

		loop.Tick(TimeSpan.Zero);

		Assert.Equal(0, reader.ReadCount);
		Assert.Equal(0, rpc.UpdateCount);
	}

	[Fact]
	public void GameIsHandledByTheAdapterThatReportsIt()
	{
		var other = new FakeEmulatorAdapter("otheremu");
		var eden = new FakeEmulatorAdapter("eden");
		var reader = new FakeSaveReader();
		var rpc = new FakePresenceClient();
		var loop = CreateLoop([other, eden], reader, rpc);

		other.SetRunning(true, null);
		eden.SetRunning(true, Scarlet);
		eden.NextSavePath = @"C:\saves\main";
		reader.NextResult = new SaveReadResult(PaldeaState(), null, true);

		loop.Tick(TimeSpan.Zero);

		Assert.Equal(Scarlet, eden.LastLocateGame);
		Assert.Null(other.LastLocateGame);
		Assert.Equal(1, rpc.UpdateCount);
	}

	[Fact]
	public void DisconnectedPresence_ReconnectsBeforeUpdating()
	{
		var adapter = new FakeEmulatorAdapter("eden");
		var reader = new FakeSaveReader();
		var rpc = new FakePresenceClient { IsConnected = false };
		var loop = CreateLoop(adapter, reader, rpc);

		adapter.SetRunning(true, Scarlet);
		adapter.NextSavePath = @"C:\saves\main";
		reader.NextResult = new SaveReadResult(PaldeaState(), null, true);

		loop.Tick(TimeSpan.Zero);

		Assert.Equal(1, rpc.ConnectCount);
		Assert.Equal(1, rpc.UpdateCount);
	}

	private static AppLoop CreateLoop(
		FakeEmulatorAdapter adapter,
		FakeSaveReader reader,
		FakePresenceClient rpc
	)
	{
		return CreateLoop([adapter], reader, rpc);
	}

	private static AppLoop CreateLoop(
		IReadOnlyList<IEmulatorAdapter> adapters,
		FakeSaveReader reader,
		FakePresenceClient rpc
	)
	{
		return new AppLoop(
			new AppConfig(
				"client-id",
				TimeSpan.FromSeconds(15),
				TimeSpan.FromSeconds(5),
				[Scarlet]
			),
			adapters,
			reader,
			rpc
		);
	}

	private static (AppLoop Loop, FakeEmulatorAdapter Adapter, FakeSaveReader Reader, FakePresenceClient Rpc)
		CreateRunningLoop()
	{
		var adapter = new FakeEmulatorAdapter("eden");
		var reader = new FakeSaveReader();
		var rpc = new FakePresenceClient();
		var loop = CreateLoop(adapter, reader, rpc);

		adapter.SetRunning(true, Scarlet);
		adapter.NextSavePath = @"C:\saves\main";
		reader.NextResult = new SaveReadResult(PaldeaState(), null, true);

		return (loop, adapter, reader, rpc);
	}

	private static GameState PaldeaState()
	{
		return new GameState(
			Scarlet.Id,
			3600,
			"Artazon",
			86,
			new Dictionary<string, DexStats>
			{
				["paldea"] = new(33, 22, 400),
			}
		);
	}

	private static GameState ThreeRegionState()
	{
		return new GameState(
			Scarlet.Id,
			3600,
			"Artazon",
			86,
			new Dictionary<string, DexStats>
			{
				["paldea"] = new(33, 22, 400),
				["kitakami"] = new(8, 3, 200),
				["blueberry"] = new(0, 0, 243),
			}
		);
	}

	private sealed class FakeEmulatorAdapter(string name) : IEmulatorAdapter
	{
		private EmulatorState _state = new(false, null);

		public string Name { get; } = name;

		public string? NextSavePath { get; set; }

		public GameDefinition? LastLocateGame { get; private set; }

		public void SetRunning(bool running, GameDefinition? game)
		{
			_state = new EmulatorState(running, running ? game : null);
		}

		public EmulatorState Poll()
		{
			return _state;
		}

		public string? LocateSave(GameDefinition game)
		{
			LastLocateGame = game;

			return NextSavePath;
		}
	}

	private sealed class FakeSaveReader : ISaveReader
	{
		public SaveReadResult NextResult { get; set; } = new(null, null, false);

		public int ReadCount { get; private set; }

		public SaveReadResult Read(string savePath, string gameId)
		{
			ReadCount++;

			return NextResult;
		}
	}

	private sealed class FakePresenceClient : IPresenceClient
	{
		public bool IsConnected { get; set; } = true;

		public int ConnectCount { get; private set; }

		public int UpdateCount { get; private set; }

		public int ClearCount { get; private set; }

		public List<(string Details, string State)> Updates { get; } = [];

		public bool Connect()
		{
			ConnectCount++;
			IsConnected = true;

			return true;
		}

		public bool Update(
			string details,
			string state,
			string largeImage,
			string largeText
		)
		{
			UpdateCount++;
			Updates.Add((details, state));

			return true;
		}

		public void Clear()
		{
			ClearCount++;
		}

		public void Dispose()
		{
		}
	}
}
