#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityPhysicalCustodyRulesTests
	{
		private const string Realm = "taf:realm:physical";
		private const string Cohort = "taf:cohort:physical";
		private const string Projection = "taf:projection:physical";
		private const string Zone = "JoppaWorld.11.22.1.1.10";
		private const string Body = "taf:object:body";
		private const string Profile = "taf:profile:physical:3";
		private const string Resolver = "resolver";
		private const string Blueprint = "Snapjaw";
		private const string Object = "taf:object:gear";
		private const string Receipt = "taf:receipt:gear";

		[Test]
		public void DeathRequiresEveryExactPhysicalAndVisibleField()
		{
			ClassicAssert.IsTrue(Death());
			ClassicAssert.IsFalse(Death(actualRealm: "taf:realm:wrong"));
			ClassicAssert.IsFalse(Death(actualCohort: "taf:cohort:wrong"));
			ClassicAssert.IsFalse(Death(actualProjection: "taf:projection:wrong"));
			ClassicAssert.IsFalse(Death(actualZone: "wrong-zone"));
			ClassicAssert.IsFalse(Death(actualBody: "taf:object:wrong"));
			ClassicAssert.IsFalse(Death(actualOrdinal: 2));
			ClassicAssert.IsFalse(Death(valid: false));
			ClassicAssert.IsFalse(Death(onGround: false));
			ClassicAssert.IsFalse(Death(playerInZone: false));
			ClassicAssert.IsFalse(Death(cellVisible: false));
			ClassicAssert.IsFalse(Death(objectVisible: false));
		}

		[Test]
		public void GearRejectsCopiedNaturalPartialWrongOrdinalAndWrongOwner()
		{
			ClassicAssert.IsTrue(Gear());
			ClassicAssert.IsFalse(Gear(actualReceipt: "copied"));
			ClassicAssert.IsFalse(Gear(actualRealm: "taf:realm:wrong"));
			ClassicAssert.IsFalse(Gear(actualBody: "taf:object:wrong"));
			ClassicAssert.IsFalse(Gear(actualGearOrdinal: 7));
			ClassicAssert.IsFalse(Gear(natural: true));
			ClassicAssert.IsFalse(Gear(whole: false));
			ClassicAssert.IsFalse(Gear(zeroValue: false));
			ClassicAssert.IsFalse(Gear(untakeable: false));
			ClassicAssert.IsFalse(Gear(exactOwner: false));
		}

		[Test]
		public void CustodyClassifierQuarantinesFakeNaturalCopiedAndDuplicateMarks()
		{
			ClassicAssert.AreEqual(KingdomPolityCustodyDecision.DeleteExactGear,
				Classify(natural: false, marked: true, exact: true));
			ClassicAssert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: true, marked: true, exact: true));
			ClassicAssert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: true, marked: false, exact: false, blueprintNatural: false));
			ClassicAssert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: false, marked: true, exact: false));
			ClassicAssert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: false, marked: true, exact: true, duplicate: true));
			ClassicAssert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: false, marked: false, exact: false, collision: true));
		}

		[Test]
		public void NestedForeignCrossesOnlyAnOwnedCustodyBoundary()
		{
			ClassicAssert.IsTrue(KingdomPolityPhysicalCustodyRules.TransferCrossesOwnedBoundary(
				KingdomPolityCustodyDecision.TransferForeign, ParentOwned: true));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.TransferCrossesOwnedBoundary(
				KingdomPolityCustodyDecision.TransferForeign, ParentOwned: false));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.TransferCrossesOwnedBoundary(
				KingdomPolityCustodyDecision.DeleteExactGear, ParentOwned: true));
		}

		[Test]
		public void NthRemovalCanResumeOnlyFromExactPresentOrWitnessedAbsentState()
		{
			ClassicAssert.IsTrue(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(
				PhysicallyPresent: true, ExactWitness: false, ExactResidentId: true));
			ClassicAssert.IsTrue(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(
				PhysicallyPresent: false, ExactWitness: true, ExactResidentId: false));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(true, true, true));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(false, false, false));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(true, false, false));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(false, true, true));
		}

		[Test]
		public void SealedOrCollidingCellsAndEveryDivergentPlacementAftermathReject()
		{
			ClassicAssert.IsTrue(KingdomPolityPhysicalCustodyRules.CandidateCellAllowed(true, true,
				true, true));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.CandidateCellAllowed(true, true,
				true, false));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.CandidateCellAllowed(false, true,
				true, true));
			ClassicAssert.IsTrue(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, true,
				true, true, true));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(false, true,
				true, true, true));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, false,
				true, true, true));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, true,
				false, true, true));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, true,
				true, false, true));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, true,
				true, true, false));
		}

		private static bool Death(string actualRealm = Realm, string actualCohort = Cohort,
			string actualProjection = Projection, string actualZone = Zone, string actualBody = Body,
			int actualOrdinal = 1, bool valid = true, bool onGround = true,
			bool playerInZone = true, bool cellVisible = true, bool objectVisible = true)
		{
			return KingdomPolityPhysicalCustodyRules.ExactDeathBinding(Realm, Cohort, Projection,
				Zone, Body, 1, actualRealm, actualCohort, actualProjection, actualZone,
				actualBody, actualOrdinal, valid, onGround, playerInZone, cellVisible, objectVisible);
		}

		private static bool Gear(string actualRealm = Realm, string actualBody = Body,
			int actualGearOrdinal = 2, string actualReceipt = Receipt, bool natural = false,
			bool whole = true, bool zeroValue = true, bool untakeable = true,
			bool exactOwner = true)
		{
			return KingdomPolityPhysicalCustodyRules.ExactGearBinding(Realm, Cohort, Projection,
				Body, 1, 2, Profile, Resolver, Blueprint, Object, Receipt, actualRealm, Cohort,
				Projection, actualBody, 1, actualGearOrdinal, Profile, Resolver, Blueprint, Object,
				actualReceipt, Valid: true, Natural: natural, Whole: whole, ZeroValue: zeroValue,
				Untakeable: untakeable, ExactOwner: exactOwner);
		}

		private static KingdomPolityCustodyDecision Classify(bool natural, bool marked,
			bool exact, bool duplicate = false, bool collision = false,
			bool? blueprintNatural = null)
		{
			return KingdomPolityPhysicalCustodyRules.ClassifyCustody(natural,
				blueprintNatural ?? natural, marked, exact, duplicate, collision);
		}
	}
}
#endif
