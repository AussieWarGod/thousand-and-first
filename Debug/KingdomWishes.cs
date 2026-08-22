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
			// Factions.AddNewFaction is a Dictionary.Add, and a runtime faction can never be
			// removed or renamed — so after an expulsion the old realm's name is taken forever,
			// and re-using it would throw part-way through the rite.
			if (Factions.Exists(name))
			{
				Popup.Show("There is already a {{C|" + name + "}} in the world" + (system.Exiled && name == system.ExiledFactionName ? " — the one that put you out, which is still standing." : ".") + " Pick another name.");
				return;
			}
			Faction faction = KingdomFounding.Found(name);
			if (faction == null)
			{
				Popup.Show("The founding was refused. ({{W|kingdom:dump}} for state.)");
				return;
			}
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

		/// <summary>
		/// Moves the realm's regard for its founder to an absolute value through the engine's own
		/// reputation path, so the whole ladder &mdash; murmur, warning, the gate &mdash; runs the
		/// way it runs in play rather than being simulated. This is the reachable trigger:
		/// {{W|kingdom:regard -700}} gets a founder thrown out of their own realm by the shipped
		/// code, not by a debug shortcut.
		/// </summary>
		/// <param name="Parameter">Target reputation, e.g. <c>-700</c>. Empty reports where it stands.</param>
		[WishCommand("kingdom:regard", null)]
		public static void RegardWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			// The realm the founder holds, or — once it has put them out — the one they are
			// outside of. Mending an old realm's regard is the whole return path, so a tester
			// with no realm must still be able to move this number.
			string factionName = system.Founded ? system.KingdomFactionName : system.ExiledFactionName;
			if (string.IsNullOrEmpty(factionName))
			{
				Popup.Show("No kingdom founded yet, and none has put you out. Wish {{W|kingdom:found NAME}} first.");
				return;
			}
			if (string.IsNullOrEmpty(Parameter) || !int.TryParse(Parameter.Trim(), out var target))
			{
				Popup.Show(RegardReport(system) + "\n\nUsage: {{W|kingdom:regard AMOUNT}} (absolute, e.g. -700 to be repudiated, 0 to be heard out again).");
				return;
			}
			Faction realm = Factions.GetIfExists(factionName);
			if (realm == null)
			{
				Popup.Show("The realm's faction is not registered; nothing to move.");
				return;
			}
			int before = The.Game.PlayerReputation.Get(realm);
			// Modify rather than Set, precisely because Modify is what the world uses: it fires
			// AfterReputationChangeEvent, which is the surface the expulsion ladder listens on.
			The.Game.PlayerReputation.Modify(realm, target - before, "Wish", null, "Wish");
			Popup.Show("Regard with {{C|" + realm.DisplayName + "}}: " + before + " -> " + The.Game.PlayerReputation.Get(realm) + ".\n\n" + RegardReport(system));
		}

		/// <summary>Where the founder stands with whichever realm currently has an opinion of them.</summary>
		private static string RegardReport(KingdomSystem System)
		{
			bool held = System.Founded;
			int regard = held ? System.FounderRegard() : System.ExiledRealmRegard();
			string name = held ? System.KingdomDisplayName : System.ExiledDisplayName;
			return "{{C|" + (name ?? "-") + "}}" + (held ? "" : " (which put you out)") + " holds you {{W|" + KingdomExileRules.RegardName(KingdomExileRules.ClassifyRegard(regard)) + "}} (" + regard + ")."
				+ (held ? ("\nLast spoken of: " + KingdomExileRules.RegardName((RealmRegard)System.RegardSpoken)) : "\nThe gate opens above " + KingdomExileRules.RegardHated + ". Stand on its ground and it will put the question to you.")
				+ "\nRungs: beloved " + KingdomExileRules.RegardLoved + "+, trusted " + KingdomExileRules.RegardLiked + "+, doubted >" + KingdomExileRules.RegardDisliked + ", resented >" + KingdomExileRules.RegardHated + ", repudiated at or below " + KingdomExileRules.RegardHated + " (the gate).";
		}

		/// <summary>
		/// Puts the founder out of their own realm without waiting for the regard to fall there.
		/// Everything else is the shipped path: the realm and both its cities are kept whole, the
		/// Charter is taken, both registers record it, and nothing physical is touched.
		/// </summary>
		/// <param name="Parameter">A deed clause to record, or empty for the unnamed-deed line.</param>
		[WishCommand("kingdom:exile", null)]
		public static void ExileWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			string deed = string.IsNullOrEmpty(Parameter) ? KingdomExileRules.DeedClause("Wish") : Parameter.Trim();
			if (!system.Exile(deed, Forced: true, out var refusal))
			{
				Popup.Show(refusal + "\n\n{{K|(kingdom:regard AMOUNT walks the ladder the ordinary way.)}}");
				return;
			}
			Popup.Show(ExileReport(system) + "\n\n{{K|Walk back onto its ground and it will put the question to you, if its regard for you has risen since. kingdom:return forces the asking.}}");
		}

		/// <summary>
		/// Asks the realm that expelled the founder to take them back. Skips nothing &mdash; every
		/// requirement, the ground included, is the shipped one; it only saves a tester the walk
		/// back out and in again to make the zone activate.
		/// </summary>
		[WishCommand("kingdom:return", null)]
		public static void ReturnWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (!system.TryReturn(zone, out var refusal))
			{
				Popup.Show(refusal + "\n\n" + ExileReport(system));
				return;
			}
			Popup.Show(SeatReport(system));
		}

		/// <summary>One block describing the realm the founder is outside of, if any.</summary>
		private static string ExileReport(KingdomSystem System)
		{
			if (!System.Exiled)
			{
				return "{{C|Exile}}: none on the record.";
			}
			StringBuilder sb = new StringBuilder();
			int regard = System.ExiledRealmRegard();
			sb.Append("{{C|Exiled from}}: ").Append(System.ExiledFactionName).Append(" / ").Append(System.ExiledDisplayName)
				.Append("  cities=").Append(System.ExiledSettlementCount)
				.Append("  standings=").Append(System.ExiledStandings.Count)
				.Append("  tick=").Append(System.ExiledTick);
			sb.Append("\n{{C|Deed}}: ").Append(System.ExiledDeed ?? "-");
			sb.Append("\n{{C|Its regard}}: ").Append(regard).Append(" (").Append(KingdomExileRules.RegardName(KingdomExileRules.ClassifyRegard(regard))).Append(")")
				.Append("  asked-at=").Append((System.ReturnAskedRegard == int.MinValue) ? "never" : System.ReturnAskedRegard.ToString())
				.Append("  door-closed-told=").Append(System.DoorClosedTold);
			sb.Append("\n{{C|Its seat}}: ").Append((System.ExiledSeat != null) ? System.ExiledSeat.Describe() : "(none)");
			sb.Append("\n{{C|Its other city}}: ").Append((System.ExiledAway != null) ? System.ExiledAway.Describe() : "(none)");
			Zone here = The.Player?.CurrentZone;
			sb.Append("\n{{C|Verdict here}}: ").Append(KingdomExileRules.JudgeReturn(System.Exiled, System.Founded, System.ExiledRealmKeptGround, here != null && System.ExiledRealmHolds(here.ZoneID), regard));
			return sb.ToString();
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
				sb.Append("\nRegard: ").Append(system.FounderRegard()).Append(" (").Append(KingdomExileRules.RegardName(KingdomExileRules.ClassifyRegard(system.FounderRegard()))).Append(", last spoken ").Append(KingdomExileRules.RegardName((RealmRegard)system.RegardSpoken)).Append(")");
			}
			if (system.Exiled)
			{
				sb.Append("\n").Append(ExileReport(system));
			}
			sb.Append("\nStyle: ").Append(system.Style).Append(" (").Append(KingdomFounding.StyleGroundClause(system.Style)).Append(")").Append("  Stage: ").Append(system.Stage).Append("  Withered: ").Append(system.Withered).Append("  Famished: ").Append(system.Famished);
			sb.Append("\nFounding terrain: blueprint=").Append(system.FoundingTerrainBlueprint ?? "(none)").Append(" region=").Append(system.FoundingRegionName ?? "(none)").Append(" z=").Append(system.FoundingZLevel);
			Zone here = The.Player?.CurrentZone;
			if (here != null && system.ClaimedZones.Contains(here.ZoneID))
			{
				KingdomSurvey survey = KingdomSurvey.Take(here, system);
				sb.Append("\nHere: defence=").Append(survey.Defence()).Append(" (garrison ").Append(survey.DistrictDefenceBonus).Append(")")
					.Append(" larder=").Append(survey.FoodAbundance).Append("/").Append(survey.FoodStored).Append(" of ").Append(survey.FoodCapacity)
					.Append(" made=").Append(KingdomGrowth.FoodMadePerDay(survey)).Append(" eats=").Append(KingdomRules.RationsPerDay(system.Population))
					.Append(" hunger=").Append(system.HungerStreak)
					.Append(" beds=").Append(survey.Beds).Append(" citizens=").Append(survey.Citizens);
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
			if (!system.Founded && !system.Exiled)
			{
				Popup.Show("Nothing to reset.");
				return;
			}
			string held = system.Founded ? ("{{C|" + system.KingdomDisplayName + "}}, all " + system.SettlementCount + " of its cities") : "the realm you hold (none)";
			string remembered = system.Exiled ? (", and {{C|" + system.ExiledDisplayName + "}}, which put you out") : "";
			if (Popup.ShowYesNo("Dissolve " + held + remembered + ", and wipe all kingdom state? (Debug only; claimed-zone properties in unvisited zones are left behind.)") != DialogResult.Yes)
			{
				return;
			}
			foreach (string name in new string[2] { system.KingdomFactionName, system.ExiledFactionName })
			{
				if (string.IsNullOrEmpty(name))
				{
					continue;
				}
				// A faction cannot be unregistered at runtime; hiding it and dropping every edge
				// to it is as close as a debug reset can honestly get.
				Faction faction = Factions.GetIfExists(name);
				if (faction != null)
				{
					faction.Visible = false;
				}
				foreach (Faction item in Factions.Loop())
				{
					item.FactionFeeling.Remove(name);
				}
				The.Game.PlayerReputation.ReputationValues.Remove(name);
			}
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
			// The exile slot is state too, and a reset that left a remembered realm behind would
			// have the next founding start with a door already shut.
			system.ExiledFactionName = null;
			system.ExiledDisplayName = null;
			system.ExiledSeat = null;
			system.ExiledAway = null;
			system.ExiledDeed = null;
			system.ExiledTick = 0L;
			system.ExiledStandings.Clear();
			system.RegardSpoken = (int)RealmRegard.Beloved;
			system.ReturnAskedRegard = int.MinValue;
			system.DoorClosedTold = false;
			system.ActiveDealKeys.Clear();
			system.ActiveDealFactions.Clear();
			system.DealNextTicks.Clear();
			system.ChronicleEntries.Clear();
			system.OutsiderEntries.Clear();
			system.Standings.Clear();
			system.Manifest = null;
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
				Popup.Show(system.Exiled
					? (ExileReport(system) + "\n\n{{K|You hold no realm. The basin still pours: kingdom:found NAME founds a new one, and shuts the door on this one for good.}}")
					: "No kingdom founded. Wish {{W|kingdom:found NAME}} to begin.");
				return;
			}
			Popup.Show(SeatLine(system) + "\n" + RegardReport(system) + "\n" + KingdomReports.Status(system) + "\n\n" + KingdomReports.Standings(system)
				+ (system.Exiled ? ("\n\n" + ExileReport(system)) : ""));
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
				if (!system.Exiled)
				{
					Popup.Show("Selftest needs a founded kingdom. Wish {{W|kingdom:found NAME}} first.");
					return;
				}
				// A founder standing outside every realm is a state worth asserting on, not a
				// state to refuse to look at: it is the one where a whole realm is being held in
				// a slot nothing else reads.
				StringBuilder exileOnly = new StringBuilder();
				int exilePassed = 0;
				int exileFailed = 0;
				ExileChecks(system, exileOnly, ref exilePassed, ref exileFailed);
				Popup.Show("{{C|Kingdom selftest}} (exiled, no realm held): {{G|" + exilePassed + " passed}}" + ((exileFailed > 0) ? (", {{R|" + exileFailed + " FAILED}}") : "") + "\n" + exileOnly.ToString());
				return;
			}
			StringBuilder report = new StringBuilder();
			int passed = 0;
			int failed = 0;
			Check(report, ref passed, ref failed, "faction registered", Factions.GetIfExists(system.KingdomFactionName) != null);
			Faction kingdom = Factions.GetIfExists(system.KingdomFactionName);
			// Derived, not absolute: a realm that has come to doubt its founder must be allowed to
			// say so, and an assertion that it always loves them would forbid the whole ladder.
			int regardFeeling = Reputation.GetFeeling((float)system.FounderRegard());
			Check(report, ref passed, ref failed, "kingdom feeling toward Player mirrors its regard (" + system.FounderRegard() + " -> " + regardFeeling + ")", kingdom != null && kingdom.FactionFeeling.TryGetValue("Player", out var playerFeeling) && playerFeeling == regardFeeling);
			// The pure ladder copies four vanilla reputation thresholds by value. Walking the
			// reference in both directions is the only way a copied constant can be trusted: a
			// vanilla rebalance that moved REPUTATION_HATED would otherwise change nothing here
			// and everything in play.
			bool ladderParity = KingdomExileRules.RegardLoved == RuleSettings.REPUTATION_LOVED
				&& KingdomExileRules.RegardLiked == RuleSettings.REPUTATION_LIKED
				&& KingdomExileRules.RegardDisliked == RuleSettings.REPUTATION_DISLIKED
				&& KingdomExileRules.RegardHated == RuleSettings.REPUTATION_HATED;
			foreach (int regardProbe in new int[10] { -1000, RuleSettings.REPUTATION_HATED, RuleSettings.REPUTATION_HATED + 1, -300, RuleSettings.REPUTATION_DISLIKED, 0, RuleSettings.REPUTATION_LIKED - 1, RuleSettings.REPUTATION_LIKED, RuleSettings.REPUTATION_LOVED, 1000 })
			{
				// ClassifyRegard is ordered best-first and GetAttitude best-last, so agreement is
				// tier + attitude == 2 across the whole ladder.
				if ((int)KingdomExileRules.ClassifyRegard(regardProbe) + Reputation.GetAttitude(regardProbe) != 2)
				{
					ladderParity = false;
					report.Append("\n    regard ").Append(regardProbe).Append(" reads ").Append(KingdomExileRules.ClassifyRegard(regardProbe)).Append(" but vanilla attitude ").Append(Reputation.GetAttitude(regardProbe));
				}
			}
			Check(report, ref passed, ref failed, "the regard ladder agrees with vanilla's own reputation thresholds", ladderParity);
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
			ChainChecks(report, ref passed, ref failed);
			ExileChecks(system, report, ref passed, ref failed);
			Popup.Show("{{C|Kingdom selftest}}: {{G|" + passed + " passed}}" + ((failed > 0) ? (", {{R|" + failed + " FAILED}}") : "") + "\n" + report.ToString());
		}

		/// <summary>
		/// Asserts the loaded upgrade chains hang together. <c>Art/check_xml_refs.py</c> walks our
		/// own catalogue before it ships; this is the same walk over whatever is actually loaded,
		/// which is the only place a third-party file's chain can be caught. A chain into a design
		/// nobody declares refuses forever, and a ring improves the settlement in a circle until
		/// the reserve stops it.
		/// </summary>
		private static void ChainChecks(StringBuilder Report, ref int Passed, ref int Failed)
		{
			bool resolves = true;
			bool acyclic = true;
			foreach (KeyValuePair<string, KingdomUpgradeRules.UpgradeChain> chain in KingdomUpgrade.Chains)
			{
				if (chain.Value == null || !chain.Value.Defined)
				{
					continue;
				}
				if (!KingdomData.TryGetBuilding(chain.Value.SuccessorKey, out var successor)
					|| GameObjectFactory.Factory.GetBlueprintIfExists(successor.Blueprint) == null)
				{
					resolves = false;
					Report.Append("\n    ").Append(chain.Key).Append(" upgrades into ").Append(chain.Value.SuccessorKey).Append(", which does not resolve");
					continue;
				}
				List<string> walked = new List<string> { chain.Key };
				string at = chain.Value.SuccessorKey;
				while (KingdomUpgrade.TryGetChain(at, out var next) && !walked.Contains(at))
				{
					walked.Add(at);
					at = next.SuccessorKey;
				}
				if (walked.Contains(at))
				{
					acyclic = false;
					Report.Append("\n    upgrade chain loops: ").Append(string.Join(" -> ", walked.ToArray())).Append(" -> ").Append(at);
				}
			}
			Check(Report, ref Passed, ref Failed, "every upgrade chain names a design that resolves", resolves);
			Check(Report, ref Passed, ref Failed, "no upgrade chain loops back on itself", acyclic);
		}

		/// <summary>
		/// Asserts what an expulsion promised: that the realm it took the founder out of is still
		/// there, still owns its own ground, and has not been quietly merged with anything the
		/// founder holds now. Silent when no expulsion is on the record.
		/// </summary>
		private static void ExileChecks(KingdomSystem System, StringBuilder Report, ref int Passed, ref int Failed)
		{
			if (!System.Exiled)
			{
				return;
			}
			Faction old = Factions.GetIfExists(System.ExiledFactionName);
			// The whole promise of realm-scoped secession: the faction survives, because a runtime
			// faction cannot be unmade and the city has to go on without the founder.
			Check(Report, ref Passed, ref Failed, "the realm that put you out is still registered (" + System.ExiledFactionName + ")", old != null);
			Check(Report, ref Passed, ref Failed, "it is not the realm you hold now", System.ExiledFactionName != System.KingdomFactionName);
			Check(Report, ref Passed, ref Failed, "it has a seat to be restored into", System.ExiledSeat != null);
			bool exiledClaimsCoherent = true;
			int exiledClaims = 0;
			foreach (string zoneID in ExiledClaims(System))
			{
				exiledClaims++;
				Zone zone = The.ZoneManager.GetZone(zoneID);
				if (zone != null && zone.GetZoneProperty("faction", null) != System.ExiledFactionName)
				{
					exiledClaimsCoherent = false;
					Report.Append("\n    ").Append(zoneID).Append(" reads faction ").Append(zone.GetZoneProperty("faction", null) ?? "(none)");
				}
			}
			Check(Report, ref Passed, ref Failed, "its ground still carries its own faction property (" + exiledClaims + " claims)", exiledClaimsCoherent);
			bool disjoint = true;
			foreach (string zoneID in ExiledClaims(System))
			{
				if (System.ClaimedZones.Contains(zoneID) || (System.Away != null && System.Away.ClaimedZones.Contains(zoneID)))
				{
					disjoint = false;
					Report.Append("\n    the realm you hold also claims ").Append(zoneID);
				}
			}
			// Two realms claiming one zone would let a second founding hijack the city that
			// disowned the founder, which is the one thing exile promised could not happen.
			Check(Report, ref Passed, ref Failed, "no ground is claimed by both the old realm and the one you hold", disjoint);
			Check(Report, ref Passed, ref Failed, "its standings ledger is held apart from yours (" + System.ExiledStandings.Count + " entries)", !ReferenceEquals(System.ExiledStandings, System.Standings));
			Check(Report, ref Passed, ref Failed, "the return verdict here is reachable prose", !string.IsNullOrEmpty(KingdomExileRules.ReturnRefusal(ReturnVerdict.RegardTooLow, System.ExiledDisplayName, System.KingdomDisplayName)));
		}

		/// <summary>Every zone the expelled-from realm holds, across both of its cities.</summary>
		private static IEnumerable<string> ExiledClaims(KingdomSystem System)
		{
			if (System.ExiledSeat != null)
			{
				foreach (string zoneID in System.ExiledSeat.ClaimedZones)
				{
					yield return zoneID;
				}
			}
			if (System.ExiledAway != null)
			{
				foreach (string zoneID in System.ExiledAway.ClaimedZones)
				{
					yield return zoneID;
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
