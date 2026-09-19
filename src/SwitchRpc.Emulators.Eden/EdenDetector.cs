using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SwitchRpc.Core;

namespace SwitchRpc.Emulators.Eden;

public sealed class EdenDetector
{
	private const string ProcessName = "eden";

	private readonly IReadOnlyList<GameDefinition> _games;

	public EdenDetector(IReadOnlyList<GameDefinition> games)
	{
		_games = games;
	}

	public bool IsRunning()
	{
		return FindProcess() is not null;
	}

	/// <summary>
	/// Performs a single process scan and reports whether Eden is running
	/// together with the detected game, if any. The loop uses this to log
	/// process transitions without scanning twice per tick.
	/// </summary>
	public (bool Running, GameDefinition? Game) Poll()
	{
		using var process = FindProcess();

		if (process is null)
			return (false, null);

		return (true, MatchGame(CollectWindowTitles(process.Id)));
	}

	public GameDefinition? DetectGame()
	{
		return Poll().Game;
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
