#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomLabCivicRuntimeSourceTests
	{
		[Test]
		public void CanonicalOwnerIsExactActiveGroundAuthority()
		{
			string ownership = Source("Growth/KingdomLabCivicOwnership.cs");
			StringAssert.Contains("SettlementIdForOwnedZone", ownership);
			StringAssert.Contains("settlement != System.CurrentSettlementId", ownership);
			StringAssert.Contains("CivicWorks(Survey)", ownership);
			StringAssert.Contains("result.Sort((a, b) => string.CompareOrdinal(" +
				"a.IDIfAssigned, b.IDIfAssigned))",
				ownership);
			StringAssert.Contains("matches > 1", ownership);
			StringAssert.Contains("AllowClaim", ownership);
			StringAssert.Contains("ObserveMissingOwner", ownership);
			StringAssert.Contains("TryPublishOwners(raw, next", ownership);
			StringAssert.DoesNotContain("GetZone(", ownership);
			StringAssert.DoesNotContain("ZoneManager", ownership);
			StringAssert.DoesNotContain("GameObject.FindByID", ownership);

			string carrier = Source("Growth/r_KingdomLabCivicFriction.cs");
			StringAssert.Contains("CanBeReplicatedEvent", carrier);
			StringAssert.Contains("CanGenerateStacked()", carrier);
			StringAssert.Contains("FinalizeCopy", carrier);
			StringAssert.Contains("ParentObject?.RemovePart(this)", carrier);
		}

		[Test]
		public void SavantCauseUsesExactLodgeRowHomeCreedsAndFrozenTaste()
		{
			string selection = Source("Growth/KingdomLab.CivicSelection.cs");
			StringAssert.Contains("KingdomGuestbook.LodgeReceiptProperty", selection);
			StringAssert.Contains("KingdomPurpose.IsLodgedSpecialist", selection);
			StringAssert.Contains("KingdomResidents.IdOf(Body) == Row.ResidentId", selection);
			StringAssert.Contains("name == Row.Name", selection);
			StringAssert.Contains("!Lodge.StartsWith(\"intent:\"", selection);
			StringAssert.Contains("NotableLodgeReceipt(R.NotableLodgeReceiptId)",
				Source("Growth/KingdomLabCivicRules.Validation.cs"));
			StringAssert.Contains("const string prefix = \"taf:operation:\"", Source(
				"Growth/KingdomLabCivicRules.Validation.cs"));
			StringAssert.Contains("StringComparison.OrdinalIgnoreCase", selection);
			StringAssert.Contains("KingdomLodging.TryLabHome", selection);
			StringAssert.Contains("KingdomCeremonyRules.ChooseTastes", selection);
			StringAssert.Contains("(ulong)row.ArrivedTick", selection);
			StringAssert.Contains("int taste = tastes[0]", selection);
			StringAssert.Contains("lodge, row.ArrivedTick, taste, tasteTag", selection);
		}

		[Test]
		public void ShrineGuardBindsOneTargetReceiptAndPrecedesFaithMutation()
		{
			string interaction = Source("Growth/KingdomLab.CivicInteraction.cs");
			StringAssert.Contains("receipt.TargetObjectId != Target?.IDIfAssigned", interaction);
			StringAssert.Contains("receipt.EventId", interaction);
			StringAssert.Contains("receipt.CauseDigest.Substring(0, 12)", interaction);
			StringAssert.Contains("No other faith action is blocked", interaction);
			StringAssert.Contains("exact lodged cause or hall owner is gone", interaction);

			string faith = KingdomFaithLogicalSource.Read();
			int guard = faith.IndexOf("KingdomLabCivicRuntime.BlocksConsecration",
				StringComparison.Ordinal);
			int candidates = faith.IndexOf("KingdomCreed.Candidates(System)", guard,
				StringComparison.Ordinal);
			int mutation = faith.IndexOf("target.SetStringProperty(ShrineCreedProperty",
				guard, StringComparison.Ordinal);
			Assert.GreaterOrEqual(guard, 0);
			Assert.Greater(candidates, guard);
			Assert.Greater(mutation, candidates);
		}

		[Test]
		public void RehouseIsExactCasWithCrashRecoveryAndNoOtherAssignmentWrites()
		{
			string interaction = Source("Growth/KingdomLab.CivicInteraction.cs");
			string lodging = Source("Growth/KingdomLodging.LabFriction.cs");
			StringAssert.Contains("held != receipt.SourcePlotId && held != receipt.TargetPlotId",
				interaction);
			StringAssert.Contains("moved to a third plot", interaction);
			StringAssert.Contains("receipt.TargetHomeObjectId", interaction);
			StringAssert.Contains("string.Equals(held, ExpectedTargetPlot", lodging);
			StringAssert.Contains("if (Home != null) { Home = null; return false; }", lodging);
			StringAssert.Contains("plotMatches != 1 || idMatches != 1", lodging);
			StringAssert.Contains("recovered.IDIfAssigned, ExpectedTargetObjectId", lodging);
			StringAssert.Contains("TryPrepareLabRehouse", lodging);
			StringAssert.Contains("target?.IDIfAssigned, ExpectedTargetObjectId", lodging);
			Assert.AreEqual(1, Count(lodging,
				"Resident.SetStringProperty(HomePlotIdProperty, ExpectedTargetPlot)"));
			StringAssert.DoesNotContain("SetStringProperty(HomePlotIdProperty, null", lodging);
		}

		[Test]
		public void AuthoredRefusalReusesWarnedArrestableEmigrationOwner()
		{
			string selection = Source("Growth/KingdomLab.CivicSelection.cs");
			string projection = Source("Growth/KingdomLab.CivicDepartureProjection.cs");
			string lodging = KingdomLodgingLogicalSource.Read();
			StringAssert.Contains("GetPropertyOrTag(", selection);
			StringAssert.Contains("KingdomQolRules.RefusesTagName", selection);
			StringAssert.Contains("KingdomQolRules.Has(offer, authored[tag])", selection);
			StringAssert.Contains("KingdomReach.Reaches(System, Z, Owner, home)", selection);
			StringAssert.Contains("StartLabRoofBrink", projection);
			StringAssert.Contains("LawfullyRehoused", CivicRuntime());
			StringAssert.Contains("KingdomBrink.Lift", lodging);
			StringAssert.Contains("KingdomBrink.Unsay", lodging);
			StringAssert.Contains("KingdomGrowth.Emigrate", lodging);
			StringAssert.Contains("ObserveDeparture", lodging);
			StringAssert.DoesNotContain("Obliterate", CivicRuntime());
			StringAssert.DoesNotContain("TryDepart(", CivicRuntime());
			StringAssert.DoesNotContain("Departures++", CivicRuntime());
		}

		[Test]
		public void RefusalCauseUsesCurrentPhysicalProvidersNotCataloguePromises()
		{
			string civic = CivicRuntime();
			StringAssert.Contains("Survey.TryBenefits(out KingdomBenefitIndex benefits",
				civic);
			StringAssert.Contains("benefits.TagsForRoot(Owner.IDIfAssigned)", civic);
			StringAssert.Contains("unresolved physical laboratory benefit", civic);
			StringAssert.DoesNotContain("KingdomQol.OfferOf(", civic);
		}

		[Test]
		public void ReceiptsAreVisibleRetryableAndNeverReofferedOrRewarded()
		{
			string runtime = Source("Growth/KingdomLab.CivicRuntime.cs");
			string receipts = Source("Growth/KingdomLab.CivicReceipts.cs");
			string interaction = Source("Growth/KingdomLab.CivicInteraction.cs");
			string carrier = Source("Growth/r_KingdomLabCivicFriction.cs");
			StringAssert.Contains("Reconcile(System, Z, Survey, owner, part)", runtime);
			int reconcile = runtime.IndexOf("Reconcile(System", StringComparison.Ordinal);
			int fence = runtime.IndexOf("if (!allowNew) return", StringComparison.Ordinal);
			Assert.Greater(fence, reconcile);
			StringAssert.Contains("Empty(part.SavantPrice)", runtime);
			StringAssert.Contains("Empty(part.RefusalDeparture)", runtime);
			StringAssert.Contains("KingdomChronicle.RecordOnce", receipts);
			StringAssert.Contains("GetShortDescriptionEvent", carrier);
			StringAssert.Contains("This carries no reward, standing, value, or hidden grievance",
				interaction);

			string civic = CivicRuntime();
			string[] forbidden =
			{
				"AdjustStanding(", "AddXP(", "UseEnergy(", "Stat.Random",
				"TakeWater(", "GetZone(", "ZoneManager", "GameObject.Create("
			};
			for (int i = 0; i < forbidden.Length; i++)
				StringAssert.DoesNotContain(forbidden[i], civic, forbidden[i]);
		}

		[Test]
		public void InterruptionObstructionAndQuietRecoveryKeepExactEvidence()
		{
			string runtime = Source("Growth/KingdomLab.CivicRuntime.cs");
			int start = runtime.IndexOf("private static bool StartDeparture",
				StringComparison.Ordinal);
			int end = runtime.IndexOf("internal static bool RefusesHome", start,
				StringComparison.Ordinal);
			string departure = runtime.Substring(start, end - start);
			int preflight = departure.IndexOf("CanStampMarker(resident, Receipt",
				StringComparison.Ordinal);
			int receipt = departure.IndexOf("Part.Stamp(Receipt)",
				StringComparison.Ordinal);
			int recovery = departure.IndexOf("TryCompleteDepartureProjection(System",
				receipt, StringComparison.Ordinal);
			Assert.GreaterOrEqual(preflight, 0);
			Assert.Greater(receipt, preflight);
			Assert.Greater(recovery, receipt);

			string projection = Source("Growth/KingdomLab.CivicDepartureProjection.cs");
			int classify = projection.IndexOf("DepartureProjection(Resident, Receipt)",
				StringComparison.Ordinal);
			int exact = projection.IndexOf("ExactDepartureCause(System", classify,
				StringComparison.Ordinal);
			int marker = projection.IndexOf("StampMarker(Resident, Receipt)", exact,
				StringComparison.Ordinal);
			int revalidate = projection.IndexOf("ExactDepartureCause(System", marker,
				StringComparison.Ordinal);
			int clear = projection.IndexOf("SetStringProperty(KingdomLodging.HomePlotIdProperty, null)",
				revalidate, StringComparison.Ordinal);
			int readback = projection.IndexOf("!string.IsNullOrEmpty(Resident.GetStringProperty(",
				clear, StringComparison.Ordinal);
			int cohabitation = projection.IndexOf("KingdomConversion.ForgetCohabitation(Resident)",
				readback, StringComparison.Ordinal);
			int brink = projection.IndexOf("KingdomLodging.StartLabRoofBrink", cohabitation,
				StringComparison.Ordinal);
			Assert.Greater(exact, classify);
			Assert.Greater(marker, exact);
			Assert.Greater(revalidate, marker);
			Assert.Greater(clear, revalidate);
			Assert.Greater(readback, clear);
			Assert.Greater(cohabitation, readback,
				"both a fresh projection and a retry after HomePlotId cleared invalidate the cache");
			Assert.Greater(brink, cohabitation);

			string receipts = Source("Growth/KingdomLab.CivicReceipts.cs");
			int terminal = receipts.IndexOf("Part.Stamp(after)", StringComparison.Ordinal);
			int cleanup = receipts.IndexOf("TryCompleteClosedDeparture", terminal,
				StringComparison.Ordinal);
			int closeRecord = receipts.IndexOf("RecordClose(System, Part, after.Kind)",
				cleanup, StringComparison.Ordinal);
			Assert.Greater(cleanup, terminal);
			Assert.Greater(closeRecord, cleanup);
			StringAssert.Contains("ReconcileClosedDeparture(System", Source(
				"Growth/KingdomLab.CivicReconciliation.cs"));

			string interaction = Source("Growth/KingdomLab.CivicInteraction.cs");
			int choice = interaction.IndexOf("part.Stamp(chosen)",
				StringComparison.Ordinal);
			int apply = interaction.IndexOf("TryResolveRehouse(System", choice,
				StringComparison.Ordinal);
			Assert.Greater(apply, choice,
				"the durable granted intent must precede physical rehouse mutation");
			StringAssert.Contains("The exact promise remains prepared", interaction);
			StringAssert.Contains("moved to a third plot", interaction);

			string reconciliation = Source("Growth/KingdomLab.CivicReconciliation.cs");
			int projectedMarker = reconciliation.IndexOf(
				"DepartureProjection(resident, receipt)", StringComparison.Ordinal);
			int recoveredCause = reconciliation.IndexOf(
				"ExactDepartureCause(System", projectedMarker, StringComparison.Ordinal);
			Assert.GreaterOrEqual(projectedMarker, 0);
			Assert.Greater(recoveredCause, projectedMarker,
				"resident marker/home projection must classify before cause recovery");
			StringAssert.Contains("MarkerMatches(GameObject Resident",
				Source("Growth/KingdomLab.CivicReceipts.cs"));
			StringAssert.Contains("ClassifyDepartureProjection(Receipt", projection);
			StringAssert.Contains("RefusalEventProperty", projection);
			StringAssert.Contains("RefusalOwnerProperty", projection);
			StringAssert.Contains("RefusalDigestProperty", projection);
			StringAssert.Contains("LawfullyRehoused", reconciliation);
			StringAssert.Contains("unproved or still-reached replacement roof", reconciliation);
			StringAssert.Contains("targetMatch == KingdomLabObjectMatch.Missing", reconciliation);
			StringAssert.Contains("targetMatch == KingdomLabObjectMatch.Duplicate", reconciliation);
			StringAssert.Contains("targetMatch == KingdomLabObjectMatch.Missing", interaction);
			StringAssert.Contains("targetMatch == KingdomLabObjectMatch.Duplicate", interaction);
			StringAssert.Contains("KingdomLabCivicClosure.CauseGone", interaction);
			StringAssert.DoesNotContain("Popup", reconciliation);
			StringAssert.DoesNotContain("MessageQueue", reconciliation);
		}

		[Test]
		public void RemovalCoverageAndShardBoundsIncludeEveryNewCarrier()
		{
			string coverage = Source("Core/KingdomRemovalCoverage.cs");
			string generated = Source("Core/KingdomRemovalCoverage.Generated.cs");
			StringAssert.Contains("\"r_KingdomLabCivicFriction\"", coverage);
			StringAssert.Contains("\"r_TAF_LabCivicOwners_v1\"", coverage);
			StringAssert.Contains("\"r_TAF_LabRefusalDigest_v1\"", generated);
			StringAssert.Contains("\"r_TAF_LabRefusalEvent_v1\"", generated);
			StringAssert.Contains("\"r_TAF_LabRefusalOwner_v1\"", generated);

			string[] files =
			{
				"Growth/KingdomLabCivicEnums.cs",
				"Growth/KingdomLabCivicReceipt.cs",
				"Growth/KingdomLabCivicOwnerBook.cs",
				"Growth/KingdomLabCivicRules.Identity.cs",
				"Growth/KingdomLabCivicRules.Validation.cs",
				"Growth/KingdomLabCivicRules.Transitions.cs",
				"Growth/KingdomLabCivicRules.Prose.cs",
				"Growth/KingdomLabCivicOwnerRules.cs",
				"Growth/KingdomLabCivicOwnership.cs",
				"Growth/KingdomLab.CivicSelection.cs",
				"Growth/KingdomLab.CivicRuntime.cs",
				"Growth/KingdomLab.CivicDepartureProjection.cs",
				"Growth/KingdomLab.CivicReconciliation.cs",
				"Growth/KingdomLab.CivicReceipts.cs",
				"Growth/KingdomLab.CivicInteraction.cs",
				"Growth/KingdomLodging.LabFriction.cs",
				"Growth/r_KingdomLabCivicFriction.cs"
			};
			for (int i = 0; i < files.Length; i++)
				Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
					files[i])).Length, 300, files[i]);
		}

		private static string CivicRuntime()
		{
			string[] files =
			{
				"Growth/KingdomLabCivicOwnership.cs",
				"Growth/KingdomLab.CivicSelection.cs",
				"Growth/KingdomLab.CivicRuntime.cs",
				"Growth/KingdomLab.CivicDepartureProjection.cs",
				"Growth/KingdomLab.CivicReconciliation.cs",
				"Growth/KingdomLab.CivicReceipts.cs",
				"Growth/KingdomLab.CivicInteraction.cs",
				"Growth/KingdomLodging.LabFriction.cs"
			};
			string source = "";
			for (int i = 0; i < files.Length; i++) source += Source(files[i]);
			return source;
		}

		private static int Count(string SourceText, string Token)
		{
			int count = 0;
			int cursor = 0;
			while ((cursor = SourceText.IndexOf(Token, cursor,
				StringComparison.Ordinal)) >= 0)
			{
				count++;
				cursor += Token.Length;
			}
			return count;
		}

		private static string Source(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}
	}
}
#endif
