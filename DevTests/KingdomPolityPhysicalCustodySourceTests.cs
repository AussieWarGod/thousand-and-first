#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityPhysicalCustodySourceTests
	{
		[Test]
		public void SpawnUsesDistinctBoundedPlayerRouteCellsAndNeverLoadsRemoteZones()
		{
			string locator = Read("KingdomPolityEndpointRuntime.Locator.cs");
			StringAssert.Contains("HashSet<Cell> reserved", locator);
			StringAssert.Contains("maximumRadius = 12", locator);
			StringAssert.Contains("CandidateCellAllowed", locator);
			StringAssert.Contains("new FindPath(Ingress, Candidate", locator);
			StringAssert.Contains("PathGlobal: false", locator);
			StringAssert.Contains("IgnoreCreatures: true", locator);
			StringAssert.DoesNotContain("ZoneManager", locator);
			StringAssert.DoesNotContain("GetZone", locator);
		}

		[Test]
		public void GearReceiptAndCallbackAftermathBindEveryFrozenAuthorityField()
		{
			string npc = Read("KingdomPolityNpcRuntime.Gear.cs");
			string declarations = Read("KingdomPolityNpcRuntime.cs");
			StringAssert.Contains("ReceiveObject(item, NoStack: true", npc);
			AssertBefore(npc, "ReceiveObject(item, NoStack: true", "bool exactReceived");
			StringAssert.Contains("ReferenceEquals(item.InInventory, Body)", npc);
			StringAssert.Contains("ReferenceEquals(receivedResident, item)", npc);
			AssertBefore(npc, "Body.AutoEquip(item", "ExactGear(item");
			foreach (string field in new[] { "GearReceiptProperty", "GearRealmProperty",
				"GearCohortProperty", "GearProjectionProperty", "GearBodyProperty",
				"GearProfileProperty", "GearMemberOrdinalProperty", "GearOrdinalProperty" })
				StringAssert.Contains(field, declarations + npc);
			StringAssert.Contains("ContestedProperty", declarations + npc);
			StringAssert.Contains("TryMarkContestedPreparedBody", Read("KingdomPolityEndpointRuntime.cs"));
			StringAssert.Contains("ContestedWitnessKey", Read("KingdomPolityEndpointRuntime.Locator.cs"));
		}

		[Test]
		public void RecursiveCustodyPreservesForeignAndDeletesOnlyReprovedExactGear()
		{
			string proof = Read("KingdomPolityEndpointRuntime.CustodyProof.cs");
			string transfer = Read("KingdomPolityEndpointRuntime.CustodyTransfer.cs");
			StringAssert.Contains("TryBuildCustodyNode(children[i], Item", proof);
			StringAssert.Contains("HasAnyGearMark", proof);
			StringAssert.Contains("ClassifyCustody", proof);
			StringAssert.Contains("SeenGear[gear]", proof);
			StringAssert.Contains("TransferCrossesOwnedBoundary", transfer);
			AssertBefore(transfer, "TryProcessCustodyNode(Node.Children[i]",
				"TryRemoveExactGear(Node.Object");
			StringAssert.Contains("KingdomPolityNpcRuntime.ExactGear", transfer);
			StringAssert.Contains("TryFindResidentObject(expected", transfer);
			StringAssert.Contains("NoStack: true", transfer);
		}

		[Test]
		public void DeathUsesDurableExactIntentAndRemovalCallbacksBeforeAnyConsequence()
		{
			string body = Read("r_KingdomPolityCohortBody.cs");
			string death = Read("KingdomPolityEndpointRuntime.Death.cs");
			StringAssert.Contains("EarlyBeforeDeathRemovalEvent.ID", body);
			StringAssert.Contains("BeforeDestroyObjectEvent.ID", body);
			StringAssert.Contains("OnDestroyObjectEvent.ID", body);
			StringAssert.Contains("ExactPhysicalDeathBinding", death);
			StringAssert.Contains("Body.CurrentCell.IsVisible() && Body.IsVisible()", death);
			StringAssert.Contains("ReferenceEquals(Witness.Body, Body)", death);
			StringAssert.Contains("TryWriteRemovalWitness", death);
			StringAssert.DoesNotContain("TryResolveCommittedDeathIntent(", body);
			StringAssert.Contains("DeathCallbackInFlight = true", body);
			StringAssert.DoesNotContain("WitnessCohortDeath(", body);
			StringAssert.DoesNotContain("KingdomPolityActiveRuntime", body);
		}

		[Test]
		public void CleanupResumesNthFailureAndClearsOnlyExactWitnessSlotsAfterCas()
		{
			string runtime = Read("KingdomPolityEndpointRuntime.cs");
			string witness = Read("KingdomPolityEndpointRuntime.RemovalWitness.cs");
			StringAssert.Contains("RemovalCanContinue", Read("KingdomPolityEndpointRuntime.Cleanup.cs"));
			StringAssert.Contains("RemovalWitnessKey(ProjectionId, ObjectId)", witness);
			StringAssert.Contains("RemoveZoneProperty(key)", witness);
			StringAssert.DoesNotContain("StartsWith", witness);
			int commit = runtime.IndexOf("TryCommitEndpointCleanup", StringComparison.Ordinal);
			int clearAfterCommit = runtime.IndexOf("TryClearCohortRemovalWitnesses", commit,
				StringComparison.Ordinal);
			int release = runtime.IndexOf("TryReleaseForCohort", clearAfterCommit,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(commit, 0); Assert.Greater(clearAfterCommit, commit);
			Assert.Greater(release, clearAfterCommit);
			int cleanup = runtime.IndexOf("TryCleanupCurrentEndpoint", StringComparison.Ordinal);
			int cleaned = runtime.IndexOf("cohort.Phase == KingdomPolityCohortPhase.Cleaned",
				cleanup, StringComparison.Ordinal);
			int concluded = runtime.IndexOf("cohort.Phase != KingdomPolityCohortPhase.Concluded",
				cleaned, StringComparison.Ordinal);
			Assert.GreaterOrEqual(cleaned, cleanup); Assert.Greater(concluded, cleaned);
		}

		[Test]
		public void AmbientWordsClaimNoUnprovedRoadTransportOrDeparture()
		{
			string ambient = Read("KingdomPolityVisitInteraction.Ambient.cs");
			StringAssert.DoesNotContain("claimed road boundary", ambient);
			StringAssert.DoesNotContain("market route", ambient);
			StringAssert.DoesNotContain("prepares to depart", ambient);
			StringAssert.Contains("No unseen safety, road, journey, or offscreen outcome", ambient);
			StringAssert.Contains("No journey or offscreen result is inferred", ambient);
			StringAssert.Contains("No resident, citizenship, row, or body binding", ambient);
		}

		private static string Read(string name) =>
			TestMain.ReadRepositoryText(Path.Combine("Polity", name));

		private static void AssertBefore(string source, string earlier, string later)
		{
			int a = source.IndexOf(earlier, StringComparison.Ordinal);
			int b = source.IndexOf(later, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0, earlier); Assert.Greater(b, a, later);
		}
	}
}
#endif
