#if TAF_TESTS
using NUnit.Framework;

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
			Assert.IsTrue(Death());
			Assert.IsFalse(Death(actualRealm: "taf:realm:wrong"));
			Assert.IsFalse(Death(actualCohort: "taf:cohort:wrong"));
			Assert.IsFalse(Death(actualProjection: "taf:projection:wrong"));
			Assert.IsFalse(Death(actualZone: "wrong-zone"));
			Assert.IsFalse(Death(actualBody: "taf:object:wrong"));
			Assert.IsFalse(Death(actualOrdinal: 2));
			Assert.IsFalse(Death(valid: false));
			Assert.IsFalse(Death(onGround: false));
			Assert.IsFalse(Death(playerInZone: false));
			Assert.IsFalse(Death(cellVisible: false));
			Assert.IsFalse(Death(objectVisible: false));
		}

		[Test]
		public void GearRejectsCopiedNaturalPartialWrongOrdinalAndWrongOwner()
		{
			Assert.IsTrue(Gear());
			Assert.IsFalse(Gear(actualReceipt: "copied"));
			Assert.IsFalse(Gear(actualRealm: "taf:realm:wrong"));
			Assert.IsFalse(Gear(actualBody: "taf:object:wrong"));
			Assert.IsFalse(Gear(actualGearOrdinal: 7));
			Assert.IsFalse(Gear(natural: true));
			Assert.IsFalse(Gear(whole: false));
			Assert.IsFalse(Gear(zeroValue: false));
			Assert.IsFalse(Gear(untakeable: false));
			Assert.IsFalse(Gear(exactOwner: false));
		}

		[Test]
		public void CustodyClassifierQuarantinesFakeNaturalCopiedAndDuplicateMarks()
		{
			Assert.AreEqual(KingdomPolityCustodyDecision.DeleteExactGear,
				Classify(natural: false, marked: true, exact: true));
			Assert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: true, marked: true, exact: true));
			Assert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: true, marked: false, exact: false, blueprintNatural: false));
			Assert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: false, marked: true, exact: false));
			Assert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: false, marked: true, exact: true, duplicate: true));
			Assert.AreEqual(KingdomPolityCustodyDecision.Quarantine,
				Classify(natural: false, marked: false, exact: false, collision: true));
		}

		[Test]
		public void NestedForeignCrossesOnlyAnOwnedCustodyBoundary()
		{
			Assert.IsTrue(KingdomPolityPhysicalCustodyRules.TransferCrossesOwnedBoundary(
				KingdomPolityCustodyDecision.TransferForeign, ParentOwned: true));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.TransferCrossesOwnedBoundary(
				KingdomPolityCustodyDecision.TransferForeign, ParentOwned: false));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.TransferCrossesOwnedBoundary(
				KingdomPolityCustodyDecision.DeleteExactGear, ParentOwned: true));
		}

		[Test]
		public void NthRemovalCanResumeOnlyFromExactPresentOrWitnessedAbsentState()
		{
			Assert.IsTrue(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(
				PhysicallyPresent: true, ExactWitness: false, ExactResidentId: true));
			Assert.IsTrue(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(
				PhysicallyPresent: false, ExactWitness: true, ExactResidentId: false));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(true, true, true));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(false, false, false));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(true, false, false));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.RemovalCanContinue(false, true, true));
		}

		[Test]
		public void SealedOrCollidingCellsAndEveryDivergentPlacementAftermathReject()
		{
			Assert.IsTrue(KingdomPolityPhysicalCustodyRules.CandidateCellAllowed(true, true,
				true, true));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.CandidateCellAllowed(true, true,
				true, false));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.CandidateCellAllowed(false, true,
				true, true));
			Assert.IsTrue(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, true,
				true, true, true));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(false, true,
				true, true, true));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, false,
				true, true, true));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, true,
				false, true, true));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, true,
				true, false, true));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(true, true,
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
