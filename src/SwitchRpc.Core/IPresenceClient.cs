namespace SwitchRpc.Core;

/// <summary>
/// Rich Presence transport contract. The host drives this; concrete
/// implementations own the vendor-specific connection details and
/// reconnection behavior.
/// </summary>
public interface IPresenceClient : IDisposable
{
	bool IsConnected { get; }

	bool Connect();

	bool Update(
		string details,
		string state,
		string largeImage,
		string largeText
	);

	void Clear();
}
