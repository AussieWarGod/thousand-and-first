using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityVisitInteraction
	{
		internal static bool TryValidateConsignmentRecipient(KingdomSystem System,
			GameObject Body, KingdomPolityConsignmentRequest Request, out string Failure)
		{
			Failure = null;
			if (System?.PolityLedger == null ||
				!KingdomPolityCorrespondenceRules.TryValidateConsignmentRequest(
					System.PolityLedger, Request, out Failure)) return false;
			KingdomPolityCohortPlan cohort; KingdomPolityProjectionReceipt receipt;
			if (!ExactBody(System, Body, Request.RecipientCohortId, out cohort, out receipt) ||
				cohort.Purpose != KingdomPolityCohortPurpose.Envoy ||
				cohort.Phase != KingdomPolityCohortPhase.Materialized ||
				receipt.Phase != KingdomPolityProjectionPhase.Committed ||
				cohort.PolityId != Request.CounterpartyPolityId ||
				cohort.SurfaceRef != Request.SurfaceRef || Body.GetIntProperty(
					KingdomPolityEndpointRuntime.MemberOrdinalProperty, -1) != 0)
			{
				Failure = "The exact loaded envoy recipient is not present on current settlement ground.";
				return false;
			}
			return true;
		}

		internal static bool TryCaptureConsignmentRecipientWitness(KingdomSystem System,
			GameObject Body, KingdomPolityConsignmentRequest Request,
			out KingdomTradePolityRecipientWitness Witness, out string Failure)
		{
			Witness = null;
			if (!TryValidateConsignmentRecipient(System, Body, Request, out Failure)) return false;
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(
				System.PolityLedger, Request.RecipientCohortId);
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				System.PolityLedger, cohort.ManifestationReceiptId);
			return KingdomTradeRules.TryCreatePolityRecipientWitness(Request, Body.ID,
				projection.ProjectionId, out Witness, out Failure);
		}

		internal static bool TryValidateConsignmentRecipientWitness(KingdomSystem System,
			GameObject Body, KingdomPolityConsignmentRequest Request,
			KingdomTradePolityRecipientWitness Expected, out string Failure)
		{
			if (!TryCaptureConsignmentRecipientWitness(System, Body, Request,
				out KingdomTradePolityRecipientWitness current, out Failure)) return false;
			if (KingdomTradeRules.ExactPolityRecipientWitness(Expected, current)) return true;
			Failure = "The loaded envoy no longer matches the frozen body, cohort, and projection witness.";
			return false;
		}
	}
}
