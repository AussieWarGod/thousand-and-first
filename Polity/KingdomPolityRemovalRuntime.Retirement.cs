using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal sealed class KingdomPolityLeaseRetirementStep
	{
		internal string SourceId;
		internal bool Ambient;
		internal string BeforeDigest;
		internal string AfterDigest;
	}

	internal sealed class KingdomPolityFinalRetirementPlan
	{
		internal string ReceiptId;
		internal string LedgerBefore;
		internal string LedgerAfter;
		internal string DispatchBefore;
		internal string DispatchAfter;
		internal string ExperienceBefore;
		internal string ExperienceAfter;
		internal List<KingdomPolityLeaseRetirementStep> Releases =
			new List<KingdomPolityLeaseRetirementStep>();
	}

	internal sealed class KingdomPolityLoadedGroundRetirementPlan
	{
		internal string ZoneId;
		internal string LedgerDigest;
		internal List<string> CohortIds = new List<string>();
	}

	public static partial class KingdomPolityRemovalRuntime
	{
		internal static bool TryPrepareLoadedGroundRetirement(KingdomSystem System, Zone Zone,
			out KingdomPolityLoadedGroundRetirementPlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (System == null || Zone == null || !ReferenceEquals(Zone, The.Player?.CurrentZone)
				|| !System.TryReadRealmRetirement(out KingdomRealmRetirementState state,
					out Failure) || state == null || state.Phase !=
					KingdomRealmRetirementPhase.CleaningGround
				|| state.Locators.Find(x => x.ZoneId == Zone.ZoneID) == null)
				return Fail(Failure ?? "polity ground subplan lacks frozen attended authority",
					out Failure);
			KingdomPolityLoadedGroundRetirementPlan plan =
				new KingdomPolityLoadedGroundRetirementPlan
					{ ZoneId = Zone.ZoneID, LedgerDigest = LedgerDigest(System.PolityLedger) };
			for (int i = 0; i < System.PolityLedger.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan cohort = System.PolityLedger.Cohorts[i];
				KingdomPolityProjectionReceipt receipt = string.IsNullOrEmpty(
					cohort.ManifestationReceiptId) ? null : KingdomPolityAuthority.Projection(
						System.PolityLedger, cohort.ManifestationReceiptId);
				if (receipt?.ZoneId == Zone.ZoneID) plan.CohortIds.Add(cohort.CohortId);
			}
			plan.CohortIds.Sort(StringComparer.Ordinal); Plan = plan; return true;
		}

		internal static bool TryApplyLoadedGroundRetirement(KingdomSystem System, Zone Zone,
			long Tick, KingdomPolityLoadedGroundRetirementPlan Plan, out string Failure)
		{
			Failure = null;
			if (!TryPrepareLoadedGroundRetirement(System, Zone,
				out KingdomPolityLoadedGroundRetirementPlan actual, out Failure)
				|| Plan == null || Plan.ZoneId != actual.ZoneId
				|| Plan.LedgerDigest != actual.LedgerDigest
				|| Plan.CohortIds.Count != actual.CohortIds.Count)
				return Fail(Failure ?? "polity ground subplan lost its frozen revision", out Failure);
			for (int i = 0; i < Plan.CohortIds.Count; i++)
				if (Plan.CohortIds[i] != actual.CohortIds[i])
					return Fail("polity ground subplan changed cohort disposition", out Failure);
			return TryRetireLoadedGround(System, Zone, Tick, out Failure);
		}

		/// <summary>Reconciles only exact Polity bodies in player's already-loaded frozen locator.</summary>
		internal static bool TryRetireLoadedGround(KingdomSystem System, Zone Zone, long Tick,
			out string Failure)
		{
			Failure = null;
			if (System == null || Zone == null || !ReferenceEquals(Zone, The.Player?.CurrentZone)
				|| !ReferenceEquals(The.Player?.CurrentCell?.ParentZone, Zone)
				|| !System.TryReadRealmRetirement(out KingdomRealmRetirementState state,
					out Failure) || state == null || state.Phase !=
					KingdomRealmRetirementPhase.CleaningGround)
				return Fail(Failure ?? "polity ground retirement lacks an attended confirmed plan",
					out Failure);
			KingdomRemovalLocator locator = state.Locators.Find(x => x.ZoneId == Zone.ZoneID);
			if (locator == null || locator.SettlementId != System.SettlementIdForOwnedZone(
				Zone.ZoneID)) return Fail("loaded polity ground is outside the frozen exact locator set",
				out Failure);
			List<string> ids = new List<string>();
			for (int i = 0; i < System.PolityLedger.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan cohort = System.PolityLedger.Cohorts[i];
				KingdomPolityProjectionReceipt receipt = string.IsNullOrEmpty(
					cohort.ManifestationReceiptId) ? null : KingdomPolityAuthority.Projection(
						System.PolityLedger, cohort.ManifestationReceiptId);
					if (receipt?.ZoneId == Zone.ZoneID && (cohort.Phase ==
						KingdomPolityCohortPhase.Planned || cohort.Phase ==
						KingdomPolityCohortPhase.Materialized || cohort.Phase ==
						KingdomPolityCohortPhase.Concluded || cohort.Phase ==
						KingdomPolityCohortPhase.Abandoned)) ids.Add(cohort.CohortId);
			}
			ids.Sort(StringComparer.Ordinal);
			for (int i = 0; i < ids.Count; i++)
				if (!KingdomPolityEndpointRuntime.TryWithdrawCurrentEndpoint(System, ids[i], Tick,
					out Failure)) return false;
			for (int i = 0; i < System.PolityLedger.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan cohort = System.PolityLedger.Cohorts[i];
				KingdomPolityProjectionReceipt receipt = string.IsNullOrEmpty(
					cohort.ManifestationReceiptId) ? null : KingdomPolityAuthority.Projection(
						System.PolityLedger, cohort.ManifestationReceiptId);
					if (receipt?.ZoneId == Zone.ZoneID && cohort.Phase !=
						KingdomPolityCohortPhase.Cleaned && cohort.Phase !=
						KingdomPolityCohortPhase.Abandoned && cohort.Phase !=
						KingdomPolityCohortPhase.Archived)
					return Fail("loaded polity cohort remains unresolved at " + Zone.ZoneID,
						out Failure);
			}
			return true;
		}

		/// <summary>Read-only exact final subplan. Generic retirement authorizes this before apply.</summary>
		internal static bool TryPrepareFinalRetirement(KingdomSystem System, long Tick,
			out KingdomPolityFinalRetirementPlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (System == null || !System.TryReadRealmRetirement(
				out KingdomRealmRetirementState state, out Failure) || state == null
				|| state.Phase != KingdomRealmRetirementPhase.CleaningGround)
				return Fail(Failure ?? "polity final retirement lacks confirmed authority", out Failure);
			for (int i = 0; i < state.Locators.Count; i++)
				if (state.Locators[i].State != KingdomRemovalLocatorState.Cleaned)
					return Fail("polity final retirement awaits locator " + state.Locators[i].ZoneId,
						out Failure);
			long inspected = Math.Max(Tick, state.UpdatedTick);
			if (!TryDescribeRealmRemovalBlocker(System, inspected,
				out List<KingdomExperienceRetirementLeaseAllowance> allowances,
				out string blocker, out Failure) || blocker != null)
				return Fail(Failure ?? blocker, out Failure);

			KingdomPolityFinalRetirementPlan plan = new KingdomPolityFinalRetirementPlan
			{
				ReceiptId = state.ReceiptId, LedgerBefore = LedgerDigest(System.PolityLedger),
				DispatchBefore = DispatchDigest(System.PolityDispatch),
				ExperienceBefore = ExperienceDigest(System.Experience)
			};
			KingdomPolityDispatchState dispatch = KingdomPolityDispatchRules.CloneState(
				System.PolityDispatch);
			if (!KingdomPolityDispatchRules.TryRetire(dispatch, dispatch.Revision,
				System.RealmId, state.ReceiptId, out Failure)) return false;
			plan.DispatchAfter = DispatchDigest(dispatch);
			KingdomPolityLedger ledger = KingdomPolityRules.Clone(System.PolityLedger);
			if (!KingdomPolityRemovalRules.TrySettleBodylessRetirement(ledger, dispatch,
				ledger.Revision, state.ReceiptId, out KingdomPolityPublicationResult _,
				out Failure)) return false;
			plan.LedgerAfter = LedgerDigest(ledger);

			KingdomExperienceLedger experience = KingdomExperienceCodec.DecodeEnvelopeRaw(
				KingdomExperienceCodec.EncodeEnvelope(System.Experience));
			List<string> sources = AllowanceSources(allowances);
			for (int i = 0; i < sources.Count; i++)
			{
				string source = sources[i]; bool ambient = HasAudience(experience, source);
				KingdomPolityLeaseRetirementStep step = new KingdomPolityLeaseRetirementStep
					{ SourceId = source, Ambient = ambient,
						BeforeDigest = ExperienceDigest(experience) };
				bool released = ambient ? KingdomExperienceRules.TryReleasePresentation(experience,
					experience.Revision, KingdomPolityExperienceRuntime.AudienceReservationId(source),
					KingdomPolityExperienceRuntime.BodyReservationId(source), source,
					out KingdomExperienceCapacityFault _, out Failure)
					: KingdomExperienceRules.TryReleaseBodies(experience, experience.Revision,
						KingdomPolityExperienceRuntime.BodyReservationId(source), source,
						out KingdomExperienceCapacityFault _, out Failure);
				if (!released) return false;
				step.AfterDigest = ExperienceDigest(experience); plan.Releases.Add(step);
			}
			plan.ExperienceAfter = ExperienceDigest(experience); Plan = plan; return true;
		}

		/// <summary>Applies only the revalidated frozen cuts; every retry starts with a fresh plan.</summary>
		internal static bool TryApplyFinalRetirement(KingdomSystem System,
			KingdomPolityFinalRetirementPlan Plan, out string Failure)
		{
			Failure = null;
			if (!ExactStart(System, Plan, out Failure)) return false;
			if (!KingdomPolityDispatchRules.TryRetire(System.PolityDispatch,
				System.PolityDispatch.Revision, System.RealmId, Plan.ReceiptId, out Failure)
				|| DispatchDigest(System.PolityDispatch) != Plan.DispatchAfter
				|| LedgerDigest(System.PolityLedger) != Plan.LedgerBefore
				|| ExperienceDigest(System.Experience) != Plan.ExperienceBefore)
				return Fail(Failure ?? "polity dispatch cut differed from its subplan", out Failure);
			if (!KingdomPolityRemovalRules.TrySettleBodylessRetirement(System.PolityLedger,
				System.PolityDispatch, System.PolityLedger.Revision, Plan.ReceiptId,
				out KingdomPolityPublicationResult _, out Failure)
				|| LedgerDigest(System.PolityLedger) != Plan.LedgerAfter)
				return Fail(Failure ?? "polity semantic cut differed from its subplan", out Failure);
			for (int i = 0; i < Plan.Releases.Count; i++)
			{
				KingdomPolityLeaseRetirementStep step = Plan.Releases[i];
				if (ExperienceDigest(System.Experience) != step.BeforeDigest)
					return Fail("W0 retirement cut lost its frozen revision", out Failure);
				bool released = step.Ambient
					? KingdomPolityExperienceRuntime.TryReleaseAmbient(System, step.SourceId,
						out Failure)
					: KingdomPolityExperienceRuntime.TryReleaseDirected(System, step.SourceId,
						out Failure);
				if (!released || ExperienceDigest(System.Experience) != step.AfterDigest)
					return Fail(Failure ?? "W0 retirement cut differed from its subplan", out Failure);
			}
			return ExperienceDigest(System.Experience) == Plan.ExperienceAfter
				|| Fail("W0 retirement terminal digest differs from its subplan", out Failure);
		}

		internal static bool TrySettleRetirementAuthority(KingdomSystem System, long Tick,
			out string Failure)
		{
			if (!TryPrepareFinalRetirement(System, Tick, out KingdomPolityFinalRetirementPlan plan,
				out Failure) || !TryApplyFinalRetirement(System, plan, out Failure)) return false;
			if (!TryDescribeRealmRemovalBlocker(System, Math.Max(Tick, 0L),
				out List<KingdomExperienceRetirementLeaseAllowance> remaining,
				out string blocker, out Failure)) return false;
			return blocker == null && remaining.Count == 0
				|| Fail(blocker ?? "polity retirement left W0 lease allowances", out Failure);
		}

		private static bool ExactStart(KingdomSystem S, KingdomPolityFinalRetirementPlan P,
			out string Failure)
		{
			Failure = null;
			return S != null && P != null && LedgerDigest(S.PolityLedger) == P.LedgerBefore
				&& DispatchDigest(S.PolityDispatch) == P.DispatchBefore
				&& ExperienceDigest(S.Experience) == P.ExperienceBefore
				|| Fail("polity final subplan lost its exact source revisions", out Failure);
		}

		private static string LedgerDigest(KingdomPolityLedger L)
		{
			return KingdomPolityRules.Sha256(KingdomPolityCodec.EncodeEnvelope(L));
		}

		private static string ExperienceDigest(KingdomExperienceLedger L)
		{
			return KingdomPolityRules.Sha256(KingdomExperienceCodec.EncodeEnvelope(L));
		}

		private static string DispatchDigest(KingdomPolityDispatchState S)
		{
			List<string> rows = new List<string> { S.Version.ToString(), S.RealmId ?? "",
				S.Revision.ToString(), S.HasWindow.ToString(), S.LastWindowOrdinal.ToString(),
				S.WindowCauseTick.ToString(), S.FutureCauseFloorTick.ToString(),
				S.EndpointDigest ?? "", S.EndpointCount.ToString(), S.CompletedMask.ToString(),
				S.Fault ?? "", KingdomPolityDispatchRules.DirectAuthorityDigest(S) };
			for (int i = 0; i < S.DirectRecords.Count; i++) rows.Add(S.DirectRecords[i].RecordId
				+ "|" + S.DirectRecords[i].AcknowledgedTick.ToString());
			return KingdomPolityRules.ActivationDigest("polity-dispatch-state-v1", rows);
		}

		private static List<string> AllowanceSources(
			IList<KingdomExperienceRetirementLeaseAllowance> Values)
		{
			List<string> rows = new List<string>();
			for (int i = 0; i < Values.Count; i++)
			{
				string source = Values[i].Audience?.SourceId ?? Values[i].Bodies?.SourceId;
				if (!rows.Contains(source)) rows.Add(source);
			}
			rows.Sort(StringComparer.Ordinal); return rows;
		}

		private static bool HasAudience(KingdomExperienceLedger L, string Source)
		{
			for (int i = 0; i < L.Audiences.Count; i++) if (L.Audiences[i].SourceId == Source) return true;
			return false;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
