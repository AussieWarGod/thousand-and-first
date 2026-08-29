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
				Faction faction = Factions.GetIfExists(standing.Key);
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
			Faction realmFaction = Factions.GetIfExists(system.KingdomFactionName);
			foreach (System.Collections.Generic.KeyValuePair<string, int> policy in
				system.RealmPolicyToward)
			{
				Faction faction = Factions.GetIfExists(policy.Key);
				if (faction == null) continue;
				int feeling = 0;
				if (realmFaction != null)
					realmFaction.FactionFeeling.TryGetValue(policy.Key, out feeling);
				if (realmFaction == null || feeling != Reputation.GetFeeling((float)policy.Value))
				{
					mirrorConsistent = false;
					report.Append("\n    mismatch: realm policy toward ").Append(policy.Key)
						.Append(" is ").Append(policy.Value).Append(" but feeling ").Append(feeling);
				}
				checkedCount++;
			}
			Check(report, ref passed, ref failed, "both directional mirrors agree across " +
				checkedCount + " owned edges", mirrorConsistent);
			bool carriesValid = system.RegardSpilloverRemainders.Count <=
				KingdomStandingRules.MaxRelationships;
			foreach (System.Collections.Generic.KeyValuePair<string, int> carry in
				system.RegardSpilloverRemainders)
				if (!KingdomStandingRules.EligibleForeignFaction(carry.Key,
					system.KingdomFactionName) ||
					Factions.GetIfExists(carry.Key) == null ||
					!KingdomStandingRules.ValidRemainder(carry.Value)) carriesValid = false;
			Check(report, ref passed, ref failed,
				"fractional spillover carry is bounded, signed, and names live foreign factions",
				carriesValid);
			bool observationsValid = system.RegardSpilloverObservedReputation.Count <=
				KingdomStandingRules.MaxRelationships;
			foreach (System.Collections.Generic.KeyValuePair<string, int> baseline in
				system.RegardSpilloverObservedReputation)
				if (!KingdomStandingRules.EligibleForeignFaction(baseline.Key,
					system.KingdomFactionName) || Factions.GetIfExists(baseline.Key) == null)
					observationsValid = false;
			Check(report, ref passed, ref failed,
				"spillover observations are bounded and name live foreign factions",
				observationsValid);
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

	}
}
