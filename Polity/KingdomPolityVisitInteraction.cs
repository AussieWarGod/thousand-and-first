using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Player-witnessed first-contact choices; no choice is made offscreen.</summary>
	public static partial class KingdomPolityVisitInteraction
	{
		public static bool CanAnswer(GameObject Body, GameObject Actor, string CohortId)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			KingdomPolityCohortPlan cohort; KingdomPolityProjectionReceipt receipt;
			return Actor != null && Actor.IsPlayer() && ExactBody(system, Body, CohortId,
				out cohort, out receipt) && ((cohort.Purpose >= KingdomPolityCohortPurpose.Guard &&
				cohort.Purpose <= KingdomPolityCohortPurpose.Migrant &&
				cohort.Purpose != KingdomPolityCohortPurpose.Envoy &&
				cohort.Purpose != KingdomPolityCohortPurpose.Warband &&
				CanAnswerAmbient(system, cohort)) || (cohort.Purpose == KingdomPolityCohortPurpose.Envoy &&
				cohort.Phase == KingdomPolityCohortPhase.Materialized ||
				cohort.Purpose == KingdomPolityCohortPurpose.Warband &&
				(cohort.Phase == KingdomPolityCohortPhase.Materialized ||
				 cohort.Phase == KingdomPolityCohortPhase.Concluded))) &&
				receipt.Phase == KingdomPolityProjectionPhase.Committed &&
				Body.GetIntProperty(KingdomPolityEndpointRuntime.MemberOrdinalProperty, -1) == 0;
		}

		public static void Answer(GameObject Body, GameObject Actor, string CohortId)
		{
			try
			{
				if (!CanAnswer(Body, Actor, CohortId)) return;
				KingdomSystem system = The.Game.GetSystem<KingdomSystem>();
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(
					system.PolityLedger, CohortId);
				if (IsAmbient(cohort.Purpose))
				{
					AnswerAmbient(system, Body, cohort); return;
				}
				if (!KingdomMaster.NewWorkAllowed(system)
					&& !HasCommittedAnswerRecovery(system, cohort))
				{
					Popup.Show("Settlement simulation is paused. The delegation remains here, " +
						"but no new answer can be recorded until the realm resumes.");
					return;
				}
				if (cohort.Purpose == KingdomPolityCohortPurpose.Warband)
				{
					AnswerConflict(system, CohortId); return;
				}
				KingdomPolityIncidentRecord terms = TermsFor(system.PolityLedger, CohortId);
				if (terms == null) Welcome(system, CohortId);
				else AnswerTerms(system, terms, CohortId, Body);
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: polity delegation failed", ex);
				KingdomLog.Log("polity: delegation failed (" + ex.GetType().Name + ": " + ex.Message + ")");
				Popup.Show("The delegation cannot be heard just now. Nothing changes.");
			}
		}

		private static bool HasCommittedAnswerRecovery(KingdomSystem System,
			KingdomPolityCohortPlan Cohort)
		{
			if (System?.PolityLedger == null || Cohort == null) return false;
			if (Cohort.Purpose == KingdomPolityCohortPurpose.Warband)
			{
				KingdomPolityIncidentRecord clash = ClashFor(System.PolityLedger,
					Cohort.CohortId);
				return clash?.Conclusion != null || clash?.Intervention != null;
			}
			KingdomPolityIncidentRecord terms = TermsFor(System.PolityLedger,
				Cohort.CohortId);
			return terms?.Conclusion != null;
		}

		private static void Welcome(KingdomSystem System, string CohortId)
		{
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(System.PolityLedger,
				CohortId); KingdomPolityRecord polity = cohort == null ? null :
				KingdomPolityAuthority.Polity(System.PolityLedger, cohort.PolityId);
			string name = polity == null ? "the visiting polity" : polity.DisplayName;
			if (Popup.ShowYesNo(KingdomPresentation.Rich(name) +
				" offers formal contact and witnessed passage. Receive the delegation?") !=
				DialogResult.Yes) return;
			long tick = Now(); string witnessed = Witnessed("welcome", CohortId, N(tick));
			if (!KingdomPolityCohortRules.TryConcludeEndpointCohort(System.PolityLedger,
				System.PolityLedger.Revision, CohortId, witnessed,
				out KingdomPolityPublicationResult _, out string failure))
			{
				Popup.Show("The welcome does not settle: " + failure); return;
			}
			Popup.Show("The delegation is received. Its return is recorded when the party departs.");
		}

		private static void AnswerTerms(KingdomSystem System,
			KingdomPolityIncidentRecord Terms, string CohortId, GameObject Body)
		{
			if (Terms.Conclusion != null)
			{
				if (!KingdomPolityCohortRules.TryConcludeEndpointCohort(System.PolityLedger,
					System.PolityLedger.Revision, CohortId, Terms.Conclusion.ConclusionId,
					out KingdomPolityPublicationResult _, out string recoveryFailure))
					KingdomLog.Log("polity: envoy conclusion recovery refused (" +
						recoveryFailure + ")");
				return;
			}
			if (!OfferConsignment(System, Terms, CohortId, Body,
				out string consignmentStatus)) return;
			KingdomPolityHospitalityProof hospitality = OfferHospitality(System, Terms);
			Terms = TermsFor(System.PolityLedger, CohortId);
			int picked = Popup.PickOption(Title: "Terms at the first contact",
				Intro: "The delegation asks for mutual recognition and safe passage. " +
					"Your answer changes the directed relation; refusal may open a witnessed clash." +
					consignmentStatus +
					(hospitality == null ? "" :
					 " One larder serving and one dram of fresh water are committed to the table."),
				Options: new[] { "Accept the terms", "Make a counteroffer", "Offer a truce",
					"Refuse the terms", "Answer later" }, AllowEscape: true);
			if (picked < 0 || picked > 3) return;
			KingdomPolityTermsChoice choice = (KingdomPolityTermsChoice)(picked + 1);
			long tick = Now(); string witnessed = Witnessed("terms", Terms.IncidentPlanId,
				choice.ToString(), N(tick));
			if (!KingdomPolityDiplomacyRules.TryAnswerTerms(System.PolityLedger,
				System.PolityLedger.Revision, Terms.IncidentPlanId, choice, witnessed, tick,
				hospitality,
				out KingdomPolityPublicationResult _, out string failure))
			{
				Popup.Show("The answer is not recorded: " + failure); return;
			}
			KingdomPolityHospitalityRuntime.TryCleanupApplied(System, Terms.IncidentPlanId);
			Terms = TermsFor(System.PolityLedger, CohortId);
			if (Terms?.Conclusion == null || !KingdomPolityCohortRules.TryConcludeEndpointCohort(
				System.PolityLedger, System.PolityLedger.Revision, CohortId,
				Terms.Conclusion.ConclusionId, out KingdomPolityPublicationResult _, out failure))
			{
				KingdomLog.Log("polity: terms committed; envoy conclusion awaits recovery (" +
					(failure ?? "missing conclusion") + ")");
			}
			Popup.Show(choice == KingdomPolityTermsChoice.Refuse
				? "The terms are refused. A finite confrontation may now be witnessed here."
				: "Your witnessed answer is recorded. The delegation prepares to depart.");
		}

		private static KingdomPolityHospitalityProof OfferHospitality(KingdomSystem System,
			KingdomPolityIncidentRecord Terms)
		{
			KingdomPolityHospitalityTransaction transaction = Terms.Hospitality;
			if (transaction?.Phase == KingdomPolityHospitalityPhase.Debited)
				return transaction.Proof;
			if (transaction != null &&
				transaction.Phase != KingdomPolityHospitalityPhase.Planned) return null;
			if (transaction == null && Popup.ShowYesNo(
				"Offer one serving from a dedicated larder and one dram of fresh water? " +
				"This is optional; refusing or lacking stores never prevents an answer.") !=
				DialogResult.Yes) return null;
			if (KingdomPolityHospitalityRuntime.TryOffer(System, Terms.IncidentPlanId, Now(),
				out KingdomPolityHospitalityProof proof, out string failure)) return proof;
			Popup.Show(failure ?? "The exact hospitality serving cannot be committed. " +
				"You may still answer normally.");
			return null;
		}

		public static void WitnessWarbandDeath(GameObject Body, string CohortId)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomPolityEndpointRuntime.TryReplayDeathIntents(system, CohortId,
				out string failure)) KingdomLog.Log(
				"polity: warband death intent replay refused (" + failure + ")");
		}

		internal static bool TryReplayWarbandDeath(KingdomSystem System,
			KingdomPolityDeathIntentRecord Intent, out string Failure)
		{
			Failure = null;
			KingdomPolityLedger ledger = System?.PolityLedger;
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(ledger,
				Intent?.CohortId);
			KingdomPolityProjectionReceipt projection = cohort == null ? null :
				KingdomPolityAuthority.Projection(ledger, cohort.ManifestationReceiptId);
			if (ledger == null || cohort == null || projection == null || Intent == null ||
				Intent.Visibility != KingdomPolityDeathVisibility.PlayerVisible ||
				!Intent.Representative || Intent.Purpose != KingdomPolityCohortPurpose.Warband ||
				Intent.Ordinal < 0 || Intent.Ordinal >= cohort.ResolvedMembers.Count ||
				!KingdomPolityDeathIntentRules.ExactBinding(Intent, ledger.RealmId,
					cohort.CohortId, projection.ProjectionId, projection.ZoneId,
					KingdomPolityCohortRules.PreparedObjectId(cohort, Intent.Ordinal),
					Intent.Ordinal, cohort.Purpose, true))
				return ReplayFail("warband death intent lost its exact incident participant", out Failure);
			if (!KingdomPolityEndpointRuntime.TryResolveDeathIncident(ledger, Intent,
				out KingdomPolityIncidentRecord clash, out Failure))
				return ReplayFail("warband death intent lacks its exact clash", out Failure);
			if (cohort.Phase == KingdomPolityCohortPhase.Concluded &&
				(clash.Conclusion == null || cohort.RewardEventId !=
					clash.Conclusion.ConclusionId))
				return ReplayFail(
					"concluded warband lacks the exact intended clash conclusion", out Failure);
			string witnessed = Witnessed("clash", clash.IncidentPlanId,
				Intent.ObjectId, N(Intent.Tick));
			string receiptId = KingdomPolityRules.ActivationId(
				"taf:receipt:polity-clash:v1:", "polity-loaded-clash-receipt-v1", witnessed);
			if (!KingdomPolityEndpointRuntime.TryConcludeCurrentEndpointClash(System,
				clash.IncidentPlanId, Intent.Tick, new List<string> { witnessed },
				new List<KingdomPolitySystemicDelta>(), new List<KingdomPolityRelationDelta>(),
				new List<string> { receiptId }, out Failure)) return false;
			if (!KingdomPolityEndpointRuntime.TryResolveDeathIncident(System.PolityLedger,
				Intent, out clash, out Failure)) return false;
			return TryConcludeParticipants(System, clash, out Failure);
		}

		private static bool ExactBody(KingdomSystem System, GameObject Body, string CohortId,
			out KingdomPolityCohortPlan Cohort, out KingdomPolityProjectionReceipt Receipt)
		{
			Cohort = System?.PolityLedger == null ? null :
				KingdomPolityAuthority.Cohort(System.PolityLedger, CohortId);
			Receipt = Cohort == null ? null : KingdomPolityAuthority.Projection(
				System.PolityLedger, Cohort.ManifestationReceiptId);
			int ordinal = Body == null ? -1 : Body.GetIntProperty(
				KingdomPolityEndpointRuntime.MemberOrdinalProperty, -1);
			XRL.World.Parts.r_KingdomPolityCohortBody part = Body == null ? null :
				Body.GetPart<XRL.World.Parts.r_KingdomPolityCohortBody>();
			return System != null && Cohort != null && Receipt != null && part != null &&
				!part.Inert && part.Representative && part.RealmId == System.RealmId &&
				part.CohortId == CohortId && part.Purpose == Cohort.Purpose &&
				KingdomPolityRules.Usable(System.PolityLedger) &&
				GameObject.Validate(Body) && Body.CurrentCell != null && Body.CurrentZone != null &&
				ReferenceEquals(Body.CurrentZone, The.Player?.CurrentZone) &&
				The.Player?.CurrentCell != null &&
				System.SettlementIdForOwnedZone(Body.CurrentZone.ZoneID) == Cohort.SurfaceRef &&
				KingdomWord.StandsIn(Body.CurrentZone) &&
				KingdomPolityCohortRules.ExactEndpointReceipt(Cohort, Receipt,
					Body.CurrentZone.ZoneID) && ordinal >= 0 && ordinal < Cohort.ResolvedMembers.Count &&
				Body.ID == KingdomPolityCohortRules.PreparedObjectId(Cohort, ordinal) &&
				Receipt.SourceRef == CohortId &&
				KingdomPolityAuthority.Contains(Receipt.ObjectIds, Body.ID) &&
				Body.GetStringProperty(KingdomPolityEndpointRuntime.CohortOwnerProperty) == Cohort.PolityId &&
				Body.GetStringProperty(KingdomPolityEndpointRuntime.CohortProperty) == CohortId &&
				Body.GetStringProperty(KingdomPolityEndpointRuntime.ProjectionProperty) == Receipt.ProjectionId;
		}

		private static KingdomPolityIncidentRecord TermsFor(KingdomPolityLedger L, string CohortId)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].Purpose == KingdomPolityCohortPurpose.Envoy &&
					KingdomPolityAuthority.Contains(L.Incidents[i].ParticipantCohortRefs, CohortId))
					return L.Incidents[i];
			return null;
		}

		private static KingdomPolityIncidentRecord ClashFor(KingdomPolityLedger L, string CohortId)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].Purpose == KingdomPolityCohortPurpose.Warband &&
					KingdomPolityAuthority.Contains(L.Incidents[i].ParticipantCohortRefs, CohortId))
					return L.Incidents[i];
			return null;
		}

		private static string Witnessed(string Kind, params string[] Values)
		{
			return KingdomPolityRules.ActivationId("taf:fact:witnessed:polity-visit:v1:",
				"polity-visit-witness-v1-" + Kind, Values);
		}

		private static long Now() { return Math.Max(0L, The.Game?.TimeTicks ?? 0L); }
		private static string N(long Value) { return Value.ToString(CultureInfo.InvariantCulture); }
		private static bool ReplayFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
