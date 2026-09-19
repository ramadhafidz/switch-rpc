using DiscordRPC;

namespace SwitchRpc.Discord;

/// <summary>
/// Thin wrapper around the DiscordRPC library so no other project touches
/// Discord types directly.
/// </summary>
public sealed class PresenceClient : IDisposable
{
	private readonly DiscordRpcClient _client;
	private Timestamps? _sessionStart;
	private bool _initialized;

	public PresenceClient(string clientId)
	{
		_client = new DiscordRpcClient(clientId);
	}

	public bool Connect()
	{
		if (_initialized)
			return true;

		_initialized = _client.Initialize();

		if (_initialized)
			_sessionStart = Timestamps.Now;
		else
			_sessionStart = null;

		return _initialized;
	}

	public bool IsConnected => _initialized;

	public bool Update(
		string details,
		string state,
		string largeImage,
		string largeText
	)
	{
		if (!_initialized)
			return false;

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

	public void Clear()
	{
		if (_initialized)
			_client.ClearPresence();
	}

	public void Dispose()
	{
		_client.Dispose();
		_initialized = false;
		_sessionStart = null;
	}
}
