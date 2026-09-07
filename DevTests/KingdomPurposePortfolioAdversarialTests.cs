#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoIsProtected(
				new KingdomPurposeCargoEvidence()));
			for (int i = 0; i < fields.Length; i++)
				ClassicAssert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoIsProtected(fields[i]),
					"owned cargo field " + i + " must protect on presence alone");
		}

		[Test]
		public void PurposeCargoOwnedFieldsRejectMissingWrongAndDualTypes()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				true, false, true));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				false, false, true));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				false, true, true));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				true, true, true));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				false, true, false));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				false, false, false));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				true, false, false));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
				true, true, false));
		}

		[Test]
		public void WireEnumsAppendWithoutRenumberingBodyPurposes()
		{
			ClassicAssert.AreEqual(1, (byte)KingdomPurposeKind.Flesh);
			ClassicAssert.AreEqual(2, (byte)KingdomPurposeKind.Chrome);
			ClassicAssert.AreEqual(3, (byte)KingdomPurposeKind.Deep);
			ClassicAssert.AreEqual(4, (byte)KingdomPurposeKind.Forge);
			ClassicAssert.AreEqual(5, (byte)KingdomPurposeKind.Harvest);
			ClassicAssert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(
				typeof(KingdomPurposePairPhase)));
			ClassicAssert.AreEqual(11, (byte)KingdomPurposePairPhase.Quarantined);
			ClassicAssert.AreEqual(12, (byte)KingdomPurposeOperationPhase.Quarantined);
			ClassicAssert.AreEqual(13, (byte)KingdomPurposeOperationPhase.PickupComplete);
			ClassicAssert.AreEqual(14, (byte)KingdomPurposeOperationPhase.LandingPending);
		}

		[Test]
		public void NestedOperationIsPairEpochAndEndpointBound()
		{
			KingdomPurposePairReceipt frozen = Pair();
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(frozen, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out var fault), fault.ToString());
			string encoded = KingdomPurposePortfolioRules.EncodeOperation(operation);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryDecodeOperation(encoded, out var decoded));
			ClassicAssert.AreEqual(encoded, KingdomPurposePortfolioRules.EncodeOperation(decoded));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryDecodeOperation(encoded + "x", out _));

			KingdomPurposePairReceipt running = frozen.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = operation;
			running.NextOperationOrdinal++;
			running.Revision++;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, running, out fault),
				fault.ToString());
			KingdomPurposePairReceipt crossed = running.Copy();
			crossed.Operation.PairEpoch++;
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidPair(crossed, out fault));
			ClassicAssert.AreEqual(KingdomPurposePairFault.Identity, fault);
			crossed = running.Copy();
			crossed.Operation.SourceOutputStoreId = "another-store";
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidPair(crossed, out fault));
			ClassicAssert.AreEqual(KingdomPurposePairFault.Identity, fault);
		}

		[Test]
		public void EvidenceCannotAppearBeforeItsCallbackPhase()
		{
			KingdomPurposePairReceipt pair = Pair();
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperation(pair, "skipped", 2,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out _, out _));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out _));
			operation.EffectBeforeDigest = D;
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out _));
			operation.EffectBeforeDigest = null;
			operation.TransportJobId = "early-job";
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out _));
			operation.TransportJobId = null;
			operation.Phase = KingdomPurposeOperationPhase.InputDebitPending;
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out _),
				"an exempt operation has no partner-input callback");
			operation.Phase = KingdomPurposeOperationPhase.Acknowledged;
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out _),
				"pair credit consumption, not a free-standing operation phase, acknowledges delivery");
		}

		[Test]
		public void OutputCargoIsExactAndRouteTamperRejecting()
		{
			KingdomPurposePairReceipt pair = Pair();
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "operation", 1,
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
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreateCargo(pair, operation,
				"cargo", "job", out var cargo, out var fault), fault.ToString());
			string cargoReceipt = KingdomPurposePortfolioRules.EncodeCargo(cargo);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryDecodeCargo(cargoReceipt, out var copy));
			ClassicAssert.AreEqual(cargoReceipt, KingdomPurposePortfolioRules.EncodeCargo(copy));
			operation.OutputCargoId = cargo.ObjectId;
			operation.OutputCargoReceipt = cargoReceipt;
			operation.TransportJobId = cargo.TransportJobId;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidOperation(operation, out fault),
				fault.ToString());
			cargo.RouteDigest = E;
			operation.OutputCargoReceipt = KingdomPurposePortfolioRules.EncodeCargo(cargo);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidOperation(operation, out fault));
			ClassicAssert.AreEqual(KingdomPurposePairFault.Identity, fault);
		}

		[Test]
		public void OrphanResumeDissolveAndQuarantineAreOneWay()
		{
			KingdomPurposePairReceipt frozen = Pair();
			KingdomPurposePairReceipt orphan = frozen.Copy();
			orphan.Phase = KingdomPurposePairPhase.Orphaned;
			orphan.ResumePhase = KingdomPurposePairPhase.Frozen;
			orphan.Revision++;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, orphan, out _));
			KingdomPurposePairReceipt resumed = orphan.Copy();
			resumed.Phase = KingdomPurposePairPhase.Frozen;
			resumed.ResumePhase = KingdomPurposePairPhase.Invalid;
			resumed.Revision++;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(orphan, resumed, out _));

			KingdomPurposePairReceipt quarantine = frozen.Copy();
			quarantine.Phase = KingdomPurposePairPhase.Quarantined;
			quarantine.Fault = "physical callback observed a third state";
			quarantine.Revision++;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, quarantine, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidTransition(quarantine, resumed, out _));

			KingdomPurposePairReceipt dormant = frozen.Copy();
			dormant.Phase = KingdomPurposePairPhase.Dormant;
			dormant.Revision++;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, dormant, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperation(dormant, "late", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out _, out _));
			frozen.Fault = "fault outside quarantine";
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidPair(frozen, out _));
		}

		[Test]
		public void ExactTopologyOrphansOnceAndRejoinResumesSameEpoch()
		{
			KingdomPurposePairReceipt frozen = Pair();
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(frozen,
				new string[] { "city-a" }, out var orphan, out var fault), fault.ToString());
			ClassicAssert.AreEqual(KingdomPurposePairPhase.Orphaned, orphan.Phase);
			ClassicAssert.AreEqual(KingdomPurposePairPhase.Frozen, orphan.ResumePhase);
			ClassicAssert.AreEqual(frozen.Epoch, orphan.Epoch);
			ClassicAssert.AreEqual(frozen.Revision + 1, orphan.Revision);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(orphan,
				new string[] { "city-a" }, out var unchanged, out fault), fault.ToString());
			ClassicAssert.AreEqual(orphan.Revision, unchanged.Revision);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(orphan,
				new string[] { "city-c", "city-b", "city-a" }, out var resumed, out fault),
				fault.ToString());
			ClassicAssert.AreEqual(KingdomPurposePairPhase.Frozen, resumed.Phase);
			ClassicAssert.AreEqual(KingdomPurposePairPhase.Invalid, resumed.ResumePhase);
			ClassicAssert.AreEqual(orphan.Epoch, resumed.Epoch);
			ClassicAssert.AreEqual(orphan.Revision + 1, resumed.Revision,
				"an unrelated third city must not alter the paired epoch");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReconcileTopology(resumed,
				new string[] { "city-a", "city-a" }, out _, out _));
		}

		[Test]
		public void ExhaustedCountersRemainReadableButRefuseEveryIncrement()
		{
			KingdomPurposePairReceipt exhausted = Pair();
			exhausted.Revision = int.MaxValue;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(exhausted,
				new string[] { "city-a", "city-b" }, out var unchanged, out var fault),
				fault.ToString());
			ClassicAssert.AreEqual(int.MaxValue, unchanged.Revision,
				"a no-op topology read does not need counter headroom");
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReconcileTopology(exhausted,
				new string[] { "city-a" }, out var refused, out fault));
			ClassicAssert.IsNull(refused);
			ClassicAssert.AreEqual(KingdomPurposePairFault.Bounds, fault);

			KingdomPurposePairReceipt attempted = exhausted.Copy();
			attempted.Phase = KingdomPurposePairPhase.Dormant;
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidTransition(
				exhausted, attempted, out _));

			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(Pair(), "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out fault), fault.ToString());
			operation.Revision = int.MaxValue;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidOperation(operation, out fault),
				fault.ToString());
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidOperationTransition(
				operation, operation.Copy()));
		}

		[Test]
		public void NewOperationReservesCompletionAndCreditHeadroom()
		{
			ClassicAssert.AreEqual(79, KingdomPurposePortfolioRules.MaxOrdinaryOperationAdvances);
			ClassicAssert.AreEqual(77, KingdomPurposePortfolioRules.MaxExemptOperationAdvances);
			ClassicAssert.AreEqual(1, KingdomPurposePortfolioRules.TerminalPairRevisionReserve);
			ClassicAssert.AreEqual(82, KingdomPurposePortfolioRules.NormalOperationAdmissionHeadroom);
			ClassicAssert.AreEqual(160, KingdomPurposePortfolioRules.ReturnOperationAdmissionHeadroom);
			ClassicAssert.AreEqual(238, KingdomPurposePortfolioRules.BootstrapOperationAdmissionHeadroom);
			AssertBoundary(KingdomPurposePairPhase.Frozen, 238);
			AssertBoundary(KingdomPurposePairPhase.SecondPending, 160);
			AssertBoundary(KingdomPurposePairPhase.Active, 82);
			AssertBoundary(KingdomPurposePairPhase.CargoAwaitingActivation, 82);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.CanStartOperationAtRevision(
				-1, KingdomPurposePairPhase.Frozen));

			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.BootstrapOutstanding,
				KingdomPurposePairPhase.Invalid, 0, out int bootstrap));
			ClassicAssert.AreEqual(237, bootstrap);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.BootstrapOutstanding,
				KingdomPurposePairPhase.Invalid, 77, out int secondPending));
			ClassicAssert.AreEqual(160, secondPending);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.ReturnOutstanding,
				KingdomPurposePairPhase.Invalid, 0, out int returned));
			ClassicAssert.AreEqual(159, returned);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.OperationOutstanding,
				KingdomPurposePairPhase.Invalid, 0, out int ordinary));
			ClassicAssert.AreEqual(81, ordinary);
		}

		private static void AssertBoundary(KingdomPurposePairPhase phase, int required)
		{
			int last = int.MaxValue - required;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.CanStartOperationAtRevision(last, phase));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.CanStartOperationAtRevision(last + 1,
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
			ClassicAssert.AreEqual(int.MaxValue, revision);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(
				KingdomPurposePairPhase.Dormant, KingdomPurposePairPhase.Invalid, 0,
				out int terminal));
			ClassicAssert.AreEqual(0, terminal);
		}

		private static void AssertExactHeadroom(int revision, KingdomPurposePairPhase phase,
			int operationRevision, int expected)
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRequiredPairRevisionHeadroom(phase,
				KingdomPurposePairPhase.Invalid, operationRevision, out int required));
			ClassicAssert.AreEqual(expected, required);
			ClassicAssert.AreEqual(required, int.MaxValue - revision);
		}

		[Test]
		public void OrphanedPairAdvancesOnlyItsAlreadyCommittedOperation()
		{
			KingdomPurposePairReceipt frozen = Pair();
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(frozen, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out var fault), fault.ToString());
			KingdomPurposePairReceipt running = frozen.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = operation;
			running.NextOperationOrdinal++;
			running.Revision++;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(frozen, running, out fault),
				fault.ToString());
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReconcileTopology(running,
				new string[] { "city-a" }, out var orphan, out fault), fault.ToString());
			KingdomPurposePairReceipt advanced = orphan.Copy();
			advanced.Operation.Phase = KingdomPurposeOperationPhase.LocalDebitPending;
			advanced.Operation.LocalDebitReceipt =
				KingdomPurposePortfolioTestData.LocalDebit(advanced.Operation);
			advanced.Operation.Revision++;
			advanced.Revision++;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(orphan, advanced, out fault),
				fault.ToString());
			ClassicAssert.AreEqual(KingdomPurposePairPhase.Orphaned, advanced.Phase);
			ClassicAssert.AreEqual(KingdomPurposePairPhase.BootstrapOutstanding, advanced.ResumePhase);
			advanced.Operation.OperationId = "replacement-operation";
			advanced.Operation.Revision++;
			advanced.Revision++;
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.ValidTransition(orphan, advanced, out _));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperation(orphan, "new", 2,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out _, out _));
		}

		[Test]
		public void CatalogueCopiesCannotMutateFrozenRecipes()
		{
			var recipes = KingdomPurposePortfolioRules.AllRecipes();
			recipes[0].CargoKey = "tampered";
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryRecipe(KingdomPurposeKind.Deep,
				KingdomPurposeKind.Forge, out var exact));
			ClassicAssert.AreEqual("deep-ore-assay", exact.CargoKey);
			ClassicAssert.AreEqual(2, KingdomPurposePortfolioRules.Partners(
				KingdomPurposeKind.Deep).Count);
		}

		[Test]
		public void NewEpochMayFreezeTwoExistingShellsWithoutReusingCargo()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("new-pair", "realm", 8,
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, "city-a", "city-b", "work-a",
				"work-b", "zone-a", "zone-b", "input-a", "output-a", "input-b", "output-b",
				"gate-a", "gate-b", D, out var pair, out var fault), fault.ToString());
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "new-bootstrap", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out var operation, out fault), fault.ToString());
			ClassicAssert.AreEqual("work-b", operation.DestinationWorkId);
			KingdomPurposePairReceipt running = pair.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = operation;
			running.NextOperationOrdinal++;
			running.Revision++;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.ValidTransition(pair, running, out fault),
				fault.ToString());
		}

		[Test]
		public void BodyAuthorityIsCanonicalAndRequiredByBodyOperations()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("body-pair", "realm", 9,
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
			ClassicAssert.IsNotNull(receipt);
			ClassicAssert.IsTrue(KingdomPurposeBodyAuthorityRules.TryDecode(receipt, out var decoded));
			ClassicAssert.AreEqual(receipt, KingdomPurposeBodyAuthorityRules.Encode(decoded));
			ClassicAssert.IsFalse(KingdomPurposeBodyAuthorityRules.TryDecode(receipt + "x", out _));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "body-op", 1,
				KingdomPurposeKind.Chrome, true, false, null, null, "annexe-enrolment", receipt,
				null, out _, out fault), fault.ToString());
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryCreateOperation(pair, "body-op", 1,
				KingdomPurposeKind.Chrome, true, false, null, null, "annexe-enrolment",
				"portfolio-production", null, out _, out _));
		}

		private static KingdomPurposePairReceipt Pair()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("pair", "realm", 7,
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, "city-a", "city-b", "work-a",
				null,
				"zone-a", "zone-b", "input-a", "output-a", "input-b", "output-b",
				"gate-a", "gate-b", D, out var pair, out _));
			return pair;
		}
	}
}
#endif
