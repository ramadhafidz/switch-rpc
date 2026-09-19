using SwitchRpc.Emulators.Eden;

namespace SwitchRpc.Tests;

public class EdenSaveLocatorTests
{
	[Fact]
	public void Locate_FindsSaveInNestedDirectory()
	{
		var root = Path.Combine(Path.GetTempPath(), $"switchrpc-test-{Guid.NewGuid():N}");

		try
		{
			var saveDir = Path.Combine(
				root,
				"0000000000000000",
				"EXAMPLEUSER",
				"0100A3D008C5C000"
			);

			Directory.CreateDirectory(saveDir);

			var savePath = Path.Combine(saveDir, "main");
			File.WriteAllBytes(savePath, [1, 2, 3]);

			var locator = new EdenSaveLocator(root);

			Assert.Equal(savePath, locator.Locate("0100A3D008C5C000"));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Locate_DirectoryWithoutMainFile_ReturnsNull()
	{
		var root = Path.Combine(Path.GetTempPath(), $"switchrpc-test-{Guid.NewGuid():N}");

		try
		{
			Directory.CreateDirectory(
				Path.Combine(root, "0000000000000000", "0100A3D008C5C000")
			);

			var locator = new EdenSaveLocator(root);

			Assert.Null(locator.Locate("0100A3D008C5C000"));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Locate_MissingRoot_ReturnsNull()
	{
		var locator = new EdenSaveLocator(
			Path.Combine(Path.GetTempPath(), $"switchrpc-nonexistent-{Guid.NewGuid():N}")
		);

		Assert.Null(locator.Locate("0100A3D008C5C000"));
	}
}
