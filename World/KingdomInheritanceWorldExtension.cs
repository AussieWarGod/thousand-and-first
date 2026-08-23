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
	/// Claims one exact, still-mutable Joppa surface site after vanilla world construction.
	/// Selection is pure and stable; this hook consumes no runtime RNG and creates no object.
	/// </summary>
	[JoppaWorldBuilderExtension]
	public sealed class KingdomInheritanceWorldExtension : IJoppaWorldBuilderExtension
	{
		public override void OnAfterBuild(JoppaWorldBuilder Builder)
		{
			KingdomInheritanceState state = KingdomInheritanceState.Instance;
			MutabilityMap removedMap = null;
			int removedX = -1;
			int removedY = -1;
			string removedTerrain = "";
			bool staged = false;
			try
			{
				string legacyId;
				string oldGroundZoneId;
				string preferredTerrain;
				if (state == null || !state.TrySelectionInputs(out legacyId,
					out oldGroundZoneId, out preferredTerrain))
				{
					return;
				}
				if (Builder == null || Builder.mutableMap == null || Builder.worldInfo == null
					|| Builder.WorldZone == null
					|| Builder.WorldZone.ZoneID != KingdomInheritanceSiteRules.WorldId)
				{
					state.RefuseBootstrap("the active world builder was not the canonical Joppa world");
					return;
				}

				List<KingdomInheritanceSiteCandidate> candidates =
					new List<KingdomInheritanceSiteCandidate>(240 * 75);
				KingdomInheritanceWorldIndex index = new KingdomInheritanceWorldIndex(
					Builder.WorldZone, Builder.worldInfo);
				for (int y = 0; y < 75; y++)
				{
					for (int x = 0; x < 240; x++)
					{
						KingdomInheritanceSiteCandidate candidate;
						if (KingdomInheritanceWorldRuntime.TryCandidate(Builder.WorldZone,
							Builder.mutableMap, Builder.worldInfo, index, x, y, out candidate))
						{
							candidates.Add(candidate);
						}
					}
				}

				KingdomInheritanceSiteCandidate selected;
				KingdomInheritanceSiteFault fault;
				if (!KingdomInheritanceSiteRules.TrySelect(candidates, legacyId,
					oldGroundZoneId, preferredTerrain, out selected, out fault))
				{
					state.RefuseBootstrap("no compatible mutable surface site remained: "
						+ fault.ToString());
					return;
				}

				int targetX;
				int targetY;
				if (!KingdomInheritanceSiteRules.TrySurfaceCoordinates(selected.ZoneId,
					out targetX, out targetY)
					|| Builder.mutableMap.GetMutable(targetX, targetY) != 1)
				{
					state.RefuseBootstrap("the selected mutable site changed before reservation");
					return;
				}
				Builder.mutableMap.RemoveMutableLocation(Location2D.Get(targetX, targetY));
				removedMap = Builder.mutableMap;
				removedX = targetX;
				removedY = targetY;
				removedTerrain = selected.TerrainTag ?? "";
				if (Builder.mutableMap.GetMutable(targetX, targetY) != 0)
				{
					RestoreRemoved(removedMap, removedX, removedY, removedTerrain);
					removedMap = null;
					state.RefuseBootstrap("the exact selected site could not be removed from the mutable pool");
					return;
				}
				string failure;
				if (!state.StageSite(selected, targetX, targetY, Builder.mutableMap,
					Builder.worldInfo, out failure))
				{
					RestoreRemoved(removedMap, removedX, removedY, removedTerrain);
					removedMap = null;
					state.RefuseBootstrap("the selected site could not be staged: " + failure);
				}
				else
				{
					staged = true;
				}
			}
			catch (Exception ex)
			{
				if (!staged)
				{
					RestoreRemoved(removedMap, removedX, removedY, removedTerrain);
				}
				state?.RefuseBootstrap("the Joppa inheritance extension failed closed: " + ex.Message);
				MetricsManager.LogError("ThousandAndFirst inheritance world extension", ex);
			}
		}

		private static void RestoreRemoved(MutabilityMap Map, int X, int Y, string Terrain)
		{
			if (Map != null && X >= 0 && Y >= 0 && Map.GetMutable(X, Y) == 0)
			{
				Map.AddMutableLocation(Location2D.Get(X, Y), Terrain, 1);
			}
		}
	}

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

	/// <summary>Engine-bound, read-only facts kept outside pure selection rules.</summary>
	internal static class KingdomInheritanceWorldRuntime
	{
		internal static bool TryCandidate(Zone WorldZone, MutabilityMap Map, WorldInfo Info,
			int X, int Y, out KingdomInheritanceSiteCandidate Candidate)
		{
			return TryCandidate(WorldZone, Map, Info, null, X, Y, out Candidate);
		}

		internal static bool TryCandidate(Zone WorldZone, MutabilityMap Map, WorldInfo Info,
			KingdomInheritanceWorldIndex Index, int X, int Y,
			out KingdomInheritanceSiteCandidate Candidate)
		{
			Candidate = null;
			if (WorldZone == null || Map == null || Info == null
				|| WorldZone.ZoneID != KingdomInheritanceSiteRules.WorldId
				|| X < 0 || X >= 240 || Y < 0 || Y >= 75 || The.Game == null
				|| The.ZoneManager == null)
			{
				return false;
			}

			KingdomInheritanceSiteCandidate candidate = new KingdomInheritanceSiteCandidate();
			candidate.ZoneId = ZoneID.Assemble(KingdomInheritanceSiteRules.WorldId,
				X / 3, Y / 3, X % 3, Y % 3, KingdomInheritanceSiteRules.SurfaceDepth);
			candidate.Mutable = Map.GetMutable(X, Y) == 1;
			candidate.Built = The.ZoneManager.IsZoneBuilt(candidate.ZoneId);
			candidate.HasMapNote = Index == null
				? JournalAPI.GetMapNotesForZone(candidate.ZoneId).Count != 0
				: Index.HasMapNote(candidate.ZoneId);
			candidate.HasGeneratedLocation = Index == null
				? HasGeneratedLocation(Info, candidate.ZoneId, X, Y)
				: Index.HasGeneratedLocation(candidate.ZoneId, X, Y);
			candidate.HasZoneBuilder = The.ZoneManager.CountBuildersFor(candidate.ZoneId) != 0;
			candidate.HasExplicitName = HasExplicitName(candidate.ZoneId);
			candidate.HasReservedZoneProperty = The.ZoneManager.HasZoneProperty(candidate.ZoneId,
				"SkipTerrainBuilders") || The.ZoneManager.HasZoneProperty(candidate.ZoneId, "NoBiomes");
			candidate.Special = The.ZoneManager.CountPartsFor(candidate.ZoneId) != 0;

			KingdomInheritanceWorldIndex.ParasangFacts indexed = Index?.Facts(X, Y);
			GameObject terrain = indexed == null ? WorldZone.GetCell(X / 3, Y / 3)
				?.GetFirstObjectWithPart("TerrainTravel") : null;
			GameObjectBlueprint blueprint = terrain?.GetBlueprint();
			if (indexed == null && (terrain == null || blueprint == null))
			{
				candidate.Special = true;
				candidate.TerrainRank = KingdomInheritanceSiteRules.MaxTerrainRank + 1;
				Candidate = candidate;
				return true;
			}
			if (indexed != null)
			{
				candidate.TerrainBlueprint = indexed.TerrainBlueprint;
				candidate.TerrainTag = indexed.TerrainTag;
				candidate.Water = indexed.Water;
				candidate.TerrainRank = indexed.TerrainRank;
				candidate.Tier = indexed.Tier;
				candidate.Special |= indexed.Special;
			}
			else
			{
				candidate.TerrainBlueprint = terrain.Blueprint ?? "";
				candidate.TerrainTag = terrain.GetTag("Terrain", terrain.Blueprint) ?? "";
				candidate.Water = blueprint.DescendsFrom("TerrainWater");
				candidate.TerrainRank = TerrainRank(blueprint);
				int tier;
				if (!int.TryParse(terrain.GetTag("RegionTier", ""), NumberStyles.None,
					CultureInfo.InvariantCulture, out tier))
				{
					candidate.Special = true;
					tier = 0;
				}
				candidate.Tier = tier;
				List<CellBlueprint> cells = The.ZoneManager.GetCellBlueprints(
					KingdomInheritanceSiteRules.WorldId, X / 3, Y / 3);
				for (int i = 0; i < cells.Count; i++)
				{
					if (cells[i] == null || !cells[i].Mutable)
					{
						candidate.Special = true;
						break;
					}
				}
			}
			Candidate = candidate;
			return true;
		}

		internal static bool ValidateSelected(string TargetZoneId, string TerrainBlueprint,
			int X, int Y, MutabilityMap Map, WorldInfo Info, bool requireRemovedMap,
			out string Failure)
		{
			Failure = "";
			int parsedX;
			int parsedY;
			if (!KingdomInheritanceSiteRules.TrySurfaceCoordinates(TargetZoneId,
				out parsedX, out parsedY) || parsedX != X || parsedY != Y)
			{
				Failure = "the target zone id no longer names its exact surface coordinate";
				return false;
			}
			if (Map == null || Info == null || The.Game == null || The.ZoneManager == null
				|| !ReferenceEquals(The.Game.GetObjectGameState("JoppaWorldInfo"), Info))
			{
				Failure = "the selected Joppa mutability or world-info owner is unavailable";
				return false;
			}
			int expectedMutable = requireRemovedMap ? 0 : 1;
			if (Map.GetMutable(X, Y) != expectedMutable)
			{
				Failure = "the selected site's mutable reservation changed";
				return false;
			}

			Zone worldZone = The.ZoneManager.GetZone(KingdomInheritanceSiteRules.WorldId);
			KingdomInheritanceSiteCandidate candidate;
			if (!TryCandidate(worldZone, Map, Info, X, Y, out candidate)
				|| candidate.ZoneId != TargetZoneId
				|| candidate.TerrainBlueprint != TerrainBlueprint
				|| candidate.Built || candidate.Water || candidate.Special
				|| candidate.HasMapNote || candidate.HasGeneratedLocation
				|| candidate.HasZoneBuilder || candidate.HasExplicitName
				|| candidate.HasReservedZoneProperty
				|| candidate.Tier < KingdomInheritanceSiteRules.MinTier
				|| candidate.Tier > KingdomInheritanceSiteRules.MaxTier
				|| candidate.TerrainRank < 0
				|| candidate.TerrainRank > KingdomInheritanceSiteRules.MaxTerrainRank)
			{
				Failure = "the selected site acquired terrain, builder, note, name, or location conflicts";
				return false;
			}
			return true;
		}

		internal static int TerrainRank(GameObjectBlueprint Blueprint)
		{
			if (Blueprint.DescendsFrom("TerrainSaltdunes"))
			{
				return 0;
			}
			if (Blueprint.DescendsFrom("TerrainFlowerfields"))
			{
				return 1;
			}
			if (Blueprint.DescendsFrom("TerrainDesertCanyon"))
			{
				return 2;
			}
			if (Blueprint.DescendsFrom("TerrainHills"))
			{
				return 3;
			}
			return KingdomInheritanceSiteRules.MaxTerrainRank + 1;
		}

		private static bool HasGeneratedLocation(WorldInfo Info, string ZoneId, int X, int Y)
		{
			foreach (GeneratedLocationInfo location in Info.allLocationTypes)
			{
				if (location != null && (location.targetZone == ZoneId
					|| (location.zoneLocation != null && location.zoneLocation.X == X
						&& location.zoneLocation.Y == Y)))
				{
					return true;
				}
			}
			return false;
		}

		private static bool HasExplicitName(string ZoneId)
		{
			XRLGame game = The.Game;
			return game.HasStringGameState("ZoneName_" + ZoneId)
				|| game.HasStringGameState("ZoneNameContext_" + ZoneId)
				|| game.HasStringGameState("ZoneIndefiniteArticle_" + ZoneId)
				|| game.HasStringGameState("ZoneDefiniteArticle_" + ZoneId)
				|| game.HasBooleanGameState("ZoneProperName_" + ZoneId);
		}
	}
}
