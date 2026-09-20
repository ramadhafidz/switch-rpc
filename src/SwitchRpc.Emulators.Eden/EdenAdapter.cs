using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SwitchRpc.Core;

namespace SwitchRpc.Emulators.Eden;

/// <summary>
/// Eden emulator integration: process and window detection, running-game
/// identification, and save location through EdenSaveLocator.
/// </summary>
public sealed class EdenAdapter : IEmulatorAdapter
{
	private const string ProcessName = "eden";

	private readonly IReadOnlyList<GameDefinition> _games;
	private readonly EdenSaveLocator _locator;

	public EdenAdapter(
		IReadOnlyList<GameDefinition> games,
		string? saveRoot = null
	)
	{
		_games = games;
		_locator = saveRoot is null
			? new EdenSaveLocator()
			: new EdenSaveLocator(saveRoot);
	}

	public string Name => "eden";

	/// <summary>
	/// Performs a single process scan and reports whether Eden is running
	/// together with the detected game, if any. The loop uses this to log
	/// process transitions without scanning twice per tick.
	/// </summary>
	public EmulatorState Poll()
	{
		using var process = FindProcess();

		if (process is null)
			return new EmulatorState(false, null);

		return new EmulatorState(
			true,
			MatchGame(CollectWindowTitles(process.Id))
		);
	}

	public string? LocateSave(GameDefinition game)
	{
		return _locator.Locate(game.TitleId);
	}

	private List<string> CollectWindowTitles(int processId)
	{
		var titles = new List<string>();

		EnumWindows((hWnd, _) =>
		{
			GetWindowThreadProcessId(hWnd, out var windowProcessId);

			if (windowProcessId != processId || !IsWindowVisible(hWnd))
				return true;

			var text = new StringBuilder(512);

			if (GetWindowText(hWnd, text, 512) > 0)
				titles.Add(text.ToString());

			return true;
		}, IntPtr.Zero);

		return titles;
	}

	private GameDefinition? MatchGame(List<string> titles)
	{
		foreach (var title in titles)
		{
			foreach (var game in _games)
			{
				if (title.Contains(game.DisplayName, StringComparison.Ordinal))
					return game;
			}
		}

		return null;
	}

	private static Process? FindProcess()
	{
		return Process.GetProcessesByName(ProcessName)
			.FirstOrDefault();
	}

	private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
