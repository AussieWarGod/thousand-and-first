using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementAuthority
	{
		public static bool TryCleanActiveGround(KingdomSystem System, Zone Zone,
			out KingdomRealmRetirementState State,
			out KingdomRealmRetirementReport Report, out string Failure)
		{
			State = null; Report = null; Failure = null;
			if (System == null || Zone == null
				|| !System.TryReadRealmRetirement(out State, out Failure) || State == null)
				return Fail(Failure ?? "realm-removal plan is absent", out Failure);
			if (State.Phase != KingdomRealmRetirementPhase.CleaningGround)
				return Fail("realm-removal plan is not accepting ground cleanup", out Failure);
			int at = State.Locators.FindIndex(row => row.ZoneId == Zone.ZoneID);
			if (at < 0) return Fail("active zone is outside the frozen locator set", out Failure);
			long tick = Math.Max(State.UpdatedTick, The.Game?.TimeTicks ?? 0L);
			// The whole-ground custody plan and its owner-specific subplan both authorize
			// current evidence before the first endpoint mutation. Generic apply gets a fresh
			// post-endpoint plan below, so neither cut repeats a stale destructive preview.
			if (!KingdomRealmRetirementGround.TryPrepare(System, Zone,
				out KingdomRealmRemovalGroundPlan custodyPlan, out Failure)
				|| !KingdomRealmRetirementGround.TryAuthorizeRecords(State, custodyPlan, tick,
					out Failure)
				|| !KingdomPolityRemovalRuntime.TryPrepareLoadedGroundRetirement(System, Zone,
					out KingdomPolityLoadedGroundRetirementPlan polityPlan, out Failure)
				|| !KingdomPolityRemovalRuntime.TryApplyLoadedGroundRetirement(System, Zone, tick,
					polityPlan, out Failure)) return false;
			if (!TryCurrentDigests(System, State.Locators, out string _,
				out string authorityDigest, out Failure) || authorityDigest != State.AuthorityDigest)
				return Fail("realm authority diverged from its frozen removal plan", out Failure);
			KingdomRealmRemovalGroundPlan plan = null;
			bool reopening = State.Locators[at].State == KingdomRemovalLocatorState.Cleaned;
			if (reopening)
			{
				if (!KingdomRealmRetirementGround.TryPrepare(System, Zone, out plan, out Failure)
					|| !KingdomRealmRetirementGround.TryAuthorizeRecords(State, plan, tick,
						out Failure)) return false;
				if (plan.ProjectedEvidenceDigest == State.Locators[at].EvidenceDigest)
				{
					// Older receipts may have the aggregate v2 rows but not the two reserved
					// prior-unknown disclosures. Revisit publishes only those idempotent previews.
					if (!PublishPreMutationDisclosures(System, ref State, plan, tick,
						out Failure)) return false;
					Report = FromState(State); return true;
				}
			}
			if (State.Locators[at].State != KingdomRemovalLocatorState.Cleaning)
			{
				if (!KingdomRealmRetirementRules.TryMarkGround(State, State.Revision, Zone.ZoneID,
					KingdomRemovalLocatorState.Cleaning, tick, 0, null,
					out KingdomRealmRetirementState cleaning, out Failure)
					|| !TryPublish(System, State, cleaning, out Failure)) return false;
				State = cleaning;
			}
			if (plan == null && !KingdomRealmRetirementGround.TryPrepare(System, Zone,
				out plan, out Failure))
			{
				string refusal = Failure;
				if (KingdomRealmRetirementRules.TryMarkGround(State, State.Revision, Zone.ZoneID,
					KingdomRemovalLocatorState.Contested, tick, 0, null,
					out KingdomRealmRetirementState contested, out string markFailure)
					&& TryPublish(System, State, contested, out markFailure)) State = contested;
				Report = FromState(State); Failure = refusal; return false;
			}
			if (!reopening && !KingdomRealmRetirementGround.TryAuthorizeRecords(State, plan, tick,
				out Failure)) return false;
			if (!PublishPreMutationDisclosures(System, ref State, plan, tick, out Failure))
				return false;
			if (!KingdomRealmRetirementGround.TryApply(System, plan,
				out string evidence, out Failure)) return false;
			if (evidence != plan.ProjectedEvidenceDigest)
				return Fail("ground cleanup differed from its authorized evidence", out Failure);
			if (!PublishObjectCompletions(System, ref State, plan, tick, out Failure))
				return false;
			if (!PublishRecord(System, ref State, plan.ObjectRecord, tick, out Failure))
			{
				string publicationFailure = Failure;
				if (KingdomRealmRetirementRules.TryMarkGround(State, State.Revision, Zone.ZoneID,
					KingdomRemovalLocatorState.Diverged, tick, 0, null,
					out KingdomRealmRetirementState diverged, out string markFailure)
					&& TryPublish(System, State, diverged, out markFailure)) State = diverged;
				Failure = publicationFailure; Report = FromState(State); return false;
			}
			if (!KingdomRealmRetirementRules.TryMarkGround(State, State.Revision, Zone.ZoneID,
				KingdomRemovalLocatorState.Cleaned, tick, plan.RetainedObjectCount, evidence,
				out KingdomRealmRetirementState cleaned, out Failure)
				|| !TryPublish(System, State, cleaned, out Failure)) return false;
			State = cleaned; Report = FromState(State); return true;
		}

		private static bool PublishPreMutationDisclosures(KingdomSystem System,
			ref KingdomRealmRetirementState State, KingdomRealmRemovalGroundPlan Plan,
			long Tick, out string Failure)
		{
			Failure = null;
			if (Plan.LegacyCitizenRecord != null)
			{
				if (!PublishRecord(System, ref State, Plan.LegacyCitizenRecord,
					Tick, out Failure)) return false;
			}
			if (Plan.SharedFactionRecord != null)
			{
				if (!PublishRecord(System, ref State, Plan.SharedFactionRecord,
					Tick, out Failure)) return false;
			}
			for (int i = 0; i < Plan.ObjectPreviewRecords.Count; i++)
				if (!PublishRecord(System, ref State, Plan.ObjectPreviewRecords[i],
					Tick, out Failure)) return false;
			return true;
		}

		private static bool PublishObjectCompletions(KingdomSystem System,
			ref KingdomRealmRetirementState State, KingdomRealmRemovalGroundPlan Plan,
			long Tick, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Plan.ObjectCompletionRecords.Count; i++)
				if (!PublishRecord(System, ref State, Plan.ObjectCompletionRecords[i],
					Tick, out Failure)) return false;
			return true;
		}

		private static bool PublishRecord(KingdomSystem System,
			ref KingdomRealmRetirementState State, KingdomRemovalRecord Record,
			long Tick, out string Failure)
		{
			Failure = null;
			if (!KingdomRealmRetirementRules.TryRecord(State, State.Revision, Record, Tick,
				out KingdomRealmRetirementState next, out Failure)
				|| !TryPublish(System, State, next, out Failure)) return false;
			State = next; return true;
		}
	}
}
