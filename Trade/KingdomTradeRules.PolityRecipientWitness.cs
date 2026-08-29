using System;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		public static bool TryCreatePolityRecipientWitness(
			KingdomPolityConsignmentRequest Request, string BodyId, string ProjectionId,
			out KingdomTradePolityRecipientWitness Witness, out string Failure)
		{
			Witness = null; Failure = null;
			if (!KingdomPolityCorrespondenceRules.TryValidateConsignmentRequestShape(
				Request, out Failure) || !KingdomPolityRules.TypedId(BodyId,
					"taf:object:polity-cohort:v1:") || !KingdomPolityRules.TypedId(
					ProjectionId, "taf:projection:cohort:v1:"))
			{
				Failure = Failure ?? "Polity recipient identity is not exact"; return false;
			}
			KingdomTradePolityRecipientWitness candidate =
				new KingdomTradePolityRecipientWitness
				{
					BodyId = BodyId, CohortId = Request.RecipientCohortId,
					ProjectionId = ProjectionId, SurfaceRef = Request.SurfaceRef,
					RequestDigest = Request.RequestDigest
				};
			candidate.WitnessDigest = PolityRecipientWitnessDigest(candidate);
			if (!TryValidatePolityRecipientWitnessShape(candidate, out Failure)) return false;
			Witness = candidate; return true;
		}

		internal static bool TryValidatePolityConsignmentCheckpoint(
			KingdomTradeOperation Operation, KingdomPolityConsignmentRequest Request,
			KingdomTradePolityRecipientWitness Live, int ExactBodyMatches,
			string SettlementName, out string Failure)
		{
			Failure = null;
			if (ExactBodyMatches != 1)
			{
				Failure = ExactBodyMatches > 1
					? "Polity recipient body id is ambiguous on loaded ground"
					: "Polity recipient body is absent from loaded ground";
				return false;
			}
			if (!TryValidatePolityRecipientWitnessShape(Live, out Failure) ||
				!PolityConsignmentMatches(Operation, Request, SettlementName,
					Operation?.PolityRecipient) ||
				!ExactPolityRecipientWitness(Operation.PolityRecipient, Live))
			{
				Failure = Failure ?? "Loaded polity recipient differs from frozen landing witness";
				return false;
			}
			return true;
		}

		internal static bool TryValidatePolityRecipientWitnessShape(
			KingdomTradePolityRecipientWitness Witness, out string Failure)
		{
			Failure = null;
			if (Witness == null || TooLong(Witness.BodyId, MaxIdChars) ||
				TooLong(Witness.CohortId, MaxIdChars) || TooLong(Witness.ProjectionId,
					MaxIdChars) || TooLong(Witness.SurfaceRef, MaxIdChars) ||
				TooLong(Witness.RequestDigest, 64) || TooLong(Witness.WitnessDigest, 64) ||
				!KingdomPolityRules.TypedId(Witness.BodyId,
				"taf:object:polity-cohort:v1:") || !KingdomPolityRules.TypedId(
				Witness.CohortId, "taf:cohort:") || !KingdomPolityRules.TypedId(
				Witness.ProjectionId, "taf:projection:cohort:v1:") ||
				!KingdomPolityRules.TypedId(Witness.SurfaceRef, "taf:settlement:v1:") ||
				!CanonicalSha256(Witness.RequestDigest) || !CanonicalSha256(
					Witness.WitnessDigest) || !string.Equals(Witness.WitnessDigest,
						PolityRecipientWitnessDigest(Witness), StringComparison.Ordinal))
			{
				Failure = "Polity recipient witness is malformed or changed"; return false;
			}
			return true;
		}

		internal static bool ValidPolityRecipientProof(KingdomTradeProof Proof)
		{
			if (Proof == null) return false;
			if (Proof.Kind != KingdomTradeOperationKind.PolityConsignmentDelivery)
				return Proof.PolityRecipient == null;
			if (Proof.PolityRecipient == null)
				return Proof.Disposition == KingdomTradePhase.Quarantined;
			return TryValidatePolityRecipientWitnessShape(Proof.PolityRecipient,
				out string _) && Proof.PolityRecipient.SurfaceRef == Proof.SettlementId;
		}

		internal static bool ExactPolityRecipientWitness(
			KingdomTradePolityRecipientWitness Left,
			KingdomTradePolityRecipientWitness Right)
		{
			return Left != null && Right != null && Left.BodyId == Right.BodyId &&
				Left.CohortId == Right.CohortId && Left.ProjectionId == Right.ProjectionId &&
				Left.SurfaceRef == Right.SurfaceRef && Left.RequestDigest == Right.RequestDigest &&
				Left.WitnessDigest == Right.WitnessDigest;
		}

		internal static KingdomTradePolityRecipientWitness ClonePolityRecipientWitness(
			KingdomTradePolityRecipientWitness Source)
		{
			return Source == null ? null : new KingdomTradePolityRecipientWitness
			{
				BodyId = Source.BodyId, CohortId = Source.CohortId,
				ProjectionId = Source.ProjectionId, SurfaceRef = Source.SurfaceRef,
				RequestDigest = Source.RequestDigest, WitnessDigest = Source.WitnessDigest
			};
		}

		/// <summary>
		/// Closes only frozen legs that never crossed their mutation intent. Proved value remains
		/// proved and must enter retained Trade custody before quarantine can retire.
		/// </summary>
		internal static void SealUnstartedPolityConsignmentLegs(
			KingdomTradeOperation Operation)
		{
			if (Operation?.Kind != KingdomTradeOperationKind.PolityConsignmentDelivery ||
				Operation.WaterLegs == null) return;
			for (int i = 0; i < Operation.WaterLegs.Count; i++)
				if (Operation.WaterLegs[i]?.State == KingdomTradePhysicalState.Prepared)
					Operation.WaterLegs[i].State = KingdomTradePhysicalState.Skipped;
		}

		/// <summary>
		/// Pure crash-cut classifier. Only the immutable exact before or after vessel state
		/// can resolve an Intent; a third state stays visibly ambiguous and is never replayed.
		/// </summary>
		public static KingdomTradeWaterIntentResolution ClassifyPolityWaterIntent(
			KingdomTradeWaterLeg Leg, int Capacity, int Volume, string Composition)
		{
			if (Leg == null || Leg.State != KingdomTradePhysicalState.Intent || Capacity < 0 ||
				Volume < 0 || Volume > Capacity || Capacity != Leg.Capacity ||
				Composition == null) return KingdomTradeWaterIntentResolution.Invalid;
			bool before = Volume == Leg.Before && string.Equals(Composition,
				Leg.BeforeComposition, StringComparison.Ordinal);
			bool after = Volume == Leg.After && string.Equals(Composition,
				Leg.AfterComposition, StringComparison.Ordinal);
			if (before == after) return KingdomTradeWaterIntentResolution.Ambiguous;
			return before ? KingdomTradeWaterIntentResolution.Before :
				KingdomTradeWaterIntentResolution.After;
		}

		internal static bool HasPolityWaterIntent(KingdomTradeOperation Operation)
		{
			if (Operation?.Kind != KingdomTradeOperationKind.PolityConsignmentDelivery ||
				Operation.WaterLegs == null) return false;
			for (int i = 0; i < Operation.WaterLegs.Count; i++)
				if (Operation.WaterLegs[i]?.State == KingdomTradePhysicalState.Intent) return true;
			return false;
		}

		internal static bool ValidSkippedPolityWaterLeg(KingdomTradeOperation Operation,
			KingdomTradeWaterLeg Leg)
		{
			return Operation != null && Leg != null && Operation.Kind ==
				KingdomTradeOperationKind.PolityConsignmentDelivery && Operation.WaterDirection ==
				KingdomTradeWaterDirection.Debit && Leg.State ==
				KingdomTradePhysicalState.Skipped && Leg.ZoneId == Operation.ZoneId &&
				Leg.BeforeComposition == "water=1000" && Leg.AfterComposition ==
				(Leg.After == 0 ? "empty" : "water=1000") &&
				NormalizeWaterLeg(Leg, KingdomTradeWaterDirection.Debit);
		}

		/// <summary>Read-only gate used before NewOperation can consume sequence or proof space.</summary>
		public static bool TryValidatePolityConsignmentPreparation(KingdomTradeBook Book,
			KingdomPolityConsignmentRequest Request, string ZoneId, string SettlementId,
			string SettlementName, KingdomTradePolityRecipientWitness Witness,
			out string Failure)
		{
			Failure = null;
			if (!BookUsable(Book) || Book.OpenOperation != null ||
				!KingdomPolityCorrespondenceRules.TryValidateConsignmentRequestShape(Request,
					out Failure) || !ValidName(ZoneId) || !ValidId(SettlementId) ||
				!ValidName(SettlementName) || !string.Equals(Book.RealmId,
					Request.CurrentPolityId, StringComparison.Ordinal) ||
				!IdentityContainsSettlement(Book, SettlementId) || SettlementId !=
				Request.SurfaceRef || !TryValidatePolityRecipientWitnessShape(Witness,
					out Failure) || Witness.CohortId != Request.RecipientCohortId ||
				Witness.SurfaceRef != SettlementId || Witness.RequestDigest != Request.RequestDigest)
			{
				Failure = Failure ?? "Polity consignment preparation authority is not exact";
				return false;
			}
			return true;
		}

		private static string PolityRecipientWitnessDigest(
			KingdomTradePolityRecipientWitness Witness)
		{
			return Witness == null ? null : KingdomPolityRules.ActivationDigest(
				"trade-polity-recipient-witness-v1", Witness.BodyId ?? "",
				Witness.CohortId ?? "", Witness.ProjectionId ?? "", Witness.SurfaceRef ?? "",
				Witness.RequestDigest ?? "");
		}
	}
}
