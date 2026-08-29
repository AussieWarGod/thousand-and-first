#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPlotLabourWindowRulesTests
	{
		[Test]
		public void CanonicalWindowRoundTripsExactBoundedWitness()
		{
			KingdomPlotLabourWindow expected = Window(25L, 50, 100, 1, true);
			Assert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(expected, out string encoded));
			Assert.AreEqual("w1|25|50|100|1|1", encoded);
			Assert.IsTrue(KingdomPlotLabourWindowRules.TryDecode(encoded, out var actual));
			Assert.AreEqual(expected.Tick, actual.Tick);
			Assert.AreEqual(expected.LabourPercent, actual.LabourPercent);
			Assert.AreEqual(expected.InfrastructurePercent, actual.InfrastructurePercent);
			Assert.AreEqual(expected.Hands, actual.Hands);
			Assert.AreEqual(expected.Selected, actual.Selected);
		}

		[Test]
		public void CodecRejectsUnboundedContradictoryAndNoncanonicalWitnesses()
		{
			Assert.IsFalse(KingdomPlotLabourWindowRules.TryEncode(
				Window(0L, 100, 100, 3, true), out _));
			Assert.IsFalse(KingdomPlotLabourWindowRules.TryEncode(
				Window(0L, 50, 100, 1, false), out _));
			Assert.IsFalse(KingdomPlotLabourWindowRules.TryEncode(
				Window(0L, 50, 40, 1, true), out _));
			Assert.IsFalse(KingdomPlotLabourWindowRules.TryDecode(
				"w1|01|50|100|1|1", out _));
		}

		[Test]
		public void MissingMalformedOrWrongAnchorNeverInfersAbsentCrew()
		{
			Assert.IsFalse(KingdomPlotLabourWindowRules.TryForInterval(null, 10L, out _));
			Assert.IsFalse(KingdomPlotLabourWindowRules.TryForInterval("bad", 10L, out _));
			Assert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(
				Window(9L, 100, 100, 2, true), out string stale));
			Assert.IsFalse(KingdomPlotLabourWindowRules.TryForInterval(stale, 10L, out _));
		}

		[Test]
		public void MissingWindowSpendsOldIntervalThenCurrentWitnessWorksOnlyForward()
		{
			Assert.IsFalse(KingdomPlotLabourWindowRules.TryForInterval(
				null, 0L, out _));
			KingdomPlotLabourStep migrationWake = KingdomPlotLabourRules.Advance(
				Receipt(100L, 100L, 0L), 50L, 0, 0);
			Assert.AreEqual(0L, migrationWake.WorkedTicks);
			Assert.AreEqual(100L, migrationWake.RemainingTicks);
			Assert.AreEqual(50L, migrationWake.NextTick);

			KingdomPlotLabourStep later = Advance(
				Window(50L, 100, 100, 2, true),
				Receipt(100L, migrationWake.RemainingTicks, migrationWake.NextTick), 100L);
			Assert.AreEqual(50L, later.WorkedTicks);
			Assert.AreEqual(50L, later.RemainingTicks);
		}

		[Test]
		public void SameTickWitnessAnchorsOnlyTheFollowingInterval()
		{
			KingdomPlotLabourReceipt receipt = Receipt(100L, 100L, 50L);
			KingdomPlotLabourStep sameTick = KingdomPlotLabourRules.Advance(
				receipt, 50L, 100, 100);
			Assert.IsFalse(sameTick.WriteReceipt);
			Assert.AreEqual(100L, sameTick.RemainingTicks);

			KingdomPlotLabourStep later = Advance(
				Window(50L, 100, 100, 2, true), receipt, 80L);
			Assert.AreEqual(30L, later.WorkedTicks);
			Assert.AreEqual(70L, later.RemainingTicks);
		}

		[Test]
		public void UnauthorizedEqualAndForwardWakesBankOnlyCanonicalZero()
		{
			KingdomPlotLabourWindow sameTickZero = Window(50L, 0, 0, 0, false);
			Assert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(
				sameTickZero, out string encoded));
			Assert.AreEqual("w1|50|0|0|0|0", encoded);
			KingdomPlotLabourStep afterAuthorityReturns = Advance(sameTickZero,
				Receipt(100L, 100L, 50L), 80L);
			Assert.AreEqual(0L, afterAuthorityReturns.WorkedTicks);
			Assert.AreEqual(100L, afterAuthorityReturns.RemainingTicks);

			KingdomPlotLabourWindow forwardZero = Window(
				afterAuthorityReturns.NextTick, 0, 0, 0, false);
			KingdomPlotLabourStep following = Advance(forwardZero,
				Receipt(100L, afterAuthorityReturns.RemainingTicks,
					afterAuthorityReturns.NextTick), 100L);
			Assert.AreEqual(0L, following.WorkedTicks);
			Assert.AreEqual(100L, following.RemainingTicks);
		}

		[Test]
		public void SamePassArrivalCannotWorkAbsenceButWorksFollowingInterval()
		{
			Assert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(
				Window(0L, 50, 100, 1, true), out string beforeAbsence));
			Assert.IsTrue(KingdomPlotLabourWindowRules.TryForInterval(
				beforeAbsence, 0L, out var oldCrew));
			KingdomPlotLabourStep wake = KingdomPlotLabourRules.Advance(
				Receipt(200L, 200L, 0L), 100L, oldCrew.LabourPercent,
				oldCrew.InfrastructurePercent);
			Assert.AreEqual(50L, wake.WorkedTicks);
			Assert.AreEqual(150L, wake.RemainingTicks);

			Assert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(
				Window(100L, 100, 100, 2, true), out string afterArrival));
			Assert.IsTrue(KingdomPlotLabourWindowRules.TryForInterval(
				afterArrival, wake.NextTick, out var newCrew));
			KingdomPlotLabourStep later = KingdomPlotLabourRules.Advance(
				Receipt(200L, wake.RemainingTicks, wake.NextTick), 200L,
				newCrew.LabourPercent, newCrew.InfrastructurePercent);
			Assert.AreEqual(100L, later.WorkedTicks);
			Assert.AreEqual(50L, later.RemainingTicks);
		}

		[Test]
		public void YardLossStallsOnlyFollowingWitnessedIntervalAndDoesNotBankIt()
		{
			KingdomPlotLabourStep beforeLoss = Advance(
				Window(0L, 100, 100, 2, true), Receipt(300L, 300L, 0L), 100L);
			Assert.AreEqual(100L, beforeLoss.WorkedTicks);
			KingdomPlotLabourStep withoutYard = Advance(
				Window(100L, 100, 0, 2, true),
				Receipt(300L, beforeLoss.RemainingTicks, beforeLoss.NextTick), 200L);
			Assert.AreEqual(0L, withoutYard.WorkedTicks);
			Assert.AreEqual(200L, withoutYard.RemainingTicks);
			Assert.AreEqual(200L, withoutYard.NextTick);
		}

		private static KingdomPlotLabourStep Advance(KingdomPlotLabourWindow Window,
			KingdomPlotLabourReceipt Receipt, long Now)
		{
			Assert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(Window, out string encoded));
			Assert.IsTrue(KingdomPlotLabourWindowRules.TryForInterval(
				encoded, Receipt.LastTick, out var witnessed));
			return KingdomPlotLabourRules.Advance(Receipt, Now, witnessed.LabourPercent,
				witnessed.InfrastructurePercent);
		}

		private static KingdomPlotLabourWindow Window(long tick, int labour,
			int infrastructure, int hands, bool selected)
		{
			return new KingdomPlotLabourWindow
			{
				Tick = tick, LabourPercent = labour,
				InfrastructurePercent = infrastructure,
				Hands = hands, Selected = selected
			};
		}

		private static KingdomPlotLabourReceipt Receipt(long required, long remaining,
			long last)
		{
			return new KingdomPlotLabourReceipt
			{
				Schema = KingdomPlotLabourRules.CurrentSchema,
				HasRequiredTicks = true, RequiredTicks = required,
				HasRemainingTicks = true, RemainingTicks = remaining,
				HasLastTick = true, LastTick = last
			};
		}
	}
}
#endif
