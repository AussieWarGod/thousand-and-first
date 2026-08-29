using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityVisitInteraction
	{
		/// <summary>Compatibility entrypoint. A durable exact intent remains the only authority.</summary>
		internal static KingdomPolityEnvoyDeathOutcome WitnessEnvoyDeath(GameObject Body,
			string CohortId, GameObject Killer)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomPolityEndpointRuntime.TryReplayDeathIntents(system, CohortId,
				out string failure))
			{
				KingdomLog.Log("polity: envoy death intent replay refused (" + failure + ")");
				return KingdomPolityEnvoyDeathOutcome.Refused;
			}
			return KingdomPolityEnvoyDeathOutcome.Committed;
		}

		/// <summary>Replays only the attribution and exact tuple frozen before body release.</summary>
		internal static bool TryReplayEnvoyDeath(KingdomSystem System,
			KingdomPolityDeathIntentRecord Intent, out KingdomPolityEnvoyDeathOutcome Outcome,
			out string Failure)
		{
			Outcome = KingdomPolityEnvoyDeathOutcome.Refused; Failure = null;
			KingdomPolityLedger ledger = System?.PolityLedger;
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(ledger,
				Intent?.CohortId);
			KingdomPolityProjectionReceipt projection = cohort == null ? null :
				KingdomPolityAuthority.Projection(ledger, cohort.ManifestationReceiptId);
			if (ledger == null || cohort == null || projection == null || Intent == null ||
				Intent.Visibility != KingdomPolityDeathVisibility.PlayerVisible ||
				!Intent.Representative || Intent.Purpose != KingdomPolityCohortPurpose.Envoy ||
				Intent.Ordinal < 0 || Intent.Ordinal >= cohort.ResolvedMembers.Count ||
				!KingdomPolityDeathIntentRules.ExactBinding(Intent, ledger.RealmId,
					cohort.CohortId, projection.ProjectionId, projection.ZoneId,
					KingdomPolityCohortRules.PreparedObjectId(cohort, Intent.Ordinal),
					Intent.Ordinal, cohort.Purpose, true))
				return ReplayFail("envoy death intent lost its exact audience body", out Failure);
			if (!KingdomPolityEndpointRuntime.TryResolveDeathIncident(ledger, Intent,
				out KingdomPolityIncidentRecord terms, out Failure) ||
				terms.ParticipantCohortRefs.Count != 1 ||
				terms.ParticipantCohortRefs[0] != cohort.CohortId ||
				!TryCurrentPolity(ledger, out string target))
				return ReplayFail("envoy death intent lacks its exact audience", out Failure);
			if (!KingdomPolityCorrespondenceRuntime.TryRecoverTradeReceipts(System, out Failure))
				return false;
			if (!KingdomPolityEndpointRuntime.TryResolveDeathIncident(System.PolityLedger,
				Intent, out terms, out Failure)) return false;
			if (terms?.Conclusion == null && !KingdomPolityHospitalityRuntime.
				TryPrepareForEnvoyDeath(System, terms.IncidentPlanId, out Failure)) return false;
			if (!KingdomPolityCorrespondenceRuntime.TryGetEnvoyDeathAbsence(System,
				terms.IncidentPlanId, cohort.CohortId,
				out KingdomPolityConsignmentAbsenceProof absence, out bool _, out Failure))
				return false;
			bool attributable = Intent.Attribution ==
				KingdomPolityDeathAttribution.PlayerWitnessed;
			KingdomPolityEnvoyDeathOutcome outcome = KingdomPolityEnvoyDeathOutcome.Refused;
			for (int attempt = 0; attempt < 2; attempt++)
			{
				long revision = System.PolityLedger.Revision;
				KingdomPolityPublicationResult result;
				bool applied = attributable
					? KingdomPolityDiplomacyRules.TryRecordWitnessedEnvoyHarm(
						System.PolityLedger, revision, terms.IncidentPlanId, cohort.CohortId,
						projection.ProjectionId, Intent.ObjectId, target, Intent.Tick, absence,
						out outcome, out string _, out result, out Failure)
					: KingdomPolityDiplomacyRules.TryConcludeNeutralEnvoyDeath(
						System.PolityLedger, revision, terms.IncidentPlanId, cohort.CohortId,
						projection.ProjectionId, Intent.ObjectId, target, Intent.Tick, absence,
						out outcome, out result, out Failure);
				if (applied)
				{
					Outcome = outcome; Failure = null; break;
				}
				if (result.Outcome != KingdomPolityCasOutcome.Conflict) return false;
			}
			if (Outcome == KingdomPolityEnvoyDeathOutcome.Refused)
				return ReplayFail(Failure ?? "exact envoy death closure refused", out Failure);
			KingdomPolityHospitalityRuntime.TryCleanupApplied(System, terms.IncidentPlanId);
			if (attributable)
			{
				string text = outcome == KingdomPolityEnvoyDeathOutcome.Committed
					? "{{r|The witnessed killing of this envoy is now an attributable grievance. " +
						"No unseen casualty or wider war is inferred.}}"
					: "{{r|The witnessed killing is durably recorded. Its exact consequence " +
						"awaits bounded recovery; no wider war is inferred.}}";
				try { MessageQueue.AddPlayerMessage(text); }
				catch (System.Exception ex) { KingdomLog.Log(
					"polity: envoy death message deferred (" + ex.GetType().Name + ")"); }
			}
			return true;
		}

		private static bool TryCurrentPolity(KingdomPolityLedger Ledger, out string Id)
		{
			Id = null;
			for (int i = 0; Ledger != null && i < Ledger.Polities.Count; i++)
				if (Ledger.Polities[i].Source == KingdomPolitySource.CurrentRealm &&
					Ledger.Polities[i].Lifecycle == KingdomPolityLifecycle.Active)
				{
					if (Id != null) { Id = null; return false; }
					Id = Ledger.Polities[i].PolityId;
				}
			return Id != null;
		}
	}
}
