#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source wiring only; executable report/retirement law and native cuts are separate.</summary>
	public sealed class KingdomSubsidenceReportLossRuntimeSourceTests
	{
		[Test]
		public void FailedCallAloneNeverAuthorizesChronicleLoss()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomSubsidenceStepRuntime.ReportDelivery.cs");
			StringAssert.Contains("KingdomChronicle.TryProveLostOnceAt(", source);
			StringAssert.Contains("KingdomSubsidenceReportRules.TryLoseChronicle(", source);
			StringAssert.Contains("KingdomSubsidenceReportRules.Settled(report)", source);
			StringAssert.Contains("entry.ChronicleProved || entry.ChronicleLost", source);
			string observer = TestMain.ReadRepositoryText("Chronicle/KingdomChronicle.At.cs");
			StringAssert.Contains("receipt.Fingerprint == fingerprint", observer);
			StringAssert.Contains("KingdomChronicleReceiptRules.IsTerminal(receipt)", observer);
			StringAssert.Contains("!receipt.LegacyBlocked", observer);
			StringAssert.Contains("migrated || !AtRegistryCanonical(raw, rows)", observer);
		}

		[Test]
		public void BothRetirementSitesRetainFailedEvidenceBeforeDiscardingTheirOwner()
		{
			string step = TestMain.ReadRepositoryText("Growth/KingdomSubsidenceStepRules.Departures.cs");
			StringAssert.Contains("KingdomSubsidenceReportArchive.TryRetain(prior, prior.Active.RungReportModel", step);
			StringAssert.Contains("batchWire, retained.FailureModel, prior.AnnouncementModel)", step);
			string batch = TestMain.ReadRepositoryText("Growth/KingdomSubsidenceStepRuntime.Reports.cs");
			StringAssert.Contains("KingdomSubsidenceReportArchive.TryRetain(frame.Owner.Owner.Step, batch.ReportModel", batch);
			StringAssert.Contains("retained.WithBatch(KingdomSubsidenceBatchRules.None)", batch);
		}

		[Test]
		public void SuccessfulOptionClearsRefusalAndFailedPendingPassHasAReason()
		{
			string option = TestMain.ReadRepositoryText("Growth/KingdomSubsidenceStepRuntime.Options.cs");
			StringAssert.Contains("if (complete) refusal = null;", option);
			StringAssert.Contains("else if (string.IsNullOrEmpty(refusal))", option);
			string pass = TestMain.ReadRepositoryText("Growth/KingdomSubsidenceStepRuntime.Pass.cs");
			StringAssert.Contains("if (string.IsNullOrEmpty(refusal))", pass);
			StringAssert.Contains("if (!CanStartReckoning(system))", pass);
			StringAssert.Contains("its saved evidence is retained.", pass);
		}
	}
}
#endif
