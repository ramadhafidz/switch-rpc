using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SwitchRpc.Poc.Detection;

public sealed class EdenDetector
{
	private const string ProcessName = "eden";

	public bool IsRunning()
	{
		return FindProcess() is not null;
	}

	public string? GetWindowTitle()
	{
		using var process = FindProcess();

		if (process is null)
			return null;

		var titles = new List<string>();

		EnumWindows((hWnd, _) =>
		{
			GetWindowThreadProcessId(hWnd, out var processId);

			if (processId != process.Id || !IsWindowVisible(hWnd))
				return true;

			var text = new StringBuilder(512);

			if (GetWindowText(hWnd, text, 512) > 0)
				titles.Add(text.ToString());

			return true;
		}, IntPtr.Zero);

		return titles.FirstOrDefault(title =>
			title.Contains("Pokémon", StringComparison.Ordinal));
	}

	public GameDefinition? DetectGame()
	{
		var title = GetWindowTitle();

		return title is null
			? null
			: GameCatalog.FindByWindowTitle(title);
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
