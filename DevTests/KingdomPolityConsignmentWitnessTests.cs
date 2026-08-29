#if TAF_TESTS && !TAF_CONSTRUCTION_INPUT_PORTABLE
using System;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityConsignmentWitnessTests
	{
		[Test]
		public void EveryContinuationPhaseRequiresOneExactFrozenRecipient()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			KingdomTradeOperation operation = Operation(out book, out request);
			KingdomTradePolityRecipientWitness exact = operation.PolityRecipient;
			byte[] before = KingdomTradeCodec.EncodeEnvelope(book);
			foreach (KingdomTradePhase phase in new[] { KingdomTradePhase.Prepared,
				KingdomTradePhase.ResourceIntent, KingdomTradePhase.ResourceSettled,
				KingdomTradePhase.ProjectionSettled, KingdomTradePhase.DomainIntent,
				KingdomTradePhase.DomainSettled, KingdomTradePhase.Sinks,
				KingdomTradePhase.ScheduleIntent })
			{
				operation.Phase = phase;
				Assert.IsTrue(KingdomTradeRules.TryValidatePolityConsignmentCheckpoint(
					operation, request, exact, 1, "Seat", out string failure), failure);
				Assert.IsFalse(KingdomTradeRules.TryValidatePolityConsignmentCheckpoint(
					operation, request, null, 0, "Seat", out failure));
				StringAssert.Contains("absent", failure);
				Assert.IsFalse(KingdomTradeRules.TryValidatePolityConsignmentCheckpoint(
					operation, request, null, 2, "Seat", out failure));
				StringAssert.Contains("ambiguous", failure);
			}
			operation.Phase = KingdomTradePhase.Prepared;
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodeEnvelope(book),
				"checkpoint refusal must not rewrite durable authority");
		}

		[Test]
		public void ReplacementBodyProjectionOrDigestCannotResume()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			KingdomTradeOperation operation = Operation(out book, out request);
			Assert.IsTrue(KingdomTradeRules.TryCreatePolityRecipientWitness(request,
				"taf:object:polity-cohort:v1:replacement",
				KingdomPolityConsignmentTests.RecipientProjectionId(),
				out KingdomTradePolityRecipientWitness replacement, out string failure), failure);
			Assert.IsFalse(KingdomTradeRules.TryValidatePolityConsignmentCheckpoint(operation,
				request, replacement, 1, "Seat", out failure));
			Assert.IsTrue(KingdomTradeRules.TryCreatePolityRecipientWitness(request,
				KingdomPolityConsignmentTests.RecipientBodyId(),
				"taf:projection:cohort:v1:replacement", out replacement, out failure), failure);
			Assert.IsFalse(KingdomTradeRules.TryValidatePolityConsignmentCheckpoint(operation,
				request, replacement, 1, "Seat", out failure));
			replacement = KingdomTradeRules.ClonePolityRecipientWitness(operation.PolityRecipient);
			replacement.WitnessDigest = KingdomPolityTestData.DigestB;
			Assert.IsFalse(KingdomTradeRules.TryValidatePolityConsignmentCheckpoint(operation,
				request, replacement, 1, "Seat", out failure));
		}

		[Test]
		public void CurrentCodecRoundTripsWitnessAndNormalizationRejectsTamper()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			Operation(out book, out request);
			byte[] encoded = KingdomTradeCodec.EncodeEnvelope(book);
			Assert.AreEqual(KingdomTradeCodec.CurrentWireVersion,
				BitConverter.ToInt32(encoded, 4));
			KingdomTradeBook decoded = KingdomTradeCodec.DecodeEnvelopeRaw(encoded);
			KingdomTradeRules.Normalize(decoded);
			Assert.IsTrue(KingdomTradeRules.BookUsable(decoded), decoded.SchemaFault);
			Assert.IsTrue(KingdomTradeRules.ExactPolityRecipientWitness(
				book.OpenOperation.PolityRecipient, decoded.OpenOperation.PolityRecipient));
			decoded.OpenOperation.PolityRecipient.BodyId =
				"taf:object:polity-cohort:v1:replacement";
			KingdomTradeRules.Normalize(decoded);
			Assert.AreEqual(KingdomTradePhase.Quarantined, decoded.OpenOperation.Phase);
		}

		[Test]
		public void WireV4ReaderPreservesNonConsignmentButStopsUnwitnessedConsignment()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			Operation(out book, out request);
			book.FormatVersion = 5;
			byte[] old = KingdomTradeCodec.EncodeEnvelopeV4Fixture(book);
			Assert.AreEqual(KingdomTradeCodec.ImmediatePriorWireVersion,
				BitConverter.ToInt32(old, 4));
			KingdomTradeBook migrated = KingdomTradeCodec.DecodeEnvelopeRaw(old);
			Assert.AreEqual(KingdomTradeRules.CurrentFormatVersion, migrated.FormatVersion);
			Assert.IsNull(migrated.OpenOperation.PolityRecipient);
			Assert.AreEqual(KingdomTradePhase.Quarantined, migrated.OpenOperation.Phase);
			StringAssert.Contains("lacks an exact recipient", migrated.OpenOperation.Fault);

			KingdomTradeBook ordinary = BoundBook();
			KingdomTradeOperation row = KingdomTradeRules.NewOperation(ordinary,
				KingdomTradeOperationKind.ManifestTurnback, 4L);
			Assert.IsNotNull(row);
			row.ZoneId = "zone"; row.SettlementId = request.SurfaceRef; row.SettlementName = "Seat";
			row.ManifestId = "taf:manifest:ordinary"; row.OriginId = row.DestinationId = request.SurfaceRef;
			row.OriginName = row.DestinationName = "Seat";
			ordinary.FormatVersion = 5;
			migrated = KingdomTradeCodec.DecodeEnvelopeRaw(
				KingdomTradeCodec.EncodeEnvelopeV4Fixture(ordinary));
			Assert.AreEqual(KingdomTradeRules.CurrentFormatVersion, migrated.FormatVersion);
			Assert.AreEqual(KingdomTradeOperationKind.ManifestTurnback,
				migrated.OpenOperation.Kind);
		}

		[Test]
		public void ProvedDebitRequiresRetentionWhenLandingWitnessIsLost()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			KingdomTradeOperation operation = Operation(out book, out request);
			operation.Phase = KingdomTradePhase.Quarantined;
			operation.ProvedWater = operation.RequestedWater;
			operation.WaterLegs.Add(new KingdomTradeWaterLeg { OwnerId = "taf:store:one",
				ZoneId = "zone", Capacity = 20, Before = 12, Delta = 8, After = 4,
				BeforeComposition = "water=1000", AfterComposition = "water=1000",
				State = KingdomTradePhysicalState.Proved });
			operation.Outbox = Skipped(operation.Id);
			Assert.IsTrue(KingdomTradeRules.HasUnresolvedEffects(operation));
			operation.RetainedBefore = 0L; operation.RetainedDelta = 8L;
			operation.RetainedAfter = 8L; operation.RetainedState = KingdomTradePhysicalState.Proved;
			Assert.IsFalse(KingdomTradeRules.HasUnresolvedEffects(operation));
		}

		[Test]
		public void RecipientLossSkipsOnlyNeverStartedLegsAndKeepsProvedValueOpen()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			KingdomTradeOperation operation = Operation(out book, out request);
			operation.Phase = KingdomTradePhase.ResourceIntent;
			operation.WaterLegs.Add(new KingdomTradeWaterLeg { OwnerId = "taf:store:proved",
				ZoneId = "zone", Capacity = 20, Before = 12, Delta = 3, After = 9,
				BeforeComposition = "water=1000", AfterComposition = "water=1000",
				State = KingdomTradePhysicalState.Proved });
			operation.WaterLegs.Add(new KingdomTradeWaterLeg { OwnerId = "taf:store:unstarted",
				ZoneId = "zone", Capacity = 20, Before = 12, Delta = 5, After = 7,
				BeforeComposition = "water=1000", AfterComposition = "water=1000",
				State = KingdomTradePhysicalState.Prepared });
			operation.ProvedWater = 3;
			KingdomTradeRules.SealUnstartedPolityConsignmentLegs(operation);
			Assert.AreEqual(KingdomTradePhysicalState.Proved, operation.WaterLegs[0].State);
			Assert.AreEqual(KingdomTradePhysicalState.Skipped, operation.WaterLegs[1].State);
			operation.Phase = KingdomTradePhase.Quarantined;
			operation.Outbox = Skipped(operation.Id);
			Assert.IsTrue(KingdomTradeRules.HasUnresolvedEffects(operation),
				"proved value must remain open until retained custody settles");
			operation.RetainedBefore = 0L; operation.RetainedDelta = 3L;
			operation.RetainedAfter = 3L;
			operation.RetainedState = KingdomTradePhysicalState.Proved;
			Assert.IsFalse(KingdomTradeRules.HasUnresolvedEffects(operation));
			book.RetainedEscrowDrams = 3L;
			operation.Fault = "exact recipient vanished after proved prefix";
			Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Quarantined, 90L, operation.Fault));
			Assert.IsNull(book.OpenOperation);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out KingdomTradePolityConsignmentReceipt receipt,
				out KingdomTradePolityConsignmentReceiptKind kind, out string failure), failure);
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.TerminalFailed, kind);
			Assert.AreEqual(3, receipt.RetainedDrams);
		}

		[Test]
		public void ZeroAvailabilityRetiresOneTerminalFailureWithoutCustodyLeak()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			KingdomTradeOperation operation = Operation(out book, out request);
			operation.Phase = KingdomTradePhase.Quarantined;
			operation.Fault = "no fresh water was available";
			operation.Outbox = Skipped(operation.Id);
			Assert.IsFalse(KingdomTradeRules.HasUnresolvedEffects(operation));
			Assert.IsTrue(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Quarantined, 91L, operation.Fault));
			Assert.IsNull(book.OpenOperation);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out KingdomTradePolityConsignmentReceipt receipt,
				out KingdomTradePolityConsignmentReceiptKind kind, out string failure), failure);
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.TerminalFailed, kind);
			Assert.AreEqual(0, receipt.DebitedDrams);
			Assert.AreEqual(0L, book.RetainedEscrowDrams);
		}

		[Test]
		public void ThirdIntentStateCannotRetireOrLoseItsVisibleCustodyQuestion()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			KingdomTradeOperation operation = Operation(out book, out request);
			operation.Phase = KingdomTradePhase.Quarantined;
			operation.Fault = "intent vessel is neither exact before nor after";
			operation.AmbiguousWater = 3;
			operation.WaterLegs.Add(new KingdomTradeWaterLeg
			{
				OwnerId = "taf:store:third", ZoneId = "zone", Capacity = 20,
				Before = 12, Delta = 3, After = 9, BeforeComposition = "water=1000",
				AfterComposition = "water=1000", State = KingdomTradePhysicalState.Lost
			});
			operation.Outbox = Skipped(operation.Id);
			Assert.IsTrue(KingdomTradeRules.HasUnresolvedEffects(operation));
			Assert.IsFalse(KingdomTradeRules.Retire(book, operation,
				KingdomTradePhase.Quarantined, 92L, operation.Fault));
			Assert.AreSame(operation, book.OpenOperation);
			Assert.AreEqual(3, operation.AmbiguousWater);
		}

		[Test]
		public void CallbackThrowCutRoundTripsAndClassifiesBeforeAfterOrThirdState()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			KingdomTradeOperation operation = Operation(out book, out request);
			operation.Phase = KingdomTradePhase.ResourceIntent;
			operation.WaterLegs.Add(new KingdomTradeWaterLeg
			{
				OwnerId = "taf:store:callback-cut", ZoneId = "zone", Capacity = 20,
				Before = 12, Delta = 3, After = 9, BeforeComposition = "water=1000",
				AfterComposition = "water=1000", State = KingdomTradePhysicalState.Intent
			});
			operation.WaterLegs.Add(new KingdomTradeWaterLeg
			{
				OwnerId = "taf:store:sealed-suffix", ZoneId = "zone", Capacity = 20,
				Before = 12, Delta = 5, After = 7, BeforeComposition = "water=1000",
				AfterComposition = "water=1000", State = KingdomTradePhysicalState.Skipped
			});
			Assert.IsTrue(KingdomTradeRules.ValidSkippedPolityWaterLeg(operation,
				operation.WaterLegs[1]));
			byte[] cut = KingdomTradeCodec.EncodeEnvelope(book);
			KingdomTradeBook beforeBook = KingdomTradeCodec.DecodeEnvelopeRaw(cut);
			KingdomTradeRules.Normalize(beforeBook);
			Assert.IsTrue(KingdomTradeRules.BookUsable(beforeBook), beforeBook.SchemaFault);
			KingdomTradeWaterLeg leg = beforeBook.OpenOperation.WaterLegs[0];
			Assert.AreEqual(KingdomTradePhysicalState.Intent, leg.State);
			Assert.AreEqual(KingdomTradeWaterIntentResolution.Before,
				KingdomTradeRules.ClassifyPolityWaterIntent(leg, 20, 12, "water=1000"));
			leg.State = KingdomTradePhysicalState.Prepared;
			KingdomTradeRules.SealUnstartedPolityConsignmentLegs(beforeBook.OpenOperation);
			Assert.AreEqual(KingdomTradePhysicalState.Skipped, leg.State);
			Assert.AreEqual(KingdomTradePhysicalState.Skipped,
				beforeBook.OpenOperation.WaterLegs[1].State);
			beforeBook.OpenOperation.Phase = KingdomTradePhase.Quarantined;
			beforeBook.OpenOperation.Fault = "recipient absent before exact debit";
			beforeBook.OpenOperation.Outbox = Skipped(beforeBook.OpenOperation.Id);
			Assert.IsTrue(KingdomTradeRules.Retire(beforeBook, beforeBook.OpenOperation,
				KingdomTradePhase.Quarantined, 80L, beforeBook.OpenOperation.Fault));
			Assert.IsNull(beforeBook.OpenOperation);

			KingdomTradeBook afterBook = KingdomTradeCodec.DecodeEnvelopeRaw(cut);
			KingdomTradeRules.Normalize(afterBook);
			leg = afterBook.OpenOperation.WaterLegs[0];
			int capacity = 20, volume = 12;
			const string composition = "water=1000";
			try
			{
				int changed = Math.Min(volume, 3); volume -= changed;
				Assert.AreEqual(3, changed);
				throw new InvalidOperationException("simulated callback after committed drain");
			}
			catch (InvalidOperationException) { }
			Assert.AreEqual(9, volume);
			Assert.AreEqual(KingdomTradeWaterIntentResolution.After,
				KingdomTradeRules.ClassifyPolityWaterIntent(leg, capacity, volume,
					composition));
			leg.State = KingdomTradePhysicalState.Proved;
			afterBook.OpenOperation.ProvedWater = 3;
			Assert.AreEqual(KingdomTradePhysicalState.Skipped,
				afterBook.OpenOperation.WaterLegs[1].State);
			afterBook.OpenOperation.Phase = KingdomTradePhase.Quarantined;
			afterBook.OpenOperation.Fault = "recipient absent after exact debit";
			afterBook.OpenOperation.RetainedDelta = 3L;
			afterBook.OpenOperation.RetainedAfter = 3L;
			afterBook.OpenOperation.RetainedState = KingdomTradePhysicalState.Proved;
			afterBook.RetainedEscrowDrams = 3L;
			afterBook.OpenOperation.Outbox = Skipped(afterBook.OpenOperation.Id);
			Assert.IsTrue(KingdomTradeRules.Retire(afterBook, afterBook.OpenOperation,
				KingdomTradePhase.Quarantined, 81L, afterBook.OpenOperation.Fault));
			Assert.IsNull(afterBook.OpenOperation);

			KingdomTradeBook thirdBook = KingdomTradeCodec.DecodeEnvelopeRaw(cut);
			KingdomTradeRules.Normalize(thirdBook);
			leg = thirdBook.OpenOperation.WaterLegs[0];
			Assert.AreEqual(KingdomTradeWaterIntentResolution.Ambiguous,
				KingdomTradeRules.ClassifyPolityWaterIntent(leg, 20, 10, "water=1000"));
			leg.State = KingdomTradePhysicalState.Lost;
			thirdBook.OpenOperation.AmbiguousWater = 3;
			Assert.AreEqual(KingdomTradePhysicalState.Skipped,
				thirdBook.OpenOperation.WaterLegs[1].State);
			thirdBook.OpenOperation.Phase = KingdomTradePhase.Quarantined;
			thirdBook.OpenOperation.Fault = "recipient absent with a third vessel state";
			thirdBook.OpenOperation.Outbox = Skipped(thirdBook.OpenOperation.Id);
			Assert.IsFalse(KingdomTradeRules.Retire(thirdBook, thirdBook.OpenOperation,
				KingdomTradePhase.Quarantined, 82L, thirdBook.OpenOperation.Fault));
			Assert.IsNotNull(thirdBook.OpenOperation);
		}

		[Test]
		public void InvalidPreparationLeavesSequenceProofCapacityAndBytesUntouched()
		{
			KingdomTradeBook book;
			KingdomPolityConsignmentRequest request;
			KingdomTradeOperation operation = Operation(out book, out request);
			book.OpenOperation = null;
			byte[] before = KingdomTradeCodec.EncodeEnvelope(book);
			Assert.IsFalse(KingdomTradeRules.TryValidatePolityConsignmentPreparation(book,
				request, "zone", "taf:settlement:v1:foreign", "Seat",
				operation.PolityRecipient, out string failure));
			StringAssert.Contains("authority", failure);
			CollectionAssert.AreEqual(before, KingdomTradeCodec.EncodeEnvelope(book));
		}

		[Test]
		public void UnacknowledgedConsignmentProofSurvivesMoreThanSixtyFourRetirements()
		{
			KingdomTradeBook source;
			KingdomPolityConsignmentRequest request;
			Operation(out source, out request);
			KingdomTradeBook receiptBook = KingdomPolityConsignmentTests.TradeBookForWitness(
				request, 4, KingdomTradePhase.Terminal);
			KingdomTradeProof pinned = receiptBook.RecentProofs[0];
			KingdomTradeBook book = BoundBook(); book.RecentProofs.Clear();
			book.RetiredThrough = 1L; book.NextOperationSequence = 2L;
			book.RecentProofs.Add(pinned);
			for (long sequence = 2L; sequence <= 130L; sequence++)
			{
				if (book.RecentProofs.Count >= KingdomTradeRules.MaxRecentProofs)
					Assert.IsTrue(KingdomTradeRules.EnsureRetirementCapacity(book));
				book.RecentProofs.Add(OrdinaryProof(sequence));
				book.RetiredThrough = sequence; book.NextOperationSequence = sequence + 1L;
			}
			int matches = 0;
			for (int i = 0; i < book.RecentProofs.Count; i++)
				if (book.RecentProofs[i].ManifestId == request.ConsignmentId) matches++;
			Assert.AreEqual(1, matches);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out KingdomTradePolityConsignmentReceipt receipt,
				out KingdomTradePolityConsignmentReceiptKind kind, out string failure), failure);
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.Landed, kind);
			Assert.AreEqual(4, receipt.DeliveredDrams);
		}

		[Test]
		public void ConsumedProofAcknowledgementCompactsOnceAcrossBothCrashCuts()
		{
			KingdomPolityLedger ledger = KingdomPolityConsignmentTests.Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out _, out string failure), failure);
			KingdomTradeBook book = KingdomPolityConsignmentTests.TradeBookForWitness(
				request, 4, KingdomTradePhase.Terminal);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out KingdomTradePolityConsignmentReceipt receipt,
				out KingdomTradePolityConsignmentReceiptKind kind, out failure), failure);
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.Landed, kind);
			byte[] tradeBeforeConclusion = KingdomTradeCodec.EncodeEnvelope(book);
			Assert.IsFalse(KingdomTradeRules.TryAcknowledgePolityConsignment(book, ledger,
				request, receipt, out _, out failure));
			CollectionAssert.AreEqual(tradeBeforeConclusion, KingdomTradeCodec.EncodeEnvelope(book));

			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				ledger.Revision, receipt, out _, out failure), failure);
			// Crash before acknowledgement: both owners reload, AlreadyApplied then exact ack.
			ledger = KingdomPolityCodec.DecodeEnvelope(KingdomPolityCodec.EncodeEnvelope(ledger));
			book = KingdomTradeCodec.DecodeEnvelopeRaw(KingdomTradeCodec.EncodeEnvelope(book));
			KingdomTradeRules.Normalize(book);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				0L, receipt, out KingdomPolityPublicationResult replay, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, replay.Outcome);
			Assert.IsTrue(KingdomTradeRules.TryAcknowledgePolityConsignment(book, ledger,
				request, receipt, out bool changed, out failure), failure);
			Assert.IsTrue(changed); Assert.AreEqual(0, book.RecentProofs.Count);
			Assert.AreEqual(1, book.CompactedProofs.Count);
			Assert.AreEqual(1, book.CompactedProofs[0].ProofCount);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out _, out kind, out failure), failure);
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.Missing, kind);

			// Crash after acknowledgement: conclusion authenticates the missing-proof retry.
			ledger = KingdomPolityCodec.DecodeEnvelope(KingdomPolityCodec.EncodeEnvelope(ledger));
			book = KingdomTradeCodec.DecodeEnvelopeRaw(KingdomTradeCodec.EncodeEnvelope(book));
			KingdomTradeRules.Normalize(book);
			byte[] stable = KingdomTradeCodec.EncodeEnvelope(book);
			Assert.IsTrue(KingdomTradeRules.TryAcknowledgePolityConsignment(book, ledger,
				request, receipt, out changed, out failure), failure);
			Assert.IsFalse(changed);
			CollectionAssert.AreEqual(stable, KingdomTradeCodec.EncodeEnvelope(book));
			Assert.IsNotNull(KingdomTradeRules.NewOperation(book,
				KingdomTradeOperationKind.ManifestTurnback, 100L),
				"acknowledgement must free exact recent-proof capacity");
		}

		[Test]
		public void WrongReceiptOrProofCannotAcknowledgeOrMutateEitherOwner()
		{
			KingdomPolityLedger ledger = KingdomPolityConsignmentTests.Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out _, out string failure), failure);
			KingdomTradeBook book = KingdomPolityConsignmentTests.TradeBookForWitness(
				request, 4, KingdomTradePhase.Terminal);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out KingdomTradePolityConsignmentReceipt receipt, out _, out failure), failure);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				ledger.Revision, receipt, out _, out failure), failure);
			byte[] polity = KingdomPolityCodec.EncodeEnvelope(ledger);
			byte[] trade = KingdomTradeCodec.EncodeEnvelope(book);
			byte[] validTrade = (byte[])trade.Clone();
			receipt.DeliveredDrams++;
			Assert.IsFalse(KingdomTradeRules.TryAcknowledgePolityConsignment(book, ledger,
				request, receipt, out _, out failure));
			CollectionAssert.AreEqual(polity, KingdomPolityCodec.EncodeEnvelope(ledger));
			CollectionAssert.AreEqual(trade, KingdomTradeCodec.EncodeEnvelope(book));
			receipt.DeliveredDrams--;
			book.RecentProofs.Add(book.RecentProofs[0]);
			trade = KingdomTradeCodec.EncodeEnvelope(book);
			Assert.IsFalse(KingdomTradeRules.TryAcknowledgePolityConsignment(book, ledger,
				request, receipt, out _, out failure));
			StringAssert.Contains("duplicated", failure);
			CollectionAssert.AreEqual(trade, KingdomTradeCodec.EncodeEnvelope(book));
			book = KingdomTradeCodec.DecodeEnvelopeRaw(validTrade);
			KingdomTradeRules.Normalize(book);
			book.RecentProofs[0].ManifestId = "taf:manifest:foreign-proof";
			trade = KingdomTradeCodec.EncodeEnvelope(book);
			Assert.IsFalse(KingdomTradeRules.TryAcknowledgePolityConsignment(book, ledger,
				request, receipt, out _, out failure));
			CollectionAssert.AreEqual(trade, KingdomTradeCodec.EncodeEnvelope(book));
		}

		[Test]
		public void TerminalFailureAcknowledgesOnlyAfterZeroDeltaConclusion()
		{
			KingdomPolityLedger ledger = KingdomPolityConsignmentTests.Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out _, out string failure), failure);
			KingdomTradeBook book = KingdomPolityConsignmentTests.TradeBookForWitness(
				request, 0, KingdomTradePhase.Quarantined);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out KingdomTradePolityConsignmentReceipt receipt,
				out KingdomTradePolityConsignmentReceiptKind kind, out failure), failure);
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.TerminalFailed, kind);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				ledger.Revision, receipt, out _, out failure), failure);
			Assert.IsTrue(KingdomTradeRules.TryAcknowledgePolityConsignment(book, ledger,
				request, receipt, out bool changed, out failure), failure);
			Assert.IsTrue(changed); Assert.AreEqual(0, book.RecentProofs.Count);
			Assert.AreEqual(1, book.CompactedProofs[0].ProofCount);
		}

		private static KingdomTradeOperation Operation(out KingdomTradeBook Book,
			out KingdomPolityConsignmentRequest Request)
		{
			KingdomPolityLedger ledger = KingdomPolityConsignmentTests.Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out Request, out _, out string failure), failure);
			Book = BoundBook();
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(Book,
				KingdomTradeOperationKind.PolityConsignmentDelivery, 60L);
			operation.ZoneId = "zone"; operation.SettlementId = Request.SurfaceRef;
			operation.SettlementName = "Seat"; operation.CharterId = Request.CorrespondencePlanId;
			operation.ManifestId = Request.ConsignmentId; operation.DealKey = Request.CounterpartyPolityId;
			operation.DealDisplayName = Request.NeedRef; operation.Faction = Request.RecipientCohortId;
			operation.MaterialClaim = Request.RequestDigest; operation.OriginId = Request.SurfaceRef;
			operation.DestinationId = Request.SurfaceRef; operation.OriginName = "Seat";
			operation.DestinationName = "Seat"; operation.WaterDirection = KingdomTradeWaterDirection.Debit;
			operation.RequestedWater = Request.RequestedDrams;
			Assert.IsTrue(KingdomTradeRules.TryCreatePolityRecipientWitness(Request,
				KingdomPolityConsignmentTests.RecipientBodyId(),
				KingdomPolityConsignmentTests.RecipientProjectionId(),
				out KingdomTradePolityRecipientWitness witness, out failure), failure);
			operation.PolityRecipient = witness;
			return operation;
		}

		private static KingdomTradeBook BoundBook()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, KingdomPolityTestData.Realm,
				new[] { KingdomPolityTestData.Settlement }, out string failure), failure);
			return book;
		}

		private static KingdomTradeOutbox Skipped(string id)
		{
			return new KingdomTradeOutbox { EventId = id,
				ChronicleState = KingdomTradeSinkState.Skipped,
				LedgerState = KingdomTradeSinkState.Skipped,
				MessageState = KingdomTradeSinkState.Skipped,
				DeedState = KingdomTradeSinkState.Skipped };
		}

		private static KingdomTradeProof OrdinaryProof(long sequence)
		{
			return new KingdomTradeProof
			{
				RealmId = KingdomPolityTestData.Realm, Sequence = sequence,
				Id = KingdomTradeRules.OperationId(KingdomPolityTestData.Realm, sequence),
				OperationEvidenceHash = KingdomPolityTestData.DigestA,
				Kind = KingdomTradeOperationKind.ManifestTurnback,
				Disposition = KingdomTradePhase.Terminal, RequestedWater = 0,
				ProvedWater = 0, SettlementId = KingdomPolityTestData.Settlement,
				ManifestId = "taf:manifest:ordinary-" + sequence,
				ChronicleState = KingdomTradeSinkState.Skipped,
				LedgerState = KingdomTradeSinkState.Skipped,
				MessageState = KingdomTradeSinkState.Skipped,
				DeedState = KingdomTradeSinkState.Skipped, Tick = sequence
			};
		}
	}
}
#endif
