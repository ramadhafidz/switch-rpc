using PKHeX.Core;
using SwitchRpc.Poc.State;

namespace SwitchRpc.Poc.Saves;

/// <summary>
/// Reads Scarlet/Violet saves directly through PKHeX.Core. The extraction
/// logic mirrors the verified bridge implementation in
/// bridge/PokemonSaveReader (ScarletVioletReader + LocationExtractor);
/// no save offsets or PKHeX APIs beyond those verified there.
/// </summary>
public sealed class SvSaveReader : ISaveStateReader
{
	private const uint KPlayerCurrentFieldID = 0xF17EB014;
	private const uint KPlayerCurrentLocationID = 0x19FC5B7B;
	private const uint KCoordinates = 0x708D1511;

	public bool CanRead(SaveFile save)
	{
		return save is SAV9SV;
	}

	public GameState Read(SaveFile save, string gameId)
	{
		if (save is not SAV9SV sv)
			throw new ArgumentException(
				"Save file is not a Scarlet/Violet save.",
				nameof(save)
			);

		var playtime = sv.Played;
		var locationId = GetLocationId(sv);

		return new GameState(
			gameId,
			(playtime.PlayedHours * 3600L)
				+ (playtime.PlayedMinutes * 60L)
				+ playtime.PlayedSeconds,
			GetLocationName(sv, locationId),
			locationId,
			GetDexStats(sv)
		);
	}

	private static Dictionary<string, DexStats> GetDexStats(SAV9SV save)
	{
		var paldeaSeen = 0;
		var paldeaCaught = 0;

		var kitakamiSeen = 0;
		var kitakamiCaught = 0;

		var blueberrySeen = 0;
		var blueberryCaught = 0;

		for (ushort species = 1; species <= save.MaxSpeciesID; species++)
		{
			var group = GetDexGroup(save, species);

			if (group == 0)
				continue;

			var seen = GetSeen(save, species);
			var caught = GetCaught(save, species);

			switch (group)
			{
				case 1:
					if (seen)
						paldeaSeen++;

					if (caught)
						paldeaCaught++;

					break;

				case 2:
					if (seen)
						kitakamiSeen++;

					if (caught)
						kitakamiCaught++;

					break;

				case 3:
					if (seen)
						blueberrySeen++;

					if (caught)
						blueberryCaught++;

					break;
			}
		}

		return new Dictionary<string, DexStats>
		{
			["paldea"] = new(paldeaSeen, paldeaCaught, 400),
			["kitakami"] = new(kitakamiSeen, kitakamiCaught, 200),
			["blueberry"] = new(blueberrySeen, blueberryCaught, 243),
		};
	}

	private static byte GetDexGroup(SAV9SV save, ushort species)
	{
		for (byte form = 0; form <= save.Personal.GetFormEntry(species, 0).FormCount; form++)
		{
			var pi = save.Personal.GetFormEntry(species, form);

			if (pi.DexPaldea != 0)
				return 1;

			if (pi.DexKitakami != 0)
				return 2;

			if (pi.DexBlueberry != 0)
				return 3;
		}

		return 0;
	}

	private static bool GetSeen(SAV9SV save, ushort species)
	{
		return save.Zukan.GetRevision() switch
		{
			0 => save.Zukan.DexPaldea.GetSeen(species),
			1 => save.Zukan.DexKitakami.GetSeen(species),
			_ => false
		};
	}

	private static bool GetCaught(SAV9SV save, ushort species)
	{
		return save.Zukan.GetRevision() switch
		{
			0 => save.Zukan.DexPaldea.GetCaught(species),
			1 => save.Zukan.DexKitakami.GetCaught(species),
			_ => false
		};
	}

	private static int? GetLocationId(SAV9SV save)
	{
		if (!save.Blocks.TryGetBlock(
			KPlayerCurrentLocationID,
			out var block
		))
		{
			return null;
		}

		var data = block.Raw.Span;

		if (data.Length < 4)
			return null;

		return BitConverter.ToInt32(data);
	}

	private static string? GetLocationName(
		SAV9SV save,
		int? locationId
	)
	{
		if (locationId is null)
			return null;

		return PKHeX.Core.GameInfo.GetLocationName(
			isEggLocation: false,
			location: (ushort)locationId.Value,
			format: 9,
			generation: 9,
			version: save.Version
		);
	}
}
