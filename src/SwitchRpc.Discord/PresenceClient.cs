using DiscordRPC;

namespace SwitchRpc.Discord;

/// <summary>
/// Thin wrapper around the DiscordRPC library so no other project touches
/// Discord types directly.
///
/// Connection state is tracked through the client's lifecycle events,
/// which fire on the library's own RPC thread (AutoEvents). SetPresence is
/// fire-and-forget — it only queues a message — so a dead pipe is noticed
/// asynchronously through OnClose/OnConnectionFailed rather than by an
/// update returning false. When that happens, the next Connect() must
/// Deinitialize first: the library keeps its initialized flag after the
/// pipe dies and refuses to Initialize again.
///
/// The session start timestamp is preserved across reconnections so the
/// Discord timer stays continuous.
/// </summary>
public sealed class PresenceClient : IDisposable
{
	private readonly DiscordRpcClient _client;
	private Timestamps? _sessionStart;
	private volatile bool _connected;

	public PresenceClient(string clientId)
	{
		_client = new DiscordRpcClient(clientId, autoEvents: true);

		_client.OnReady += (_, _) => _connected = true;
		_client.OnClose += (_, _) => _connected = false;
		_client.OnConnectionFailed += (_, _) => _connected = false;
		_client.OnError += (_, _) => _connected = false;
	}

	public bool IsConnected => _connected && _client.IsInitialized;

	public bool Connect()
	{
		if (IsConnected)
			return true;

		try
		{
			// After the pipe died, the library still reports itself as
			// initialized; deinitialize so Initialize is allowed to run.
			if (_client.IsInitialized)
				_client.Deinitialize();

			if (_client.Initialize())
			{
				_connected = true;
				_sessionStart ??= Timestamps.Now;

				return true;
			}
		}
		catch (Exception error)
		{
			// Transient IPC failures are expected whenever Discord is not
			// running; the caller logs the outcome and retries later.
			Console.WriteLine($"Discord connect error: {error.Message}");
		}

		_connected = false;
		_sessionStart = null;

		return false;
	}

	public bool Update(
		string details,
		string state,
		string largeImage,
		string largeText
	)
	{
		if (!IsConnected)
			return false;

		try
		{
			_client.SetPresence(new RichPresence
			{
				Details = details,
				State = state,
				Assets = new Assets
				{
					LargeImageKey = largeImage,
					LargeImageText = largeText
				},
				Timestamps = _sessionStart
			});

			return true;
		}
		catch (Exception error)
		{
			Console.WriteLine($"RPC update failed: {error.Message}");

			_connected = false;

			return false;
		}
	}

	public void Clear()
	{
		if (!IsConnected)
			return;

		try
		{
			_client.ClearPresence();
		}
		catch (Exception error)
		{
			Console.WriteLine($"RPC clear failed: {error.Message}");
		}
	}

	public void Dispose()
	{
		_client.Dispose();
		_connected = false;
		_sessionStart = null;
	}
}
