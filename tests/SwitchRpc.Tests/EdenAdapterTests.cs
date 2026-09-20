using SwitchRpc.Core;
using SwitchRpc.Emulators.Eden;
using Xunit;

namespace SwitchRpc.Tests;

public sealed class EdenAdapterTests
{
	[Fact]
	public void Name_IsEden()
	{
		var adapter = new EdenAdapter([]);

		Assert.Equal("eden", adapter.Name);
	}

	[Fact]
	public void LocateSave_ResolvesThroughTheSaveLocator()
	{
		var titleId = "0100A3D008C5C000";
		var (root, savePath) = CreateSaveRoot(titleId);

		try
		{
			var adapter = new EdenAdapter([], root);
			var game = new GameDefinition(
				"pokemon_scarlet",
				"Pokémon Scarlet",
				"eden",
				titleId,
				"scarlet"
			);

			Assert.Equal(savePath, adapter.LocateSave(game));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void LocateSave_ReturnsNull_WhenNoSaveExists()
	{
		var root = Directory.CreateTempSubdirectory("switchrpc-locator").FullName;

		try
		{
			var adapter = new EdenAdapter([], root);
			var game = new GameDefinition(
				"pokemon_scarlet",
				"Pokémon Scarlet",
				"eden",
				"0100A3D008C5C000",
				"scarlet"
			);

			Assert.Null(adapter.LocateSave(game));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static (string Root, string SavePath) CreateSaveRoot(string titleId)
	{
		var root = Directory.CreateTempSubdirectory("switchrpc-locator").FullName;
		var saveDirectory = Path.Combine(
			root,
			"0000000000000000",
			"E926F9C876E9EA7E74A75E7913AA10B8",
			titleId
		);

		Directory.CreateDirectory(saveDirectory);

		var savePath = Path.Combine(saveDirectory, "main");
		File.WriteAllBytes(savePath, [1, 2, 3]);

		return (root, savePath);
	}
}
