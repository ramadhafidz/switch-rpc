namespace SwitchRpc.Tests;

/// <summary>
/// Writes PKHeX save data to temporary files for round-trip tests.
/// </summary>
internal static class TempSaveFile
{
	public static string Write(byte[] data)
	{
		var path = Path.Combine(
			Path.GetTempPath(),
			$"switchrpc-test-{Guid.NewGuid():N}.sav"
		);

		File.WriteAllBytes(path, data);

		return path;
	}
}
