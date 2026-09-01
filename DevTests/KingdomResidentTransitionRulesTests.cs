#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomResidentTransitionRulesTests
	{
		[TestCase(KingdomResidentTransitionClaim.AuthorityUnproved)]
		[TestCase(KingdomResidentTransitionClaim.NamedCook)]
		[TestCase(KingdomResidentTransitionClaim.AssentingMoot)]
		[TestCase(KingdomResidentTransitionClaim.PhysicalHappening)]
		[TestCase(KingdomResidentTransitionClaim.OpenLodge)]
		[TestCase(KingdomResidentTransitionClaim.Expedition)]
		[TestCase(KingdomResidentTransitionClaim.BountyManning)]
		[TestCase(KingdomResidentTransitionClaim.Keeper)]
		[TestCase(KingdomResidentTransitionClaim.StasisCustody)]
		[TestCase(KingdomResidentTransitionClaim.PreparedMarketHandoff)]
		[TestCase(KingdomResidentTransitionClaim.SuccessionProtectedResident)]
		[TestCase(KingdomResidentTransitionClaim.LabRefusalDeparture)]
		[TestCase(KingdomResidentTransitionClaim.ResidentDeparture)]
		public void AccessionRefusesEveryRoleWithoutAnExactClosure(
			KingdomResidentTransitionClaim claim)
		{
			Assert.IsFalse(KingdomResidentTransitionRules.CanAccede(claim));
		}

		[TestCase(KingdomResidentTransitionClaim.None)]
		[TestCase(KingdomResidentTransitionClaim.CivicOffice)]
		[TestCase(KingdomResidentTransitionClaim.CompletedLegendaryMarket)]
		[TestCase(KingdomResidentTransitionClaim.SuccessionAccessionOwner)]
		[TestCase(KingdomResidentTransitionClaim.PolityResidentBridge)]
		public void AccessionAllowsCleanOrExactlyClosableAuthority(
			KingdomResidentTransitionClaim claim)
		{
			Assert.IsTrue(KingdomResidentTransitionRules.CanAccede(claim));
		}

		[Test]
		public void CompetingClosableOwnersStillRefuseAccession()
		{
			Assert.IsFalse(KingdomResidentTransitionRules.CanAccede(
				KingdomResidentTransitionClaim.CivicOffice
					| KingdomResidentTransitionClaim.CompletedLegendaryMarket));
		}

		[Test]
		public void ExactSuccessionOwnerMayCrossItsOwnProtection()
		{
			Assert.IsTrue(KingdomResidentTransitionRules.CanAccede(
				KingdomResidentTransitionClaim.SuccessionAccessionOwner
					| KingdomResidentTransitionClaim.SuccessionProtectedResident));
		}

		[TestCase(KingdomResidentTransitionClaim.AuthorityUnproved)]
		[TestCase(KingdomResidentTransitionClaim.NamedCook)]
		[TestCase(KingdomResidentTransitionClaim.AssentingMoot)]
		[TestCase(KingdomResidentTransitionClaim.PhysicalHappening)]
		[TestCase(KingdomResidentTransitionClaim.OpenLodge)]
		[TestCase(KingdomResidentTransitionClaim.Expedition)]
		[TestCase(KingdomResidentTransitionClaim.BountyManning)]
		[TestCase(KingdomResidentTransitionClaim.Keeper)]
		[TestCase(KingdomResidentTransitionClaim.StasisCustody)]
		[TestCase(KingdomResidentTransitionClaim.PreparedMarketHandoff)]
		[TestCase(KingdomResidentTransitionClaim.CivicOffice)]
		[TestCase(KingdomResidentTransitionClaim.CompletedLegendaryMarket)]
		[TestCase(KingdomResidentTransitionClaim.MarketStock)]
		[TestCase(KingdomResidentTransitionClaim.MarketTransfer)]
		[TestCase(KingdomResidentTransitionClaim.NativeMerchantStock)]
		[TestCase(KingdomResidentTransitionClaim.SuccessionAccessionOwner)]
		[TestCase(KingdomResidentTransitionClaim.SuccessionProtectedResident)]
		[TestCase(KingdomResidentTransitionClaim.LabRefusalDeparture)]
		[TestCase(KingdomResidentTransitionClaim.PolityResidentBridge)]
		[TestCase(KingdomResidentTransitionClaim.ResidentDeparture)]
		public void DestructionRefusesEveryLiveRoleEndpointOrCustody(
			KingdomResidentTransitionClaim claim)
		{
			Assert.IsFalse(KingdomResidentTransitionRules.CanDestroy(claim));
		}

		[TestCase(KingdomResidentTransitionClaim.NamedCook,
			KingdomResidentTransitionClaim.CookDeparturePrepared)]
		[TestCase(KingdomResidentTransitionClaim.CivicOffice,
			KingdomResidentTransitionClaim.OfficeDeparturePrepared)]
		public void ExactPreparedRoleReceiptClosesOnlyItsOwnClaim(
			KingdomResidentTransitionClaim role, KingdomResidentTransitionClaim prepared)
		{
			Assert.IsTrue(KingdomResidentTransitionRules.CanDestroy(role | prepared));
			Assert.IsFalse(KingdomResidentTransitionRules.CanDestroy(role | prepared
				| KingdomResidentTransitionClaim.StasisCustody));
		}

		[Test]
		public void DestructionPreflightAllowsOnlyReversibleRoleAndPolityPreparation()
		{
			KingdomResidentTransitionClaim closable =
				KingdomResidentTransitionClaim.NamedCook
				| KingdomResidentTransitionClaim.CivicOffice
				| KingdomResidentTransitionClaim.PolityResidentBridge;
			Assert.IsTrue(KingdomResidentTransitionRules.CanPrepareDestroy(closable));
			Assert.IsFalse(KingdomResidentTransitionRules.CanPrepareDestroy(closable
				| KingdomResidentTransitionClaim.MarketStock));
		}

		[Test]
		public void TerminalReceiptsAndCleanNestedInventoryProjectNoClaim()
		{
			Assert.IsTrue(KingdomResidentTransitionRules.CanAccede(
				KingdomResidentTransitionClaim.None));
			Assert.IsTrue(KingdomResidentTransitionRules.CanDestroy(
				KingdomResidentTransitionClaim.None));
		}

		[Test]
		public void ExactLabCapabilityConsumesOnlyTheLabClaim()
		{
			Assert.IsTrue(KingdomResidentTransitionRules.CanDestroy(
				KingdomResidentTransitionClaim.LabRefusalDeparture,
				ExactLabAuthorization: true));
			Assert.IsFalse(KingdomResidentTransitionRules.CanDestroy(
				KingdomResidentTransitionClaim.LabRefusalDeparture
					| KingdomResidentTransitionClaim.CivicOffice,
				ExactLabAuthorization: true));
		}

		[TestCase(1, 1, false, true)]
		[TestCase(0, 1, false, false)]
		[TestCase(1, 0, false, false)]
		[TestCase(2, 1, false, false)]
		[TestCase(1, 2, false, false)]
		[TestCase(0, 0, true, true)]
		[TestCase(1, 0, true, true)]
		[TestCase(0, 1, true, true)]
		[TestCase(2, 0, true, false)]
		public void CarrierMultiplicityIsExactAndRepairOnly(int rows, int bindings,
			bool repair, bool expected)
		{
			Assert.AreEqual(expected,
				KingdomResidentTransitionRules.ExactCarrierMultiplicity(rows, bindings,
					repair));
		}

		[Test]
		public void DepartureJournalNormalizesOnlyTheTrueOldSaveDefault()
		{
			KingdomResidentDepartureOperation normalized =
				KingdomResidentDepartureRules.NormalizeOldDefault(null);
			Assert.IsTrue(KingdomResidentDepartureRules.IsEmpty(normalized));
			KingdomResidentDepartureOperation residue = new KingdomResidentDepartureOperation
			{
				RealmId = "foreign-residue"
			};
			Assert.IsFalse(KingdomResidentDepartureRules.IsEmpty(residue));
			Assert.AreSame(residue,
				KingdomResidentDepartureRules.NormalizeOldDefault(residue));
			Assert.IsFalse(KingdomResidentDepartureRules.Valid(residue));
		}

		[Test]
		public void DepartureJournalAdvancesMonotonicallyAndRejectsSkippedOrTamperedPhases()
		{
			KingdomResidentDepartureOperation operation = Departure();
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(operation));
			Assert.IsFalse(KingdomResidentDepartureRules.Advance(operation,
				KingdomResidentDeparturePhase.Prepared,
				KingdomResidentDeparturePhase.CitizenshipRemoved));
			KingdomResidentDeparturePhase[] phases =
			{
				KingdomResidentDeparturePhase.RolesPrepared,
				KingdomResidentDeparturePhase.CitizenshipRemoved,
				KingdomResidentDeparturePhase.CarriersRemoved,
				KingdomResidentDeparturePhase.RolesClosed,
				KingdomResidentDeparturePhase.EffectsPublished
			};
			for (int i = 0; i < phases.Length; i++)
			{
				Assert.IsTrue(KingdomResidentDepartureRules.Advance(operation,
					(KingdomResidentDeparturePhase)(i + 1), phases[i]));
				Assert.AreEqual(i + 2L, operation.Revision);
			}
			operation.BodyObjectId = "foreign-body";
			Assert.IsFalse(KingdomResidentDepartureRules.Valid(operation));
		}

		[Test]
		public void DepartureJournalCopyRoundTripsDeedIdentityWithoutAliasing()
		{
			KingdomResidentDepartureOperation operation = Departure();
			operation.PriorPolity = new KingdomPolityNamedFigureRecord
			{
				FigureId = "taf:figure:v1:test", PolityId = "taf:polity:v1:test",
				DisplayName = operation.ResidentName, RoleKey = "salvager",
				Origin = KingdomPolityFigureOrigin.PromotedByDeed,
				Phase = KingdomPolityFigurePhase.Active, CauseRef = "deed:cause",
				ChronicleRef = "deed:chronicle", DeedSummary = "found the glass cache",
				ResidentId = operation.ResidentId,
				ResidentSettlementId = operation.SettlementId
			};
			operation.PolityConclusionRef = "taf:conclusion:resident:test";
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(operation));

			KingdomResidentDepartureOperation copy = operation.Copy();
			Assert.AreNotSame(operation, copy);
			Assert.AreNotSame(operation.PriorPolity, copy.PriorPolity);
			Assert.AreEqual(operation.OperationId, copy.OperationId);
			Assert.AreEqual(operation.Revision, copy.Revision);
			Assert.AreEqual(operation.Cause, copy.Cause);
			Assert.AreEqual(operation.PolityConclusionRef, copy.PolityConclusionRef);
			Assert.AreEqual("found the glass cache", copy.PriorPolity.DeedSummary);
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(copy));
		}

		[Test]
		public void DepartureJournalRejectsPartialOrMixedTypedAuthorization()
		{
			KingdomResidentDepartureOperation operation = Departure();
			operation.AuthorizationEventId = "event-without-kind";
			Assert.IsFalse(KingdomResidentDepartureRules.Valid(operation));
			operation.AuthorizationKind =
				(int)KingdomResidentDestructionAuthorizationKind.LabRefusalDeparture;
			operation.AuthorizationOwnerObjectId = "owner";
			operation.AuthorizationCauseDigest = "digest";
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(operation));
		}

		private static KingdomResidentDepartureOperation Departure()
		{
			string realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
			string settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);
			KingdomResidentDepartureOperation operation =
				new KingdomResidentDepartureOperation
				{
					Version = KingdomResidentDepartureOperation.CurrentVersion,
					Phase = (int)KingdomResidentDeparturePhase.Prepared,
					Revision = 1L, RealmId = realm, SettlementId = settlement,
					ResidentId = 7, BodyObjectId = "body-7", ZoneId = "zone-7",
					ResidentName = "Ari", Origin = "Joppa", PreparedTick = 19L,
					DeparturesBefore = 3, Chronicled = false,
					ChronicleLine = "", LedgerLine = "", Cause = "for dry country"
				};
			operation.OperationId = KingdomResidentDepartureRules.Id(realm, settlement,
				operation.ResidentId, operation.BodyObjectId, operation.PreparedTick);
			return operation;
		}
	}
}
#endif
