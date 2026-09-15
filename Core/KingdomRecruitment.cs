using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomRecruitment
	{
		internal const string FactionTag = "r_TAF_RecruitFaction";
		internal const string OriginTag = "r_TAF_RecruitOrigin";

		internal static bool TryChoose(KingdomSystem System, SemanticEventKey Key,
			ulong Ordinal, bool FirstGuest, out string Blueprint, out string Origin,
			out string Name, out string Failure)
		{
			Blueprint = Origin = Name = Failure = null;
			if (!TryCatalogue(System, FirstGuest, out var weighted, out var origins,
				out var factions, out Failure)) return false;
			if (!KingdomSemanticSelectionRules.TryChoose(System.SimulationSeed,
				KingdomSemanticSelectionRules.RulesVersion, System.CurrentSettlementId,
				KingdomSemanticSelection.GrowthArrivalStream, KingdomSemanticSelection.PersonEventKind,
				Ordinal, 0U, weighted, out Blueprint, out var fault))
			{
				Failure = "settler reputation draw refused: " + fault; return false;
			}
			Origin = origins[Blueprint];
			return TryNativeName(System, Key, GameObjectFactory.Factory.Blueprints[Blueprint],
				factions[Blueprint], out Name, out Failure);
		}

		internal static string PendingNeed(KingdomSystem System)
		{
			var growth = System?.LifecycleBook?.Growth;
			if (!KingdomGrowth.Enabled || !KingdomMaster.NewWorkAllowed(System) || growth == null
				|| growth.ArrivalCandidate != null || growth.ArrivalOpportunity != null
				|| growth.ArrivalOp != null || growth.ArrivalDebtRanges.Count == 0) return null;
			// The next cadence pass observes configured story policy before choosing its pool.
			// Reading status must not publish an option epoch or freeze an arrival.
			bool first = growth.ArrivalDebtRanges[0].FirstOrdinal == 1UL
				&& XRL.UI.Options.GetOption(KingdomExperienceOptions.StoryOptionId, "Yes") != "No";
			if (TryCatalogue(System, first, out _, out _, out _, out string failure)) return null;
			return failure == KingdomRecruitmentRules.NoEligibleFailure
				? "No settlers are willing to come: improve your reputation and the settlement's standing with their people."
				: "Recruitment is unavailable: " + failure + ".";
		}

		private static bool TryCatalogue(KingdomSystem System, bool FirstGuest,
			out List<KingdomSemanticWeightedEntry> weighted, out Dictionary<string, string> origins,
			out Dictionary<string, string> factions, out string Failure)
		{
			weighted = null; origins = factions = null; Failure = null;
			if (System == null || The.Game?.PlayerReputation == null
				|| !System.TryCaptureRegardLedger(out _))
			{
				Failure = "settler reputation authority is unavailable"; return false;
			}
			if (!KingdomSemanticSelection.TryLoadSimpleCatalogue("r_KingdomSettlers", null,
				out var source, out Failure)) return false;
			weighted = new List<KingdomSemanticWeightedEntry>();
			origins = new Dictionary<string, string>(StringComparer.Ordinal);
			factions = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var entry in source)
			{
				if (FirstGuest && !KingdomLifecycleRules.GrowthFirstGuestBlueprintAllowed(entry.StableKey)) continue;
				GameObjectBlueprint body = GameObjectFactory.Factory.Blueprints[entry.StableKey];
				string factionName = body.GetTag(FactionTag, null), origin = body.GetTag(OriginTag, null);
				if (!body.HasTag("r_KingdomSettler") || body.HasTag("BaseObject")
					|| body.HasTag("Named") || body.HasPart("GivesRep")
					|| string.IsNullOrEmpty(factionName) || !ValidText(origin)
					|| !ExactNativeFaction(body, factionName)
					|| !body.HasPart("Brain") || !body.HasPart("Body"))
				{
					Failure = "settler catalogue lacks an ordinary coherent recruit: " + entry.StableKey;
					return false;
				}
				Faction faction = Factions.GetIfExists(factionName);
				if (faction == null || !System.CanReserveDirectionalRelationship(factionName)) continue;
				if (!System.TryGetRegardPair(factionName, out int civic, out _))
				{
					Failure = "settler civic reputation is unreadable: " + factionName; return false;
				}
				int personal = The.Game.PlayerReputation.Get(faction);
				bool hostile = The.Game.PlayerReputation.GetFeeling(faction) < 0
					|| Reputation.GetFeeling(civic) < 0;
				if (!KingdomRecruitmentRules.TryWeight(entry.Weight, personal, civic, hostile, out ulong weight))
				{
					Failure = "settler base weight exceeds its bound: " + entry.StableKey; return false;
				}
				if (weight == 0UL) continue;
				weighted.Add(new KingdomSemanticWeightedEntry(entry.StableKey, weight));
				origins.Add(entry.StableKey, origin); factions.Add(entry.StableKey, factionName);
			}
			if (weighted.Count == 0) { Failure = KingdomRecruitmentRules.NoEligibleFailure; return false; }
			return true;
		}

		private static bool ValidText(string Text)
		{
			if (string.IsNullOrWhiteSpace(Text) || Text.Length > 128) return false;
			foreach (char c in Text) if (char.IsControl(c) || char.IsSurrogate(c)) return false;
			return true;
		}

		private static bool ExactNativeFaction(GameObjectBlueprint Body, string Faction)
		{
			string value = Body.GetPartParameter<string>("Brain", "Factions", null);
			string prefix = Faction + "-";
			return value != null && value.StartsWith(prefix, StringComparison.Ordinal)
				&& int.TryParse(value.Substring(prefix.Length), out int weight) && weight > 0;
		}
	}
}
