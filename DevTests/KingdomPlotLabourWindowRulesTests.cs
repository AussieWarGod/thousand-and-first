#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPlotLabourWindowRulesTests
	{
		[Test]
		public void CanonicalWindowRoundTripsExactBoundedWitness()
		{
			KingdomPlotLabourWindow expected = Window(25L, 50, 100, 1, true);
			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(expected, out string encoded));
			ClassicAssert.AreEqual("w1|25|50|100|1|1", encoded);
			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryDecode(encoded, out var actual));
			ClassicAssert.AreEqual(expected.Tick, actual.Tick);
			ClassicAssert.AreEqual(expected.LabourPercent, actual.LabourPercent);
			ClassicAssert.AreEqual(expected.InfrastructurePercent, actual.InfrastructurePercent);
			ClassicAssert.AreEqual(expected.Hands, actual.Hands);
			ClassicAssert.AreEqual(expected.Selected, actual.Selected);
		}

		[Test]
		public void CodecRejectsUnboundedContradictoryAndNoncanonicalWitnesses()
		{
			ClassicAssert.IsFalse(KingdomPlotLabourWindowRules.TryEncode(
				Window(0L, 100, 100, 3, true), out _));
			ClassicAssert.IsFalse(KingdomPlotLabourWindowRules.TryEncode(
				Window(0L, 50, 100, 1, false), out _));
			ClassicAssert.IsFalse(KingdomPlotLabourWindowRules.TryEncode(
				Window(0L, 50, 40, 1, true), out _));
			ClassicAssert.IsFalse(KingdomPlotLabourWindowRules.TryDecode(
				"w1|01|50|100|1|1", out _));
		}

		[Test]
		public void MissingMalformedOrWrongAnchorNeverInfersAbsentCrew()
		{
			ClassicAssert.IsFalse(KingdomPlotLabourWindowRules.TryForInterval(null, 10L, out _));
			ClassicAssert.IsFalse(KingdomPlotLabourWindowRules.TryForInterval("bad", 10L, out _));
			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(
				Window(9L, 100, 100, 2, true), out string stale));
			ClassicAssert.IsFalse(KingdomPlotLabourWindowRules.TryForInterval(stale, 10L, out _));
		}

		[Test]
		public void MissingWindowSpendsOldIntervalThenCurrentWitnessWorksOnlyForward()
		{
			ClassicAssert.IsFalse(KingdomPlotLabourWindowRules.TryForInterval(
				null, 0L, out _));
			KingdomPlotLabourStep migrationWake = KingdomPlotLabourRules.Advance(
				Receipt(100L, 100L, 0L), 50L, 0, 0);
			ClassicAssert.AreEqual(0L, migrationWake.WorkedTicks);
			ClassicAssert.AreEqual(100L, migrationWake.RemainingTicks);
			ClassicAssert.AreEqual(50L, migrationWake.NextTick);

			KingdomPlotLabourStep later = Advance(
				Window(50L, 100, 100, 2, true),
				Receipt(100L, migrationWake.RemainingTicks, migrationWake.NextTick), 100L);
			ClassicAssert.AreEqual(50L, later.WorkedTicks);
			ClassicAssert.AreEqual(50L, later.RemainingTicks);
		}

		[Test]
		public void SameTickWitnessAnchorsOnlyTheFollowingInterval()
		{
			KingdomPlotLabourReceipt receipt = Receipt(100L, 100L, 50L);
			KingdomPlotLabourStep sameTick = KingdomPlotLabourRules.Advance(
				receipt, 50L, 100, 100);
			ClassicAssert.IsFalse(sameTick.WriteReceipt);
			ClassicAssert.AreEqual(100L, sameTick.RemainingTicks);

			KingdomPlotLabourStep later = Advance(
				Window(50L, 100, 100, 2, true), receipt, 80L);
			ClassicAssert.AreEqual(30L, later.WorkedTicks);
			ClassicAssert.AreEqual(70L, later.RemainingTicks);
		}

		[Test]
		public void UnauthorizedEqualAndForwardWakesBankOnlyCanonicalZero()
		{
			KingdomPlotLabourWindow sameTickZero = Window(50L, 0, 0, 0, false);
			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(
				sameTickZero, out string encoded));
			ClassicAssert.AreEqual("w1|50|0|0|0|0", encoded);
			KingdomPlotLabourStep afterAuthorityReturns = Advance(sameTickZero,
				Receipt(100L, 100L, 50L), 80L);
			ClassicAssert.AreEqual(0L, afterAuthorityReturns.WorkedTicks);
			ClassicAssert.AreEqual(100L, afterAuthorityReturns.RemainingTicks);

			KingdomPlotLabourWindow forwardZero = Window(
				afterAuthorityReturns.NextTick, 0, 0, 0, false);
			KingdomPlotLabourStep following = Advance(forwardZero,
				Receipt(100L, afterAuthorityReturns.RemainingTicks,
					afterAuthorityReturns.NextTick), 100L);
			ClassicAssert.AreEqual(0L, following.WorkedTicks);
			ClassicAssert.AreEqual(100L, following.RemainingTicks);
		}

		[Test]
		public void SamePassArrivalCannotWorkAbsenceButWorksFollowingInterval()
		{
			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(
				Window(0L, 50, 100, 1, true), out string beforeAbsence));
			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryForInterval(
				beforeAbsence, 0L, out var oldCrew));
			KingdomPlotLabourStep wake = KingdomPlotLabourRules.Advance(
				Receipt(200L, 200L, 0L), 100L, oldCrew.LabourPercent,
				oldCrew.InfrastructurePercent);
			ClassicAssert.AreEqual(50L, wake.WorkedTicks);
			ClassicAssert.AreEqual(150L, wake.RemainingTicks);

			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(
				Window(100L, 100, 100, 2, true), out string afterArrival));
			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryForInterval(
				afterArrival, wake.NextTick, out var newCrew));
			KingdomPlotLabourStep later = KingdomPlotLabourRules.Advance(
				Receipt(200L, wake.RemainingTicks, wake.NextTick), 200L,
				newCrew.LabourPercent, newCrew.InfrastructurePercent);
			ClassicAssert.AreEqual(100L, later.WorkedTicks);
			ClassicAssert.AreEqual(50L, later.RemainingTicks);
		}

		[Test]
		public void YardLossStallsOnlyFollowingWitnessedIntervalAndDoesNotBankIt()
		{
			KingdomPlotLabourStep beforeLoss = Advance(
				Window(0L, 100, 100, 2, true), Receipt(300L, 300L, 0L), 100L);
			ClassicAssert.AreEqual(100L, beforeLoss.WorkedTicks);
			KingdomPlotLabourStep withoutYard = Advance(
				Window(100L, 100, 0, 2, true),
				Receipt(300L, beforeLoss.RemainingTicks, beforeLoss.NextTick), 200L);
			ClassicAssert.AreEqual(0L, withoutYard.WorkedTicks);
			ClassicAssert.AreEqual(200L, withoutYard.RemainingTicks);
			ClassicAssert.AreEqual(200L, withoutYard.NextTick);
		}

		private static KingdomPlotLabourStep Advance(KingdomPlotLabourWindow Window,
			KingdomPlotLabourReceipt Receipt, long Now)
		{
			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryEncode(Window, out string encoded));
			ClassicAssert.IsTrue(KingdomPlotLabourWindowRules.TryForInterval(
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
