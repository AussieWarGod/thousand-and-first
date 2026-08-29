using System;
using System.Globalization;

namespace ThousandAndFirst
{
	internal enum KingdomPolityCustodyDecision : byte
	{
		PreserveNatural = 1,
		DeleteExactGear = 2,
		TransferForeign = 3,
		Quarantine = 4
	}

	public enum KingdomPolityCleanupEvidenceProof : byte
	{
		Absent = 0,
		Exact = 1,
		Ambiguous = 2,
		Unscannable = 3
	}

	public enum KingdomPolityLegacyRewriteRecovery : byte
	{
		Applied = 1,
		OldBytesPreserved = 2,
		Ambiguous = 3
	}

	/// <summary>Pure identities and aftermath gates for finite polity bodies and their loadout.</summary>
	internal static class KingdomPolityPhysicalCustodyRules
	{
		internal const string DeathRemovalKind = "death";
		internal const string CleanupRemovalKind = "cleanup";
		internal const string GearRemovalKind = "gear-cleanup";

		internal static string GearObjectId(string RealmId, string CohortId,
			string ProjectionId, string BodyId, int MemberOrdinal, int GearOrdinal,
			string ProfileRef, string Resolver, string Blueprint)
		{
			return KingdomPolityRules.ActivationId("taf:object:polity-gear:v1:",
				"polity-gear-object-v1", RealmId, CohortId, ProjectionId, BodyId,
				N(MemberOrdinal), N(GearOrdinal), ProfileRef, Resolver, Blueprint);
		}

		internal static string GearReceipt(string RealmId, string CohortId,
			string ProjectionId, string BodyId, int MemberOrdinal, int GearOrdinal,
			string ProfileRef, string Resolver, string Blueprint)
		{
			return KingdomPolityRules.ActivationId("taf:receipt:polity-gear:v1:",
				"polity-gear-receipt-v1", RealmId, CohortId, ProjectionId, BodyId,
				N(MemberOrdinal), N(GearOrdinal), ProfileRef, Resolver, Blueprint);
		}

		internal static string RemovalWitnessKey(string ProjectionId, string ObjectId)
		{
			return KingdomPolityRules.ActivationKey(
				"r_TAF_PolityBodyRemovalWitness_v1:", "polity-removal-witness-key-v1",
				ProjectionId, ObjectId);
		}

		internal static string DeathIntentKey(string ProjectionId, string ObjectId)
		{
			return KingdomPolityRules.ActivationKey(
				"r_TAF_PolityDeathIntent_v1:", "polity-death-intent-key-v1",
				ProjectionId, ObjectId);
		}

		internal static string CleanupIntentKey(string ProjectionId, string ObjectId)
		{
			return KingdomPolityRules.ActivationKey("r_TAF_PolityCleanupIntent_v1:",
				"polity-cleanup-intent-key-v1", ProjectionId, ObjectId);
		}

		internal static string RemovalWitness(string Kind, string RealmId, string CohortId,
			string ProjectionId, string ZoneId, string ObjectId, int Ordinal)
		{
			return KingdomPolityRules.ActivationId(
				"taf:receipt:polity-body-removal-witness:v1:",
				"polity-body-removal-witness-v1", Kind, RealmId, CohortId,
					ProjectionId, ZoneId, ObjectId, N(Ordinal));
		}

		internal static string PreparedCleanupIntent(string RealmId, string CohortId,
			string ProjectionId, string ZoneId, string ObjectId, int Ordinal, int X, int Y,
			byte CohortPhase, byte ProjectionPhase)
		{
			return KingdomPolityRules.ActivationId("taf:intent:polity-cleanup:v1:",
				"polity-prepared-cleanup-intent-v1", RealmId, CohortId, ProjectionId, ZoneId,
				ObjectId, N(Ordinal), N(X), N(Y), N(CohortPhase), N(ProjectionPhase));
		}

		internal static string ContestedWitnessKey(string ProjectionId, string ObjectId)
		{
			return KingdomPolityRules.ActivationKey(
				"r_TAF_PolityPhysicalContested_v1:", "polity-physical-contested-key-v1",
				ProjectionId, ObjectId);
		}

		internal static string ContestedWitness(string RealmId, string CohortId,
			string ProjectionId, string ZoneId, string ObjectId, int Ordinal)
		{
			return KingdomPolityRules.ActivationId(
				"taf:receipt:polity-physical-contested:v1:",
				"polity-physical-contested-witness-v1", RealmId, CohortId, ProjectionId,
				ZoneId, ObjectId, N(Ordinal));
		}

		internal static bool ExactDeathBinding(string ExpectedRealmId, string ExpectedCohortId,
			string ExpectedProjectionId, string ExpectedZoneId, string ExpectedBodyId,
			int ExpectedOrdinal, string ActualRealmId, string ActualCohortId,
			string ActualProjectionId, string ActualZoneId, string ActualBodyId,
			int ActualOrdinal, bool Valid, bool OnGround, bool PlayerInZone,
			bool CellVisible, bool ObjectVisible)
		{
			return Valid && OnGround && PlayerInZone && CellVisible && ObjectVisible &&
				ExpectedOrdinal >= 0 && ExpectedOrdinal == ActualOrdinal &&
				Same(ExpectedRealmId, ActualRealmId) && Same(ExpectedCohortId, ActualCohortId) &&
				Same(ExpectedProjectionId, ActualProjectionId) &&
				Same(ExpectedZoneId, ActualZoneId) && Same(ExpectedBodyId, ActualBodyId);
		}

		internal static bool ExactPhysicalDeathBinding(string ExpectedRealmId,
			string ExpectedCohortId, string ExpectedProjectionId, string ExpectedZoneId,
			string ExpectedBodyId, int ExpectedOrdinal, string ActualRealmId,
			string ActualCohortId, string ActualProjectionId, string ActualZoneId,
			string ActualBodyId, int ActualOrdinal, bool Valid, bool OnGround, bool PlayerInZone)
		{
			return Valid && OnGround && PlayerInZone && ExpectedOrdinal >= 0 &&
				ExpectedOrdinal == ActualOrdinal && Same(ExpectedRealmId, ActualRealmId) &&
				Same(ExpectedCohortId, ActualCohortId) &&
				Same(ExpectedProjectionId, ActualProjectionId) &&
				Same(ExpectedZoneId, ActualZoneId) && Same(ExpectedBodyId, ActualBodyId);
		}

		internal static bool ExactPlacementAftermath(bool ReturnedSameObject, bool Valid,
			bool ExactCell, bool ExactIdentity, bool ExactCustody)
		{
			return ReturnedSameObject && Valid && ExactCell && ExactIdentity && ExactCustody;
		}

		internal static bool CandidateCellAllowed(bool Distinct, bool Passable, bool Empty,
			bool RouteProved)
		{
			return Distinct && Passable && Empty && RouteProved;
		}

		internal static bool RemovalCanContinue(bool PhysicallyPresent, bool ExactWitness,
			bool ExactResidentId)
		{
			return PhysicallyPresent ? !ExactWitness && ExactResidentId :
				ExactWitness && !ExactResidentId;
		}

		internal static KingdomPolityCleanupEvidenceProof ClassifyCleanupEvidence(
			bool ScanComplete, int MatchingSlots, bool ExactType, bool ExactValue)
		{
			if (!ScanComplete) return KingdomPolityCleanupEvidenceProof.Unscannable;
			if (MatchingSlots == 0) return KingdomPolityCleanupEvidenceProof.Absent;
			return MatchingSlots == 1 && ExactType && ExactValue
				? KingdomPolityCleanupEvidenceProof.Exact
				: KingdomPolityCleanupEvidenceProof.Ambiguous;
		}

		internal static KingdomPolityCleanupEvidenceProof ClassifyResidentEvidence(
			bool ScanComplete, int Matches)
		{
			if (!ScanComplete) return KingdomPolityCleanupEvidenceProof.Unscannable;
			if (Matches == 0) return KingdomPolityCleanupEvidenceProof.Absent;
			return Matches == 1 ? KingdomPolityCleanupEvidenceProof.Exact :
				KingdomPolityCleanupEvidenceProof.Ambiguous;
		}

		internal static bool PreparedAbsenceCanRollback(
			KingdomPolityCleanupEvidenceProof Intent,
			KingdomPolityCleanupEvidenceProof FinalWitness)
		{
			return (Intent == KingdomPolityCleanupEvidenceProof.Exact &&
				(FinalWitness == KingdomPolityCleanupEvidenceProof.Absent ||
				 FinalWitness == KingdomPolityCleanupEvidenceProof.Exact)) ||
				(Intent == KingdomPolityCleanupEvidenceProof.Absent &&
				 FinalWitness == KingdomPolityCleanupEvidenceProof.Exact);
		}

		internal static bool CleanupIntentCanClear(
			KingdomPolityCleanupEvidenceProof Intent,
			KingdomPolityCleanupEvidenceProof FinalWitness)
		{
			return Intent == KingdomPolityCleanupEvidenceProof.Exact &&
				FinalWitness == KingdomPolityCleanupEvidenceProof.Exact;
		}

		internal static bool CleanupIntentClearAcknowledged(
			KingdomPolityCleanupEvidenceProof Intent,
			KingdomPolityCleanupEvidenceProof FinalWitness)
		{
			return Intent == KingdomPolityCleanupEvidenceProof.Absent &&
				FinalWitness == KingdomPolityCleanupEvidenceProof.Exact;
		}

		internal static KingdomPolityLegacyRewriteRecovery ClassifyLegacyRewriteRecovery(
			bool ReadComplete, bool Present, bool ExactType, bool ExactCurrent,
			bool ExactLegacy)
		{
			if (ReadComplete && Present && ExactType && ExactCurrent)
				return KingdomPolityLegacyRewriteRecovery.Applied;
			if (ReadComplete && Present && ExactType && ExactLegacy)
				return KingdomPolityLegacyRewriteRecovery.OldBytesPreserved;
			return KingdomPolityLegacyRewriteRecovery.Ambiguous;
		}

		internal static KingdomPolityCustodyDecision ClassifyCustody(bool ClaimsNatural,
			bool BlueprintNatural, bool HasAnyGearMark, bool ExactGear, bool DuplicateGear,
			bool ExactIdCollision)
		{
			if (ClaimsNatural != BlueprintNatural) return KingdomPolityCustodyDecision.Quarantine;
			if (HasAnyGearMark)
				return !ClaimsNatural && ExactGear && !DuplicateGear ?
					KingdomPolityCustodyDecision.DeleteExactGear :
					KingdomPolityCustodyDecision.Quarantine;
			if (ExactIdCollision) return KingdomPolityCustodyDecision.Quarantine;
			return ClaimsNatural ? KingdomPolityCustodyDecision.PreserveNatural :
				KingdomPolityCustodyDecision.TransferForeign;
		}

		internal static bool TransferCrossesOwnedBoundary(
			KingdomPolityCustodyDecision Decision, bool ParentOwned)
		{
			return Decision == KingdomPolityCustodyDecision.TransferForeign && ParentOwned;
		}

		internal static bool ExactGearBinding(string ExpectedRealmId, string ExpectedCohortId,
			string ExpectedProjectionId, string ExpectedBodyId, int ExpectedMemberOrdinal,
			int ExpectedGearOrdinal, string ExpectedProfileRef, string ExpectedResolver,
			string ExpectedBlueprint, string ExpectedObjectId, string ExpectedReceipt,
			string ActualRealmId, string ActualCohortId, string ActualProjectionId,
			string ActualBodyId, int ActualMemberOrdinal, int ActualGearOrdinal,
			string ActualProfileRef, string ActualResolver, string ActualBlueprint,
			string ActualObjectId, string ActualReceipt, bool Valid, bool Natural,
			bool Whole, bool ZeroValue, bool Untakeable, bool ExactOwner)
		{
			return Valid && !Natural && Whole && ZeroValue && Untakeable && ExactOwner &&
				ExpectedMemberOrdinal == ActualMemberOrdinal &&
				ExpectedGearOrdinal == ActualGearOrdinal && Same(ExpectedRealmId, ActualRealmId) &&
				Same(ExpectedCohortId, ActualCohortId) &&
				Same(ExpectedProjectionId, ActualProjectionId) &&
				Same(ExpectedBodyId, ActualBodyId) && Same(ExpectedProfileRef, ActualProfileRef) &&
				Same(ExpectedResolver, ActualResolver) &&
				Same(ExpectedBlueprint, ActualBlueprint) && Same(ExpectedObjectId, ActualObjectId) &&
				Same(ExpectedReceipt, ActualReceipt);
		}

		private static bool Same(string A, string B)
		{
			return string.Equals(A, B, StringComparison.Ordinal);
		}

		private static string N(int Value)
		{
			return Value.ToString(CultureInfo.InvariantCulture);
		}
	}
}
