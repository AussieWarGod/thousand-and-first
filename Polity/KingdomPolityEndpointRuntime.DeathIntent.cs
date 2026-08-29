using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		private const string AmbiguousDeathIntentFailure =
			"removal witness slot contains foreign or ambiguous authority";

		private static bool TryReadDeathIntent(Zone Zone, string RealmId,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt, int Ordinal,
			out KingdomPolityDeathIntentState State, out KingdomPolityDeathIntentRecord Intent,
			out string Failure)
		{
			State = KingdomPolityDeathIntentState.Clear; Intent = null; Failure = null;
			if (Zone == null || Cohort == null || Receipt == null || Ordinal < 0 ||
				Ordinal >= Cohort.ResolvedMembers.Count)
				return FailPhysical("death authority changed during callbacks", out Failure);
			string objectId = KingdomPolityCohortRules.PreparedObjectId(Cohort, Ordinal);
			string key = KingdomPolityPhysicalCustodyRules.DeathIntentKey(
				Receipt.ProjectionId, objectId);
			if (!TryReadExactDeathIntentSlot(Zone, key, out bool any, out bool exactType,
				out string wire, out Failure)) return false;
			// Declared ahead of the short-circuit: a slot of the wrong type never reaches TryDecode,
			// and a null record is what every downstream reader treats as "nothing was decoded".
			KingdomPolityDeathIntentRecord record = null;
			bool decoded = exactType && KingdomPolityDeathIntentRules.TryDecode(wire,
				out record, out string _);
			bool binding = decoded && KingdomPolityDeathIntentRules.ExactBinding(record, RealmId,
				Cohort.CohortId, Receipt.ProjectionId, Zone.ZoneID, objectId, Ordinal,
				Cohort.Purpose, Ordinal == 0);
			if (decoded && record.Provenance == KingdomPolityDeathIntentProvenance.LegacyV1)
			{
				if (!binding) return FailPhysical(AmbiguousDeathIntentFailure, out Failure);
				KingdomPolityLedger ledger = The.Game?.GetSystem<KingdomSystem>()?.PolityLedger;
				bool visible = record.Visibility == KingdomPolityDeathVisibility.PlayerVisible;
				if (ledger == null || !KingdomPolityDeathIncidentRules.TryFreeze(ledger, Cohort,
					Ordinal, visible, out record.IncidentPlanId, out record.IncidentId,
					out record.IncidentDigest, out Failure)) return false;
				// One-way: the value frozen here is durable and write-once, and it is stamped
				// frozen-at-read so it can never be restated as a death-time claim.
				record.Provenance = KingdomPolityDeathIntentProvenance.FrozenAtFirstRead;
				if (!TryRewriteLegacyDeathIntent(Zone, wire, record, out Failure)) return false;
			}
			State = KingdomPolityDeathIntentRules.Classify(any, exactType, decoded, binding);
			if (State == KingdomPolityDeathIntentState.Ambiguous)
				return FailPhysical(AmbiguousDeathIntentFailure, out Failure);
			if (State == KingdomPolityDeathIntentState.Outstanding) Intent = record;
			return true;
		}

		private static bool TryWriteDeathIntent(Zone Zone,
			KingdomPolityDeathIntentRecord Intent, out string Failure)
		{
			Failure = null;
			if (Zone == null || Intent == null || Intent.ZoneId != Zone.ZoneID ||
				!KingdomPolityDeathIntentRules.TryEncode(Intent, out string expected, out Failure))
				return false;
			string key = KingdomPolityPhysicalCustodyRules.DeathIntentKey(Intent.ProjectionId,
				Intent.ObjectId);
			if (!TryReadExactDeathIntentSlot(Zone, key, out bool present, out bool exactType,
				out string existing, out Failure)) return false;
			if (present)
			{
				if (exactType && string.Equals(existing, expected,
					StringComparison.Ordinal)) return true;
				return FailPhysical(AmbiguousDeathIntentFailure, out Failure);
			}
			try { Zone.SetZoneProperty(key, expected); }
			catch (Exception ex) { return FailPhysical(
				"death intent write failed: " + ex.Message, out Failure); }
			if (!TryReadExactDeathIntentSlot(Zone, key, out present, out exactType,
				out existing, out Failure)) return false;
			if (present && exactType && string.Equals(existing, expected,
				StringComparison.Ordinal)) return true;
			return FailPhysical("death intent did not survive exact writeback", out Failure);
		}

		internal static bool TryClearDeathIntent(Zone Zone,
			KingdomPolityDeathIntentRecord Intent, out string Failure)
		{
			Failure = null;
			if (Zone == null || Intent == null || Intent.ZoneId != Zone.ZoneID ||
				!KingdomPolityDeathIntentRules.TryEncode(Intent, out string expected, out Failure))
				return false;
			string key = KingdomPolityPhysicalCustodyRules.DeathIntentKey(Intent.ProjectionId,
				Intent.ObjectId);
			if (!TryReadExactDeathIntentSlot(Zone, key, out bool present, out bool exactType,
				out string actual, out Failure)) return false;
			if (!present) return true;
			if (!exactType || !string.Equals(actual, expected, StringComparison.Ordinal))
				return FailPhysical(AmbiguousDeathIntentFailure, out Failure);
			try { Zone.RemoveZoneProperty(key); }
			catch (Exception ex) { return FailPhysical(
				"death intent clear failed: " + ex.Message, out Failure); }
			if (!TryReadExactDeathIntentSlot(Zone, key, out present, out exactType,
				out actual, out Failure)) return false;
			return !present || FailPhysical("exact death intent survived cleanup", out Failure);
		}

		private static bool TryReadExactDeathIntentSlot(Zone Zone, string Key,
			out bool Present, out bool ExactString, out string Value, out string Failure)
		{
			Present = false; ExactString = false; Value = null; Failure = null;
			try
			{
				if (Zone == null || The.ZoneManager?.ZoneProperties == null)
					return FailPhysical("death intent ground is unavailable", out Failure);
				if (!The.ZoneManager.ZoneProperties.TryGetValue(Zone.ZoneID,
					out Dictionary<string, object> properties) || properties == null ||
					!properties.TryGetValue(Key, out object raw)) return true;
				Present = true; ExactString = raw is string; Value = raw as string; return true;
			}
			catch (Exception ex)
			{
				return FailPhysical("death intent slot inspection failed: " + ex.Message,
					out Failure);
			}
		}

		private static bool TryClearCohortDeathIntents(Zone Zone, KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Cohort.ResolvedMembers.Count; i++)
			{
				if (!TryReadDeathIntent(Zone, Ledger.RealmId, Cohort, Receipt, i,
					out KingdomPolityDeathIntentState state,
					out KingdomPolityDeathIntentRecord intent, out Failure)) return false;
				if (state == KingdomPolityDeathIntentState.Outstanding &&
					!TryClearDeathIntent(Zone, intent, out Failure)) return false;
			}
			return true;
		}

		internal static bool TryReplayDeathIntents(KingdomSystem System, string CohortId,
			out string Failure)
		{
			Failure = null;
			try
			{
				if (!TryAdmit(System, CohortId, out Zone zone, out KingdomPolityLedger ledger,
					out KingdomPolityCohortPlan cohort, out Failure)) return false;
				KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(ledger,
					cohort.ManifestationReceiptId);
				if (!ExactReceipt(cohort, receipt, zone, out Failure)) return false;
				if (receipt.Phase != KingdomPolityProjectionPhase.Committed && receipt.Phase !=
					KingdomPolityProjectionPhase.Cleaned) return FailPhysical(
						"death intent projection has no committed physical authority", out Failure);
				if (!TryObserve(zone, ledger.RealmId, cohort, receipt,
					out GameObject[] observed, out Failure)) return false;
				long now = Math.Max(0L, The.Game?.TimeTicks ?? 0L);
				for (int i = 0; i < cohort.ResolvedMembers.Count; i++)
				{
					cohort = KingdomPolityAuthority.Cohort(ledger, CohortId);
					receipt = KingdomPolityAuthority.Projection(ledger, cohort.ManifestationReceiptId);
					if (!TryReadDeathIntent(zone, ledger.RealmId, cohort, receipt, i,
						out KingdomPolityDeathIntentState state,
						out KingdomPolityDeathIntentRecord intent, out Failure)) return false;
					if (state == KingdomPolityDeathIntentState.Clear) continue;
					if (!KingdomPolityDeathIntentRules.CausalTick(intent, receipt.CommittedTick, now))
						return FailPhysical("death authority changed during callbacks", out Failure);
					bool present = GameObject.Validate(observed[i]);
					bool witnessed = HasRemovalWitness(zone,
						KingdomPolityPhysicalCustodyRules.DeathRemovalKind, ledger.RealmId,
						cohort.CohortId, receipt.ProjectionId, intent.ObjectId, i);
					if (present)
					{
						XRL.World.Parts.r_KingdomPolityCohortBody bridge = observed[i].GetPart<
							XRL.World.Parts.r_KingdomPolityCohortBody>();
						if (bridge != null && bridge.IsDeathCallbackInFlight)
						{
							if (now <= intent.Tick) return FailPhysical(
								"death callback teardown remains in flight", out Failure);
							bridge.RecoverDeathCallbackGuard();
						}
						if (!TryBuildCustodyPlan(ledger, zone, ledger.RealmId, cohort, receipt,
							observed[i], i, AllowRemovedGear: true, out FrozenCustodyPlan _,
							out Failure)) return false;
						if (witnessed && !TryClearRemovalWitness(zone, ledger.RealmId,
							cohort.CohortId, receipt.ProjectionId, intent.ObjectId, i,
							Gear: false, out Failure)) return false;
						if (!TryClearDeathIntent(zone, intent, out Failure)) return false;
						continue;
					}
					if (!witnessed) return FailPhysical(
						"cohort body is absent without an exact death or cleanup witness", out Failure);
					if (!TryCommitDeathIntentConsequence(System, intent, out Failure) ||
						!TryClearDeathIntent(zone, intent, out Failure)) return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				return FailPhysical("death intent replay failed: " + ex.Message, out Failure);
			}
		}

		private static bool TryCommitDeathIntentConsequence(KingdomSystem System,
			KingdomPolityDeathIntentRecord Intent, out string Failure)
		{
			Failure = null;
			if (System?.PolityLedger == null || Intent == null)
				return FailPhysical("death consequence lacks durable polity authority", out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(
				System.PolityLedger, Intent.CohortId);
			if (cohort == null) return FailPhysical(
				"death consequence lost its exact cohort", out Failure);
			KingdomPolityDeathIntentAction action = KingdomPolityDeathIntentRules.Decide(Intent,
				cohort.Phase);
			switch (action)
			{
			case KingdomPolityDeathIntentAction.Clear:
				if (cohort.Phase == KingdomPolityCohortPhase.Concluded &&
					!ExactConcludedAuthority(System.PolityLedger, cohort, Intent))
					return FailPhysical(
						"concluded cohort lacks the exact intended incident consequence", out Failure);
				if (cohort.Phase == KingdomPolityCohortPhase.Abandoned)
					return KingdomPolityExperienceRuntime.TryReleaseForCohort(System, cohort,
						out Failure);
				return true;
			case KingdomPolityDeathIntentAction.Abandon:
				for (int attempt = 0; attempt < 2; attempt++)
				{
					long revision = System.PolityLedger.Revision;
					if (KingdomPolityCohortRules.TryAbandonEndpointCohort(System.PolityLedger,
						revision, Intent, ExactDeathRemovalWitness: true,
						out KingdomPolityPublicationResult result, out Failure))
					{
						cohort = KingdomPolityAuthority.Cohort(System.PolityLedger, Intent.CohortId);
						return KingdomPolityExperienceRuntime.TryReleaseForCohort(System, cohort,
							out Failure);
					}
					if (result.Outcome != KingdomPolityCasOutcome.Conflict) return false;
				}
				return false;
			case KingdomPolityDeathIntentAction.ReplayWarband:
				return KingdomPolityVisitInteraction.TryReplayWarbandDeath(System, Intent, out Failure);
			case KingdomPolityDeathIntentAction.ReplayEnvoy:
				return KingdomPolityVisitInteraction.TryReplayEnvoyDeath(System, Intent,
					out KingdomPolityEnvoyDeathOutcome outcome, out Failure) && outcome !=
					KingdomPolityEnvoyDeathOutcome.Refused;
			default:
				return FailPhysical("death authority changed during callbacks", out Failure);
			}
		}

		private static bool ExactConcludedAuthority(KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Cohort, KingdomPolityDeathIntentRecord Intent)
		{
			if (Ledger == null || Cohort?.Phase != KingdomPolityCohortPhase.Concluded ||
				!KingdomPolityRules.SemanticId(Cohort.RewardEventId)) return false;
			if (Cohort.Purpose != KingdomPolityCohortPurpose.Envoy &&
				Cohort.Purpose != KingdomPolityCohortPurpose.Warband) return true;
			if (!TryResolveDeathIncident(Ledger, Intent,
				out KingdomPolityIncidentRecord match, out string _)) return false;
			return match?.Conclusion != null && (Cohort.RewardEventId ==
				match.Conclusion.ConclusionId || KingdomPolityAuthority.Contains(
					match.Conclusion.ReceiptRefs, Cohort.RewardEventId));
		}

		private static bool SameIntent(KingdomPolityDeathIntentRecord A,
			KingdomPolityDeathIntentRecord B)
		{
			if (!KingdomPolityDeathIntentRules.TryEncode(A, out string a, out string _) ||
				!KingdomPolityDeathIntentRules.TryEncode(B, out string b, out string _)) return false;
			return string.Equals(a, b, StringComparison.Ordinal);
		}
	}
}
