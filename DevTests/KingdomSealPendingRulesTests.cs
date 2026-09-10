#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomSealPendingRulesTests
	{
		[TestCase(true, true, 0, true)]
		[TestCase(false, true, 0, false)]
		[TestCase(true, false, 0, false)]
		[TestCase(true, true, 1, false)]
		[TestCase(true, false, 1, false)]
		[TestCase(true, true, -1, false)]
		public void OnlyAnUnconnectedRoadlessEntranceCanWait(bool entrance, bool noEntry,
			int streets, bool expected)
			=> Assert.That(KingdomSealPendingRules.RoadlessEntrance(entrance, noEntry, streets),
				Is.EqualTo(expected));

		[Test]
		public void PendingCaptureRefusesBeforeUnavailableFallbackOrPublication()
		{
			string capture = TestMain.ReadRepositoryText("Core/KingdomSeal.Capture.cs");
			int pending = capture.IndexOf("|| Spatial == KingdomInheritanceSpatialCaptureResult.Pending)");
			int clear = capture.IndexOf("Record = null;", pending);
			int refuse = capture.IndexOf("return false;", clear);
			int fallback = capture.IndexOf("if (Spatial == KingdomInheritanceSpatialCaptureResult.Unavailable)");
			Assert.That(pending, Is.GreaterThanOrEqualTo(0));
			Assert.That(clear, Is.GreaterThan(pending));
			Assert.That(refuse, Is.GreaterThan(clear));
			Assert.That(fallback, Is.GreaterThan(refuse));
			string stage = TestMain.ReadRepositoryText("Core/KingdomSeal.Staging.cs");
			Assert.That(stage, Does.Contain("out probe, out Failure, out Spatial)"));
			Assert.That(stage, Does.Contain("out next, out Failure, out Spatial)"));
		}

		[Test]
		public void NativeWitnessComparesTheActualStageAcrossRealDailyIntervals()
		{
			string source = TestMain.ReadRepositoryText("Harness/KingdomSealRoadlessNativeProvider.cs");
			Assert.That(source, Does.Contain("KingdomScenarioCompletedHeart.Complete(Game, System, Zone)"));
			Assert.That(source, Does.Contain("System.Population == 0"));
			Assert.That(source, Does.Contain("Seal.NativePendingStageEvidence() == Before"));
			Assert.That(source, Does.Contain("Notices() == NoticeBaseline + 1"));
			Assert.That(source, Does.Contain("synthetic-heart-calendar=true"));
			Assert.That(source, Does.Not.Contain("SetZoneProperty("));
			string observer = TestMain.ReadRepositoryText("Harness/KingdomSeal.NativePendingObservation.cs");
			Assert.That(observer, Does.Contain("GetStore().ReadStage(OriginGameId)"));
			Assert.That(observer, Does.Contain("record.Compose()"));
			Assert.That(observer, Does.Contain("!staged && spatial == KingdomInheritanceSpatialCaptureResult.Pending"));
		}

		[Test]
		public void OnlyTheTypedPendingResultChangesAutomaticReporting()
		{
			string adapter = TestMain.ReadRepositoryText("Core/KingdomInheritanceSpatial.cs");
			Assert.That(adapter, Does.Contain("fault == KingdomInheritanceSpatialFault.PublicEntrance"));
			Assert.That(adapter, Does.Contain("entrySide == KingdomInheritanceSpatialRules.NoEntry, streetX.Count"));
			string report = TestMain.ReadRepositoryText("Core/KingdomSeal.Utilities.cs");
			Assert.That(report, Does.Contain("if (Spatial != KingdomInheritanceSpatialCaptureResult.Pending)"));
			Assert.That(report, Does.Contain("ReportFailure(Action, Failure); return;"));
			Assert.That(report, Does.Contain("No new seal has been written."));
			string events = TestMain.ReadRepositoryText("Core/KingdomSeal.cs");
			Assert.That(events, Does.Contain("ReportCaptureFailure(\"daily stage\", failure, spatial)"));
			Assert.That(events, Does.Contain("ReportCaptureFailure(\"BeforeSave stage\", failure, spatial)"));
			Assert.That(events, Does.Contain("ReportFailure(\"daily stage\", ex.Message, ex)"));
			string semantic = TestMain.ReadRepositoryText("Core/KingdomSeal.Semantic.cs");
			Assert.That(semantic, Does.Contain("seal.ReportCaptureFailure(Reason, Failure, Spatial)"));
			string pass = TestMain.ReadRepositoryText("Core/KingdomSystem.z21.SemanticPass.cs");
			Assert.That(pass, Does.Contain("out failure, out spatial)"));
			Assert.That(pass, Does.Contain("&& spatial != KingdomInheritanceSpatialCaptureResult.Pending)"));
			Assert.That(report, Does.Contain("string.Equals(LastPendingKey, key, StringComparison.Ordinal)"));
		}
	}
}
#endif
