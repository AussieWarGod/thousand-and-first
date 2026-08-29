using System;
using System.Globalization;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityVisitInteraction
	{
		internal static string ActionLabel(KingdomPolityCohortPurpose Purpose)
		{
			switch (Purpose)
			{
			case KingdomPolityCohortPurpose.Guard: return "Ask about the watch";
			case KingdomPolityCohortPurpose.Patrol: return "Hear the local report";
			case KingdomPolityCohortPurpose.Courier: return "Hear the current deed";
			case KingdomPolityCohortPurpose.Trader: return "Inspect the market fact";
			case KingdomPolityCohortPurpose.Migrant: return "Hear the request";
			case KingdomPolityCohortPurpose.Warband: return "Address confrontation";
			default: return "Hear delegation";
			}
		}

		internal static string ActionVerb(KingdomPolityCohortPurpose Purpose)
		{
			switch (Purpose)
			{
			case KingdomPolityCohortPurpose.Guard: return "ask about the watch";
			case KingdomPolityCohortPurpose.Patrol: return "hear the local report";
			case KingdomPolityCohortPurpose.Courier: return "hear the current deed";
			case KingdomPolityCohortPurpose.Trader: return "inspect the market fact";
			case KingdomPolityCohortPurpose.Migrant: return "hear the request";
			case KingdomPolityCohortPurpose.Warband: return "address the confrontation";
			default: return "hear the delegation";
			}
		}

		private static bool CanAnswerAmbient(KingdomSystem System,
			KingdomPolityCohortPlan Cohort)
		{
			if (!IsAmbient(Cohort?.Purpose ?? KingdomPolityCohortPurpose.None) ||
				(Cohort.Phase != KingdomPolityCohortPhase.Materialized &&
				 Cohort.Phase != KingdomPolityCohortPhase.Concluded) ||
				KingdomPolityDispatchRules.Expired(Cohort, Now())) return false;
			return TryCurrentAmbientFacts(System, Cohort,
				out KingdomPolityEndpointFacts _, out string _);
		}

		private static void AnswerAmbient(KingdomSystem System, GameObject Body,
			KingdomPolityCohortPlan Cohort)
		{
			if (!TryCurrentAmbientFacts(System, Cohort,
				out KingdomPolityEndpointFacts facts, out string failure))
			{
				Popup.Show("This company no longer matches the current settlement facts. " + failure);
				return;
			}
			string report = AmbientReport(System, Cohort, facts);
			bool acknowledge = Cohort.Purpose == KingdomPolityCohortPurpose.Courier ||
				Cohort.Purpose == KingdomPolityCohortPurpose.Migrant;
			if (!acknowledge)
			{
				Popup.Show(report); return;
			}
			if (Cohort.Phase == KingdomPolityCohortPhase.Concluded)
			{
				Popup.Show(report + "\n\nYour acknowledgement is already recorded."); return;
			}
			string accept = Cohort.Purpose == KingdomPolityCohortPurpose.Courier
				? "Acknowledge the message" : "Acknowledge the request (admit no resident)";
			int picked = Popup.PickOption(Title: ActionLabel(Cohort.Purpose), Intro: report,
				Options: new[] { accept, "Answer later" }, AllowEscape: true);
			if (picked != 0) return;
			// Cancellation is free. Pause is checked only after explicit choice and before CAS.
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Popup.Show("Settlement simulation is paused. Nothing is acknowledged."); return;
			}
			string witnessed = AmbientAcknowledgement(Cohort);
			if (!KingdomPolityCohortRules.TryConcludeEndpointCohort(System.PolityLedger,
				System.PolityLedger.Revision, Cohort.CohortId, witnessed,
				out KingdomPolityPublicationResult _, out failure))
			{
				Popup.Show("The acknowledgement is not recorded: " + failure); return;
			}
			Popup.Show(Cohort.Purpose == KingdomPolityCohortPurpose.Courier
				? "The exact local statement is acknowledged. No departure or journey is inferred."
				: "The request is acknowledged without admitting a resident. No departure is inferred.");
		}

		private static bool TryCurrentAmbientFacts(KingdomSystem System,
			KingdomPolityCohortPlan Cohort, out KingdomPolityEndpointFacts Facts,
			out string Failure)
		{
			Facts = null; Failure = null;
			long tick = Now();
			if (System == null || Cohort == null || Cohort.PresentationOptionKind !=
				KingdomExperienceOptionKind.AmbientUse ||
				!KingdomPolityEndpointFactRuntime.TryOffer(System, tick,
					out KingdomPolityDispatchOffer offer, out Failure)) return false;
			for (int i = 0; i < offer.Endpoints.Count; i++)
				if (offer.Endpoints[i].SettlementId == Cohort.SurfaceRef) Facts = offer.Endpoints[i];
			if (Facts == null)
			{
				Failure = "the frozen endpoint is absent from current topology"; return false;
			}
			long cause = Cohort.EventOrdinal > (ulong)(long.MaxValue /
				KingdomPolityDispatchRules.PeriodTicks) ? long.MaxValue :
				(long)Cohort.EventOrdinal * KingdomPolityDispatchRules.PeriodTicks;
			if (!KingdomPolityDispatchRules.TryCreateForPurpose(System.RealmId, Facts,
				offer.Endpoints.Count, Cohort.EventOrdinal, cause, Cohort.Purpose,
				out KingdomPolityDueWork exact, out Failure)) return false;
			if (exact.CohortId != Cohort.CohortId || exact.EventStreamId != Cohort.EventStreamId ||
				exact.SourceRef != Cohort.SourceRef || exact.SettlementId != Cohort.SurfaceRef ||
				exact.MemberCount != Cohort.ScaleBudget)
			{
				Failure = "current facts differ from the frozen cohort cause"; return false;
			}
			return true;
		}

		private static string AmbientReport(KingdomSystem System, KingdomPolityCohortPlan Cohort,
			KingdomPolityEndpointFacts Facts)
		{
			switch (Cohort.Purpose)
			{
			case KingdomPolityCohortPurpose.Guard:
				return "The watch reports " + Facts.Population.ToString(CultureInfo.InvariantCulture) +
					" people under the current gate order. This is a report, not a new guard posting.";
			case KingdomPolityCohortPurpose.Patrol:
				return "The patrol speaks at this exact loaded settlement. No road condition, " +
					"journey, or offscreen result is inferred.";
			case KingdomPolityCohortPurpose.Courier:
				return "The courier repeats the current local deed, " + KingdomPresentation.Rich(
					CurrentDeed(System, Cohort.SurfaceRef)) +
					". Acknowledgement proves no transport or route.";
			case KingdomPolityCohortPurpose.Trader:
				return "The current settlement has tier " + Facts.ShopTier.ToString(
					CultureInfo.InvariantCulture) + " market standing. This proves no route, " +
					"wares, shop, trade, or entitlement.";
			default:
				return "The company asks after " + Facts.KnownStorageSpace.ToString(
					CultureInfo.InvariantCulture) +
					" known room. Acknowledgement does not admit a resident or promise housing.";
			}
		}

		private static string CurrentDeed(KingdomSystem System, string SettlementId)
		{
			if (System?.City?.SettlementId == SettlementId)
				return string.IsNullOrEmpty(System.LastDeed) ? "the settlement's current deed" :
					System.LastDeed;
			if (System != null && System.TryFindSettlement(SettlementId, out bool seated,
				out KingdomSettlement settlement) && !seated && !string.IsNullOrEmpty(
					settlement?.LastDeed)) return settlement.LastDeed;
			return "the settlement's current deed";
		}

		private static string AmbientAcknowledgement(KingdomPolityCohortPlan Cohort)
		{
			return KingdomPolityRules.ActivationId(
				"taf:event:polity-ambient-acknowledgement:v1:",
				"polity-ambient-acknowledgement-v1", Cohort.CohortId, Cohort.SourceRef,
				((byte)Cohort.Purpose).ToString(CultureInfo.InvariantCulture),
				Cohort.EventOrdinal.ToString(CultureInfo.InvariantCulture));
		}

		private static bool IsAmbient(KingdomPolityCohortPurpose Purpose)
		{
			return Purpose == KingdomPolityCohortPurpose.Guard ||
				Purpose == KingdomPolityCohortPurpose.Patrol ||
				Purpose == KingdomPolityCohortPurpose.Courier ||
				Purpose == KingdomPolityCohortPurpose.Trader ||
				Purpose == KingdomPolityCohortPurpose.Migrant;
		}
	}
}
