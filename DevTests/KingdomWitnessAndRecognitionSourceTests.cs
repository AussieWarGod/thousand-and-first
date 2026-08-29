#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Tests;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomWitnessAndRecognitionSourceTests
	{
		[Test]
		public void ExplicitAdapterUsesVerifiedObjectSurfaceAndNeverScansOrMutatesCustody()
		{
			string source = TestMain.ReadRepositoryText(
				"Core/KingdomArtifactRecognitionRuntime.cs");
			StringAssert.Contains("GameObject Selected", source);
			StringAssert.Contains("Selected.ID", source);
			StringAssert.Contains("Selected.Blueprint", source);
			StringAssert.Contains("Selected.ShortDisplayNameStripped", source);
			StringAssert.Contains("Selected.Physics?.Owner", source);
			Assert.IsFalse(source.Contains("Inventory"));
			Assert.IsFalse(source.Contains("GetObjects"));
			Assert.IsFalse(source.Contains("TakeObject"));
			Assert.IsFalse(source.Contains("RemoveObject"));
			Assert.IsFalse(source.Contains("Physics.Owner ="));
			Assert.IsFalse(source.Contains("Journal"));
		}

		[Test]
		public void FixedWitnessAndRecognitionRulesExposeNoEconomyOrJournalSurface()
		{
			string witness = TestMain.ReadRepositoryText("Experience/KingdomWitnessWorkRules.cs");
			string recognition = TestMain.ReadRepositoryText(
				"Core/KingdomArtifactRecognitionRules.cs");
			StringAssert.Contains("Portable = false", witness);
			StringAssert.Contains("CommerceValue = 0", witness);
			StringAssert.Contains("CommerceValue = 0", recognition);
			StringAssert.Contains("CustodyClaimed = false", recognition);
			Assert.IsFalse((witness + recognition).Contains("Journal"));
			Assert.IsFalse((witness + recognition).Contains("Inventory"));
		}

		[Test]
		public void QudApiEvidenceIsPinnedToArchivedBuild()
		{
			string runtime = TestMain.ReadRepositoryText(
				"Core/KingdomArtifactRecognitionRuntime.cs");
			StringAssert.Contains("Qud 2.0.211.51 API evidence", runtime);
			StringAssert.Contains("GameObject.cs lines 424-463", runtime);
			StringAssert.Contains("749-760", runtime);
			StringAssert.Contains("4942-4988", runtime);
			StringAssert.Contains("8945-8954", runtime);
		}

		[Test]
		public void ClosedRaisingReachesExplicitReceiptFirstCharterFlow()
		{
			string physical = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomPhysicalHappenings.00.QueueOpenAndDrive.cs")
				+ TestMain.ReadRepositoryText(
					"Simulation/City/KingdomPhysicalHappenings.08.WitnessWork.cs");
			string runtime = TestMain.ReadRepositoryText(
				"Experience/KingdomWitnessWorkCharterRuntime.Commit.cs");
			int clear = physical.IndexOf("if (!Clear(book, lifecycle, operation.EventId))",
				StringComparison.Ordinal);
			int capture = physical.IndexOf("CaptureClosedWitness(system, operation, nowTick)",
				clear, StringComparison.Ordinal);
			Assert.Greater(capture, clear, "only a committed close may become an O5 source");
			StringAssert.Contains("KingdomWorkKind.Construction", physical);
			StringAssert.Contains("RestorationSettled", physical);
			StringAssert.Contains("TryPreparePlanned", runtime);
			StringAssert.Contains("TryReadBackRow", runtime);
			StringAssert.Contains("TryAttachPrepared", runtime);
			StringAssert.Contains("TryObserve", runtime);
			Assert.Less(runtime.IndexOf("TryReadBackRow", StringComparison.Ordinal),
				runtime.IndexOf("TryAttachPrepared", StringComparison.Ordinal));
			Assert.IsFalse((physical + runtime).Contains("Journal"));
			Assert.IsFalse(runtime.Contains("Inventory"));
			Assert.IsFalse(runtime.Contains("AddObject"));
		}

		[Test]
		public void EveryOfferClosesAndMovedCarrierLosesAuthorityBeforeOwnedDetach()
		{
			string charter = TestMain.ReadRepositoryText(
				"Experience/KingdomWitnessWorkCharterRuntime.cs");
			string recovery = TestMain.ReadRepositoryText(
				"Experience/KingdomWitnessWorkCharterRuntime.Recovery.cs");
			string readback = TestMain.ReadRepositoryText(
				"Experience/KingdomWitnessWorkProjectionRuntime.Readback.cs");
			StringAssert.Contains("Decline one closed account", charter);
			StringAssert.Contains("TryDeclinePlanned", charter);
			StringAssert.Contains("Ground.Memory, Lease", charter);
			StringAssert.Contains("KingdomWitnessWorkPhase.Declined", charter);
			StringAssert.Contains("KingdomGovernanceScope.Commit(\"decline fixed witness work\")",
				charter);
			int receiptMiss = recovery.IndexOf("if (!found)", StringComparison.Ordinal);
			int loss = recovery.IndexOf("TryReconcile", receiptMiss, StringComparison.Ordinal);
			int idOnly = recovery.IndexOf("ExactObjectLoaded", loss, StringComparison.Ordinal);
			int fresh = recovery.IndexOf("TryReadBackRow", idOnly, StringComparison.Ordinal);
			int detach = recovery.IndexOf("TryDetach", fresh, StringComparison.Ordinal);
			Assert.Greater(loss, receiptMiss);
			Assert.Greater(idOnly, loss);
			Assert.Greater(fresh, idOnly);
			Assert.Greater(detach, fresh);
			StringAssert.Contains("TryFindUnique", readback);
			StringAssert.Contains("Duplicate physical identity", readback);
			StringAssert.Contains("MarkerOwnsReceipt", readback);
			StringAssert.Contains("carrier.RemovePart(marker)", readback);
			StringAssert.Contains("Marker.FieldsAuthenticated()", readback);
			StringAssert.Contains("if (marker == null) return true", readback);
			int diverged = recovery.IndexOf(
				"observation == KingdomWitnessCarrierObservation.Diverged",
				StringComparison.Ordinal);
			int divergedLoss = recovery.IndexOf("TryReconcile", diverged,
				StringComparison.Ordinal);
			int divergedFresh = recovery.IndexOf("TryReadBackRow", divergedLoss,
				StringComparison.Ordinal);
			int divergedDetach = recovery.IndexOf("TryDetach", divergedFresh,
				StringComparison.Ordinal);
			Assert.Greater(divergedLoss, diverged);
			Assert.Greater(divergedFresh, divergedLoss);
			Assert.Greater(divergedDetach, divergedFresh);
			int owns = readback.IndexOf("if (!MarkerOwnsReceipt", StringComparison.Ordinal);
			int remove = readback.IndexOf("carrier.RemovePart(marker)", owns,
				StringComparison.Ordinal);
			Assert.Greater(remove, owns,
				"foreign or unauthenticated marker must refuse before removal");
			Assert.IsFalse((recovery + readback).Contains("MoveTo"));
			Assert.IsFalse((recovery + readback).Contains("AddObject"));
			Assert.IsFalse((recovery + readback).Contains("RemoveObject"));
		}
	}
}
#endif
