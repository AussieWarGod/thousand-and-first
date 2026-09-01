using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomWishes
	{
		[WishCommand("kingdom:dump", null)]
		public static void DumpWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			StringBuilder sb = new StringBuilder();
			sb.Append("{{C|KINGDOM STATE DUMP}} tick ").Append(The.Game.TimeTicks);
			sb.Append("\nFounded: ").Append(system.Founded ? (system.KingdomFactionName + " / " + KingdomPresentation.Rich(system.KingdomDisplayName)) : "no");
			if (system.Founded)
			{
				// The seat is the whole of the multi-city surface: which city the flat fields
				// currently describe, what the other one holds, and whether every settlement
				// field still has a flat field to be carried in.
				sb.Append("\n").Append(SeatReport(system));
				sb.Append("\nRegard: ").Append(system.FounderRegard()).Append(" (").Append(KingdomExileRules.RegardName(KingdomExileRules.ClassifyRegard(system.FounderRegard()))).Append(", last spoken ").Append(KingdomExileRules.RegardName((RealmRegard)system.RegardSpoken)).Append(")");
			}
			if (system.Exiled)
			{
				sb.Append("\n").Append(ExileReport(system));
			}
			sb.Append("\nStyle: ").Append(system.Style).Append(" (").Append(KingdomFounding.StyleGroundClause(system.Style)).Append(")").Append("  Stage: ").Append(system.Stage).Append("  Withered: ").Append(system.Withered);
			sb.Append("\nFounding terrain: blueprint=").Append(system.FoundingTerrainBlueprint ?? "(none)").Append(" region=").Append(system.FoundingRegionName ?? "(none)").Append(" z=").Append(system.FoundingZLevel);
			Zone here = The.Player?.CurrentZone;
			if (here != null && system.ClaimedZones.Contains(here.ZoneID))
			{
				KingdomSurvey survey = KingdomSurvey.Take(here, system);
				string roof = survey.TryBenefits(out KingdomBenefitIndex benefits,
					out string roofFailure) ? benefits.Total("roof").ToString()
					: "unproved (" + roofFailure + ")";
				sb.Append("\nHere: defence=").Append(survey.Defence()).Append(" (garrison ").Append(survey.DistrictDefenceBonus).Append(")")
					.Append(" larder=").Append(survey.FoodAbundance).Append("/").Append(survey.FoodStored).Append(" of ").Append(survey.FoodCapacity)
					.Append(" passiveFoodRate=0")
					.Append(" kitchens=").Append(KingdomCapabilityRuntime.Count(here, survey,
						KingdomBenefitCapabilities.Cooking, "debug meal"))
					.Append(" beds=").Append(roof).Append(" citizens=").Append(survey.Citizens);
			}
			sb.Append(KingdomLodging.DumpLine(system, here));
			sb.Append(KingdomCreed.DumpLine(system));
			sb.Append(KingdomConversion.DumpLine(system, here));
			sb.Append(KingdomWaterRite.DumpLine(system, here));
			sb.Append("\nPop: ").Append(system.Population).Append("  DryStreak: ").Append(system.DryStreak).Append("  HasShopkeeper: ").Append(system.HasShopkeeper);
			sb.Append("\nNextArrival: ").Append(system.NextArrivalTick).Append("  Raid: state=").Append(system.RaidState).Append(" faction=").Append(system.RaidFactionName ?? "-").Append(" due=").Append(system.RaidDueTick).Append(" last=").Append(system.LastRaidTick);
			sb.Append("\nClaims: ").Append(string.Join(", ", system.ClaimedZones));
			sb.Append("\nDistricts: ");
			foreach (System.Collections.Generic.KeyValuePair<string, string> d in system.ZoneDistricts)
			{
				sb.Append(d.Key).Append("=").Append(d.Value).Append(" ");
			}
				sb.Append(KingdomReports.TradeStatus(system, Detailed: true));
			sb.Append("\nStandings: ").Append(system.Standings.Count).Append("  Chronicle: ").Append(system.ChronicleEntries.Count).Append("/").Append(system.OutsiderEntries.Count);
			sb.Append("\nRegistry: ").Append(KingdomData.Buildings.Count).Append(" buildings, ").Append(KingdomData.Deals.Count).Append(" deals, ").Append(KingdomData.Styles.Count).Append(" styles");
			if (zone != null)
			{
				sb.Append("\nHere (").Append(zone.ZoneID).Append("): claimed=").Append(system.ClaimedZones.Contains(zone.ZoneID));
				sb.Append(" stored=").Append(KingdomGrowth.CountStoredWater(zone)).Append(" open=").Append(KingdomGrowth.CountOpenWater(zone)).Append(" space=").Append(KingdomGrowth.CountStorageSpace(zone));
				int citizens = 0;
				int caravans = 0;
				foreach (GameObject obj in zone.GetObjects())
				{
					if (obj.GetIntProperty("KingdomCitizen") == 1)
					{
						citizens++;
					}
					if (obj.GetIntProperty("KingdomCaravan") == 1)
					{
						caravans++;
					}
				}
				sb.Append(" citizens-here=").Append(citizens).Append(" caravans-here=").Append(caravans);
			}
			string text = sb.ToString();
			KingdomLog.Log(ConsoleLib.Console.ColorUtility.StripFormatting(text));
			Popup.Show(text);
		}

		/// <summary>Read-only proof of one physical paired delve. Parameter is an optional head
		/// zone id; absent uses the zone under the player. The probe never generates a zone.</summary>
		[WishCommand("kingdom:delvelink", null)]
		public static void DelveLinkWish(string Parameter)
		{
			string head = string.IsNullOrWhiteSpace(Parameter)
				? The.Player?.CurrentZone?.ZoneID : Parameter.Trim();
			if (string.IsNullOrEmpty(head) || head.Length > KingdomDelveLinkRules.MaxZoneChars)
			{
				Popup.Show("Use {{W|kingdom:delvelink}} in a shaft head zone, or pass its exact zone id.");
				return;
			}
			string foot;
			bool canonicalFoot = KingdomDelveRules.TryFootZoneId(head, out foot);
			string state = The.Game?.GetStringGameState(KingdomDelveLink.LinkState + head, null);
			KingdomDelveLinkReceipt receipt;
			bool canonicalReceipt = KingdomDelveLink.TryReadPhysicalReceipt(head, out receipt);
			bool physical = KingdomDelveLink.PhysicalLinkStands(head);
			StringBuilder report = new StringBuilder();
			report.Append("{{C|Delve physical-link proof}}\nHead: ").Append(head)
				.Append("\nFoot: ").Append(canonicalFoot ? foot : "(no canonical foot)")
				.Append("\nState: ").Append(state == null ? "absent (legacy lane)"
					: state == KingdomDelveLink.Tombstone ? "struck/tombstoned"
					: canonicalReceipt ? "canonical" : "corrupt or partial")
				.Append("\nLegacy int: ").Append(The.Game == null ? 0
					: The.Game.GetIntGameState(KingdomDelve.ShaftState + head))
				.Append("\nPhysical proof: ").Append(physical ? "{{G|STANDS}}" : "{{R|FAILS}}");
			if (canonicalReceipt)
			{
				report.Append("\nCell: ").Append(receipt.X).Append(',').Append(receipt.Y)
					.Append("\nRoot: ").Append(receipt.RootId)
					.Append("\nDown: ").Append(receipt.HeadEndpointId)
					.Append("\nUp: ").Append(receipt.FootEndpointId);
			}
			Popup.Show(report.ToString());
		}

		[WishCommand("kingdom:raid", null)]
		public static void RaidWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (!system.Founded || zone == null || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Stand in a claimed zone first.");
				return;
			}
			KingdomRaids.OnZoneActivated(system, zone);
			if (KingdomRaidIncidentRules.Active(system.LifecycleBook?.RaidLedger) == null)
			{
				string source = KingdomLifecycleRules.ChildId(system.LifecycleBook.SettlementId,
					"debug-raid-" + system.LifecycleBook.RaidNextSequence, 0);
				if (!KingdomRaids.RecordProvocation(system, "Snapjaws", "debug-test-provocation",
					source, "the debug wish explicitly challenged a snapjaw scout", zone.ZoneID, 1))
				{
					Popup.Show("The explicit test grievance could not be minted; inspect the raid lifecycle fault.");
					return;
				}
				Popup.Show("An explicit snapjaw test grievance was minted. It has a stable source and target, but remains only rumor: no demand has been delivered and no clock is running. Wake once to receive the physical demand, then read it to acknowledge and start its answer window.");
			}
			else
			{
				KingdomRaids.OnZoneActivated(system, zone);
				KingdomRaidIncident incident = KingdomRaidIncidentRules.Active(system.LifecycleBook.RaidLedger);
				Popup.Show(incident == null ? "The explicit test incident is resolved."
					: ("Test incident " + incident.Id + " is " + incident.State
						+ ", channel " + incident.ChannelState + ", due "
						+ (incident.DueTick == 0L ? "not running" : incident.DueTick.ToString())
						+ ", target " + incident.TargetZoneId + "."));
			}
		}

	}
}
