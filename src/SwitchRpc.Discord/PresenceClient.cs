using DiscordRPC;

namespace SwitchRpc.Discord;

/// <summary>
/// Thin wrapper around the DiscordRPC library so no other project touches
/// Discord types directly. Connection state is tracked through the
/// client's lifecycle events, so a disconnect that happens mid-run makes
/// the next update attempt reconnect instead of silently doing nothing.
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
		_client = new DiscordRpcClient(clientId);

		_client.OnReady += (_, _) => _connected = true;
		_client.OnClose += (_, _) => _connected = false;
		_client.OnConnectionFailed += (_, _) => _connected = false;
		_client.OnError += (_, _) => _connected = false;
	}

	public bool IsConnected => _connected;

	public bool Connect()
	{
		if (_connected)
			return true;

		try
		{
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
		if (!_connected)
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
		if (!_connected)
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
