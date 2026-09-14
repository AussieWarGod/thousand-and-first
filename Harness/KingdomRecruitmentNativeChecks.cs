using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Rules;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomRecruitmentNativeChecks
	{
		private static readonly string[] FactionsToProbe =
			{ "Farmers", "Mechanimists", "Snapjaws", "Issachari", "Hindren", "Dromad" };
		private readonly KingdomSystem System;
		private readonly XRLGame Game;
		private readonly Action<bool, string> Require;
		private KingdomGrowthArrivalCandidate Frozen;
		private string FrozenName, FrozenOrigin, FrozenBlueprint, FrozenHash;
		private KingdomRegardLedgerSnapshot CivicBefore;
		private Dictionary<string, float> PersonalBefore;
		private readonly Dictionary<string, int?> FeelingsBefore = new Dictionary<string, int?>();

		internal KingdomRecruitmentNativeChecks(KingdomSystem System, XRLGame Game,
			Action<bool, string> Require)
		{
			this.System = System; this.Game = Game; this.Require = Require;
		}

		internal void Probe(StringBuilder Evidence)
		{
			Capture();
			try
			{
				SetRegards(null);
				Require(!KingdomSemanticSelection.TryPrepareGrowthArrivalPayload(System, 71UL,
					Game.TimeTicks, false, out var absent, out string failure)
					&& absent == null && failure == Simulation.Kernel.KingdomRecruitmentRules.NoEligibleFailure,
					"hostile recruitment pool did not refuse without a person plan");
				int bodies = 0;
				foreach (string faction in FactionsToProbe)
				{
					SetRegards(faction);
					for (int firstGuest = 0; firstGuest < 2; firstGuest++)
					{
						Random ambient = Stat.Rnd, naming = Stat.NamingRnd;
						Require(KingdomSemanticSelection.TryPrepareGrowthArrivalPayload(System,
							72UL, Game.TimeTicks, firstGuest == 1, out var plan, out failure), failure);
						Require(KingdomSemanticSelection.TryPrepareGrowthArrivalPayload(System,
							72UL, Game.TimeTicks, firstGuest == 1, out var repeat, out failure), failure);
						Require(ReferenceEquals(ambient, Stat.Rnd) && ReferenceEquals(naming, Stat.NamingRnd),
							"native recruitment left ambient random streams replaced");
						Require(plan.Name == repeat.Name && plan.Origin == repeat.Origin
							&& plan.Blueprint == repeat.Blueprint && plan.Creed == repeat.Creed,
							"native recruitment rerolled a fixed event");
						var blueprint = GameObjectFactory.Factory.Blueprints[plan.Blueprint];
						Require(blueprint.GetTag(KingdomRecruitment.FactionTag, null) == faction
							&& blueprint.GetTag(KingdomRecruitment.OriginTag, null) == plan.Origin,
							"native recruitment mixed culture, origin or hostile source faction");
						GameObject body = GameObject.Create(plan.Blueprint);
						try
						{
							Require(GameObject.Validate(body) && body.Body != null && body.Brain != null
								&& body.GetPrimaryFaction() == faction && body.CurrentCell == null,
								"recruit did not retain its native body and source allegiance");
							string culture = blueprint.GetTag("Culture", null), species = blueprint.GetTag("Species", null);
							Require((culture == null || body.GetCulture() == culture)
								&& (species == null || body.GetSpecies() == species),
								"recruit body disagreed with native naming culture/species");
							if (faction == "Hindren") Require(body.HasPart("MultipleLegs"),
								"hindren recruit lost native leg mutation");
							bodies++;
						}
						finally { if (GameObject.Validate(body)) body.Obliterate(); }
					}
				}
				Evidence.Append("; recruitment-catalogue factions=6 bodies=").Append(bodies)
					.Append(" both-routes=true deterministic=true native-culture=true hostile-pool-refused=true");
			}
			finally { Restore(); }
			Evidence.Append(" synthetic-reputation-and-unplaced-bodies=true restored=true");
		}

		internal void Observe(StringBuilder Evidence)
		{
			var candidate = System.LifecycleBook?.Growth?.ArrivalCandidate;
			Require(candidate != null && candidate.FirstGuest != null, "actual first guest was not frozen");
			if (Frozen == null)
			{
				Frozen = candidate; FrozenName = candidate.PlannedName; FrozenOrigin = candidate.PlannedOrigin;
				FrozenBlueprint = candidate.Blueprint; FrozenHash = candidate.PlanHash;
				Capture(); SetRegards(null);
				Evidence.Append("; recruitment-frozen before-hostility=true name=").Append(FrozenName)
					.Append(" blueprint=").Append(FrozenBlueprint);
				return;
			}
			try
			{
				Require(ReferenceEquals(candidate, Frozen) && candidate.PlannedName == FrozenName
					&& candidate.PlannedOrigin == FrozenOrigin && candidate.Blueprint == FrozenBlueprint
					&& candidate.PlanHash == FrozenHash, "hostility rerolled the existing first guest");
				Evidence.Append("; recruitment-frozen after-hostility=true identity-unchanged=true");
			}
			finally { Restore(); }
		}

		private void Capture()
		{
			Require(PersonalBefore == null && System.TryCaptureRegardLedger(out CivicBefore),
				"recruitment probe could not retain civic authority");
			PersonalBefore = Game.PlayerReputation.ReputationValues;
			Game.PlayerReputation.ReputationValues = new Dictionary<string, float>(PersonalBefore);
			foreach (string name in FactionsToProbe)
			{
				Faction faction = Factions.GetIfExists(name);
				Require(faction != null, "native recruitment faction missing: " + name);
				FeelingsBefore[name] = faction.FactionFeeling.TryGetValue("Player", out int feeling) ? feeling : (int?)null;
			}
		}

		private void SetRegards(string OnlyFriendly)
		{
			foreach (string name in FactionsToProbe)
			{
				int value = name == OnlyFriendly ? 1000 : -1000;
				Game.PlayerReputation.Set(name, value);
				Require(System.TrySetRegardForRealm(name, value, false), "recruitment civic probe refused");
			}
		}

		private void Restore()
		{
			if (PersonalBefore == null) return;
			Game.PlayerReputation.ReputationValues = PersonalBefore;
			foreach (var pair in FeelingsBefore)
			{
				var feelings = Factions.GetIfExists(pair.Key).FactionFeeling;
				if (pair.Value.HasValue) feelings["Player"] = pair.Value.Value;
				else feelings.Remove("Player");
			}
			Require(System.TryRestoreRegardLedger(CivicBefore), "recruitment civic snapshot restoration failed");
			PersonalBefore = null; CivicBefore = null; FeelingsBefore.Clear();
		}
	}
}
