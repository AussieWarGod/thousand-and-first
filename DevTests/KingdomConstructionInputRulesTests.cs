#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public class KingdomConstructionInputRulesTests
	{
		private static readonly string A = new string('a', 64);
		private static readonly string B = new string('b', 64);
		private static readonly string C = new string('c', 64);

		[Test]
		public void PersistedEnumsRemainAppendOnlyAndExplicit()
		{
			AssertEnum<KingdomConstructionInputKind>(0, 1, 2, 3, 4);
			AssertEnum<KingdomConstructionInputTopology>(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
			AssertEnum<KingdomConstructionInputTxPhase>(0, 1, 2, 3, 4, 5, 6, 7,
				8, 9, 10, 11, 12, 13, 14, 15);
			AssertEnum<KingdomConstructionInputSourcePhase>(0, 1, 2, 3, 4, 5, 6, 7,
				8, 9, 10, 11);
			AssertEnum<KingdomConstructionInputCargoPhase>(0, 1, 2, 3, 4, 5, 6, 7,
				8, 9, 10, 11, 12, 13);
			AssertEnum<KingdomConstructionInputFault>(0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
				10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21);
				ClassicAssert.AreEqual(3, KingdomConstructionInputRules.Schema);
				ClassicAssert.AreEqual(1, KingdomConstructionInputRules.LegacySchema);
			ClassicAssert.AreEqual(16, KingdomConstructionInputRules.MaxChildren);
		}

		[Test]
		public void CreateFreezesThreeDayFloorExactClaimsAndCoordinateOnlyRoute()
		{
			KingdomConstructionInputReceipt receipt = Base();
			ClassicAssert.AreEqual(6, receipt.WaterReserveFloor);
			ClassicAssert.AreEqual("", receipt.ChildAt(0).SourceObjectId ?? "");
			ClassicAssert.AreEqual("", receipt.ChildAt(0).TargetObjectId ?? "");
			ClassicAssert.AreEqual(KingdomConstructionInputCargoShape.OpaqueObjectManifest,
				receipt.ChildAt(0).CargoShape);
			ClassicAssert.AreEqual(2, receipt.ChildAt(0).CargoCount, "central amount is object count");
			ClassicAssert.AreEqual("Cistern", receipt.SourceAt(0).Blueprint);
			ClassicAssert.AreEqual("EmptyWaterskin", receipt.CargoAt(0).Blueprint);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryValidate(receipt, out var fault), fault.ToString());
		}

		[Test]
		public void CodecRoundTripIsCanonicalAndRejectsCorruptionFutureSchemaAndBounds()
		{
			KingdomConstructionInputReceipt receipt = Base();
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryEncode(receipt, out string encoded,
				out var fault), fault.ToString());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryDecode(encoded, out var decoded,
				out fault), fault.ToString());
			ClassicAssert.AreEqual(receipt.PlanDigest, decoded.PlanDigest);
			ClassicAssert.AreEqual("", decoded.ChildAt(0).SourceObjectId);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryEncode(decoded, out string again,
				out fault));
			ClassicAssert.AreEqual(encoded, again);

			string corrupt = encoded.Substring(0, encoded.Length - 1)
				+ (encoded[encoded.Length - 1] == '0' ? "1" : "0");
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryDecode(corrupt, out decoded, out fault));
			ClassicAssert.AreEqual(KingdomConstructionInputFault.Digest, fault);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryDecode(
				new string('x', KingdomConstructionInputRules.MaxEncodedChars + 1),
				out decoded, out fault));

			string[] fields = encoded.Split('|');
			byte[] payload = Convert.FromBase64String(fields[1]);
				payload[6] = 4; payload[7] = payload[8] = payload[9] = 0;
			string future = fields[0] + "|" + Convert.ToBase64String(payload) + "|" + Hash(payload);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryDecode(future, out decoded, out fault));
			ClassicAssert.AreEqual(KingdomConstructionInputFault.FutureSchema, fault);
		}

		[Test]
		public void SchemaOneSingleRequiredObjectReceiptRemainsCanonicalAndRecoverable()
		{
			KingdomConstructionInputReceipt current = Base("mat-stack", true);
			KingdomConstructionInputReceipt provisional = new KingdomConstructionInputReceipt(
				KingdomConstructionInputRules.LegacySchema, current.ReceiptId,
				current.ConstructionJobId, current.OwnerKey, current.OwnerEpoch,
				current.TargetZoneId, current.TargetX, current.TargetY,
				current.ConstructionIntentDigest, current.RequiredObjectId,
				current.WaterRequested, current.MaterialRequestedClaim,
				current.WaterReserveFloor, current.MaterialReservePolicyVersion,
				current.PriorWaterSpent, current.PriorWaterLost,
				current.PriorMaterialSpentClaim, current.PriorMaterialLostClaim,
				current.TxPhase, current.Revision, null, current.PauseStartedTick,
				current.PausedTicks, current.CopySources(), current.CopyCargo(),
				current.CopyChildren());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryPlanDigest(provisional,
				out string legacyDigest));
			KingdomConstructionInputReceipt legacy = new KingdomConstructionInputReceipt(
				provisional.Schema, provisional.ReceiptId, provisional.ConstructionJobId,
				provisional.OwnerKey, provisional.OwnerEpoch, provisional.TargetZoneId,
				provisional.TargetX, provisional.TargetY,
				provisional.ConstructionIntentDigest, provisional.RequiredObjectId,
				provisional.WaterRequested, provisional.MaterialRequestedClaim,
				provisional.WaterReserveFloor, provisional.MaterialReservePolicyVersion,
				provisional.PriorWaterSpent, provisional.PriorWaterLost,
				provisional.PriorMaterialSpentClaim, provisional.PriorMaterialLostClaim,
				provisional.TxPhase, provisional.Revision, legacyDigest,
				provisional.PauseStartedTick, provisional.PausedTicks,
				provisional.CopySources(), provisional.CopyCargo(), provisional.CopyChildren());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryEncode(legacy,
				out string encoded, out var fault), fault.ToString());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryDecode(encoded,
				out var decoded, out fault), fault.ToString());
			ClassicAssert.AreEqual(KingdomConstructionInputRules.LegacySchema, decoded.Schema);
			ClassicAssert.AreEqual(1, decoded.RequiredObjectCount);
			ClassicAssert.AreEqual("mat-stack", decoded.RequiredObjectAt(0));
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryEncode(decoded,
				out string canonical, out fault));
			ClassicAssert.AreEqual(encoded, canonical);
		}

		[Test]
		public void SchemaTwoReceiptRemainsCanonicalForAttendedMigration()
		{
			KingdomConstructionInputReceipt current = Base("mat-stack", true);
			KingdomConstructionInputReceipt provisional = new KingdomConstructionInputReceipt(
				2, current.ReceiptId, current.ConstructionJobId, current.OwnerKey,
				current.OwnerEpoch, current.TargetZoneId, current.TargetX, current.TargetY,
				current.ConstructionIntentDigest, current.CopyRequiredObjectIds(),
				current.WaterRequested, current.MaterialRequestedClaim,
				current.WaterReserveFloor, current.MaterialReservePolicyVersion,
				current.PriorWaterSpent, current.PriorWaterLost,
				current.PriorMaterialSpentClaim, current.PriorMaterialLostClaim,
				current.TxPhase, current.Revision, null, current.PauseStartedTick,
				current.PausedTicks, current.CopySources(), current.CopyCargo(),
				current.CopyChildren());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryPlanDigest(provisional,
				out string digest));
			KingdomConstructionInputReceipt schemaTwo = new KingdomConstructionInputReceipt(
				2, provisional.ReceiptId, provisional.ConstructionJobId, provisional.OwnerKey,
				provisional.OwnerEpoch, provisional.TargetZoneId, provisional.TargetX,
				provisional.TargetY, provisional.ConstructionIntentDigest,
				provisional.CopyRequiredObjectIds(), provisional.WaterRequested,
				provisional.MaterialRequestedClaim, provisional.WaterReserveFloor,
				provisional.MaterialReservePolicyVersion, provisional.PriorWaterSpent,
				provisional.PriorWaterLost, provisional.PriorMaterialSpentClaim,
				provisional.PriorMaterialLostClaim, provisional.TxPhase,
				provisional.Revision, digest, provisional.PauseStartedTick,
				provisional.PausedTicks, provisional.CopySources(), provisional.CopyCargo(),
				provisional.CopyChildren());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryEncode(schemaTwo,
				out string encoded, out var fault), fault.ToString());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryDecode(encoded,
				out var decoded, out fault), fault.ToString());
			ClassicAssert.AreEqual(2, decoded.Schema);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.ExactChildBinding(decoded, 0,
				decoded.ChildAt(0).JobId, decoded.ChildAt(0).TripId,
				decoded.ConstructionJobId, 2, decoded.PlanDigest));
		}

		[Test]
		public void GeneratedIdsChangeReceiptHashButNeverImmutablePlanDigest()
		{
			KingdomConstructionInputReceipt receipt = SourcePending(Base());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryReceiptDigest(receipt,
				out string before, out var fault));
			string plan = receipt.PlanDigest;
			receipt = Source(receipt, 1, KingdomConstructionInputSourcePhase.SplitIntent);
			receipt = SourceEvidence(receipt, 1, "mat-remainder", null, null, 0);
			ClassicAssert.AreEqual(plan, receipt.PlanDigest);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryReceiptDigest(receipt,
				out string after, out fault));
			ClassicAssert.AreNotEqual(before, after);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryUpdateSourceEvidence(receipt,
				receipt.Revision, 1, "rewritten", null, null, 0, out var ignored, out fault));
			ClassicAssert.AreEqual(KingdomConstructionInputFault.Witness, fault);
		}

		[Test]
		public void ParentAndLineFsmRejectEveryIllegalSmallShapeAndStaleRevision()
		{
			KingdomConstructionInputReceipt initial = Base();
			for (int i = 0; i <= 15; i++)
			{
				bool ok = KingdomConstructionInputRules.TryTransitionTransaction(initial,
					initial.Revision, initial.TxPhase, (KingdomConstructionInputTxPhase)i,
					out var changed, out var fault);
				bool expected = i == (int)KingdomConstructionInputTxPhase.Reserved
					|| i == (int)KingdomConstructionInputTxPhase.RollbackPending
					|| i == (int)KingdomConstructionInputTxPhase.CancellationPending
					|| i == (int)KingdomConstructionInputTxPhase.Quarantined;
				ClassicAssert.AreEqual(expected, ok, "parent next " + i);
			}
			KingdomConstructionInputReceipt reserved = Tx(initial,
				KingdomConstructionInputTxPhase.Reserved);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryTransitionTransaction(reserved,
				initial.Revision, reserved.TxPhase, KingdomConstructionInputTxPhase.SourcePending,
				out var stale, out var staleFault));
			ClassicAssert.AreEqual(KingdomConstructionInputFault.Revision, staleFault);

			KingdomConstructionInputReceipt quarantined = Tx(initial,
				KingdomConstructionInputTxPhase.Quarantined);
			for (int i = 0; i <= 11; i++)
			{
				bool ok = KingdomConstructionInputRules.TryTransitionSource(quarantined,
					quarantined.Revision, 1, KingdomConstructionInputSourcePhase.Reserved,
					(KingdomConstructionInputSourcePhase)i, out var changed, out var fault);
				ClassicAssert.AreEqual(i == 2 || i == 11, ok, "partial source next " + i);
			}
		}

		[Test]
		public void StagedMultiLineLoadingMustFinishBeforeRouting()
		{
			KingdomConstructionInputReceipt receipt = PreparedSources(SourcePending(Base()));
			receipt = PrepareCargoAtSource(receipt);
			receipt = Cargo(receipt, 0, KingdomConstructionInputCargoPhase.PickupIntent);
			receipt = Move(receipt, 0, KingdomConstructionInputCargoPhase.InFlight,
				KingdomConstructionInputTopology.CarrierInventory, "carrier-0", "source-zone", 2, 2, 0, 0);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryTransitionTransaction(receipt,
				receipt.Revision, receipt.TxPhase, KingdomConstructionInputTxPhase.Routing,
				out var ignored, out var fault));
			receipt = Cargo(receipt, 1, KingdomConstructionInputCargoPhase.PickupIntent);
			receipt = Move(receipt, 1, KingdomConstructionInputCargoPhase.InFlight,
				KingdomConstructionInputTopology.CarrierInventory, "carrier-1", "source-zone", 3, 2, 0, 0);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.Routing);
			ClassicAssert.AreEqual(KingdomConstructionInputTxPhase.Routing, receipt.TxPhase);
		}

		[Test]
		public void HappyPathRoundTripsAtSaveCutsAndCommitsExactEstablishedAccounting()
		{
			KingdomConstructionInputReceipt receipt = AtRouting(); RoundTrip(ref receipt);
			receipt = Move(receipt, 0, KingdomConstructionInputCargoPhase.Landed,
				KingdomConstructionInputTopology.LandingEscrow, "landing", "target-zone", 9, 9, 0, 0);
			receipt = Move(receipt, 1, KingdomConstructionInputCargoPhase.Landed,
				KingdomConstructionInputTopology.LandingEscrow, "landing", "target-zone", 9, 9, 0, 0);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.LandedAwaitingOwner); RoundTrip(ref receipt);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.DebitPending);
			for (int i = 0; i < 2; i++)
			{
				receipt = Cargo(receipt, i, KingdomConstructionInputCargoPhase.DebitIntent);
				receipt = Move(receipt, i, KingdomConstructionInputCargoPhase.Spent,
					KingdomConstructionInputTopology.Consumed, "construction-job", "target-zone",
					9, 9, receipt.CargoAt(i).Amount, 0);
				receipt = Source(receipt, i, KingdomConstructionInputSourcePhase.Spent);
			}
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.Closing);
			var beforeClaims = new KingdomConstructionClaims
			{
				WaterRequested = 10, WaterSpent = 0, WaterOutstanding = 10,
				WaterLost = 0, Exact = true,
				MaterialRequested = MaterialClaim(KingdomMaterial.Timber, 1),
				MaterialSpent = EmptyClaim(),
				MaterialOutstanding = MaterialClaim(KingdomMaterial.Timber, 1),
				MaterialLost = EmptyClaim()
			};
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryCommittedClaims(receipt,
				beforeClaims, out var committedClaims));
			ClassicAssert.AreEqual(10, committedClaims.WaterSpent);
			ClassicAssert.AreEqual(0, committedClaims.WaterOutstanding);
			ClassicAssert.AreEqual(MaterialClaim(KingdomMaterial.Timber, 1),
				committedClaims.MaterialSpent);
			ClassicAssert.AreEqual(MaterialClaim(KingdomMaterial.Timber, 1),
				committedClaims.MaterialLost);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.Committed); RoundTrip(ref receipt);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryCommittedClaims(receipt,
				beforeClaims, out committedClaims));
			ClassicAssert.IsTrue(KingdomConstructionInputRules.CommittedClaimsExact(receipt,
				10, 0, 10, MaterialClaim(KingdomMaterial.Timber, 1), EmptyClaim(),
				MaterialClaim(KingdomMaterial.Timber, 1)),
				"Lost means all physical debit, even with zero ProvedLost");
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryDeriveConservation(receipt,
				KingdomConstructionInputKind.Water, out var water, out var fault));
			ClassicAssert.AreEqual(10, water.Spent); ClassicAssert.AreEqual(0, water.ProvedLost);
		}

		[Test]
		public void UnreplacedObservedLossCanNeverCloseAsFullyFunded()
		{
			KingdomConstructionInputReceipt receipt = AtRouting();
			for (int i = 0; i < 2; i++) receipt = Move(receipt, i,
				KingdomConstructionInputCargoPhase.Landed,
				KingdomConstructionInputTopology.LandingEscrow, "landing", "target-zone", 9, 9, 0, 0);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.LandedAwaitingOwner);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.DebitPending);
			for (int i = 0; i < 2; i++)
			{
				receipt = Cargo(receipt, i, KingdomConstructionInputCargoPhase.DebitIntent);
				int lost = i == 0 ? 1 : 0;
				receipt = Move(receipt, i, KingdomConstructionInputCargoPhase.Spent,
					KingdomConstructionInputTopology.Consumed, "construction-job", "target-zone",
					9, 9, receipt.CargoAt(i).Amount - lost, lost);
				receipt = Source(receipt, i, KingdomConstructionInputSourcePhase.Spent);
			}
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryTransitionTransaction(receipt,
				receipt.Revision, receipt.TxPhase, KingdomConstructionInputTxPhase.Closing,
				out var ignored, out var fault));
			ClassicAssert.AreEqual(KingdomConstructionInputFault.Transition, fault);
		}

		[Test]
		public void RollbackAndPostCustodyCompensationAreDisjointAndTerminal()
		{
			KingdomConstructionInputReceipt rollback = SourcePending(Base());
			rollback = Source(rollback, 1, KingdomConstructionInputSourcePhase.SplitIntent);
			rollback = Tx(rollback, KingdomConstructionInputTxPhase.RollbackPending);
			rollback = Source(rollback, 1, KingdomConstructionInputSourcePhase.RestoreIntent);
			rollback = Source(rollback, 1, KingdomConstructionInputSourcePhase.Restored);
			rollback = Tx(rollback, KingdomConstructionInputTxPhase.RolledBack);
			ClassicAssert.AreEqual(KingdomConstructionInputTxPhase.RolledBack, rollback.TxPhase);

			KingdomConstructionInputReceipt compensation = AtRouting();
			compensation = Tx(compensation, KingdomConstructionInputTxPhase.CompensationPending);
			for (int i = 0; i < 2; i++)
			{
				compensation = Cargo(compensation, i,
					KingdomConstructionInputCargoPhase.CompensationIntent);
				compensation = Move(compensation, i,
					KingdomConstructionInputCargoPhase.Compensated,
					KingdomConstructionInputTopology.Returned, "stockpile", "source-zone",
					1 + i, 1, 0, 0);
				compensation = Source(compensation, i,
					KingdomConstructionInputSourcePhase.CompensationIntent);
				compensation = Source(compensation, i,
					KingdomConstructionInputSourcePhase.Compensated);
			}
			compensation = Tx(compensation, KingdomConstructionInputTxPhase.Compensated);
			ClassicAssert.AreEqual(KingdomConstructionInputTxPhase.Compensated, compensation.TxPhase);
		}

		[Test]
		public void MixedCancellationRestoresUntouchedLinesAndCompensatesDebitedLines()
		{
			KingdomConstructionInputReceipt mixed = SourcePending(Base());
			mixed = Source(mixed, 0, KingdomConstructionInputSourcePhase.TransferIntent);
			mixed = Source(mixed, 0, KingdomConstructionInputSourcePhase.Debited);
			mixed = Cargo(mixed, 0, KingdomConstructionInputCargoPhase.CreateIntent);
			mixed = CargoEvidence(mixed, 0, "water-cargo",
				KingdomConstructionInputTopology.Invalid, null, null, -1, -1, 0, 0);
			mixed = CargoEvidence(mixed, 0, "water-cargo",
				KingdomConstructionInputTopology.CarrierInventory, "carrier-0",
				"source-zone", 1, 1, 0, 0);
			mixed = Cargo(mixed, 0, KingdomConstructionInputCargoPhase.AtSource);
			mixed = Tx(mixed, KingdomConstructionInputTxPhase.CancellationPending);

			mixed = Cargo(mixed, 0, KingdomConstructionInputCargoPhase.ReleaseIntent);
			mixed = CargoEvidence(mixed, 0, "water-cargo",
				KingdomConstructionInputTopology.Released, "returned-water",
				"source-zone", 1, 1, 0, 0);
			mixed = Cargo(mixed, 0, KingdomConstructionInputCargoPhase.Released);
			mixed = Source(mixed, 0, KingdomConstructionInputSourcePhase.CompensationIntent);
			mixed = Source(mixed, 0, KingdomConstructionInputSourcePhase.Compensated);
			mixed = Tx(mixed, KingdomConstructionInputTxPhase.Cancelled);

			ClassicAssert.IsTrue(KingdomConstructionInputRules.IsTerminal(mixed));
			ClassicAssert.AreEqual(KingdomConstructionInputSourcePhase.Reserved,
				mixed.SourceAt(1).Phase);
			ClassicAssert.AreEqual(KingdomConstructionInputCargoPhase.Planned,
				mixed.CargoAt(1).Phase);
		}

		[Test]
		public void CancellationRefusesAfterDebitIntentBecauseCommitRecoveryWins()
		{
			KingdomConstructionInputReceipt receipt = AtRouting();
			for (int i = 0; i < 2; i++)
				receipt = Move(receipt, i, KingdomConstructionInputCargoPhase.Landed,
					KingdomConstructionInputTopology.LandingEscrow, "landing",
					"target-zone", 9, 9, 0, 0);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.LandedAwaitingOwner);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.DebitPending);
			receipt = Cargo(receipt, 0, KingdomConstructionInputCargoPhase.DebitIntent);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryTransitionTransaction(receipt,
				receipt.Revision, receipt.TxPhase,
				KingdomConstructionInputTxPhase.CancellationPending,
				out var ignored, out var fault));
			ClassicAssert.AreEqual(KingdomConstructionInputFault.Transition, fault);
		}

		[Test]
		public void PauseNeverMasksAfterOrThirdStateRecovery()
		{
			ClassicAssert.AreEqual(KingdomConstructionInputDecision.Acknowledge,
				KingdomConstructionInputRules.DecidePhysicalMutation(A, B, B, true));
			ClassicAssert.AreEqual(KingdomConstructionInputDecision.Quarantine,
				KingdomConstructionInputRules.DecidePhysicalMutation(A, B, C, true));
			ClassicAssert.AreEqual(KingdomConstructionInputDecision.WaitPaused,
				KingdomConstructionInputRules.DecidePhysicalMutation(A, B, A, true));
			ClassicAssert.AreEqual(KingdomConstructionInputDecision.Apply,
				KingdomConstructionInputRules.DecidePhysicalMutation(A, B, A, false));

			KingdomConstructionInputReceipt receipt = Base();
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TrySetPaused(receipt, receipt.Revision,
				100, true, out receipt, out var fault));
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TrySetPaused(receipt, receipt.Revision,
				107, false, out receipt, out fault));
			ClassicAssert.AreEqual(7, receipt.PausedTicks);

			KingdomConstructionInputReceipt rebased = Base();
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryRebaseMasterPause(rebased,
				rebased.Revision, 200, 240, out rebased, out fault));
			ClassicAssert.IsFalse(rebased.Paused);
			ClassicAssert.AreEqual(40, rebased.PausedTicks);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryEffectiveArrivalTick(300,
				rebased.PausedTicks, out long arrival));
			ClassicAssert.AreEqual(340, arrival);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryEffectiveArrivalTick(
				long.MaxValue, 1, out _));
		}

		[Test]
		public void IntentOwnerChildAndBuildTruthBindingsAreExactWhileLandingMayDiffer()
		{
			KingdomConstructionInputReceipt receipt = Base();
			KingdomConstructionInputIntent intent = Intent(B, "realm", 4, 5);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.ExactIntentBinding(receipt, intent, 0));
			ClassicAssert.AreNotEqual(intent.X, receipt.TargetX, "build cell and landing anchor differ");
			ClassicAssert.IsFalse(KingdomConstructionInputRules.ExactIntentBinding(receipt,
				Intent(C, "realm", 4, 5), 0), "catalog/build-truth drift refuses");
			ClassicAssert.IsFalse(KingdomConstructionInputRules.ExactIntentBinding(receipt,
				Intent(B, "exiled-realm", 4, 5), 0));
			ClassicAssert.IsFalse(KingdomConstructionInputRules.ExactIntentBinding(receipt, intent, 1));
			ClassicAssert.IsTrue(KingdomConstructionInputRules.ExactChildBinding(receipt, 0, 101, 101,
					"construction-job", receipt.Schema, receipt.PlanDigest));
			ClassicAssert.IsFalse(KingdomConstructionInputRules.ExactChildBinding(receipt, 0, 101, 102,
					"construction-job", receipt.Schema, receipt.PlanDigest));
		}

		[Test]
		public void RegistryUpdateLawAcceptsOneCasOperationAndRejectsArbitraryBatchEdits()
		{
			KingdomConstructionInputReceipt current = Base();
			KingdomConstructionInputReceipt tx = Tx(current, KingdomConstructionInputTxPhase.Reserved);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.ValidReceiptUpdate(current, tx));
			KingdomConstructionInputReceipt pending = Tx(tx,
				KingdomConstructionInputTxPhase.SourcePending);
			KingdomConstructionInputReceipt split = Source(pending, 1,
				KingdomConstructionInputSourcePhase.SplitIntent);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.ValidReceiptUpdate(pending, split));
			KingdomConstructionInputReceipt evidence = SourceEvidence(split, 1,
				"mat-remainder", null, null, 0);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.ValidReceiptUpdate(split, evidence));
			ClassicAssert.IsFalse(KingdomConstructionInputRules.ValidReceiptUpdate(evidence, evidence));

			KingdomConstructionInputSourceLine[] rows = evidence.CopySources();
			rows[1] = rows[1].WithEvidence(rows[1].RemainderObjectId, A, B, 0);
			KingdomConstructionInputReceipt batch = evidence.Copy(evidence.TxPhase,
				evidence.Revision + 1, evidence.PauseStartedTick, evidence.PausedTicks,
				rows, null, null);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryValidate(batch, out var fault));
			ClassicAssert.IsFalse(KingdomConstructionInputRules.ValidReceiptUpdate(evidence, batch),
				"two evidence fields cannot bypass one-update CAS");

			KingdomConstructionInputReceipt paused;
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TrySetPaused(evidence, evidence.Revision,
				50, true, out paused, out fault));
			ClassicAssert.IsTrue(KingdomConstructionInputRules.ValidReceiptUpdate(evidence, paused));
			ClassicAssert.IsFalse(KingdomConstructionInputRules.IsTerminal(paused));
		}

		[Test]
		public void RequiredObjectConsumesExactlyOneWholeMaterialObject()
		{
			KingdomConstructionInputReceipt exact = Base("mat-stack", true);
			ClassicAssert.AreEqual("mat-stack", exact.RequiredObjectId);
			ClassicAssert.AreEqual(0, exact.SourceAt(1).ResidualAfter);
			Assert.Throws<AssertionException>(() => Base("other-object", true));
		}

		[Test]
		public void WaterReserveIsPerSettlementAggregateAcrossZonesAndVessels()
		{
			ClassicAssert.IsNotNull(WaterAcrossSettlements(60, 60),
				"two settlements contribute distinct six- and nine-dram floors");
			ClassicAssert.IsNotNull(WaterAcrossOneSettlement(40, 40));
			Assert.Throws<AssertionException>(() => WaterAcrossOneSettlement(50, 50));
		}

		[Test]
		public void RepeatedLiquidSourceMustBeOneExactResidualChain()
		{
			ClassicAssert.IsNotNull(ChainedWater(true));
			Assert.Throws<AssertionException>(() => ChainedWater(false));
		}

		[Test]
		public void DuplicateAndReorderedAuthoritiesRefuseBeforeDigestAcceptance()
		{
			KingdomConstructionInputReceipt receipt = Base();
			var duplicate = CloneSource(receipt.SourceAt(1), 1, receipt.SourceAt(0).LineId);
			var bad = Rebuild(receipt, new[] { receipt.SourceAt(0), duplicate }, null, null);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryValidate(bad, out var fault));
			ClassicAssert.AreEqual(KingdomConstructionInputFault.Duplicate, fault);
			bad = Rebuild(receipt, new[] { receipt.SourceAt(1), receipt.SourceAt(0) }, null, null);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryValidate(bad, out fault));
			var duplicateChildren = new[] { Child(0, 101, 0, 1, "source-zone", 1, 1),
				Child(1, 101, 1, 1, "source-zone", 2, 1) };
			bad = Rebuild(receipt, null, null, duplicateChildren);
			ClassicAssert.IsFalse(KingdomConstructionInputRules.TryValidate(bad, out fault));
			ClassicAssert.AreEqual(KingdomConstructionInputFault.Child, fault);
		}

		private static KingdomConstructionInputReceipt Base(string required = null, bool whole = false)
		{
			string material = MaterialClaim(KingdomMaterial.Timber, 1);
			KingdomConstructionInputIntent intent = Intent(B, "realm", 4, 5);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryIntentDigest(intent,
				out string intentDigest, out var fault));
			int before = whole ? 1 : 2;
			var sources = new[]
			{
				new KingdomConstructionInputSourceLine(0, "water-line",
					KingdomConstructionInputKind.Water, KingdomConstructionInputRules.WaterClassification,
					"settlement", "source-zone", "water-holder", "water-source",
					KingdomConstructionInputTopology.LiquidVessel, 1, 1, "Cistern",
					100, 10, 90, 100, 0, 6, 0, 4, 0, null,
					KingdomConstructionInputSourcePhase.Reserved, null, null, null, 0),
				new KingdomConstructionInputSourceLine(1, "material-line",
					KingdomConstructionInputKind.Material, UnitMaterial(KingdomMaterial.Timber),
					"settlement", "source-zone", "material-holder", "mat-stack",
					KingdomConstructionInputTopology.ContainerInventory, 2, 1, "Wood",
					before, 1, before - 1, before, 0, 0, 1, 5, 1,
					whole ? null : "split-marker", KingdomConstructionInputSourcePhase.Reserved,
					null, null, null, 0)
			};
			var cargo = new[]
			{
				new KingdomConstructionInputCargoLine(0, "cargo-water", "create-water",
					KingdomConstructionInputKind.Water, KingdomConstructionInputRules.WaterClassification,
					10, KingdomConstructionInputRules.WaterCargoBlueprint,
					KingdomConstructionInputRules.WaterCargoCapacity, 0, null, 101, 101, null,
					KingdomConstructionInputCargoPhase.Planned,
					KingdomConstructionInputTopology.Invalid, null, null, -1, -1, null, null, 0, 0),
				new KingdomConstructionInputCargoLine(1, "cargo-material", "bind-material",
					KingdomConstructionInputKind.Material, UnitMaterial(KingdomMaterial.Timber),
					1, "Wood", before, 1, "mat-stack", 101, 101, null,
					KingdomConstructionInputCargoPhase.Planned,
					KingdomConstructionInputTopology.Invalid, null, null, -1, -1, null, null, 0, 0)
			};
			var children = new[] { Child(0, 101, 0, 2, "source-zone", 1, 1) };
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryCreate("input-receipt",
				"construction-job", "realm", 0, "target-zone", 9, 9, intentDigest,
				required, 10, material, 2, 1, 0, 0, EmptyClaim(), EmptyClaim(),
				sources, cargo, children, out var receipt, out fault), fault.ToString());
			return receipt;
		}

		private static KingdomConstructionInputReceipt WaterAcrossSettlements(int first, int second)
		{
			string empty = EmptyClaim();
			var intent = new KingdomConstructionInputIntent("water-job", "realm", "target-zone",
				1, 1, 4, 5, null, null, null, A, B, first + second, empty, 1, 2, 3);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryIntentDigest(intent,
				out string digest, out var fault));
			var sources = new[]
			{
				WaterSource(0, "s1", "z1", "h1", "w1", 100, first, 6),
				WaterSource(1, "s2", "z2", "h2", "w2", 100, second, 9)
			};
			var cargo = new[] { WaterCargo(0, first, 201), WaterCargo(1, second, 202) };
			var children = new[] { Child(0, 201, 0, 1, "z1", 1, 1),
				Child(1, 202, 1, 1, "z2", 2, 2) };
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryCreate("water-receipt", "water-job",
				"realm", 0, "target-zone", 9, 9, digest, null, first + second, empty,
				5, 1, 0, 0, empty, empty, sources, cargo, children,
				out var receipt, out fault), fault.ToString());
			return receipt;
		}

		private static KingdomConstructionInputReceipt WaterAcrossOneSettlement(int first, int second)
		{
			string empty = EmptyClaim();
			var intent = new KingdomConstructionInputIntent("water-job", "realm", "target-zone",
				1, 1, 4, 5, null, null, null, A, B, first + second, empty, 1, 2, 3);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryIntentDigest(intent,
				out string digest, out var fault));
			var sources = new[]
			{
				WaterSource(0, "same", "z1", "h1", "w1", 100, first, 15),
				WaterSource(1, "same", "z2", "h2", "w2", 100, second, 15)
			};
			var cargo = new[] { WaterCargo(0, first, 201), WaterCargo(1, second, 202) };
			var children = new[] { Child(0, 201, 0, 1, "z1", 1, 1),
				Child(1, 202, 1, 1, "z2", 2, 2) };
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryCreate("water-receipt", "water-job",
				"realm", 0, "target-zone", 9, 9, digest, null, first + second, empty,
				5, 1, 0, 0, empty, empty, sources, cargo, children,
				out var receipt, out fault), fault.ToString());
			return receipt;
		}

		private static KingdomConstructionInputReceipt ChainedWater(bool exact)
		{
			string empty = EmptyClaim();
			var intent = new KingdomConstructionInputIntent("chain-job", "realm", "target-zone",
				1, 1, 4, 5, null, null, null, A, B, 70, empty, 1, 2, 3);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryIntentDigest(intent,
				out string digest, out var fault));
			var first = WaterSource(0, "same", "z1", "holder", "water", 100, 40, 6);
			int secondBefore = exact ? 60 : 61;
			var second = new KingdomConstructionInputSourceLine(1, "water-1",
				KingdomConstructionInputKind.Water, KingdomConstructionInputRules.WaterClassification,
				"same", "z1", "holder", "water", KingdomConstructionInputTopology.LiquidVessel,
				1, 1, "Cistern", secondBefore, 30, secondBefore - 30, 100, 0, 6, 1, 1, 0,
				null, KingdomConstructionInputSourcePhase.Reserved, null, null, null, 0);
			var cargo = new[] { WaterCargo(0, 40, 201), WaterCargo(1, 30, 202) };
			var children = new[] { Child(0, 201, 0, 1, "z1", 1, 1),
				Child(1, 202, 1, 1, "z1", 1, 1) };
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryCreate("chain-receipt", "chain-job",
				"realm", 0, "target-zone", 9, 9, digest, null, 70, empty, 2, 1,
				0, 0, empty, empty, new[] { first, second }, cargo, children,
				out var receipt, out fault), fault.ToString());
			return receipt;
		}

		private static KingdomConstructionInputSourceLine WaterSource(int ordinal,
			string settlement, string zone, string holder, string objectId, int before,
			int take, int floor)
		{
			return new KingdomConstructionInputSourceLine(ordinal, "water-" + ordinal,
				KingdomConstructionInputKind.Water, KingdomConstructionInputRules.WaterClassification,
				settlement, zone, holder, objectId, KingdomConstructionInputTopology.LiquidVessel,
				ordinal + 1, ordinal + 1, "Cistern", before, take, before - take,
				100, 0, floor, ordinal, 1, ordinal, null,
				KingdomConstructionInputSourcePhase.Reserved, null, null, null, 0);
		}

		private static KingdomConstructionInputCargoLine WaterCargo(int ordinal, int amount, int job)
		{
			return new KingdomConstructionInputCargoLine(ordinal, "wc-" + ordinal,
				"wm-" + ordinal, KingdomConstructionInputKind.Water,
				KingdomConstructionInputRules.WaterClassification, amount,
				KingdomConstructionInputRules.WaterCargoBlueprint,
				KingdomConstructionInputRules.WaterCargoCapacity,
				ordinal, null, job, job, null, KingdomConstructionInputCargoPhase.Planned,
				KingdomConstructionInputTopology.Invalid, null, null, -1, -1, null, null, 0, 0);
		}

		private static KingdomConstructionInputChild Child(int ordinal, int job,
			int start, int count, string sourceZone, int x, int y)
		{
			return new KingdomConstructionInputChild(ordinal, job, job, start, count,
				KingdomConstructionInputCargoShape.OpaqueObjectManifest, job + 1000, null,
				sourceZone, x, y, job + 2000, null, "target-zone", 9, 9, 20, C, 0, 0);
		}

		private static KingdomConstructionInputIntent Intent(string buildDigest,
			string owner, int x, int y)
		{
			return new KingdomConstructionInputIntent("construction-job", owner, "target-zone",
				1, 1, x, y, "plot", "source", "building", A, buildDigest, 10,
				MaterialClaim(KingdomMaterial.Timber, 1), 1, 2, 3);
		}

		private static KingdomConstructionInputReceipt SourcePending(KingdomConstructionInputReceipt r)
		{ return Tx(Tx(r, KingdomConstructionInputTxPhase.Reserved), KingdomConstructionInputTxPhase.SourcePending); }

		private static KingdomConstructionInputReceipt PreparedSources(KingdomConstructionInputReceipt r)
		{
			r = Source(r, 0, KingdomConstructionInputSourcePhase.TransferIntent);
			r = Source(r, 0, KingdomConstructionInputSourcePhase.Debited);
			r = Source(r, 1, KingdomConstructionInputSourcePhase.SplitIntent);
			r = SourceEvidence(r, 1, "mat-remainder", null, null, 0);
			r = Source(r, 1, KingdomConstructionInputSourcePhase.SplitProved);
			r = Source(r, 1, KingdomConstructionInputSourcePhase.TransferIntent);
			return Source(r, 1, KingdomConstructionInputSourcePhase.Debited);
		}

		private static KingdomConstructionInputReceipt PrepareCargoAtSource(KingdomConstructionInputReceipt r)
		{
			r = Cargo(r, 0, KingdomConstructionInputCargoPhase.CreateIntent);
			r = CargoEvidence(r, 0, "water-cargo", KingdomConstructionInputTopology.Invalid,
				null, null, -1, -1, 0, 0);
			r = CargoEvidence(r, 0, "water-cargo", KingdomConstructionInputTopology.CarrierInventory,
				"carrier-0", "source-zone", 1, 1, 0, 0);
			r = Cargo(r, 0, KingdomConstructionInputCargoPhase.AtSource);
			r = CargoEvidence(r, 1, "mat-stack", KingdomConstructionInputTopology.Invalid,
				null, null, -1, -1, 0, 0);
			return Move(r, 1, KingdomConstructionInputCargoPhase.AtSource,
				KingdomConstructionInputTopology.ContainerInventory, "material-holder",
				"source-zone", 2, 1, 0, 0);
		}

		private static KingdomConstructionInputReceipt AtRouting()
		{
			KingdomConstructionInputReceipt r = PrepareCargoAtSource(
				PreparedSources(SourcePending(Base())));
			for (int i = 0; i < 2; i++)
			{
				r = Cargo(r, i, KingdomConstructionInputCargoPhase.PickupIntent);
				r = Move(r, i, KingdomConstructionInputCargoPhase.InFlight,
					KingdomConstructionInputTopology.CarrierInventory, "carrier-" + i,
					"source-zone", 2 + i, 2, 0, 0);
			}
			return Tx(r, KingdomConstructionInputTxPhase.Routing);
		}

		private static KingdomConstructionInputReceipt Tx(KingdomConstructionInputReceipt r,
			KingdomConstructionInputTxPhase next)
		{
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryTransitionTransaction(r, r.Revision,
				r.TxPhase, next, out var changed, out var fault), fault.ToString()); return changed;
		}

		private static KingdomConstructionInputReceipt Source(KingdomConstructionInputReceipt r,
			int ordinal, KingdomConstructionInputSourcePhase next)
		{
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryTransitionSource(r, r.Revision, ordinal,
				r.SourceAt(ordinal).Phase, next, out var changed, out var fault), fault.ToString());
			return changed;
		}

		private static KingdomConstructionInputReceipt SourceEvidence(KingdomConstructionInputReceipt r,
			int ordinal, string remainder, string before, string after, int lost)
		{
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryUpdateSourceEvidence(r, r.Revision,
				ordinal, remainder, before, after, lost, out var changed, out var fault), fault.ToString());
			return changed;
		}

		private static KingdomConstructionInputReceipt Cargo(KingdomConstructionInputReceipt r,
			int ordinal, KingdomConstructionInputCargoPhase next)
		{
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryTransitionCargo(r, r.Revision, ordinal,
				r.CargoAt(ordinal).Phase, next, out var changed, out var fault), fault.ToString());
			return changed;
		}

		private static KingdomConstructionInputReceipt CargoEvidence(KingdomConstructionInputReceipt r,
			int ordinal, string objectId, KingdomConstructionInputTopology topology,
			string owner, string zone, int x, int y, int spent, int lost)
		{
			var old = r.CargoAt(ordinal);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryUpdateCargoEvidence(r, r.Revision,
				ordinal, objectId, topology, owner, zone, x, y, old.BeforeWitnessHash,
				old.AfterWitnessHash, spent, lost, out var changed, out var fault), fault.ToString());
			return changed;
		}

		private static KingdomConstructionInputReceipt Move(KingdomConstructionInputReceipt r,
			int ordinal, KingdomConstructionInputCargoPhase next,
			KingdomConstructionInputTopology topology, string owner, string zone,
			int x, int y, int spent, int lost)
		{
			var old = r.CargoAt(ordinal);
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryTransitionCargoWithEvidence(r,
				r.Revision, ordinal, old.Phase, next, old.ObjectId, topology, owner, zone,
				x, y, old.BeforeWitnessHash, old.AfterWitnessHash, spent, lost,
				out var changed, out var fault), fault.ToString()); return changed;
		}

		private static void RoundTrip(ref KingdomConstructionInputReceipt receipt)
		{
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryEncode(receipt, out string encoded,
				out var fault), fault.ToString());
			ClassicAssert.IsTrue(KingdomConstructionInputRules.TryDecode(encoded, out receipt, out fault),
				fault.ToString());
		}

		private static KingdomConstructionInputReceipt Rebuild(KingdomConstructionInputReceipt r,
			KingdomConstructionInputSourceLine[] sources, KingdomConstructionInputCargoLine[] cargo,
			KingdomConstructionInputChild[] children)
		{
			return new KingdomConstructionInputReceipt(r.Schema, r.ReceiptId, r.ConstructionJobId,
				r.OwnerKey, r.OwnerEpoch, r.TargetZoneId, r.TargetX, r.TargetY,
				r.ConstructionIntentDigest, r.CopyRequiredObjectIds(), r.WaterRequested,
				r.MaterialRequestedClaim, r.WaterReserveFloor, r.MaterialReservePolicyVersion,
				r.PriorWaterSpent, r.PriorWaterLost, r.PriorMaterialSpentClaim,
				r.PriorMaterialLostClaim, r.TxPhase, r.Revision, r.PlanDigest,
				r.PauseStartedTick, r.PausedTicks, sources ?? r.CopySources(),
				cargo ?? r.CopyCargo(), children ?? r.CopyChildren());
		}

		private static KingdomConstructionInputSourceLine CloneSource(
			KingdomConstructionInputSourceLine x, int ordinal, string lineId)
		{
			return new KingdomConstructionInputSourceLine(ordinal, lineId, x.Kind,
				x.Classification, x.SourceSettlementId, x.SourceZoneId, x.HolderId,
				x.SourceObjectId, x.Topology, x.X, x.Y, x.Blueprint, x.Before, x.Take,
				x.ResidualAfter, x.HolderStockBefore, x.PriorReserved, x.ReserveFloor,
				x.CargoOrdinal, x.RouteCost, x.DedicationOrdinal, x.RemainderMarker,
				x.Phase, x.RemainderObjectId, x.BeforeWitnessHash, x.AfterWitnessHash, x.ProvedLost);
		}

		private static string UnitMaterial(KingdomMaterial material)
		{ return MaterialClaim(material, 1); }

		private static string MaterialClaim(KingdomMaterial material, int count)
		{
			KingdomMaterialTally tally = new KingdomMaterialTally(); tally.Set(material, count);
			return new KingdomMaterialDebitCost(tally).ToClaimString();
		}

		private static string EmptyClaim()
		{ return new KingdomMaterialDebitCost().ToClaimString(); }

		private static string Hash(byte[] bytes)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] digest = sha.ComputeHash(bytes); StringBuilder text = new StringBuilder(64);
				for (int i = 0; i < digest.Length; i++)
					text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
				return text.ToString();
			}
		}

		private static void AssertEnum<T>(params int[] expected)
		{
			Array values = Enum.GetValues(typeof(T)); ClassicAssert.AreEqual(expected.Length, values.Length);
			for (int i = 0; i < expected.Length; i++)
				ClassicAssert.AreEqual(expected[i], Convert.ToInt32(values.GetValue(i)), typeof(T).Name);
		}
	}
}
#endif
