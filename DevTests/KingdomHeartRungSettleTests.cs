#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The heart's rung effects are settled by ONE shared settlement helper, and every route that
	/// can raise a rung calls it. Issue #138: the improvement route is the only route a rung above
	/// the first can climb by, and it wrote no rung at all, so rung 2 -&gt; 3 refused for not
	/// accreting from its standing rung.
	/// </summary>
	public class KingdomHeartRungSettleTests
	{
		private const string Settle = "Growth/KingdomPlotHeartRules.Settle.cs";
		private const string Plot = "Growth/KingdomPlot2.34.EffectsAndFurnishing.cs";
		private const string Caller = "Growth/KingdomUpgrade.26.HeartRung.cs";
		private const string Handover = "Growth/KingdomUpgrade.25.HandoverRemoval.cs";
		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		private static void Ordered(string source, params string[] tokens)
		{
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(token, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, token);
				cursor = at + token.Length;
			}
		}

		[Test]
		public void TheImprovementRouteSettlesTheRungAndDoesNotKeepASecondCopy()
		{
			string handover = Read(Handover);
			Assert.That(handover, Does.Contain(
				"TrySettleImprovementHeartRung(System, Z, Successor, Job)"));
			// The comment says what the recovery path actually does with a refusal.
			Assert.That(handover, Does.Contain("quarantines it with this exact reason"));
			Assert.That(Read(Caller), Does.Contain("quarantines it with the caller's"));
			// One shared helper: the improvement route must not re-implement the stamp.
			foreach (string forbidden in new[] { "HeartRungProperty", "OnRungRaised",
				"ReconcileBasinCapacity" })
			{
				Assert.That(handover, Does.Not.Contain(forbidden), forbidden);
				Assert.That(Read(Caller), Does.Not.Contain(forbidden), forbidden);
			}
			Assert.That(Read(Caller), Does.Contain(
				"KingdomPlots.TrySettleHeartRung(System, Z, Successor, Job.TargetKey,"));
		}

		[Test]
		public void TheRungSettlesBeforeTheReceiptCompletesSoARefusalStaysRetryable()
		{
			// Before Complete on purpose: a refusal must leave the receipt non-terminal for the
			// ordinary recovery path, rather than close the job over an unwritten rung.
			Ordered(Read(Handover),
				"KingdomPhysicalPhase.FinalRemoved",
				"TrySettleImprovementHeartRung(System, Z, Successor, Job)",
				"KingdomConstruction.Complete(ref Job)",
				"r_KingdomScaffold.TellCompletion(System, Successor, Job)");
		}

		[Test]
		public void ThePlotRouteCallsTheSameHelperWithItsOwnEndpointProof()
		{
			string plot = Read(Plot);
			Assert.That(plot, Does.Contain(
				"TrySettleHeartRung(System, Z, Building, Job.TargetKey,"));
			Assert.That(plot, Does.Contain("ExactPlotEffectEndpoint(System, Z, Building, settling)"));
			Assert.That(plot, Does.Contain(
				"ExactPlotFinalRootCustody(settling.OutputId, Building)"));
			// The plot route no longer keeps its own copy of the stamp or the callback.
			foreach (string forbidden in new[] { "Z.SetZoneProperty(HeartRungProperty",
				"KingdomCeremonyHeart.OnRungRaised", "ReconcileBasinCapacity(System, Building, Z)" })
				Assert.That(plot, Does.Not.Contain(forbidden), forbidden);
		}

		[Test]
		public void TheCeremonyCallbackIsAtMostOnceThroughTheZeroOneTwoMarker()
		{
			// state 0 -> mark Attempting(1) -> callback -> re-prove -> settle to 2. An interrupted
			// Attempting marker is honestly lost: state 1 never fires the callback again.
			Ordered(Read(Settle),
				"int state = Building.GetIntProperty(HeartEffectProperty);",
				"if (state < 0 || state > 2) return false;",
				"if (state == 0)",
				"Building.SetIntProperty(HeartEffectProperty, 1);",
				"KingdomCeremonyHeart.OnRungRaised(System, Z, TargetKey, true);",
				"if (!Prove()) return false;",
				"if (!callbackReturned) return false;",
				"if (Building.GetIntProperty(HeartEffectProperty) == 1)",
				"Building.SetIntProperty(HeartEffectProperty, 2);",
				"if (Building.GetIntProperty(HeartEffectProperty) != 2) return false;");
		}

		[Test]
		public void AFailedOrRelocatedCallbackRefusesBeforeTheEffectIsMarkedSettled()
		{
			string settle = Read(Settle);
			int prove = settle.IndexOf("if (!Prove()) return false;", StringComparison.Ordinal);
			int returned = settle.IndexOf("if (!callbackReturned) return false;",
				StringComparison.Ordinal);
			int settled = settle.IndexOf("Building.SetIntProperty(HeartEffectProperty, 2);",
				StringComparison.Ordinal);
			Assert.That(prove, Is.GreaterThan(0));
			Assert.That(prove, Is.LessThan(settled), "the endpoint is re-proved before settling");
			Assert.That(returned, Is.LessThan(settled), "a thrown callback refuses before settling");
		}

		[Test]
		public void ARungIsNeverStampedBackwardAndNothingIsOwedOffTheLadder()
		{
			string settle = Read(Settle);
			Assert.That(settle, Does.Contain("KingdomPlotRules.RungMaySettle(rung, standing)"));
			// The refusal is diagnosable: one log line naming the ground, the standing rung, the
			// receipt's rung and the design, and nothing else on that branch.
			Assert.That(settle, Does.Contain(
				"KingdomLog.Log(\"heart rung: refused a receipt below the standing rung: zone \""));
			foreach (string token in new[] { "+ Z.ZoneID", "standing ", "receipt ", "design " })
				Assert.That(settle, Does.Contain(token), token);
			foreach (string forbidden in new[] { "Ledger.Note", "MessageQueue", "SetIntProperty(\"r_TAF" })
				Assert.That(settle, Does.Not.Contain(forbidden), forbidden);
			Assert.That(settle, Does.Contain(
				"if (Building.GetIntProperty(HeartPlotProperty) != 1) return true;"));
			Assert.That(settle, Does.Contain("if (rung <= 0) return true;"));
			// The stamp is written and read back before any callback runs.
			Ordered(settle, "Z.SetZoneProperty(HeartRungProperty, wire);",
				"if (Z.GetZoneProperty(HeartRungProperty, null) != wire) return false;",
				"KingdomCeremonyHeart.OnRungRaised(System, Z, TargetKey, true);");
		}

		[Test]
		public void TheImprovementEndpointProvesTheExactSuccessorJobAndGround()
		{
			string caller = Read(Caller);
			foreach (string token in new[] {
				"Job.Route != KingdomConstructionRoute.Improvement",
				"KingdomConstruction.Owns(System, Z, Job)",
				"KingdomConstruction.IsCurrent(Job)",
				"ReferenceEquals(exact, Successor)",
				"Successor.IDIfAssigned == Job.OutputId",
				"Successor.CurrentCell == Z.GetCell(Job.X, Job.Y)",
				"KingdomConstruction.HasReceipt(Successor, Job)",
				"Successor.GetStringProperty(BuildKeyProperty) == Job.TargetKey",
				"r_KingdomScaffold.HasRemovalProof(Successor, Job.SubjectId)" })
				Assert.That(caller, Does.Contain(token), token);
			// Proved before the helper runs AND passed in to be re-asked after the callback.
			Ordered(caller, "return ExactImprovementHeartEndpoint(System, Z, Successor, Job)",
				"KingdomPlots.TrySettleHeartRung(System, Z, Successor, Job.TargetKey,",
				"() => ExactImprovementHeartEndpoint(System, Z, Successor, Job));");
		}

	}
}
#endif
