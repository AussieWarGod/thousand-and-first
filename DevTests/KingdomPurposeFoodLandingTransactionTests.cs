#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPurposeFoodLandingTransactionTests
	{
		[Test]
		public void CreditCleanupThenPairCasRefusedRetryConverges()
		{
			KingdomPurposeLandingTransactionState before = Retirement(
				KingdomPurposePairPhase.CargoAwaitingConsumption,
				KingdomPurposeLandingCargoRecordShape.WholeCurrent, 0);
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveLandingRetirement(before, false,
					out KingdomPurposeLandingTransactionState waiting));
			Assert.AreEqual(before.PairPhase, waiting.PairPhase);
			Assert.AreEqual(before.PairRevision, waiting.PairRevision);
			Assert.IsFalse(waiting.RootPresent);
			Assert.AreEqual(0, waiting.ExactServingMarks);
			Assert.AreEqual(KingdomPurposeLandingCargoRecordShape.CleanLegacy,
				waiting.CargoRecord);
			Assert.AreEqual(KingdomPurposeLandingCleanupStep.RootRetired, waiting.Cleanup);
			Assert.IsFalse(KingdomPurposePortfolioRules.RetiredCargoIsReleased(waiting));

			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveLandingRetirement(waiting, true,
					out KingdomPurposeLandingTransactionState published));
			Assert.AreEqual(KingdomPurposePairPhase.Active, published.PairPhase);
			Assert.AreEqual(KingdomPurposeOperationPhase.Invalid, published.OperationPhase);
			Assert.AreEqual(before.PairRevision + 1, published.PairRevision);
			Assert.IsTrue(KingdomPurposePortfolioRules.RetiredCargoIsReleased(published));
		}

		[Test]
		public void BootstrapCleanupThenReturnCasRefusedRetryConverges()
		{
			KingdomPurposeLandingTransactionState before = Retirement(
				KingdomPurposePairPhase.SecondPending,
				KingdomPurposeLandingCargoRecordShape.CleanLegacy, 6);
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveLandingRetirement(before, false,
					out KingdomPurposeLandingTransactionState waiting));
			Assert.AreEqual(KingdomPurposePairPhase.SecondPending, waiting.PairPhase);
			Assert.AreEqual(KingdomPurposeLandingCleanupStep.RootRetired, waiting.Cleanup);
			Assert.IsFalse(KingdomPurposePortfolioRules.RetiredCargoIsReleased(waiting));

			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveLandingRetirement(waiting, true,
					out KingdomPurposeLandingTransactionState published));
			Assert.AreEqual(KingdomPurposePairPhase.ReturnOutstanding, published.PairPhase);
			Assert.AreEqual(KingdomPurposeOperationPhase.Prepared, published.OperationPhase);
			Assert.AreEqual(before.PairRevision + 1, published.PairRevision);
			Assert.IsTrue(KingdomPurposePortfolioRules.RetiredCargoIsReleased(published));
		}

		[Test]
		public void FailedCleanupCasCannotExposeProvisionToAnotherOperation()
		{
			KingdomPurposeLandingTransactionState before = Retirement(
				KingdomPurposePairPhase.CargoAwaitingConsumption,
				KingdomPurposeLandingCargoRecordShape.WholeCurrent, 4);
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveLandingRetirement(before, false,
					out KingdomPurposeLandingTransactionState waiting));
			Assert.IsFalse(KingdomPurposePortfolioRules.RetiredCargoIsReleased(waiting));
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.Refused,
				KingdomPurposePortfolioRules.DriveCompetingOperationAdmission(waiting, true,
					out KingdomPurposeLandingTransactionState refused));
			AssertSame(waiting, refused);

			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveLandingRetirement(waiting, true,
					out KingdomPurposeLandingTransactionState released));
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.OperationAdmitted,
				KingdomPurposePortfolioRules.DriveCompetingOperationAdmission(released, true,
					out KingdomPurposeLandingTransactionState admitted));
			Assert.AreEqual(KingdomPurposePairPhase.OperationOutstanding, admitted.PairPhase);
			Assert.AreEqual(released.NextOperationOrdinal + 1, admitted.NextOperationOrdinal);
		}

		[Test]
		public void WholeCurrentCargoRecordCanRetireAndCredit()
		{
			KingdomPurposeLandingTransactionState before = Retirement(
				KingdomPurposePairPhase.CargoAwaitingConsumption,
				KingdomPurposeLandingCargoRecordShape.WholeCurrent, 3);
			AssertPublishedRetirement(before, KingdomPurposePairPhase.Active);
		}

		[Test]
		public void CleanLegacyCargoCanRetireAndCredit()
		{
			KingdomPurposeLandingTransactionState before = Retirement(
				KingdomPurposePairPhase.CargoAwaitingConsumption,
				KingdomPurposeLandingCargoRecordShape.CleanLegacy, 3);
			AssertPublishedRetirement(before, KingdomPurposePairPhase.Active);
		}

		[Test]
		public void PartialCargoRecordLeavesExactServingMarksAndRoot()
		{
			KingdomPurposeLandingTransactionState before = Retirement(
				KingdomPurposePairPhase.CargoAwaitingConsumption,
				KingdomPurposeLandingCargoRecordShape.PartialCurrent, 3);
			AssertRetirementRefusesWithoutMutation(before);
		}

		[Test]
		public void MalformedCoResidentEvidenceLeavesExactServingMarksAndRoot()
		{
			KingdomPurposeLandingTransactionState before = Retirement(
				KingdomPurposePairPhase.SecondPending,
				KingdomPurposeLandingCargoRecordShape.WholeCurrent, 5);
			before.MalformedCoResidentEvidence = true;
			AssertRetirementRefusesWithoutMutation(before);
		}

		[Test]
		public void TornCargoRecordLeavesExactServingMarksRootAndPair()
		{
			KingdomPurposeLandingTransactionState before = Retirement(
				KingdomPurposePairPhase.CargoAwaitingActivation,
				KingdomPurposeLandingCargoRecordShape.TornOrForeign, 2);
			AssertRetirementRefusesWithoutMutation(before);
		}

		[Test]
		public void SettledAttemptClearedThenCustodyFailureCannotRetryFresh()
		{
			KingdomPurposeLandingTransactionState before = Landing();
			before.Attempt = KingdomPurposeLandingAttemptState.Settled;
			before.Custody = KingdomPurposeLandingCustodyProof.NullNestedInventoryIndex;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.EntryProved,
				KingdomPurposePortfolioRules.DriveLandingEntryReconciliation(before,
					out KingdomPurposeLandingTransactionState entered));
			Assert.AreEqual(KingdomPurposeLandingAttemptState.Clear, entered.Attempt);
			Assert.AreEqual(KingdomPurposeLandingCleanupStep.AttemptRetired, entered.Cleanup);
			Assert.IsFalse(KingdomPurposePortfolioRules.LandingCanOfferFresh(entered));

			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(entered, false,
					out KingdomPurposeLandingTransactionState faulted));
			Assert.IsTrue(faulted.FaultPresent);
			Assert.AreEqual(KingdomPurposeLandingAttemptState.Clear, faulted.Attempt);
			Assert.AreEqual(KingdomPurposeLandingCleanupStep.AttemptRetired, faulted.Cleanup);
			Assert.IsFalse(KingdomPurposePortfolioRules.LandingCanOfferFresh(faulted));

			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(faulted, false,
					out KingdomPurposeLandingTransactionState retry));
			Assert.IsTrue(retry.FaultPresent);
			Assert.IsFalse(KingdomPurposePortfolioRules.LandingCanOfferFresh(retry));
		}

		[Test]
		public void NullZoneRootIndexRefusesCustody()
		{
			AssertCustodyRefuses(KingdomPurposeLandingCustodyProof.NullZoneRootIndex);
		}

		[Test]
		public void NullNestedInventoryIndexRefusesCustody()
		{
			AssertCustodyRefuses(KingdomPurposeLandingCustodyProof.NullNestedInventoryIndex);
		}

		[Test]
		public void InvalidListedCustodyObjectRefuses()
		{
			AssertCustodyRefuses(KingdomPurposeLandingCustodyProof.InvalidListedObject);
		}

		[Test]
		public void FinalDestinationStoreMissingFromCellListStaysFaultedAfterRefusedCas()
		{
			KingdomPurposeLandingTransactionState before = Landing();
			before.StoreRack = KingdomPurposeLandingStoreRackProof.MissingFromCellList;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(before, false,
					out KingdomPurposeLandingTransactionState faulted));
			Assert.IsTrue(faulted.FaultPresent);
			Assert.AreEqual(before.PairPhase, faulted.PairPhase);
			Assert.AreEqual(before.PairRevision, faulted.PairRevision);
			Assert.AreEqual(before.ExactServingMarks, faulted.ExactServingMarks);
			Assert.IsTrue(faulted.RootPresent);

			faulted.StoreRack = KingdomPurposeLandingStoreRackProof.Exact;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(faulted, false,
					out KingdomPurposeLandingTransactionState retry));
			Assert.IsTrue(retry.FaultPresent);
			Assert.IsFalse(KingdomPurposePortfolioRules.LandingCanOfferFresh(retry));
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.Quarantined,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(retry, true,
					out KingdomPurposeLandingTransactionState quarantined));
			Assert.AreEqual(KingdomPurposePairPhase.Quarantined, quarantined.PairPhase);
		}

		[Test]
		public void CallbackRosterMutationStaysFaultedAfterRefusedQuarantineCas()
		{
			KingdomPurposeLandingTransactionState before = Landing();
			before.MeasuredRosterExact = false;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(before, false,
					out KingdomPurposeLandingTransactionState faulted));
			Assert.IsTrue(faulted.FaultPresent);
			Assert.AreEqual(before.ExactServingMarks, faulted.ExactServingMarks);
			faulted.MeasuredRosterExact = true;
			Assert.IsFalse(KingdomPurposePortfolioRules.LandingCanOfferFresh(faulted));
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(faulted, false,
					out KingdomPurposeLandingTransactionState retry));
			Assert.IsTrue(retry.FaultPresent);
		}

		[Test]
		public void FinalCleanupThenDeliveredCasRefusedRetryConverges()
		{
			KingdomPurposeLandingTransactionState before = Landing();
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(before, false,
					out KingdomPurposeLandingTransactionState waiting));
			Assert.AreEqual(KingdomPurposeOperationPhase.LandingPending,
				waiting.OperationPhase);
			Assert.AreEqual(KingdomPurposeLandingCleanupStep.MarksRetired, waiting.Cleanup);
			Assert.AreEqual(0, waiting.ExactServingMarks);
			Assert.IsFalse(waiting.FaultPresent);

			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(waiting, true,
					out KingdomPurposeLandingTransactionState delivered));
			Assert.AreEqual(KingdomPurposePairPhase.CargoAwaitingConsumption,
				delivered.PairPhase);
			Assert.AreEqual(KingdomPurposeOperationPhase.Delivered, delivered.OperationPhase);
			Assert.AreEqual(before.PairRevision + 1, delivered.PairRevision);
			Assert.AreEqual(before.OperationRevision + 1, delivered.OperationRevision);
		}

		[Test]
		public void FinalDeliveryThenCreditRetirementComposesWithoutStateReset()
		{
			KingdomPurposeLandingTransactionState landing = Landing();
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(landing, true,
					out KingdomPurposeLandingTransactionState delivered));
			Assert.AreEqual(KingdomPurposeLandingCleanupStep.PairPublished, delivered.Cleanup);
			Assert.IsTrue(delivered.RootPresent);
			Assert.AreEqual(KingdomPurposeLandingCargoRecordShape.WholeCurrent,
				delivered.CargoRecord);
			Assert.IsFalse(KingdomPurposePortfolioRules.RetiredCargoIsReleased(delivered));

			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveLandingRetirement(delivered, true,
					out KingdomPurposeLandingTransactionState credited));
			Assert.AreEqual(KingdomPurposePairPhase.Active, credited.PairPhase);
			Assert.AreEqual(0, credited.OperationRevision);
			Assert.IsFalse(credited.RootPresent);
			Assert.AreEqual(KingdomPurposeLandingCargoRecordShape.CleanLegacy,
				credited.CargoRecord);
			Assert.IsTrue(KingdomPurposePortfolioRules.RetiredCargoIsReleased(credited));
		}

		[Test]
		public void FinalBootstrapDeliveryThenReturnRetirementComposesWithoutStateReset()
		{
			KingdomPurposeLandingTransactionState landing = Landing();
			landing.PairPhase = KingdomPurposePairPhase.BootstrapOutstanding;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(landing, true,
					out KingdomPurposeLandingTransactionState delivered));
			Assert.AreEqual(KingdomPurposePairPhase.SecondPending, delivered.PairPhase);
			Assert.IsFalse(KingdomPurposePortfolioRules.RetiredCargoIsReleased(delivered));

			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveLandingRetirement(delivered, true,
					out KingdomPurposeLandingTransactionState returned));
			Assert.AreEqual(KingdomPurposePairPhase.ReturnOutstanding, returned.PairPhase);
			Assert.AreEqual(KingdomPurposeOperationPhase.Prepared, returned.OperationPhase);
			Assert.AreEqual(delivered.NextOperationOrdinal + 1, returned.NextOperationOrdinal);
			Assert.AreEqual(0, returned.OperationRevision);
			Assert.IsTrue(KingdomPurposePortfolioRules.RetiredCargoIsReleased(returned));
		}

		[Test]
		public void SettledAttemptEntryReconciliationComposesWithFinalCheckpoint()
		{
			KingdomPurposeLandingTransactionState before = Landing();
			before.Attempt = KingdomPurposeLandingAttemptState.Settled;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.EntryProved,
				KingdomPurposePortfolioRules.DriveLandingEntryReconciliation(before,
					out KingdomPurposeLandingTransactionState entered));
			Assert.AreEqual(KingdomPurposeLandingAttemptState.Clear, entered.Attempt);
			Assert.AreEqual(KingdomPurposeLandingCleanupStep.AttemptRetired, entered.Cleanup);
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(entered, false,
					out KingdomPurposeLandingTransactionState waiting));
			Assert.AreEqual(KingdomPurposeLandingAttemptState.Clear, waiting.Attempt);
			Assert.AreEqual(KingdomPurposeLandingCleanupStep.MarksRetired, waiting.Cleanup);
			Assert.IsFalse(waiting.FaultPresent);
		}

		[Test]
		public void ExhaustedCountersRefuseBeforeCleanupOrAdmission()
		{
			KingdomPurposeLandingTransactionState pair = Retirement(
				KingdomPurposePairPhase.CargoAwaitingConsumption,
				KingdomPurposeLandingCargoRecordShape.WholeCurrent, 4);
			pair.PairRevision = int.MaxValue;
			AssertRetirementRefusesWithoutMutation(pair);
			KingdomPurposeLandingTransactionState landingPair = Landing();
			landingPair.PairRevision = int.MaxValue;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.Refused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(landingPair, true,
					out KingdomPurposeLandingTransactionState landingPairRefused));
			AssertSame(landingPair, landingPairRefused);

			KingdomPurposeLandingTransactionState operation = Landing();
			operation.OperationRevision = int.MaxValue;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.Quarantined,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(operation, true,
					out KingdomPurposeLandingTransactionState operationQuarantined));
			Assert.AreEqual(KingdomPurposePairPhase.Quarantined,
				operationQuarantined.PairPhase);
			Assert.AreEqual(operation.PairRevision + 1, operationQuarantined.PairRevision);
			Assert.IsTrue(operationQuarantined.FaultPresent);
			Assert.AreEqual(operation.ExactServingMarks,
				operationQuarantined.ExactServingMarks);
			operation.Attempt = KingdomPurposeLandingAttemptState.Settled;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.Refused,
				KingdomPurposePortfolioRules.DriveLandingEntryReconciliation(operation,
					out KingdomPurposeLandingTransactionState entryRefused));
			AssertSame(operation, entryRefused);

			KingdomPurposeLandingTransactionState replacement = Retirement(
				KingdomPurposePairPhase.CargoAwaitingActivation,
				KingdomPurposeLandingCargoRecordShape.WholeCurrent, 4);
			replacement.NextOperationOrdinal = int.MaxValue;
			AssertRetirementRefusesWithoutMutation(replacement);
			KingdomPurposeLandingTransactionState returning = Retirement(
				KingdomPurposePairPhase.SecondPending,
				KingdomPurposeLandingCargoRecordShape.WholeCurrent, 4);
			returning.NextOperationOrdinal = int.MaxValue;
			AssertRetirementRefusesWithoutMutation(returning);

			KingdomPurposeLandingTransactionState lastCredit = Retirement(
				KingdomPurposePairPhase.CargoAwaitingConsumption,
				KingdomPurposeLandingCargoRecordShape.WholeCurrent, 4);
			lastCredit.NextOperationOrdinal = int.MaxValue;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveLandingRetirement(lastCredit, true,
					out KingdomPurposeLandingTransactionState released));
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.Refused,
				KingdomPurposePortfolioRules.DriveCompetingOperationAdmission(released, true,
					out KingdomPurposeLandingTransactionState admissionRefused));
			AssertSame(released, admissionRefused);
		}

		[Test]
		public void OrphanedCommittedFinalLandingAdvancesResumePhase()
		{
			AssertOrphanedLanding(KingdomPurposePairPhase.BootstrapOutstanding,
				KingdomPurposePairPhase.SecondPending);
			AssertOrphanedLanding(KingdomPurposePairPhase.ReturnOutstanding,
				KingdomPurposePairPhase.CargoAwaitingActivation);
			AssertOrphanedLanding(KingdomPurposePairPhase.OperationOutstanding,
				KingdomPurposePairPhase.CargoAwaitingConsumption);
		}

		[Test]
		public void RuntimeRetirementSequenceIsPinnedForExecutableComposition()
		{
			string drive = Source("Growth/KingdomPurposePortfolio.OperationDrive.cs");
			string retire = Between(drive,
				"private static bool TryRetireDeliveredPurposeLanding(", "\n\t\t}\n\t}\n}");
			Ordered(retire, "TryRootedPurposeCargoExact(Operation, out GameObject cargo)",
				"PurposeCargoRecordIsRetirable(cargo, receipt, carried)",
				"OnlyRetirableLandingEvidence(destination, allowed, receipt, prefilter)",
				"TryRetirePurposeLandingMarks(destination, receipt, prefilter)",
				"NoPurposeLandingEvidenceRemains(destination, allowed)",
				"TryClearPurposeLandingWitnesses(cargo, receipt, carried)",
				"NoPurposeLandingEvidenceRemains(destination, null)");
			string control = Source("Growth/KingdomPurposePortfolio.OperationControl.cs");
			string start = Between(control, "private static bool TryStartPortfolioOperation(",
				"private static bool TryPortfolioOperationPreflight(");
			Ordered(start,
				"if (!KingdomPurposePortfolioRules.CanStartOperationAtRevision(",
				"Pair.Revision, Pair.Phase)",
				"|| Pair.NextOperationOrdinal == int.MaxValue)",
				"next.NextOperationOrdinal++", "ExactPublishedPortfolioPair(Pair)",
				"TryRetireCreditedPurposeCargo(Pair.Operation)",
				"TryPublishPortfolioPair(Pair, next, out Failure)");
			string credit = Between(drive, "private static bool AcceptPortfolioCredit(",
				"private static bool ExactPublishedPortfolioPair(");
			Ordered(credit, "if (!KingdomPurposePortfolioRules.PairRevisionHeadroomIsValid(Pair)",
				"(activation && (!KingdomPurposePortfolioRules.CanStartOperationAtRevision(",
				"Pair.Revision, Pair.Phase) || Pair.NextOperationOrdinal == int.MaxValue))",
				"activating.NextOperationOrdinal++", "TryRetireCreditedPurposeCargo(Pair.Operation)");
		}

		[Test]
		public void RuntimeFinalProofAndCustodyOrderIsPinnedForExecutableComposition()
		{
			string drive = Source("Growth/KingdomPurposePortfolio.OperationDrive.cs");
			string loop = Between(drive, "private static bool DrivePortfolioOperation(",
				"private static bool BeginLocalDebit(");
			Ordered(loop,
				"if (Published.Revision == int.MaxValue || operation.Revision == int.MaxValue)",
				"DrivePurposeLanding(System, before, out Published, out Failure)");
			string landing = Source("Growth/KingdomPurposePortfolio.LandingFood.cs");
			string entry = Between(landing, "private static bool TryLandCarriedFood(",
				"private static bool CompletePurposeLanding(");
			Ordered(entry, "TryPurposeCustodyStrays(larders, DestinationZone, Cargo",
				"TryClearPurposeLandingAttempt(Cargo, receipt, physical)",
				"RecordPurposeLanded(Cargo, receipt, carried, progress)",
				"CompletePurposeLanding(Operation");
			string complete = Between(landing, "private static bool CompletePurposeLanding(",
				"private static bool FaultedLanding(");
			Ordered(complete, "PurposeLandingStillExact(Operation, Cargo, out string moved)",
				"TryRetirePurposeLandingMarks(DestinationZone, Receipt, Prefilter)");
			string fault = Between(landing, "private static bool FaultedLanding(",
				"private static void NotePurposeProvisionArrival(");
			Ordered(fault, "Ambiguous = true;",
				"StampPurposeLandingFault(Cargo, Receipt, Expected, Observed)", "Fail(Reason");

			string ground = Source("Growth/KingdomPurposePortfolio.LandingGround.cs");
			StringAssert.Contains("if (roots == null) return false;", ground);
			StringAssert.Contains("if (item.Inventory.Objects == null) return false;", ground);
			StringAssert.Contains("if (!GameObject.Validate(roots[i])", ground);
			StringAssert.Contains("!Store.CurrentCell.Objects.Contains(Store)", ground);

			string output = Source("Growth/KingdomPurposePortfolio.OutputRuntime.cs");
			string delivered = Between(output, "private static bool DrivePurposeLanding(",
				"private static bool PurposeLandingStillExact(");
			Ordered(delivered, "next.Phase = KingdomPurposeOperationPhase.Delivered;",
				"next.Revision++;", "TryPublishOperation(Pair, next, delivered");
			string control = Source("Growth/KingdomPurposePortfolio.OperationControl.cs");
			string publish = Between(control, "private static bool TryPublishOperation(",
				"private static bool QuarantinePortfolio(");
			Ordered(publish, "Pair.Revision == int.MaxValue",
				"Pair.Operation.Revision == int.MaxValue", "next.Operation = Operation;",
				"next.Revision++;",
				"TryPublishPortfolioPair(Pair, next, out Failure)");
			string quarantine = Between(control, "private static bool QuarantinePortfolio(",
				"private static bool SameOperationEvidence(");
			Ordered(quarantine, "if (Pair.Revision == int.MaxValue)",
				"next.Revision++;", "TryPublishPortfolioPair(Pair, next, out Failure)");
		}

		private static KingdomPurposeLandingTransactionState Retirement(
			KingdomPurposePairPhase phase, KingdomPurposeLandingCargoRecordShape record,
			int marks)
		{
			return new KingdomPurposeLandingTransactionState
			{
				PairPhase = phase,
				OperationPhase = KingdomPurposeOperationPhase.Delivered,
				CargoRecord = record,
				Attempt = KingdomPurposeLandingAttemptState.Clear,
				EntryCustody = KingdomPurposeLandingCustodyProof.Complete,
				Custody = KingdomPurposeLandingCustodyProof.Complete,
				StoreRack = KingdomPurposeLandingStoreRackProof.Exact,
				Cleanup = KingdomPurposeLandingCleanupStep.None,
				PairRevision = 17,
				OperationRevision = 12,
				NextOperationOrdinal = phase == KingdomPurposePairPhase.SecondPending
					? 2 : phase == KingdomPurposePairPhase.CargoAwaitingActivation ? 3 : 4,
				Carried = 6,
				ExactServingMarks = marks,
				RootPresent = true,
				MeasuredRosterExact = true
			};
		}

		private static KingdomPurposeLandingTransactionState Landing()
		{
			return new KingdomPurposeLandingTransactionState
			{
				PairPhase = KingdomPurposePairPhase.OperationOutstanding,
				ResumePhase = KingdomPurposePairPhase.Invalid,
				OperationPhase = KingdomPurposeOperationPhase.LandingPending,
				CargoRecord = KingdomPurposeLandingCargoRecordShape.WholeCurrent,
				Attempt = KingdomPurposeLandingAttemptState.Clear,
				EntryCustody = KingdomPurposeLandingCustodyProof.Complete,
				Custody = KingdomPurposeLandingCustodyProof.Complete,
				StoreRack = KingdomPurposeLandingStoreRackProof.Exact,
				Cleanup = KingdomPurposeLandingCleanupStep.None,
				PairRevision = 29,
				OperationRevision = 23,
				NextOperationOrdinal = 4,
				Carried = 6,
				ExactServingMarks = 6,
				RootPresent = true,
				MeasuredRosterExact = true
			};
		}

		private static void AssertPublishedRetirement(
			KingdomPurposeLandingTransactionState before, KingdomPurposePairPhase target)
		{
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveLandingRetirement(before, true,
					out KingdomPurposeLandingTransactionState after));
			Assert.AreEqual(target, after.PairPhase);
			Assert.AreEqual(before.PairRevision + 1, after.PairRevision);
			Assert.AreEqual(0, after.OperationRevision);
			Assert.AreEqual(0, after.ExactServingMarks);
			Assert.IsFalse(after.RootPresent);
			Assert.AreEqual(KingdomPurposeLandingCargoRecordShape.CleanLegacy,
				after.CargoRecord);
			Assert.IsTrue(KingdomPurposePortfolioRules.RetiredCargoIsReleased(after));
		}

		private static void AssertRetirementRefusesWithoutMutation(
			KingdomPurposeLandingTransactionState before)
		{
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.Refused,
				KingdomPurposePortfolioRules.DriveLandingRetirement(before, true,
					out KingdomPurposeLandingTransactionState after));
			AssertSame(before, after);
			Assert.IsTrue(after.RootPresent);
			Assert.AreEqual(before.ExactServingMarks, after.ExactServingMarks);
			Assert.IsFalse(KingdomPurposePortfolioRules.RetiredCargoIsReleased(after));
		}

		private static void AssertCustodyRefuses(KingdomPurposeLandingCustodyProof proof)
		{
			Assert.IsFalse(KingdomPurposePortfolioRules.LandingCustodyIsComplete(proof));
			KingdomPurposeLandingTransactionState before = Landing();
			before.Custody = proof;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.SemanticCasRefused,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(before, false,
					out KingdomPurposeLandingTransactionState after));
			Assert.IsTrue(after.FaultPresent);
			Assert.AreEqual(before.ExactServingMarks, after.ExactServingMarks);
			Assert.IsTrue(after.RootPresent);
			Assert.AreEqual(before.PairPhase, after.PairPhase);
		}

		private static void AssertSame(KingdomPurposeLandingTransactionState expected,
			KingdomPurposeLandingTransactionState actual)
		{
			Assert.AreEqual(expected.PairPhase, actual.PairPhase);
			Assert.AreEqual(expected.ResumePhase, actual.ResumePhase);
			Assert.AreEqual(expected.OperationPhase, actual.OperationPhase);
			Assert.AreEqual(expected.CargoRecord, actual.CargoRecord);
			Assert.AreEqual(expected.Attempt, actual.Attempt);
			Assert.AreEqual(expected.EntryCustody, actual.EntryCustody);
			Assert.AreEqual(expected.Custody, actual.Custody);
			Assert.AreEqual(expected.StoreRack, actual.StoreRack);
			Assert.AreEqual(expected.Cleanup, actual.Cleanup);
			Assert.AreEqual(expected.PairRevision, actual.PairRevision);
			Assert.AreEqual(expected.OperationRevision, actual.OperationRevision);
			Assert.AreEqual(expected.NextOperationOrdinal, actual.NextOperationOrdinal);
			Assert.AreEqual(expected.Carried, actual.Carried);
			Assert.AreEqual(expected.ExactServingMarks, actual.ExactServingMarks);
			Assert.AreEqual(expected.RootPresent, actual.RootPresent);
			Assert.AreEqual(expected.MeasuredRosterExact, actual.MeasuredRosterExact);
			Assert.AreEqual(expected.MalformedCoResidentEvidence,
				actual.MalformedCoResidentEvidence);
			Assert.AreEqual(expected.FaultPresent, actual.FaultPresent);
		}

		private static void AssertOrphanedLanding(KingdomPurposePairPhase resume,
			KingdomPurposePairPhase expectedResume)
		{
			KingdomPurposeLandingTransactionState before = Landing();
			before.PairPhase = KingdomPurposePairPhase.Orphaned;
			before.ResumePhase = resume;
			Assert.AreEqual(KingdomPurposeLandingTransactionVerdict.PairPublished,
				KingdomPurposePortfolioRules.DriveFinalLandingCheckpoint(before, true,
					out KingdomPurposeLandingTransactionState after));
			Assert.AreEqual(KingdomPurposePairPhase.Orphaned, after.PairPhase);
			Assert.AreEqual(expectedResume, after.ResumePhase);
			Assert.AreEqual(before.PairRevision + 1, after.PairRevision);
			Assert.AreEqual(before.OperationRevision + 1, after.OperationRevision);
		}

		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative.Replace('/', Path.DirectorySeparatorChar));
		}

		private static string Between(string source, string from, string to)
		{
			int start = source.IndexOf(from, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, from);
			int end = source.IndexOf(to, start + from.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, to);
			return source.Substring(start, end - start);
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}
	}
}
#endif
