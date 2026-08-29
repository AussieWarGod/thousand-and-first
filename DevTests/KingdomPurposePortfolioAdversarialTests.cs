#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPurposePortfolioAdversarialTests
	{
		private const string D = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string E = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		[Test]
		public void AnyLegacyOrReciprocalSchemaPresenceProtectsPurposeCargo()
		{
			KingdomPurposeCargoEvidence[] fields = new KingdomPurposeCargoEvidence[15];
			fields[0].LegacySchema = true;
			fields[1].LegacyKey = true;
			fields[2].LegacyManifest = true;
			fields[3].LegacyConsignment = true;
			fields[4].LegacyOrigin = true;
			fields[5].LegacyDestination = true;
			fields[6].PortfolioSchema = true;
			fields[7].PortfolioReceipt = true;
			fields[8].PortfolioKey = true;
			fields[9].PortfolioFood = true;
			fields[10].LandedFood = true;
			fields[11].LandedReceipt = true;
			fields[12].LandedCount = true;
			fields[13].LandedAttempt = true;
			fields[14].LandedFault = true;
			Assert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoIsProtected(
				new KingdomPurposeCargoEvidence()));
			for (int i = 0; i < fields.Length; i++)
				Assert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoIsProtected(fields[i]),
					"owned cargo field " + i + " must protect on presence alone");
		}

		[Test]
		public void PurposeCargoOwnedFieldsRejectMissingWrongAndDualTypes()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				true, false, true));
			Assert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				false, false, true));
			Assert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				false, true, true));
			Assert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				true, true, true));
			Assert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				false, true, false));
			Assert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				false, false, false));
			Assert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				true, false, false));
			Assert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				true, true, false));
		}

		[Test]
		public void WireEnumsAppendWithoutRenumberingBodyPurposes()
		{
			Assert.AreEqual(1, (byte)KingdomPurposeKind.Flesh);
			Assert.AreEqual(2, (byte)KingdomPurposeKind.Chrome);
			Assert.AreEqual(3, (byte)KingdomPurposeKind.Deep);
			Assert.AreEqual(4, (byte)KingdomPurposeKind.Forge);
			Assert.AreEqual(5, (byte)KingdomPurposeKind.Harvest);
			Assert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(
				typeof(KingdomPurposePairPhase)));
			Assert.AreEqual(11, (byte)KingdomPurposePairPhase.Quarantined);
			Assert.AreEqual(12, (byte)KingdomPurposeOperationPhase.Quarantined);
			Assert.AreEqual(13, (byte)KingdomPurposeOperationPhase.PickupComplete);
			Assert.AreEqual(14, (byte)KingdomPurposeOperationPhase.LandingPending);
		}

		[Test]
		public void NestedOperationIsPairEpochAndEndpointBound()
		{
			KingdomPurposePairReceipt frozen = Pair();
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(frozen, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out var fault), fault.ToString());
			string encoded = KingdomPurposePortfolioRules.EncodeOperation(operation);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodeOperation(encoded, out var decoded));
			Assert.AreEqual(encoded, KingdomPurposePortfolioRules.EncodeOperation(decoded));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryDecodeOperation(encoded + "x", out _));

			KingdomPurposePairReceipt running = frozen.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = operation;
			running.NextOperationOrdinal++;
			running.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, running, out fault),
				fault.ToString());
			KingdomPurposePairReceipt crossed = running.Copy();
			crossed.Operation.PairEpoch++;
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidPair(crossed, out fault));
			Assert.AreEqual(KingdomPurposePairFault.Identity, fault);
			crossed = running.Copy();
			crossed.Operation.SourceOutputStoreId = "another-store";
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidPair(crossed, out fault));
			Assert.AreEqual(KingdomPurposePairFault.Identity, fault);
		}

		[Test]
		public void EvidenceCannotAppearBeforeItsCallbackPhase()
		{
			KingdomPurposePairReceipt pair = Pair();
			Assert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperation(pair, "skipped", 2,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out _, out _));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out _));
			operation.EffectBeforeDigest = D;
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out _));
			operation.EffectBeforeDigest = null;
			operation.TransportJobId = "early-job";
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out _));
			operation.TransportJobId = null;
			operation.Phase = KingdomPurposeOperationPhase.InputDebitPending;
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out _),
				"an exempt operation has no partner-input callback");
			operation.Phase = KingdomPurposeOperationPhase.Acknowledged;
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out _),
				"pair credit consumption, not a free-standing operation phase, acknowledges delivery");
		}

		[Test]
		public void OutputCargoIsExactAndRouteTamperRejecting()
		{
			KingdomPurposePairReceipt pair = Pair();
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out _));
			operation.Phase = KingdomPurposeOperationPhase.OutputPending;
			operation.WaterSpent = operation.WaterRequested;
			operation.FoodSpent = operation.FoodRequested;
			operation.MaterialSpent = operation.MaterialRequested;
			operation.LocalDebitReceipt = KingdomPurposePortfolioTestData.LocalDebit(operation);
			operation.EffectBeforeDigest = D;
			operation.EffectAfterDigest = D;
			operation.EffectStep = (int)KingdomPurposeEffectRefineStep.Made;
			operation.OutputBeforeDigest = D;
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateCargo(pair, operation,
				"cargo", "job", out var cargo, out var fault), fault.ToString());
			string cargoReceipt = KingdomPurposePortfolioRules.EncodeCargo(cargo);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodeCargo(cargoReceipt, out var copy));
			Assert.AreEqual(cargoReceipt, KingdomPurposePortfolioRules.EncodeCargo(copy));
			operation.OutputCargoId = cargo.ObjectId;
			operation.OutputCargoReceipt = cargoReceipt;
			operation.TransportJobId = cargo.TransportJobId;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidOperation(operation, out fault),
				fault.ToString());
			cargo.RouteDigest = E;
			operation.OutputCargoReceipt = KingdomPurposePortfolioRules.EncodeCargo(cargo);
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out fault));
			Assert.AreEqual(KingdomPurposePairFault.Identity, fault);
		}

		[Test]
		public void OrphanResumeDissolveAndQuarantineAreOneWay()
		{
			KingdomPurposePairReceipt frozen = Pair();
			KingdomPurposePairReceipt orphan = frozen.Copy();
			orphan.Phase = KingdomPurposePairPhase.Orphaned;
			orphan.ResumePhase = KingdomPurposePairPhase.Frozen;
			orphan.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, orphan, out _));
			KingdomPurposePairReceipt resumed = orphan.Copy();
			resumed.Phase = KingdomPurposePairPhase.Frozen;
			resumed.ResumePhase = KingdomPurposePairPhase.Invalid;
			resumed.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(orphan, resumed, out _));

			KingdomPurposePairReceipt quarantine = frozen.Copy();
			quarantine.Phase = KingdomPurposePairPhase.Quarantined;
			quarantine.Fault = "physical callback observed a third state";
			quarantine.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, quarantine, out _));
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidTransition(quarantine, resumed, out _));

			KingdomPurposePairReceipt dormant = frozen.Copy();
			dormant.Phase = KingdomPurposePairPhase.Dormant;
			dormant.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, dormant, out _));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperation(dormant, "late", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out _, out _));
			frozen.Fault = "fault outside quarantine";
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidPair(frozen, out _));
		}

		[Test]
		public void ExactTopologyOrphansOnceAndRejoinResumesSameEpoch()
		{
			KingdomPurposePairReceipt frozen = Pair();
			Assert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(frozen,
				new string[] { "city-a" }, out var orphan, out var fault), fault.ToString());
			Assert.AreEqual(KingdomPurposePairPhase.Orphaned, orphan.Phase);
			Assert.AreEqual(KingdomPurposePairPhase.Frozen, orphan.ResumePhase);
			Assert.AreEqual(frozen.Epoch, orphan.Epoch);
			Assert.AreEqual(frozen.Revision + 1, orphan.Revision);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(orphan,
				new string[] { "city-a" }, out var unchanged, out fault), fault.ToString());
			Assert.AreEqual(orphan.Revision, unchanged.Revision);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(orphan,
				new string[] { "city-c", "city-b", "city-a" }, out var resumed, out fault),
				fault.ToString());
			Assert.AreEqual(KingdomPurposePairPhase.Frozen, resumed.Phase);
			Assert.AreEqual(KingdomPurposePairPhase.Invalid, resumed.ResumePhase);
			Assert.AreEqual(orphan.Epoch, resumed.Epoch);
			Assert.AreEqual(orphan.Revision + 1, resumed.Revision,
				"an unrelated third city must not alter the paired epoch");
			Assert.IsFalse(KingdomPurposePortfolioRules.TryReconcileTopology(resumed,
				new string[] { "city-a", "city-a" }, out _, out _));
		}

		[Test]
		public void ExhaustedCountersRemainReadableButRefuseEveryIncrement()
		{
			KingdomPurposePairReceipt exhausted = Pair();
			exhausted.Revision = int.MaxValue;
			Assert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(exhausted,
				new string[] { "city-a", "city-b" }, out var unchanged, out var fault),
				fault.ToString());
			Assert.AreEqual(int.MaxValue, unchanged.Revision,
				"a no-op topology read does not need counter headroom");
			Assert.IsFalse(KingdomPurposePortfolioRules.TryReconcileTopology(exhausted,
				new string[] { "city-a" }, out var refused, out fault));
			Assert.IsNull(refused);
			Assert.AreEqual(KingdomPurposePairFault.Bounds, fault);

			KingdomPurposePairReceipt attempted = exhausted.Copy();
			attempted.Phase = KingdomPurposePairPhase.Dormant;
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidTransition(
				exhausted, attempted, out _));

			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(Pair(), "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out fault), fault.ToString());
			operation.Revision = int.MaxValue;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidOperation(operation, out fault),
				fault.ToString());
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidOperationTransition(
				operation, operation.Copy()));
		}

		[Test]
		public void NewOperationReservesCompletionAndCreditHeadroom()
		{
			Assert.AreEqual(79, KingdomPurposePortfolioRules.MaxOrdinaryOperationAdvances);
			Assert.AreEqual(77, KingdomPurposePortfolioRules.MaxExemptOperationAdvances);
			Assert.AreEqual(1, KingdomPurposePortfolioRules.TerminalPairRevisionReserve);
			Assert.AreEqual(82, KingdomPurposePortfolioRules.NormalOperationAdmissionHeadroom);
			Assert.AreEqual(160, KingdomPurposePortfolioRules.ReturnOperationAdmissionHeadroom);
			Assert.AreEqual(238, KingdomPurposePortfolioRules.BootstrapOperationAdmissionHeadroom);
			AssertBoundary(KingdomPurposePairPhase.Frozen, 238);
			AssertBoundary(KingdomPurposePairPhase.SecondPending, 160);
			AssertBoundary(KingdomPurposePairPhase.Active, 82);
			AssertBoundary(KingdomPurposePairPhase.CargoAwaitingActivation, 82);
			Assert.IsFalse(KingdomPurposePortfolioRules.CanStartOperationAtRevision(
				-1, KingdomPurposePairPhase.Frozen));

			Assert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.BootstrapOutstanding,
				KingdomPurposePairPhase.Invalid, 0, out int bootstrap));
			Assert.AreEqual(237, bootstrap);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.BootstrapOutstanding,
				KingdomPurposePairPhase.Invalid, 77, out int secondPending));
			Assert.AreEqual(160, secondPending);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.ReturnOutstanding,
				KingdomPurposePairPhase.Invalid, 0, out int returned));
			Assert.AreEqual(159, returned);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.OperationOutstanding,
				KingdomPurposePairPhase.Invalid, 0, out int ordinary));
			Assert.AreEqual(81, ordinary);
		}

		private static void AssertBoundary(KingdomPurposePairPhase phase, int required)
		{
			int last = int.MaxValue - required;
			Assert.IsTrue(KingdomPurposePortfolioRules.CanStartOperationAtRevision(last, phase));
			Assert.IsFalse(KingdomPurposePortfolioRules.CanStartOperationAtRevision(last + 1,
				phase));
		}

		[Test]
		public void ExactBootstrapReturnActivationChainReachesReservedTerminalRevision()
		{
			int revision = int.MaxValue
				- KingdomPurposePortfolioRules.BootstrapOperationAdmissionHeadroom;
			revision++;
			AssertExactHeadroom(revision, KingdomPurposePairPhase.BootstrapOutstanding,
				0, 237);
			revision += KingdomPurposePortfolioRules.MaxExemptOperationAdvances;
			AssertExactHeadroom(revision, KingdomPurposePairPhase.SecondPending, 77, 160);
			revision++;
			AssertExactHeadroom(revision, KingdomPurposePairPhase.ReturnOutstanding, 0, 159);
			revision += KingdomPurposePortfolioRules.MaxExemptOperationAdvances;
			AssertExactHeadroom(revision, KingdomPurposePairPhase.CargoAwaitingActivation,
				77, 82);
			revision++;
			AssertExactHeadroom(revision, KingdomPurposePairPhase.OperationOutstanding, 0, 81);
			revision += KingdomPurposePortfolioRules.MaxOrdinaryOperationAdvances;
			AssertExactHeadroom(revision, KingdomPurposePairPhase.CargoAwaitingConsumption,
				79, 2);
			revision++;
			AssertExactHeadroom(revision, KingdomPurposePairPhase.Active, 0, 1);
			revision++;
			Assert.AreEqual(int.MaxValue, revision);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.Dormant, KingdomPurposePairPhase.Invalid, 0,
				out int terminal));
			Assert.AreEqual(0, terminal);
		}

		private static void AssertExactHeadroom(int revision, KingdomPurposePairPhase phase,
			int operationRevision, int expected)
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(phase,
				KingdomPurposePairPhase.Invalid, operationRevision, out int required));
			Assert.AreEqual(expected, required);
			Assert.AreEqual(required, int.MaxValue - revision);
		}

		[Test]
		public void OrphanedPairAdvancesOnlyItsAlreadyCommittedOperation()
		{
			KingdomPurposePairReceipt frozen = Pair();
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(frozen, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out var fault), fault.ToString());
			KingdomPurposePairReceipt running = frozen.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = operation;
			running.NextOperationOrdinal++;
			running.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, running, out fault),
				fault.ToString());
			Assert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(running,
				new string[] { "city-a" }, out var orphan, out fault), fault.ToString());
			KingdomPurposePairReceipt advanced = orphan.Copy();
			advanced.Operation.Phase = KingdomPurposeOperationPhase.LocalDebitPending;
			advanced.Operation.LocalDebitReceipt =
				KingdomPurposePortfolioTestData.LocalDebit(advanced.Operation);
			advanced.Operation.Revision++;
			advanced.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(orphan, advanced, out fault),
				fault.ToString());
			Assert.AreEqual(KingdomPurposePairPhase.Orphaned, advanced.Phase);
			Assert.AreEqual(KingdomPurposePairPhase.BootstrapOutstanding, advanced.ResumePhase);
			advanced.Operation.OperationId = "replacement-operation";
			advanced.Operation.Revision++;
			advanced.Revision++;
			Assert.IsFalse(KingdomPurposePortfolioRules.ValidTransition(orphan, advanced, out _));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperation(orphan, "new", 2,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out _, out _));
		}

		[Test]
		public void CatalogueCopiesCannotMutateFrozenRecipes()
		{
			var recipes = KingdomPurposePortfolioRules.AllRecipes();
			recipes[0].CargoKey = "tampered";
			Assert.IsTrue(KingdomPurposePortfolioRules.TryRecipe(KingdomPurposeKind.Deep,
				KingdomPurposeKind.Forge, out var exact));
			Assert.AreEqual("deep-ore-assay", exact.CargoKey);
			Assert.AreEqual(2, KingdomPurposePortfolioRules.Partners(
				KingdomPurposeKind.Deep).Count);
		}

		[Test]
		public void NewEpochMayFreezeTwoExistingShellsWithoutReusingCargo()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("new-pair", "realm", 8,
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, "city-a", "city-b", "work-a",
				"work-b", "zone-a", "zone-b", "input-a", "output-a", "input-b", "output-b",
				"gate-a", "gate-b", D, out var pair, out var fault), fault.ToString());
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "new-bootstrap", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out fault), fault.ToString());
			Assert.AreEqual("work-b", operation.DestinationWorkId);
			KingdomPurposePairReceipt running = pair.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = operation;
			running.NextOperationOrdinal++;
			running.Revision++;
			Assert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(pair, running, out fault),
				fault.ToString());
		}

		[Test]
		public void BodyAuthorityIsCanonicalAndRequiredByBodyOperations()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("body-pair", "realm", 9,
				KingdomPurposeKind.Chrome, KingdomPurposeKind.Deep, "city-c", "city-d",
				"work-c", null, "zone-c", "zone-d", "input-c", "output-c", "input-d",
				"output-d", "gate-c", "gate-d", D, out var pair, out var fault),
				fault.ToString());
			KingdomPurposeBodyAuthority authority = new KingdomPurposeBodyAuthority
			{
				Kind = KingdomPurposeKind.Chrome, PairId = pair.PairId, PairEpoch = pair.Epoch,
				OperationId = "body-op", AuthorityId = "authority", SubjectObjectId = "subject",
				SubjectGeneId = "gene", ProcedureKey = "annexe-enrolment", BodyPartId = 0,
				BearerId = "", WaterCost = 180,
				BitCost = new KingdomMaterialDebitCost().ToClaimString(), PreservedCost = 0
			};
			string receipt = KingdomPurposeBodyAuthorityRules.Encode(authority);
			Assert.IsNotNull(receipt);
			Assert.IsTrue(KingdomPurposeBodyAuthorityRules.TryDecode(receipt, out var decoded));
			Assert.AreEqual(receipt, KingdomPurposeBodyAuthorityRules.Encode(decoded));
			Assert.IsFalse(KingdomPurposeBodyAuthorityRules.TryDecode(receipt + "x", out _));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "body-op", 1,
				KingdomPurposeKind.Chrome, true, false, null, null, "annexe-enrolment", receipt,
				null, out _, out fault), fault.ToString());
			Assert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperation(pair, "body-op", 1,
				KingdomPurposeKind.Chrome, true, false, null, null, "annexe-enrolment",
				"portfolio-production", null, out _, out _));
		}

		private static KingdomPurposePairReceipt Pair()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("pair", "realm", 7,
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, "city-a", "city-b", "work-a",
				null,
				"zone-a", "zone-b", "input-a", "output-a", "input-b", "output-b",
				"gate-a", "gate-b", D, out var pair, out _));
			return pair;
		}
	}
}
#endif
