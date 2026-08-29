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
			Check(Report, ref Passed, ref Failed, "its directional policy and spillover carry are held apart too",
				!ReferenceEquals(System.ExiledRealmPolicyToward, System.RealmPolicyToward) &&
				!ReferenceEquals(System.ExiledRegardSpilloverRemainders,
					System.RegardSpilloverRemainders) &&
				!ReferenceEquals(System.ExiledRegardSpilloverObservedReputation,
					System.RegardSpilloverObservedReputation));
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
