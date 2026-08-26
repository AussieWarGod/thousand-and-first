using System;
using System.Collections.Generic;
using System.IO;
using Genkit;
using Qud.API;
using XRL;
using XRL.CharacterBuilds.Qud;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.WorldBuilders;

namespace ThousandAndFirst
{
	public sealed partial class KingdomInheritanceState
	{
		internal bool TryGroundPaint(string ZoneId, out string Tile, out string Color,
			out string Render, out string Failure)
		{
			Tile = "";
			Color = "";
			Render = ".";
			Failure = "";
			if (ZoneId != TargetZoneId || TargetTerrainRank < 0
				|| TargetTerrainRank > KingdomInheritanceSiteRules.MaxTerrainRank)
			{
				Failure = "the target lost its validated terrain paint class";
				return false;
			}
			switch (TargetTerrainRank)
			{
			case 0:
				Tile = "Terrain/sw_ground_desert_1.bmp";
				Color = "&y";
				break;
			case 1:
				Tile = "Tiles/tile-dirt1.png";
				Color = "&G";
				break;
			case 2:
				Tile = "Tiles/tile-dirt1.png";
				Color = "&y";
				break;
			default:
				Tile = "Tiles/tile-dirt1.png";
				Color = "&w";
				break;
			}
			return true;
		}

		internal bool TryInstallLocationFinder(Zone Zone, string LegacyId, string TargetGameId,
			string ZoneId, int ReconstructionVersion, out string Failure)
		{
			Failure = "";
			if (Zone == null || Zone.ZoneID != ZoneId || ZoneId != TargetZoneId
				|| ReconstructionVersion != KingdomInheritEngine.ReconstructionVersion
				|| (Phase != KingdomInheritancePhase.AppliedPendingDurability
					&& Phase != KingdomInheritancePhase.Committed))
			{
				return false;
			}
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			string expected;
			if (!TryGetReservation(out legacy, out receipt) || legacy.LegacyId != LegacyId
				|| receipt.TargetGameId != TargetGameId
				|| !KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy, receipt,
					ZoneId, ReconstructionVersion, out expected)
				|| ApplicationMarker != expected
				|| (Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "")
					!= expected)
				{
					Failure = "the success-aware finder lacked an exact application marker";
					RecordDiscoveryFailure(Failure);
					return false;
				}
			try
			{
				EnsureOwnedMapNote(legacy);
				new XRL.World.ZoneBuilders.AddLocationFinder
				{
					SecretID = SecretId,
					Value = 1
				}.BuildZone(Zone);
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the success-aware finder could not create its widget: " + ex.Message;
				BestEffortHideBrokenDiscovery(Zone);
				RecordDiscoveryFailure(Failure);
				return false;
			}
		}

		internal void RecordDiscoveryFailure(string Detail)
		{
			string message = "the inherited site needs discovery repair: " + Detail;
			if (!KingdomInheritanceStateRules.PreservesApplicationProofDuringDiscoveryRepair(
				Phase, ApplyStatusValue, ApplyFaultValue, ApplicationMarker))
			{
				SetRepair(message);
				AnnounceFailure();
				return;
			}
			// Discovery is optional state layered over an already-proved application. Never
			// poison phase/status/marker merely because a note, name, or finder could not form.
			FailureDetail = Bound(message, MaxFailureChars);
			try
			{
				LogFailure(FailureDetail);
			}
			catch (Exception)
			{
			}
			if (!FailureAnnounced)
			{
				FailureAnnounced = true;
				try
				{
					MessageQueue.AddPlayerMessage("&yThe inherited kingdom entered this world, "
						+ "but its map discovery needs repair: &Y" + Detail);
				}
				catch (Exception)
				{
				}
			}
		}

		internal bool HasOnlyOwnedBuilders(string ZoneId, string LegacyId, string TargetGameId,
			int ReconstructionVersion, out string Failure)
		{
			Failure = "";
			if (The.ZoneManager == null || ZoneId != TargetZoneId)
			{
				Failure = "the exact target builder collection is unavailable";
				return false;
			}
			if (!OwnsSkipTerrainBuilders || !OwnsNoBiomes
				|| !(The.ZoneManager.GetZoneProperty(ZoneId, "SkipTerrainBuilders") is bool)
				|| !(bool)The.ZoneManager.GetZoneProperty(ZoneId, "SkipTerrainBuilders")
				|| (The.ZoneManager.GetZoneProperty(ZoneId, "NoBiomes") as string) != "Yes")
			{
				Failure = "the target's reserved generation properties changed ownership or value";
				return false;
			}
			ZoneBuilderCollection collection = The.ZoneManager.GetBuilderCollection(ZoneId);
			if (collection == null || collection.Members == null || collection.Members.Count != 2)
			{
				Failure = "the target acquired a foreign or missing persistent builder";
				return false;
			}
			bool foundSite = false;
			bool foundFinder = false;
			for (int i = 0; i < collection.Members.Count; i++)
			{
				OrderedBuilderBlueprint ordered = collection.Members[i];
				ZoneBuilderBlueprint builder = ordered.Blueprint;
				if (builder != null && ordered.Priority == 6000 && builder.Class == BuilderClass
					&& builder.GetParameter<string>("LegacyId", "") == LegacyId
					&& builder.GetParameter<string>("TargetGameId", "") == TargetGameId
					&& builder.GetParameter<string>("TargetZoneId", "") == ZoneId
					&& builder.GetParameter<int>("ReconstructionVersion", -1)
						== ReconstructionVersion)
				{
					foundSite = true;
				}
				else if (builder != null && ordered.Priority == 6100
					&& KingdomInheritanceStateRules.IsExactLocationFinderBuilder(builder.Class,
						builder.GetParameter<string>("LegacyId", ""),
						builder.GetParameter<string>("TargetGameId", ""),
						builder.GetParameter<string>("TargetZoneId", ""),
						builder.GetParameter<int>("ReconstructionVersion", -1),
						LegacyId, TargetGameId, ZoneId, ReconstructionVersion))
				{
					foundFinder = true;
				}
				else
				{
					Failure = "the target's persistent builder set is not exclusively inheritance-owned";
					return false;
				}
			}
			if (!foundSite || !foundFinder)
			{
				Failure = "the exact inherited builder or location finder is missing";
				return false;
			}
			return true;
		}

	}
}
