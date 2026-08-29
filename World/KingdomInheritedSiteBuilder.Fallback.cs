using System;
using System.Collections.Generic;
using Qud.API;
using ThousandAndFirst;
using XRL;
using XRL.World;

namespace XRL.World.ZoneBuilders
{
	public sealed partial class KingdomInheritedSiteBuilder
	{
		private bool EmergencyFallback(Zone Z)
		{
			try
			{
				if (Z == null)
				{
					return false;
				}
				if (The.ZoneManager != null && Z.ZoneID == TargetZoneId
					&& TryEmergencyVanillaRegeneration(Z))
				{
					return false;
				}
				string mapNoteId = "taf.inherit." + (LegacyId ?? "");
				List<GameObject> objects = Z.GetObjects();
				for (int i = objects.Count - 1; i >= 0; i--)
				{
					XRL.World.Parts.LocationFinder finder =
						objects[i].GetPart<XRL.World.Parts.LocationFinder>();
					if (finder != null && finder.ID == mapNoteId)
					{
						objects[i].Obliterate(null, Silent: true);
					}
				}
				JournalMapNote note = JournalAPI.GetMapNote(mapNoteId);
				if (note != null && note.ZoneID == TargetZoneId)
				{
					JournalAPI.DeleteMapNote(note);
				}
				return FillBlankEmergencyGround(Z);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst inherited-site emergency fallback", ex);
			}
			return false;
		}

		private bool TryEmergencyVanillaRegeneration(Zone Z)
		{
			ZoneBuilderCollection collection = The.ZoneManager.GetBuilderCollection(TargetZoneId);
			if (collection == null || collection.Members == null)
			{
				return false;
			}
			int siteCount = 0;
			int finderCount = 0;
			bool exactBuilderPriorities = true;
			for (int i = 0; i < collection.Members.Count; i++)
			{
				OrderedBuilderBlueprint ordered = collection.Members[i];
				ZoneBuilderBlueprint builder = ordered.Blueprint;
				if (builder != null && MatchesSiteBuilder(builder))
				{
					siteCount++;
					exactBuilderPriorities &= ordered.Priority == 6000;
				}
				else if (builder != null && MatchesFinderBuilder(builder))
				{
					finderCount++;
					exactBuilderPriorities &= ordered.Priority == 6100;
				}
			}
			object skip = The.ZoneManager.GetZoneProperty(TargetZoneId, "SkipTerrainBuilders");
			bool exactSkip = skip is bool && (bool)skip;
			bool exactNoBiomes = (The.ZoneManager.GetZoneProperty(TargetZoneId, "NoBiomes")
				as string) == "Yes";
			if (!KingdomInheritanceStateRules.CanClaimEmergencyOwnership(
				collection.Members.Count, siteCount, finderCount, exactBuilderPriorities,
				exactSkip, exactNoBiomes))
			{
				return false;
			}
			// Shared terrain-suppression properties go first. If either removal tears, the exact
			// site builder remains to retry; never strand SkipTerrainBuilders without an executor.
			The.ZoneManager.RemoveZoneProperty(TargetZoneId, "SkipTerrainBuilders");
			The.ZoneManager.RemoveZoneProperty(TargetZoneId, "NoBiomes");
			if (The.ZoneManager.HasZoneProperty(TargetZoneId, "SkipTerrainBuilders")
				|| The.ZoneManager.HasZoneProperty(TargetZoneId, "NoBiomes"))
			{
				return false;
			}
			The.ZoneManager.RemoveZoneBuilders(TargetZoneId, delegate(ZoneBuilderBlueprint builder)
			{
				return builder != null && (MatchesSiteBuilder(builder) || MatchesFinderBuilder(builder));
			});
			bool exactBuildersAbsent = true;
			collection = The.ZoneManager.GetBuilderCollection(TargetZoneId);
			if (collection != null && collection.Members != null)
			{
				for (int i = 0; i < collection.Members.Count; i++)
				{
					ZoneBuilderBlueprint builder = collection.Members[i].Blueprint;
					if (builder != null && (MatchesSiteBuilder(builder) || MatchesFinderBuilder(builder)))
					{
						exactBuildersAbsent = false;
					}
				}
			}
			if (!KingdomInheritanceStateRules.CanRegenerateAfterEmergencyCleanup(
				exactBuildersAbsent,
				!The.ZoneManager.HasZoneProperty(TargetZoneId, "SkipTerrainBuilders"),
				!The.ZoneManager.HasZoneProperty(TargetZoneId, "NoBiomes")))
			{
				return false;
			}
			JournalMapNote note = JournalAPI.GetMapNote("taf.inherit." + (LegacyId ?? ""));
			if (note != null && note.ZoneID == TargetZoneId)
			{
				JournalAPI.DeleteMapNote(note);
			}
			return true;
		}

		private bool MatchesSiteBuilder(ZoneBuilderBlueprint Builder)
		{
			return KingdomInheritanceStateRules.IsExactSiteBuilder(Builder.Class,
				Builder.GetParameter<string>("LegacyId", ""),
				Builder.GetParameter<string>("TargetGameId", ""),
				Builder.GetParameter<string>("TargetZoneId", ""),
				Builder.GetParameter<int>("ReconstructionVersion", -1),
				LegacyId ?? "", TargetGameId ?? "", TargetZoneId ?? "", ReconstructionVersion);
		}

		private bool MatchesFinderBuilder(ZoneBuilderBlueprint Builder)
		{
			return KingdomInheritanceStateRules.IsExactLocationFinderBuilder(Builder.Class,
				Builder.GetParameter<string>("LegacyId", ""),
				Builder.GetParameter<string>("TargetGameId", ""),
				Builder.GetParameter<string>("TargetZoneId", ""),
				Builder.GetParameter<int>("ReconstructionVersion", -1),
				LegacyId ?? "", TargetGameId ?? "", TargetZoneId ?? "", ReconstructionVersion);
		}

		private static bool FillBlankEmergencyGround(Zone Z)
		{
			for (int y = 0; y < Z.Height; y++)
			{
				for (int x = 0; x < Z.Width; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null) continue;
					if (string.IsNullOrEmpty(cell.PaintTile))
						cell.PaintTile = "Terrain/sw_desert.bmp";
					if (string.IsNullOrEmpty(cell.PaintTileColor)) cell.PaintTileColor = "&y";
					if (string.IsNullOrEmpty(cell.PaintColorString)) cell.PaintColorString = "&y";
					if (string.IsNullOrEmpty(cell.PaintRenderString)) cell.PaintRenderString = ".";
				}
			}
			Z.ClearReachableMap();
			int reachable = Z.BuildReachableMap(0, 0);
			if (KingdomInheritanceStateRules.CanTerminalizeHiddenFallback(reachable, 0))
			{
				return true;
			}
			return false;
		}

		private static string Nonempty(string Value, string Fallback)
		{
			return string.IsNullOrEmpty(Value) ? Fallback : Value;
		}
	}
}
