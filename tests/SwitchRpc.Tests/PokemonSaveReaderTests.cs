using SwitchRpc.Games.Pokemon;

namespace SwitchRpc.Tests;

public class PokemonSaveReaderTests
{
	[Fact]
	public void Identify_GarbageFile_ReturnsNull()
	{
		var path = TempSaveFile.Write([0x12, 0x34, 0x56, 0x78]);

		try
		{
			Assert.Null(new PokemonSaveReader().Identify(path));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void Read_UnidentifiableFile_ReturnsUnsupportedWithoutTypeName()
	{
		var path = TempSaveFile.Write([0x12, 0x34, 0x56, 0x78]);

		try
		{
			var result = new PokemonSaveReader().Read(path, "pokemon_example");

			Assert.Null(result.State);
			Assert.Null(result.SaveTypeName);
			Assert.False(result.Supported);
		}
		finally
		{
			File.Delete(path);
		}
	}
}
