using System.Text;
using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;

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
			Zone zone = The.Player?.CurrentZone;
			if (KingdomFounding.ClaimZone(zone))
			{
				Popup.Show("This zone now belongs to the kingdom: {{C|" + zone.ZoneID + "}}\n\nFuture spawns here will enroll as citizens.");
			}
			else
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
			}
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
			Zone currentZone = The.Player?.CurrentZone;
			bool currentClaimed = currentZone != null && system.ClaimedZones.Contains(currentZone.ZoneID);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{{C|").Append(system.KingdomDisplayName).Append("}}, founded tick ").Append(system.FoundedTick)
				.Append("\nStage: ")
				.Append(system.Stage)
				.Append("  Population: ")
				.Append(system.Population)
				.Append("\nClaimed zones: ")
				.Append(system.ClaimedZones.Count)
				.Append(currentClaimed ? ("  (here: " + KingdomGrowth.CountStoredWater(currentZone) + " drams stored, " + KingdomGrowth.CountOpenWater(currentZone) + " open, space for " + KingdomGrowth.CountStorageSpace(currentZone) + ")") : "")
				.Append("\nUpkeep: ")
				.Append(KingdomRules.UpkeepDrams(system.Population))
				.Append(" drams per interval  Thirst streak: ")
				.Append(system.DryStreak)
				.Append("\nNext arrival due: tick ")
				.Append(system.NextArrivalTick)
				.Append(" (now ")
				.Append(The.Game.TimeTicks)
				.Append(")\nPlayer rep with kingdom: ")
				.Append(The.Game.PlayerReputation.Get(system.KingdomFactionName))
				.Append("\n");
			int shown = 0;
			foreach (Faction faction in Factions.Loop())
			{
				if (faction.Name == system.KingdomFactionName || faction.Name == "Player" || !faction.Visible)
				{
					continue;
				}
				int standing = system.GetStanding(faction.Name);
				if (standing != 0)
				{
					faction.FactionFeeling.TryGetValue(system.KingdomFactionName, out var feeling);
					stringBuilder.Append("\n").Append(faction.DisplayName).Append(": standing ")
						.Append(standing)
						.Append(", their feeling toward us ")
						.Append(feeling);
					shown++;
					if (shown >= 18)
					{
						stringBuilder.Append("\n...");
						break;
					}
				}
			}
			Popup.Show(stringBuilder.ToString());
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
			if (!system.Founded || system.ChronicleEntries.Count == 0)
			{
				Popup.Show("The chronicle is empty.");
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{{C|The Chronicle of ").Append(system.KingdomDisplayName).Append("}}\n");
			int start = (system.ChronicleEntries.Count > 25) ? (system.ChronicleEntries.Count - 25) : 0;
			for (int i = start; i < system.ChronicleEntries.Count; i++)
			{
				stringBuilder.Append("\n").Append(system.ChronicleEntries[i]);
			}
			Popup.Show(stringBuilder.ToString());
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
