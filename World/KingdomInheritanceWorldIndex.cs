using System;
using System.Collections.Generic;
using System.Globalization;
using Genkit;
using Qud.API;
using XRL;
using XRL.World;
using XRL.World.WorldBuilders;

namespace ThousandAndFirst
{
	/// <summary>
	/// One linear preindex for facts that would otherwise allocate or scan all generated locations
	/// for each of Joppa's 18,000 surface subzones. It consumes no RNG and carries no save state.
	/// </summary>
	internal sealed class KingdomInheritanceWorldIndex
	{
		internal sealed class ParasangFacts
		{
			internal string TerrainBlueprint = "";

			internal string TerrainTag = "";

			internal int TerrainRank = KingdomInheritanceSiteRules.MaxTerrainRank + 1;

			internal int Tier;

			internal bool Water;

			internal bool Special;
		}

		private readonly HashSet<string> MapNoteZones = new HashSet<string>(StringComparer.Ordinal);

		private readonly HashSet<string> GeneratedZones = new HashSet<string>(StringComparer.Ordinal);

		private readonly HashSet<int> GeneratedCoordinates = new HashSet<int>();

		private readonly ParasangFacts[,] Parasangs = new ParasangFacts[80, 25];

		internal KingdomInheritanceWorldIndex(Zone WorldZone, WorldInfo Info)
		{
			for (int i = 0; i < JournalAPI.MapNotes.Count; i++)
			{
				JournalMapNote note = JournalAPI.MapNotes[i];
				if (note != null && !string.IsNullOrEmpty(note.ZoneID))
				{
					MapNoteZones.Add(note.ZoneID);
				}
			}
			foreach (GeneratedLocationInfo location in Info.allLocationTypes)
			{
				if (location == null)
				{
					continue;
				}
				if (!string.IsNullOrEmpty(location.targetZone))
				{
					GeneratedZones.Add(location.targetZone);
				}
				if (location.zoneLocation != null && location.zoneLocation.X >= 0
					&& location.zoneLocation.X < 240 && location.zoneLocation.Y >= 0
					&& location.zoneLocation.Y < 75)
				{
					GeneratedCoordinates.Add(location.zoneLocation.Y * 240
						+ location.zoneLocation.X);
				}
			}
			for (int py = 0; py < 25; py++)
			{
				for (int px = 0; px < 80; px++)
				{
					ParasangFacts facts = new ParasangFacts();
					GameObject terrain = WorldZone.GetCell(px, py)
						?.GetFirstObjectWithPart("TerrainTravel");
					GameObjectBlueprint blueprint = terrain?.GetBlueprint();
					if (terrain == null || blueprint == null)
					{
						facts.Special = true;
					}
					else
					{
						facts.TerrainBlueprint = terrain.Blueprint ?? "";
						facts.TerrainTag = terrain.GetTag("Terrain", terrain.Blueprint) ?? "";
						facts.Water = blueprint.DescendsFrom("TerrainWater");
						facts.TerrainRank = KingdomInheritanceWorldRuntime.TerrainRank(blueprint);
						int tier;
						if (!int.TryParse(terrain.GetTag("RegionTier", ""), NumberStyles.None,
							CultureInfo.InvariantCulture, out tier))
						{
							facts.Special = true;
							tier = 0;
						}
						facts.Tier = tier;
					}
					List<CellBlueprint> cells = The.ZoneManager.GetCellBlueprints(
						KingdomInheritanceSiteRules.WorldId, px, py);
					for (int i = 0; i < cells.Count; i++)
					{
						if (cells[i] == null || !cells[i].Mutable)
						{
							facts.Special = true;
							break;
						}
					}
					Parasangs[px, py] = facts;
				}
			}
		}

		internal bool HasMapNote(string ZoneId)
		{
			return MapNoteZones.Contains(ZoneId);
		}

		internal bool HasGeneratedLocation(string ZoneId, int X, int Y)
		{
			return GeneratedZones.Contains(ZoneId) || GeneratedCoordinates.Contains(Y * 240 + X);
		}

		internal ParasangFacts Facts(int X, int Y)
		{
			return Parasangs[X / 3, Y / 3];
		}
	}
}
