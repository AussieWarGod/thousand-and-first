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

		/// <summary>
		/// The load path and the daily pass read ONE rule, so a state one carries the other cannot
		/// call a fault. Pending is the settlement saying "not yet" -- the public entrance has no
		/// witnessed street to the zone edge -- and a save/reload of that same young settlement
		/// must not turn it into a MODERROR (issue #181).
		/// Mutation: dropping the Pending clause makes case 4 a fault, which is the defect; making
		/// every refusal tolerable makes cases 2 and 3 pass silently, which hides a broken reading.
		/// </summary>
		// The result is passed as its int: the enum is internal and this fixture is public.
		[TestCase(false, (int)KingdomInheritanceSpatialCaptureResult.Malformed, true)]
		[TestCase(false, (int)KingdomInheritanceSpatialCaptureResult.Unavailable, true)]
		[TestCase(false, (int)KingdomInheritanceSpatialCaptureResult.Captured, true)]
		[TestCase(false, (int)KingdomInheritanceSpatialCaptureResult.Pending, false)]
		[TestCase(true, (int)KingdomInheritanceSpatialCaptureResult.Captured, false)]
		[TestCase(true, (int)KingdomInheritanceSpatialCaptureResult.Pending, false)]
		[TestCase(true, (int)KingdomInheritanceSpatialCaptureResult.Malformed, false)]
		public void OnlyAPendingSpatialCaptureIsCarriedRatherThanFaulted(bool captured,
			int spatial, bool expected)
			=> Assert.That(KingdomSealSpatialRules.SpatialCaptureIsFault(captured,
					(KingdomInheritanceSpatialCaptureResult)spatial),
				Is.EqualTo(expected));

		[Test]
		public void PhysicalIngressWaitsOnlyAfterBuildingComponentsAreVerified()
		{
			string capture = TestMain.ReadRepositoryText("Core/KingdomInheritanceSpatial.cs");
			Assert.That(capture, Does.Contain("out bool ingressBlocked)"));
			Assert.That(capture, Does.Contain("return ingressBlocked ? KingdomInheritanceSpatialCaptureResult.Pending"));
			string staging = TestMain.ReadRepositoryText("Growth/KingdomArchitectureStamper.Staging.cs");
			int complete = staging.IndexOf("internal static bool TryVerifyComplete(");
			int components = staging.IndexOf("!TryExactOutput(", complete);
			int passage = staging.IndexOf("TryVerifyPassability(Z, intent, snapshot, lot", complete);
			int ingress = staging.IndexOf("out Failure, out IngressBlocked)", complete);
			Assert.That(components, Is.GreaterThan(complete));
			Assert.That(passage, Is.GreaterThan(components));
			Assert.That(ingress, Is.GreaterThan(passage));
		}

		[Test]
		public void FirstLoadWithoutAStageAlsoCarriesTypedPending()
		{
			string source = TestMain.ReadRepositoryText("Core/KingdomSeal.Synchronization.cs");
			int begin = source.IndexOf("if (stage == null)\n");
			int end = source.IndexOf("if (stage.Status ==", begin);
			string unstaged = source.Substring(begin, end - begin);
			Assert.That(unstaged, Does.Contain("out Failure, out KingdomInheritanceSpatialCaptureResult initialSpatial)"));
			Assert.That(unstaged, Does.Contain("SpatialCaptureIsFault(flushed, initialSpatial)"));
			Assert.That(unstaged, Does.Not.Contain("Dirty = false"));
			Assert.That(unstaged, Does.Not.Contain("Revision ="));
		}

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
			Assert.That(observer, Does.Contain("!captured && record == null && spatial == KingdomInheritanceSpatialCaptureResult.Pending"));
			Assert.That(observer, Does.Contain("NativePendingStageEvidence() != before"));
			Assert.That(observer, Does.Not.Contain("TryFlushLiving("));
			Assert.That(source, Does.Contain("KingdomCity.CheckIn(System, Zone, survey, tick)"));
			Assert.That(source, Does.Contain("System.City.WorkIds.Count > 0"));
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
			// Moved with the code (issue #181): the pass still reads the TYPED result, but the
			// Pending clause is now the shared rule both it and the load path ask.
			Assert.That(pass, Does.Contain("out spatial), spatial))"));
			Assert.That(pass, Does.Contain("KingdomSealSpatialRules.SpatialCaptureIsFault("));
			Assert.That(report, Does.Contain("string.Equals(LastPendingKey, key, StringComparison.Ordinal)"));
		}
	}
}
#endif
