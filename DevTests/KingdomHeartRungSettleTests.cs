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
		private const string Proof = "Growth/KingdomUpgrade.25b.HandoverProof.cs";
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
		public void TheWholeHandoverProofIsOneFunctionAndTheFirstEvaluationDidNotMove()
		{
			// Lifted whole, carve-out included, so the post-callback re-ask is the identical
			// question rather than a weaker parallel predicate.
			string proof = Read(Proof);
			foreach (string token in new[] {
				"private static bool ExactImprovementHandoverProof(KingdomSystem System, Zone Z,",
				"KingdomConstruction.FindGlobalPredecessorAuthority(Job, Successor, out _)",
				"!= KingdomPhysicalLookupState.Absent",
				"r_KingdomScaffold.IsExactSuccessor(Successor, Z,",
				"Z.GetCell(Job.X, Job.Y), Job, entry.Blueprint)",
				"Successor.HasIntProperty(r_KingdomScaffold.RemovalProofProperty)",
				"!ExactRecoverableRemovalReceipt(Job)",
				"bool legacyZeroContent = !ExactRemovalReceipt(Job)",
				"r_KingdomImprovement.VerifySettledHandoverContentCustody(Successor,",
				"settledItems != Job.PhysicalIndex",
				"settledLiquid != Job.PhysicalAmount" })
				Assert.That(proof, Does.Contain(token), token);
			// The handover still asks it exactly where the block always stood: before the
			// FinalRemoved commit.
			Ordered(Read(Handover),
				"if (!ExactImprovementHandoverProof(System, Z, Successor, Job, out Failure)) return false;",
				"KingdomPhysicalPhase.FinalRemovalPending",
				"KingdomPhysicalPhase.FinalRemoved");
			// And it is not re-derived anywhere: one copy, one carve-out.
			Assert.That(Read(Handover), Does.Not.Contain("bool legacyZeroContent"));
			Assert.That(Read(Caller), Does.Not.Contain("VerifySettledHandoverContentCustody"));
		}

		[Test]
		public void EveryCallbackBoundaryReAsksTheWholeProofNotJustTheCheapGate()
		{
			string caller = Read(Caller);
			// The cheap endpoint gate refuses before any work; the delegate the helper re-asks
			// after each callback is the WHOLE handover proof.
			// Cheap gate -> FULL proof -> settle. The middle proof closes the window opened by the
			// handover's own active.ObserveChanged reclassification, which can tear carried
			// contents while leaving the root standing.
			Ordered(caller,
				"if (!ExactImprovementHeartEndpoint(System, Z, Successor, Job)) return false;",
				"if (!ExactImprovementHandoverProof(System, Z, Successor, Job, out _)) return false;",
				"return KingdomPlots.TrySettleHeartRung(System, Z, Successor, Job.TargetKey,",
				"() => ExactImprovementHandoverProof(System, Z, Successor, Job, out _));");
			string settle = Read(Settle);
			// Boundary one: the ceremony. Boundary two: the basin. Both re-ask, and the second
			// sits OUTSIDE the guard so a swallowed basin failure still cannot settle a torn root.
			Ordered(settle, "KingdomCeremonyHeart.OnRungRaised(System, Z, TargetKey, true);",
				"if (!Prove()) return false;",
				"KingdomSystem.Guard(\"heart basin capacity\", delegate",
				"ReconcileBasinCapacity(System, Building, Z);",
				"});",
				"if (!Prove()) return false;",
				"return true;");
		}

		[Test]
		public void ALateRefusalLeavesTheHonestStateAndNeverRefiresTheCeremony()
		{
			string settle = Read(Settle);
			// By the final Prove the rung is stamped and the marker is settled, so the retry that
			// follows a refusal quarantines without firing the ceremony a second time.
			int marker = settle.LastIndexOf("Building.SetIntProperty(HeartEffectProperty, 2);",
				StringComparison.Ordinal);
			int last = settle.LastIndexOf("if (!Prove()) return false;", StringComparison.Ordinal);
			Assert.That(marker, Is.GreaterThan(0));
			Assert.That(last, Is.GreaterThan(marker),
				"the final endpoint proof runs after the marker is settled");
			Assert.That(settle, Does.Contain("Z.SetZoneProperty(HeartRungProperty, wire);"));
			Assert.That(Read(Handover), Does.Contain(
				"Failure = \"The raised heart rung could not settle its exact effects.\";"));
		}

		/// <summary>Review thread (copilot-threads-157-158.md #3): the &lt;returns&gt; doc
		/// falsely said false happens "only" when a rung WAS owed and could not be settled.
		/// Growth/KingdomPlotHeartRules.Settle.cs:41-42 refuses BEFORE HeartRungOf is even read
		/// for four independent invalid-input classes: unfounded/null System, null Z, null
		/// Prove, and an invalid Building. One case per class, pinning the guard's exact clauses
		/// and their order ahead of the rung read -- and that the doc now names both refusal
		/// classes rather than only the owed-rung one.</summary>
		[Test]
		public void EveryInvalidInputClassRefusesBeforeTheRungIsEvenRead()
		{
			string settle = Read(Settle);
			// One assertion per invalid-input class named in the guard.
			Assert.That(settle, Does.Contain("System == null"), "unfounded/absent System");
			Assert.That(settle, Does.Contain("!System.Founded"), "unfounded System");
			Assert.That(settle, Does.Contain("Z == null"), "null Z");
			Assert.That(settle, Does.Contain("Prove == null"), "null Prove");
			Assert.That(settle, Does.Contain("!GameObject.Validate(Building)"), "invalid Building");
			Ordered(settle,
				"if (System == null || !System.Founded || Z == null || Prove == null\n"
					+ "\t\t\t\t|| !GameObject.Validate(Building)) return false;",
				"int rung = KingdomPlotRules.HeartRungOf(TargetKey);");
		}

		[Test]
		public void TheReturnsDocNamesBothRefusalClassesNotOnlyTheOwedRung()
		{
			string settle = Read(Settle);
			Assert.That(settle, Does.Contain(
				"False in either of two classes, both retryable by the caller: the"));
			Assert.That(settle, Does.Contain(
				"is unusable (System not founded, Z null,"));
			Assert.That(settle, Does.Contain(
				"even read -- or the input was usable and a rung WAS"));
			Assert.That(settle, Does.Not.Contain("False only when a rung WAS owed"),
				"the doc must not claim invalid input is impossible here");
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
			// The cheap gate is proved before the helper runs; what the helper re-asks after each
			// callback is the whole handover proof, pinned by
			// EveryCallbackBoundaryReAsksTheWholeProofNotJustTheCheapGate.
			Assert.That(caller, Does.Contain(
				"if (!ExactImprovementHeartEndpoint(System, Z, Successor, Job)) return false;"));
		}

	}
}
#endif
