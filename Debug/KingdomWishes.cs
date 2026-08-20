using System.Collections.Generic;
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
				Popup.Show("The kingdom of {{C|" + system.KingdomDisplayName + "}} is already founded. ({{W|kingdom:found2 NAME:VOCATION}} founds the second city here.)");
				return;
			}
			Faction faction = KingdomFounding.Found(name);
			Popup.Show("{{C|" + faction.DisplayName + "}} is founded on " + KingdomFounding.StyleGroundClause(system.Style) + ". The chronicle begins.\n\nStandings seeded from your reputation with " + system.Standings.Count + " factions.");
		}

		/// <summary>
		/// Founds the realm's second city on the ground the tester is standing on, skipping the
		/// walk the rite would otherwise require: adjacency to the realm is forced, everything
		/// else &mdash; the two-city cap, the refusal to found on ground already held &mdash; is
		/// the shipped rule, because those are the rules worth testing.
		/// </summary>
		/// <param name="Parameter">NAME, or NAME:VOCATION. Vocation defaults to the neutral one.</param>
		[WishCommand("kingdom:found2", null)]
		public static void FoundSecondWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
				return;
			}
			string name = "Sheol";
			string vocation = KingdomSettlement.NeutralVocation;
			if (!string.IsNullOrEmpty(Parameter))
			{
				string[] parts = Parameter.Trim().Split(':');
				if (!string.IsNullOrEmpty(parts[0]))
				{
					name = parts[0].Trim();
				}
				if (parts.Length > 1 && KingdomSettlement.IsKnownVocation(parts[1].Trim().ToLowerInvariant()))
				{
					vocation = parts[1].Trim().ToLowerInvariant();
				}
			}
			if (!KingdomFounding.FoundSecond(name, vocation, zone, Force: true))
			{
				Popup.Show(RefusalOrDefault(system, zone) + "\n\nKnown vocations: " + string.Join(", ", KingdomSettlement.Vocations) + ".");
				return;
			}
			Popup.Show("{{C|" + name + "}} is founded here as " + KingdomSettlement.VocationClause(vocation) + ", the second city of {{C|" + system.KingdomDisplayName + "}}.\n\nSeated: " + system.Capture().Describe() + "\nAway: " + system.Away.Describe());
		}

		private static string RefusalOrDefault(KingdomSystem System, Zone Site)
		{
			string refusal = KingdomSettlement.SecondFoundingRefusal(KingdomFounding.JudgeSite(System, Site), System.KingdomDisplayName);
			return string.IsNullOrEmpty(refusal) ? "The founding was refused; stand in a zone the realm does not already hold." : refusal;
		}

		/// <summary>
		/// Shows which city is seated and what the dormant one holds, and &mdash; with
		/// {{W|swap}} &mdash; exchanges them where you stand, so a tester can drive both cities
		/// without the walk. The swap is a probe, not a move: walking into either city's own
		/// ground re-seats it the ordinary way, through
		/// <see cref="KingdomSystem.TrySeat"/>.
		/// </summary>
		[WishCommand("kingdom:seat", null)]
		public static void SeatWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
				return;
			}
			if (!string.IsNullOrEmpty(Parameter) && Parameter.Trim().ToLowerInvariant() == "swap")
			{
				if (system.Away == null)
				{
					Popup.Show("There is only one city. Wish {{W|kingdom:found2 NAME:VOCATION}} to found the second here.");
					return;
				}
				KingdomSettlement wasSeated = system.Capture();
				system.Restore(system.Away);
				system.Away = wasSeated;
				Popup.Show("Seat forced to {{C|" + system.SeatName + "}}.\n\n" + SeatReport(system) + "\n\n{{K|Debug probe: the flat fields now describe a city you are not standing in. Walk into either city's ground and the ordinary swap corrects it.}}");
				return;
			}
			Popup.Show(SeatReport(system));
		}

		/// <summary>One line naming the city the flat fields currently describe, and the one they
		/// do not. Prefixed to reports that would otherwise read as if the realm had one city.</summary>
		private static string SeatLine(KingdomSystem System)
		{
			return "{{C|" + System.SeatName + "}}" + KingdomSettlement.VocationSuffix(System.Vocation)
				+ ((System.Away != null) ? ("  {{K|(away: " + (System.Away.SettlementName ?? "(unnamed)") + ")}}") : "");
		}

		private static string SeatReport(KingdomSystem System)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("{{C|Realm}}: ").Append(System.KingdomFactionName ?? "-").Append(" / ").Append(System.KingdomDisplayName ?? "-")
				.Append("  cities=").Append(System.SettlementCount).Append("/").Append(KingdomSettlement.MaxSettlements);
			sb.Append("\n{{C|Seated}}: ").Append(System.Capture().Describe());
			sb.Append("\n{{C|Away}}: ").Append((System.Away != null) ? System.Away.Describe() : "(none)");
			List<string> mismatches = KingdomSettlement.SeatMismatches(typeof(KingdomSystem));
			sb.Append("\nCarried fields: ").Append(KingdomSettlement.CarriedFields().Length)
				.Append("  seat mismatches: ").Append((mismatches.Count == 0) ? "none" : string.Join("; ", mismatches.ToArray()));
			Zone here = The.Player?.CurrentZone;
			if (here != null)
			{
				sb.Append("\nHere (").Append(here.ZoneID).Append("): seat=").Append(System.ClaimedZones.Contains(here.ZoneID))
					.Append(" away=").Append(System.Away != null && System.Away.ClaimedZones.Contains(here.ZoneID))
					.Append(" rite=").Append(KingdomFounding.JudgeSite(System, here));
			}
			return sb.ToString();
		}

		/// <summary>
		/// Reports the founded city style plus the terrain evidence that produced it, and lets a
		/// tester force a different style for testing <see cref="KingdomRules.StyleAllows"/>
		/// filtering without re-founding on a different site. Forcing a style does not rewrite the
		/// recorded founding terrain &mdash; it only overrides which building/district rules apply,
		/// same as every other debug wish in this file (reversible probe, not a rewrite of history).
		/// </summary>
		[WishCommand("kingdom:style", null)]
		public static void StyleWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
				return;
			}
			if (string.IsNullOrEmpty(Parameter))
			{
				Popup.Show(StyleReport(system));
				return;
			}
			string style = Parameter.Trim().ToLowerInvariant();
			if (!KingdomRules.IsKnownStyle(style))
			{
				Popup.Show("Unknown style {{W|" + style + "}}. Known styles: " + string.Join(", ", KingdomRules.Styles) + ".");
				return;
			}
			system.Style = style;
			Popup.Show("Style forced to {{C|" + style + "}} (" + KingdomFounding.StyleGroundClause(style) + ").\n\n" + StyleReport(system));
		}

		private static string StyleReport(KingdomSystem System)
		{
			return "Style: {{C|" + System.Style + "}} (" + KingdomFounding.StyleGroundClause(System.Style) + ")"
				+ "\nFounding terrain: blueprint=" + (System.FoundingTerrainBlueprint ?? "(none)")
				+ " region=" + (System.FoundingRegionName ?? "(none)")
				+ " z=" + System.FoundingZLevel
				+ "\nKnown styles: " + string.Join(", ", KingdomRules.Styles);
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
			if (system.Founded)
			{
				// The seat is the whole of the multi-city surface: which city the flat fields
				// currently describe, what the other one holds, and whether every settlement
				// field still has a flat field to be carried in.
				sb.Append("\n").Append(SeatReport(system));
			}
			sb.Append("\nStyle: ").Append(system.Style).Append(" (").Append(KingdomFounding.StyleGroundClause(system.Style)).Append(")").Append("  Stage: ").Append(system.Stage).Append("  Withered: ").Append(system.Withered);
			sb.Append("\nFounding terrain: blueprint=").Append(system.FoundingTerrainBlueprint ?? "(none)").Append(" region=").Append(system.FoundingRegionName ?? "(none)").Append(" z=").Append(system.FoundingZLevel);
			Zone here = The.Player?.CurrentZone;
			if (here != null && system.ClaimedZones.Contains(here.ZoneID))
			{
				KingdomSurvey survey = KingdomSurvey.Take(here, system);
				sb.Append("\nHere: defence=").Append(survey.Defence()).Append(" (garrison ").Append(survey.DistrictDefenceBonus).Append(")")
					.Append(" larder=").Append(survey.FoodAbundance).Append("/").Append(survey.FoodStored)
					.Append(" beds=").Append(survey.Beds).Append(" citizens=").Append(survey.Citizens);
			}
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
			if (Popup.ShowYesNo("Dissolve {{C|" + system.KingdomDisplayName + "}}, all " + system.SettlementCount + " of its cities, and wipe all kingdom state? (Debug only; claimed-zone properties in unvisited zones are left behind.)") != DialogResult.Yes)
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
			if (zone != null && (system.ClaimedZones.Contains(zone.ZoneID) || (system.Away != null && system.Away.ClaimedZones.Contains(zone.ZoneID))))
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
			// Both cities at once: seating a blank settlement clears every per-settlement field
			// there is, so a field added later cannot be forgotten here, and Away goes with it.
			system.Restore(new KingdomSettlement());
			system.Away = null;
			system.ActiveDealKeys.Clear();
			system.ActiveDealFactions.Clear();
			system.DealNextTicks.Clear();
			system.ChronicleEntries.Clear();
			system.OutsiderEntries.Clear();
			system.Standings.Clear();
			Popup.Show("Both cities are dissolved. The ground forgets; the chronicle does not survive it.");
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
			Popup.Show(SeatLine(system) + "\n" + KingdomReports.Status(system) + "\n\n" + KingdomReports.Standings(system));
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
			List<string> seatMismatches = KingdomSettlement.SeatMismatches(typeof(KingdomSystem));
			// The one failure that loses a whole city silently: a settlement field with nowhere to
			// live on the seat is dropped on every swap.
			Check(report, ref passed, ref failed, "seat carries all " + KingdomSettlement.CarriedFields().Length + " settlement fields" + ((seatMismatches.Count > 0) ? (" (" + string.Join("; ", seatMismatches.ToArray()) + ")") : ""), seatMismatches.Count == 0);
			bool claimsDisjoint = true;
			if (system.Away != null)
			{
				foreach (string zoneID in system.Away.ClaimedZones)
				{
					if (system.ClaimedZones.Contains(zoneID))
					{
						claimsDisjoint = false;
						report.Append("\n    both cities claim ").Append(zoneID);
					}
				}
			}
			Check(report, ref passed, ref failed, "the two cities claim no ground in common", claimsDisjoint);
			Check(report, ref passed, ref failed, "the realm holds no more than " + KingdomSettlement.MaxSettlements + " cities", system.SettlementCount <= KingdomSettlement.MaxSettlements);
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
