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
