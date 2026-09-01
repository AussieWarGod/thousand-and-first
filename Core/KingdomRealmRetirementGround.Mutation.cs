using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomRealmRetirementGround
	{
		internal static bool TryApply(KingdomSystem System,
			KingdomRealmRemovalGroundPlan Plan, out string EvidenceDigest,
			out string Failure)
		{
			EvidenceDigest = null; Failure = null;
			if (System == null || Plan?.Zone == null || The.Game == null
				|| !ReferenceEquals(Plan.Zone, The.Player?.CurrentZone))
				return Fail("active ground changed after cleanup preview", out Failure);
			if (!TryRevalidate(System, Plan, out Failure)) return false;
			if (TryActualGroundEvidence(Plan, out string current, out Failure)
				&& current == Plan.ProjectedEvidenceDigest)
			{
				EvidenceDigest = current; return true;
			}
			if (Failure != null) return false;
			if (!KingdomMarketRemoval.TryPrepareTransaction(System,
				Plan.MarketStockRetirements, Plan.LegendaryMarketRetirements,
				out KingdomMarketRemovalTransaction market, out Failure)) return false;
			if (!KingdomRelocation.TryRetireForRealmRemoval(Plan.Relocation, out Failure))
				return false;
			for (int i = 0; i < Plan.StasisVaults.Count; i++)
				if (!KingdomStasisVault.TryReleaseForRealmRemoval(Plan.StasisVaults[i],
					out Failure)) return false;
			for (int i = 0; i < Plan.ExactForeignCitizens.Count; i++)
				if (!KingdomCitizenship.TryRemove(System, Plan.ExactForeignCitizens[i],
					KingdomCitizenshipRemovalReason.ForeignTransfer, out Failure)) return false;
			for (int i = 0; i < Plan.MutationObjects.Count; i++)
			{
				GameObject item = Plan.MutationObjects[i];
				if (!GameObject.Validate(item)) continue;
				if (!TryRemoveExperienceProjections(System, item, out Failure)) return false;
				if (Plan.Fallbacks.TryGetValue(item, out GameObjectBlueprint fallback))
					item.SetBlueprint(fallback);
				KingdomRemovalProjectionRuntime.StripCampfireRecipe(item);
			}
			if (!KingdomExternalOwnershipBindingRuntime.TryClearForRealmReset(Plan.Zone,
				new List<string> { System.RealmId, System.KingdomFactionName },
				Plan.ExternalOwnership, out Failure))
				return false;
			if (!TryRetireWitnessWorks(System, Plan, out Failure)) return false;
			if (!KingdomMarketRemoval.TryCommitTransaction(System, market, out Failure)) return false;
			if (TryVerify(Plan, out EvidenceDigest, out Failure)) return true;
			string verifyFailure = Failure;
			if (!KingdomMarketRemoval.TryRollback(market, out string rollbackFailure))
				KingdomLog.Log("market removal rollback failed: " + rollbackFailure);
			Failure = verifyFailure ?? "ground verification failed after market retirement";
			return false;
		}

		private static bool TryVerify(KingdomRealmRemovalGroundPlan Plan,
			out string Digest, out string Failure)
		{
			Digest = null; Failure = null;
			List<GameObject> actual = new List<GameObject>();
			if (!TryObjectGraph(Plan.Zone, actual, out Failure)) return false;
			HashSet<GameObject> present = new HashSet<GameObject>(actual);
			for (int i = 0; i < actual.Count; i++)
			{
				GameObject item = actual[i];
				if (!GameObject.Validate(item)) continue;
				if (KingdomRemovalCoverage.IsOwnedBlueprint(item.Blueprint))
					return Fail("owned blueprint remains after conversion", out Failure);
				if (item.GetPart<r_KingdomOfficeProjection>() != null
					|| item.GetPart<r_KingdomRemembranceProjection>() != null
					|| item.GetPart<r_KingdomWitnessWorkProjection>() != null
					|| (Plan.MarketStockRetirements.Contains(item)
						&& item.GetPart<r_KingdomMarketStockProjection>() != null)
					|| (Plan.LegendaryMarketRetirements.Contains(item)
						&& item.GetPart<r_KingdomLegendaryMarketProjection>() != null))
					return Fail("an exact civic experience projection remains after restoration",
						out Failure);
				if (!KingdomRemovalProjectionRuntime.TryInspectCampfire(item,
					out List<string> campfire, out Failure) || campfire.Count > 0)
					return Fail(Failure ?? "custom campfire recipe remains after conversion",
						out Failure);
			}
			foreach (GameObject removed in Plan.RemovedObjects)
				if (present.Contains(removed))
					return Fail("owner-specific value release left a removed carrier on ground",
						out Failure);
			foreach (KeyValuePair<GameObject, GameObjectBlueprint> row in Plan.Fallbacks)
				if (GameObject.Validate(row.Key) && row.Key.Blueprint != row.Value.Name)
					return Fail("owned blueprint differs from its exact vanilla fallback", out Failure);
			if (!TryActualGroundEvidence(Plan, out Digest, out Failure)) return false;
			return Digest == Plan.ProjectedEvidenceDigest
				|| Fail("ground roster differs from its bounded projected aggregate", out Failure);
		}

		private static bool TryActualGroundEvidence(KingdomRealmRemovalGroundPlan Plan,
			out string Digest, out string Failure)
		{
			Digest = null; Failure = null;
			List<GameObject> actual = new List<GameObject>();
			if (Plan?.Zone == null || !TryObjectGraph(Plan.Zone, actual, out Failure)) return false;
			List<string> rows = new List<string>();
			for (int i = 0; i < actual.Count; i++) rows.Add(ObjectRosterRow(actual[i]));
			rows.Add("zone=" + Plan.Zone.ZoneID); rows.Add("recovery=" + Plan.RecoveryDigest);
			rows.Add("legacy=" + Plan.LegacyCitizenCount); rows.Add("shared="
				+ (Plan.SharedFaction ?? "<null>"));
			rows.Sort(StringComparer.Ordinal);
			Digest = KingdomRetirementDigestRules.Evidence("clean-ground-v2", rows);
			return true;
		}

		private static bool Contains(string[] Values, string Value)
		{
			for (int i = 0; i < Values.Length; i++) if (Values[i] == Value) return true;
			return false;
		}
	}
}
