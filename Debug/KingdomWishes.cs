using System.Text;
using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	[HasWishCommand]
	public class KingdomWishes
	{
		[WishCommand("kingdom:found", null)]
		public static void FoundWish(string Parameter)
		{
			string name = (string.IsNullOrEmpty(Parameter) ? "Kavvat" : Parameter.Trim());
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				Popup.Show("The kingdom of {{C|" + system.KingdomDisplayName + "}} is already founded.");
				return;
			}
			Faction faction = KingdomFounding.Found(name);
			Popup.Show("{{C|" + faction.DisplayName + "}} is founded. The chronicle begins.\n\nStandings seeded from your reputation with " + system.Standings.Count + " factions.");
		}

		[WishCommand("kingdom:claim", null)]
		public static void ClaimWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
			}
			else if (KingdomFounding.ClaimZone(zone))
			{
				Popup.Show("This zone now belongs to the kingdom: {{C|" + zone.ZoneID + "}}\n\nFuture spawns here will enroll as citizens.");
			}
			else
			{
				Popup.Show("A claim must border the kingdom's existing ground. ({{W|kingdom:claimforce}} overrides for testing.)");
			}
		}

		[WishCommand("kingdom:claimforce", null)]
		public static void ClaimForceWish()
		{
			Zone zone = The.Player?.CurrentZone;
			if (KingdomFounding.ClaimZone(zone, Force: true))
			{
				Popup.Show("Claimed by decree: {{C|" + zone.ZoneID + "}}");
			}
			else
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
			}
		}

		[WishCommand("kingdom:dump", null)]
		public static void DumpWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			StringBuilder sb = new StringBuilder();
			sb.Append("{{C|KINGDOM STATE DUMP}} tick ").Append(The.Game.TimeTicks);
			sb.Append("\nFounded: ").Append(system.Founded ? (system.KingdomFactionName + " / " + system.KingdomDisplayName) : "no");
			sb.Append("\nStyle: ").Append(system.Style).Append("  Stage: ").Append(system.Stage).Append("  Withered: ").Append(system.Withered);
			sb.Append("\nPop: ").Append(system.Population).Append("  DryStreak: ").Append(system.DryStreak).Append("  HasShopkeeper: ").Append(system.HasShopkeeper);
			sb.Append("\nNextArrival: ").Append(system.NextArrivalTick).Append("  Raid: state=").Append(system.RaidState).Append(" faction=").Append(system.RaidFactionName ?? "-").Append(" due=").Append(system.RaidDueTick).Append(" last=").Append(system.LastRaidTick);
			sb.Append("\nClaims: ").Append(string.Join(", ", system.ClaimedZones));
			sb.Append("\nDistricts: ");
			foreach (System.Collections.Generic.KeyValuePair<string, string> d in system.ZoneDistricts)
			{
				sb.Append(d.Key).Append("=").Append(d.Value).Append(" ");
			}
			sb.Append("\nDeals: ").Append(system.ActiveDealKeys.Count);
			for (int i = 0; i < system.ActiveDealKeys.Count; i++)
			{
				sb.Append("\n  ").Append(system.ActiveDealKeys[i]).Append(" with ").Append(system.ActiveDealFactions[i]).Append(" next=").Append(system.DealNextTicks[i]);
			}
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
			if (system.RaidState == 0)
			{
				system.RaidState = 1;
				system.RaidFactionName = "Snapjaws";
				system.RaidDueTick = The.Game.TimeTicks;
				Popup.Show("Raid forced: snapjaw warning issued, due now. Trigger it with {{W|kingdom:raid}} again (or move a turn), or pay tribute via the Charter.");
			}
			else
			{
				system.RaidDueTick = The.Game.TimeTicks;
				KingdomRaids.OnZoneActivated(system, zone);
				Popup.Show("Raid executed. Population " + system.Population + ", check the field.");
			}
		}

		[WishCommand("kingdom:reset", null)]
		public static void ResetWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("Nothing to reset.");
				return;
			}
			if (Popup.ShowYesNo("Dissolve {{C|" + system.KingdomDisplayName + "}} and wipe all kingdom state? (Debug only; claimed-zone properties in unvisited zones are left behind.)") != DialogResult.Yes)
			{
				return;
			}
			string name = system.KingdomFactionName;
			Faction faction = Factions.Get(name);
			if (faction != null)
			{
				faction.Visible = false;
			}
			foreach (Faction item in Factions.Loop())
			{
				item.FactionFeeling.Remove(name);
			}
			The.Game.PlayerReputation.ReputationValues.Remove(name);
			Zone zone = The.Player?.CurrentZone;
			if (zone != null && system.ClaimedZones.Contains(zone.ZoneID))
			{
				zone.SetZoneProperty("faction", null);
			}
			KingdomCharterPart part = The.Player?.GetPart<KingdomCharterPart>();
			if (part != null)
			{
				part.RemoveAbility();
				The.Player.RemovePart(part);
			}
			system.KingdomFactionName = null;
			system.KingdomDisplayName = null;
			system.Style = "common";
			system.FoundedTick = 0L;
			system.Stage = GrowthStage.Camp;
			system.Population = 0;
			system.DryStreak = 0;
			system.Withered = false;
			system.HasShopkeeper = false;
			system.NextArrivalTick = 0L;
			system.RaidState = 0;
			system.RaidFactionName = null;
			system.RaidDueTick = 0L;
			system.LastRaidTick = 0L;
			system.ClaimedZones.Clear();
			system.ZoneDistricts.Clear();
			system.ActiveDealKeys.Clear();
			system.ActiveDealFactions.Clear();
			system.DealNextTicks.Clear();
			system.ChronicleEntries.Clear();
			system.OutsiderEntries.Clear();
			system.OriginCounts.Clear();
			system.Standings.Clear();
			Popup.Show("The kingdom is dissolved. The ground forgets; the chronicle does not survive it.");
		}

		[WishCommand("kingdom:citizen", null)]
		public static void CitizenWish()
		{
			GameObject target = null;
			Cell cell = The.Player?.CurrentCell;
			if (cell != null)
			{
				foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
				{
					target = adjacentCell.GetFirstObjectWithPart("Brain");
					if (target != null && !target.IsPlayer())
					{
						break;
					}
					target = null;
				}
			}
			if (target == null)
			{
				Popup.Show("Stand next to a creature to enroll it.");
			}
			else if (KingdomFounding.EnrollCitizen(target))
			{
				Popup.Show(target.The + target.ShortDisplayName + " joins the kingdom as a citizen.");
			}
			else
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
			}
		}

		[WishCommand("kingdom:status", null)]
		public static void StatusWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded. Wish {{W|kingdom:found NAME}} to begin.");
				return;
			}
			Popup.Show(KingdomReports.Status(system) + "\n\n" + KingdomReports.Standings(system));
		}

		[WishCommand("kingdom:standing", null)]
		public static void StandingWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet.");
				return;
			}
			if (!TryParseFactionAmount(Parameter, out var faction, out var amount))
			{
				Popup.Show("Usage: {{W|kingdom:standing FactionName:Amount}}");
				return;
			}
			system.SetStanding(faction.Name, amount);
			faction.FactionFeeling.TryGetValue(system.KingdomFactionName, out var feeling);
			Popup.Show("Kingdom standing with " + faction.DisplayName + " set to " + amount + ".\nTheir feeling toward the kingdom is now " + feeling + ".");
		}

		[WishCommand("kingdom:rep", null)]
		public static void RepWish(string Parameter)
		{
			if (!TryParseFactionAmount(Parameter, out var faction, out var amount))
			{
				Popup.Show("Usage: {{W|kingdom:rep FactionName:Amount}}");
				return;
			}
			The.Game.PlayerReputation.Modify(faction, amount, "Wish");
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				faction.FactionFeeling.TryGetValue(system.KingdomFactionName, out var feeling);
				Popup.Show("Spillover check: kingdom standing with " + faction.DisplayName + " is now " + system.GetStanding(faction.Name) + ", their feeling toward the kingdom " + feeling + ".");
			}
		}

		[WishCommand("kingdom:chronicle", null)]
		public static void ChronicleWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("The chronicle is empty.");
				return;
			}
			Popup.Show(KingdomReports.Chronicle(system) + "\n\n" + KingdomReports.Chronicle(system, Outsider: true));
		}

		[WishCommand("kingdom:grow", null)]
		public static void GrowWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (!system.Founded || zone == null || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Stand in a claimed zone first ({{W|kingdom:claim}}).");
				return;
			}
			system.NextArrivalTick = The.Game.TimeTicks;
			int before = system.Population;
			KingdomGrowth.OnZoneActivated(system, zone);
			Popup.Show("Forced growth pass: population " + before + " -> " + system.Population + ", stored water now " + KingdomGrowth.CountStoredWater(zone) + " drams.");
		}

		[WishCommand("kingdom:selftest", null)]
		public static void SelftestWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("Selftest needs a founded kingdom. Wish {{W|kingdom:found NAME}} first.");
				return;
			}
			StringBuilder report = new StringBuilder();
			int passed = 0;
			int failed = 0;
			Check(report, ref passed, ref failed, "faction registered", Factions.Get(system.KingdomFactionName) != null);
			Check(report, ref passed, ref failed, "founder is loved by the kingdom", The.Game.PlayerReputation.GetLevel(system.KingdomFactionName) == 2);
			Faction kingdom = Factions.Get(system.KingdomFactionName);
			Check(report, ref passed, ref failed, "kingdom feeling toward Player is 100", kingdom != null && kingdom.FactionFeeling.TryGetValue("Player", out var playerFeeling) && playerFeeling == 100);
			bool mirrorConsistent = true;
			int checkedCount = 0;
			foreach (System.Collections.Generic.KeyValuePair<string, int> standing in system.Standings)
			{
				Faction faction = Factions.Get(standing.Key);
				if (faction == null)
				{
					continue;
				}
				faction.FactionFeeling.TryGetValue(system.KingdomFactionName, out var feeling);
				if (feeling != Reputation.GetFeeling((float)standing.Value))
				{
					mirrorConsistent = false;
					report.Append("\n    mismatch: ").Append(standing.Key).Append(" standing ").Append(standing.Value).Append(" but feeling ").Append(feeling);
				}
				checkedCount++;
			}
			Check(report, ref passed, ref failed, "mirror consistent across " + checkedCount + " standings", mirrorConsistent);
			Faction probe = Factions.Get("Joppa");
			if (probe != null)
			{
				int standingBefore = system.GetStanding(probe.Name);
				int repBefore = The.Game.PlayerReputation.Get(probe);
				The.Game.PlayerReputation.Modify(probe, 10, null, null, null, Silent: true);
				int actualDelta = The.Game.PlayerReputation.Get(probe) - repBefore;
				int expected = standingBefore + KingdomRules.SpilloverDelta(actualDelta, system.Stage);
				Check(report, ref passed, ref failed, "live spillover (+" + actualDelta + " rep -> +" + KingdomRules.SpilloverDelta(actualDelta, system.Stage) + " standing)", system.GetStanding(probe.Name) == expected);
				The.Game.PlayerReputation.Modify(probe, -actualDelta, null, null, null, Silent: true);
				system.SetStanding(probe.Name, standingBefore);
			}
			Check(report, ref passed, ref failed, "deal lists coherent (" + system.ActiveDealKeys.Count + " deals)", system.ActiveDealKeys.Count == system.ActiveDealFactions.Count && system.ActiveDealKeys.Count == system.DealNextTicks.Count);
			LiquidVolume fresh = new LiquidVolume { Volume = 10 };
			fresh.ComponentLiquids.Add("water", 1000);
			LiquidVolume brine = new LiquidVolume { Volume = 10 };
			brine.ComponentLiquids.Add("water", 600);
			brine.ComponentLiquids.Add("salt", 400);
			LiquidVolume empty = new LiquidVolume { Volume = 0 };
			LiquidVolume unknown = new LiquidVolume { Volume = 10, ComponentLiquids = null };
			Check(report, ref passed, ref failed, "pure water is eligible", KingdomLiquids.HasFreshWater(fresh));
			Check(report, ref passed, ref failed, "water-primary brine is not eligible", !KingdomLiquids.HasFreshWater(brine));
			Check(report, ref passed, ref failed, "brine vessel cannot receive fresh water", !KingdomLiquids.CanReceiveFreshWater(brine));
			Check(report, ref passed, ref failed, "empty vessel can receive fresh water", KingdomLiquids.CanReceiveFreshWater(empty));
			Check(report, ref passed, ref failed, "unknown positive liquid cannot receive fresh water", !KingdomLiquids.CanReceiveFreshWater(unknown));
			CheckChargingPost(report, ref passed, ref failed);
			bool claimsCoherent = true;
			foreach (string zoneID in system.ClaimedZones)
			{
				Zone zone = The.ZoneManager.GetZone(zoneID);
				if (zone != null && zone.GetZoneProperty("faction", null) != system.KingdomFactionName)
				{
					claimsCoherent = false;
				}
			}
			Check(report, ref passed, ref failed, "claimed zones carry the faction property (" + system.ClaimedZones.Count + " claims)", claimsCoherent);
			Popup.Show("{{C|Kingdom selftest}}: {{G|" + passed + " passed}}" + ((failed > 0) ? (", {{R|" + failed + " FAILED}}") : "") + "\n" + report.ToString());
		}

		private static void CheckChargingPost(StringBuilder Report, ref int Passed, ref int Failed)
		{
			GameObject post = null;
			GameObject cell = null;
			try
			{
				post = GameObject.CreateUnmodified("r_KingdomChargingPost");
				cell = GameObject.CreateUnmodified("Drained Chem Cell");
				Inventory inventory = post?.GetPart<Inventory>();
				r_KingdomHandCrank crank = post?.GetPart<r_KingdomHandCrank>();
				EnergyCell battery = cell?.GetPart<EnergyCell>();
				bool structure = inventory != null && crank != null && post.GetPart<UniversalCharger>() != null && post.GetPart<Capacitor>() == null && battery != null;
				Check(Report, ref Passed, ref Failed, "charging post has inventory, charger, and hand crank", structure);
				if (!structure)
				{
					return;
				}
				inventory.AddObject(cell, Silent: true, NoStack: true);
				post.SetIntProperty("KingdomEffectiveness", 100);
				int before = battery.GetCharge();
				crank.TurnTick(The.Game.TimeTicks, 1);
				Check(Report, ref Passed, ref Failed, "staffed charging post charges an inventoried cell", battery.GetCharge() > before);
				battery.Charge = 0;
				post.SetIntProperty("KingdomEffectiveness", 0);
				crank.TurnTick(The.Game.TimeTicks, 1);
				Check(Report, ref Passed, ref Failed, "idle charging post emits no charge", battery.GetCharge() == 0);
			}
			catch (System.Exception ex)
			{
				Report.Append("\n    charging probe threw: ").Append(ex.Message);
				Check(Report, ref Passed, ref Failed, "charging post live probe completed", Result: false);
			}
			finally
			{
				if (cell != null && cell.IsValid())
				{
					cell.Obliterate(null, Silent: true);
				}
				if (post != null && post.IsValid())
				{
					post.Obliterate(null, Silent: true);
				}
			}
		}

		private static void Check(StringBuilder Report, ref int Passed, ref int Failed, string Label, bool Result)
		{
			if (Result)
			{
				Passed++;
				Report.Append("\n{{G|PASS}} ").Append(Label);
			}
			else
			{
				Failed++;
				Report.Append("\n{{R|FAIL}} ").Append(Label);
			}
		}

		private static bool TryParseFactionAmount(string Parameter, out Faction Faction, out int Amount)
		{
			Faction = null;
			if (!KingdomRules.TryParseFactionAmount(Parameter, out var factionName, out Amount))
			{
				return false;
			}
			Faction = Factions.Get(factionName);
			return Faction != null;
		}
	}
}
