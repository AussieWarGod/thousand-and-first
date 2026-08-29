#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomConstructionInputRegistryRulesTests
	{
		[Test]
		public void RoutedReceiptBindsOuterJobIntentClaimsAndWire()
		{
			KingdomConstructionJob job = NewJob();
			KingdomConstructionInputReceipt receipt = NewReceipt(job);
			Assert.IsTrue(KingdomConstructionRules.UpdateInputReceipt(ref job, receipt));
			Assert.IsTrue(KingdomConstructionRules.ValidJob(job));
			string wire;
			Assert.IsTrue(KingdomConstructionRules.TryEncode(
				new List<KingdomConstructionJob> { job }, out wire));
			Assert.AreEqual(57, wire.Split('\n')[1].Split('|').Length);
			List<KingdomConstructionJob> decoded;
			Assert.IsTrue(KingdomConstructionRules.TryDecode(wire, out decoded));
			KingdomConstructionInputReceipt reloaded;
			Assert.IsTrue(KingdomConstructionRules.TryGetInputReceipt(decoded[0], out reloaded));
			Assert.AreEqual(receipt.PlanDigest, reloaded.PlanDigest);

			KingdomConstructionJob tampered = job.Copy();
			tampered.Payload = "repriced";
			Assert.IsFalse(KingdomConstructionRules.ValidJob(tampered));
			tampered = job.Copy();
			tampered.InputReceipt += "x";
			Assert.IsFalse(KingdomConstructionRules.ValidJob(tampered));
			tampered = job.Copy();
			tampered.Claims.MaterialOutstanding = EmptyClaim();
			Assert.IsFalse(KingdomConstructionRules.ValidJob(tampered));
		}

		[Test]
		public void ActiveReceiptRequiresOnePureCasAndCommitIsAtomicFunding()
		{
			KingdomConstructionJob current = NewJob();
			KingdomConstructionInputReceipt receipt = NewReceipt(current);
			KingdomConstructionJob attached = KingdomConstructionRules.Transition(current,
				current.Phase, 11L);
			Assert.IsTrue(KingdomConstructionRules.UpdateInputReceipt(ref attached, receipt));
			Assert.IsTrue(KingdomConstructionRules.ValidRegistryUpdate(current, attached));

			KingdomConstructionInputReceipt reserved = Tx(receipt,
				KingdomConstructionInputTxPhase.Reserved);
			KingdomConstructionJob next = KingdomConstructionRules.Transition(attached,
				attached.Phase, 12L);
			Assert.IsTrue(KingdomConstructionRules.UpdateInputReceipt(ref next, reserved));
			Assert.IsTrue(KingdomConstructionRules.ValidRegistryUpdate(attached, next));
			KingdomConstructionJob unrelated = KingdomConstructionRules.Transition(next,
				next.Phase, 13L, "edited without receipt CAS");
			Assert.IsFalse(KingdomConstructionRules.ValidRegistryUpdate(next, unrelated));

			KingdomConstructionInputReceipt closing = DriveToClosing(reserved, current.Id,
				current.ZoneId);
			KingdomConstructionJob beforeCommit = next.Copy();
			beforeCommit.Revision++;
			beforeCommit.UpdatedTick++;
			Assert.IsTrue(KingdomConstructionRules.UpdateInputReceipt(ref beforeCommit, closing));
			KingdomConstructionInputReceipt committed = Tx(closing,
				KingdomConstructionInputTxPhase.Committed);
			KingdomConstructionJob funded = KingdomConstructionRules.Transition(beforeCommit,
				KingdomConstructionPhase.Funded, beforeCommit.UpdatedTick + 1L);
			funded.Claims.MaterialSpent = funded.Claims.MaterialRequested;
			funded.Claims.MaterialOutstanding = EmptyClaim();
			funded.Claims.MaterialLost = funded.Claims.MaterialRequested;
			Assert.IsTrue(KingdomConstructionRules.UpdateInputReceipt(ref funded, committed));
			Assert.IsTrue(KingdomConstructionRules.ValidJob(funded));
			Assert.IsTrue(KingdomConstructionRules.ValidRegistryUpdate(beforeCommit, funded));
			KingdomConstructionJob projected = KingdomConstructionRules.Transition(funded,
				KingdomConstructionPhase.ProjectionPending, funded.UpdatedTick + 1L);
			projected.SubjectId = "generated-works";
			projected.OutputId = "generated-works";
			projected.PhysicalPhase = KingdomPhysicalPhase.OutputIntent;
			projected.PhysicalDestinationId = "destination";
			projected.PhysicalReceipt = "projection-receipt";
			Assert.IsTrue(KingdomConstructionRules.ValidJob(projected),
				"terminal input proof must not freeze ordinary post-funding projection state");
			Assert.IsTrue(KingdomConstructionRules.ValidRegistryUpdate(funded, projected));

			KingdomConstructionJob unpaid = funded.Copy();
			unpaid.Claims.MaterialOutstanding = unpaid.Claims.MaterialRequested;
			unpaid.Claims.MaterialSpent = EmptyClaim();
			unpaid.Claims.MaterialLost = EmptyClaim();
			Assert.IsFalse(KingdomConstructionRules.ValidJob(unpaid));
		}

		[Test]
		public void TerminalCompactionRetainsFinalRoutedReplayHash()
		{
			KingdomConstructionJob job = NewJob();
			KingdomConstructionInputReceipt receipt = DriveToClosing(NewReceipt(job),
				job.Id, job.ZoneId);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.Committed);
			job.Phase = KingdomConstructionPhase.Complete;
			job.Claims.MaterialSpent = job.Claims.MaterialRequested;
			job.Claims.MaterialOutstanding = EmptyClaim();
			job.Claims.MaterialLost = job.Claims.MaterialRequested;
			job.PhysicalPhase = KingdomPhysicalPhase.Settled;
			job.Outbox = new KingdomConstructionOutbox
			{
				EventId = "construction:" + job.Id + ":paved", Mode = 1,
				ChronicleState = KingdomConstructionSinkDisposition.Skipped,
				LedgerState = KingdomConstructionSinkDisposition.Skipped,
				MessageState = KingdomConstructionSinkDisposition.Skipped,
				DeedState = KingdomConstructionSinkDisposition.Skipped
			};
			Assert.IsTrue(KingdomConstructionRules.UpdateInputReceipt(ref job, receipt));
			string finalHash = job.InputReceiptHash;
			List<KingdomConstructionJob> normalized;
			Assert.IsTrue(KingdomConstructionRules.TryNormalize(
				new List<KingdomConstructionJob> { job }, out normalized));
			Assert.IsTrue(normalized[0].Compacted);
			Assert.IsNull(normalized[0].InputReceipt);
			Assert.AreEqual(finalHash, normalized[0].InputReceiptHash);
			Assert.IsTrue(KingdomConstructionRules.ValidJob(normalized[0]));
		}

		private static KingdomConstructionJob NewJob()
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			materials.Set(KingdomMaterial.Timber, 1);
			KingdomMaterialDebitCost cost = new KingdomMaterialDebitCost(materials, null, null);
			return new KingdomConstructionJob
			{
				Id = "10000000000000000000000000000001",
				OwnerKey = KingdomConstructionRules.OwnerKey("realm", 7L, "settlement"),
				ZoneId = "target-zone", Route = KingdomConstructionRoute.RoadPaving,
				Phase = KingdomConstructionPhase.Published,
				Projection = KingdomConstructionProjection.Paving, X = 12, Y = 9,
				SubjectId = "road", SourceId = "road", TargetKey = "road-target",
				Payload = "payload", CreatedTick = 10L, StartedTick = 10L,
				DueTick = 20L, UpdatedTick = 10L, Revision = 1,
				Claims = KingdomConstructionRules.NewClaims(0, cost)
			};
		}

		private static KingdomConstructionInputReceipt NewReceipt(KingdomConstructionJob job)
		{
			KingdomConstructionInputIntent intent;
			string digest;
			Assert.IsTrue(KingdomConstructionRules.TryInputIntent(job, 0,
				job.Claims.MaterialOutstanding, out intent, out digest));
			KingdomConstructionInputSourceLine source = new KingdomConstructionInputSourceLine(
				0, "material-line", KingdomConstructionInputKind.Material,
				job.Claims.MaterialOutstanding, "settlement", "source-zone", "holder",
				"mat-1", KingdomConstructionInputTopology.ContainerInventory, 2, 3,
				"Wood", 1, 1, 0, 1, 0, 0, 0, 4, 0, null,
				KingdomConstructionInputSourcePhase.Reserved, null, null, null, 0);
			KingdomConstructionInputCargoLine cargo = new KingdomConstructionInputCargoLine(
				0, "cargo-0", "marker-0", KingdomConstructionInputKind.Material,
				job.Claims.MaterialOutstanding, 1, "Wood", 1, 0, "mat-1", 101, 101,
				null, KingdomConstructionInputCargoPhase.Planned,
				KingdomConstructionInputTopology.Invalid, null, null, -1, -1,
				null, null, 0, 0);
			KingdomConstructionInputChild child = new KingdomConstructionInputChild(0,
				101, 101, 0, 1, KingdomConstructionInputCargoShape.OpaqueObjectManifest,
				11, null, "source-zone", 2, 3, 12, null, job.ZoneId, 12, 9, 30L,
				new string('a', 64), 0, 0L);
			KingdomConstructionInputReceipt receipt;
			KingdomConstructionInputFault fault;
			Assert.IsTrue(KingdomConstructionInputRules.TryCreate("receipt-1", job.Id,
				job.OwnerKey, 7L, job.ZoneId, 12, 9, digest, null, 0,
				job.Claims.MaterialOutstanding, 0, 1, 0, 0, EmptyClaim(), EmptyClaim(),
				new[] { source }, new[] { cargo }, new[] { child }, out receipt, out fault),
				fault.ToString());
			return receipt;
		}

		private static KingdomConstructionInputReceipt DriveToClosing(
			KingdomConstructionInputReceipt receipt, string jobId, string targetZone)
		{
			if (receipt.TxPhase == KingdomConstructionInputTxPhase.ReservationPrepared)
				receipt = Tx(receipt, KingdomConstructionInputTxPhase.Reserved);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.SourcePending);
			receipt = CargoMove(receipt, KingdomConstructionInputCargoPhase.AtSource,
				"mat-1", KingdomConstructionInputTopology.ContainerInventory, "holder",
				"source-zone", 2, 3, 0);
			receipt = Source(receipt, KingdomConstructionInputSourcePhase.TransferIntent);
			receipt = Source(receipt, KingdomConstructionInputSourcePhase.Debited);
			receipt = Cargo(receipt, KingdomConstructionInputCargoPhase.PickupIntent);
			receipt = CargoMove(receipt, KingdomConstructionInputCargoPhase.InFlight,
				"mat-1", KingdomConstructionInputTopology.CarrierInventory, "carrier",
				"source-zone", 2, 3, 0);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.Routing);
			receipt = CargoMove(receipt, KingdomConstructionInputCargoPhase.Landed,
				"mat-1", KingdomConstructionInputTopology.LandingEscrow, "landing",
				targetZone, 12, 9, 0);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.LandedAwaitingOwner);
			receipt = Tx(receipt, KingdomConstructionInputTxPhase.DebitPending);
			receipt = Cargo(receipt, KingdomConstructionInputCargoPhase.DebitIntent);
			receipt = CargoMove(receipt, KingdomConstructionInputCargoPhase.Spent,
				"mat-1", KingdomConstructionInputTopology.Consumed, jobId,
				targetZone, 12, 9, 1);
			receipt = Source(receipt, KingdomConstructionInputSourcePhase.Spent);
			return Tx(receipt, KingdomConstructionInputTxPhase.Closing);
		}

		private static KingdomConstructionInputReceipt Tx(KingdomConstructionInputReceipt value,
			KingdomConstructionInputTxPhase next)
		{
			KingdomConstructionInputReceipt updated; KingdomConstructionInputFault fault;
			Assert.IsTrue(KingdomConstructionInputRules.TryTransitionTransaction(value,
				value.Revision, value.TxPhase, next, out updated, out fault), fault.ToString());
			return updated;
		}

		private static KingdomConstructionInputReceipt Source(
			KingdomConstructionInputReceipt value, KingdomConstructionInputSourcePhase next)
		{
			KingdomConstructionInputReceipt updated; KingdomConstructionInputFault fault;
			Assert.IsTrue(KingdomConstructionInputRules.TryTransitionSource(value,
				value.Revision, 0, value.SourceAt(0).Phase, next, out updated, out fault), fault.ToString());
			return updated;
		}

		private static KingdomConstructionInputReceipt Cargo(
			KingdomConstructionInputReceipt value, KingdomConstructionInputCargoPhase next)
		{
			KingdomConstructionInputReceipt updated; KingdomConstructionInputFault fault;
			Assert.IsTrue(KingdomConstructionInputRules.TryTransitionCargo(value,
				value.Revision, 0, value.CargoAt(0).Phase, next, out updated, out fault), fault.ToString());
			return updated;
		}

		private static KingdomConstructionInputReceipt CargoMove(
			KingdomConstructionInputReceipt value, KingdomConstructionInputCargoPhase next,
			string objectId, KingdomConstructionInputTopology topology, string owner,
			string zone, int x, int y, int spent)
		{
			KingdomConstructionInputReceipt updated; KingdomConstructionInputFault fault;
			Assert.IsTrue(KingdomConstructionInputRules.TryTransitionCargoWithEvidence(value,
				value.Revision, 0, value.CargoAt(0).Phase, next, objectId, topology,
				owner, zone, x, y, null, null, spent, 0, out updated, out fault), fault.ToString());
			return updated;
		}

		private static string EmptyClaim()
		{
			return new KingdomMaterialDebitCost().ToClaimString();
		}
	}
}
#endif
