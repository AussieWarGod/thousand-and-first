#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomScaffoldLabourRulesTests
	{
		[Test]
		public void CanonicalWindowRoundTripsExactBoundedWitness()
		{
			KingdomScaffoldLabourWindow expected = Window(25L, 50, 1, true);
			Assert.IsTrue(KingdomScaffoldLabourWindowRules.TryEncode(
				expected, out string encoded));
			Assert.AreEqual("s1|25|50|1|1", encoded);
			Assert.IsTrue(KingdomScaffoldLabourWindowRules.TryDecode(encoded, out var actual));
			Assert.AreEqual(expected.Tick, actual.Tick);
			Assert.AreEqual(expected.EffectivenessPercent, actual.EffectivenessPercent);
			Assert.AreEqual(expected.Hands, actual.Hands);
			Assert.AreEqual(expected.Selected, actual.Selected);
		}

		[Test]
		public void CodecRejectsUnboundedContradictoryAndNoncanonicalWitnesses()
		{
			Assert.IsFalse(KingdomScaffoldLabourWindowRules.TryEncode(
				Window(0L, 101, 2, true), out _));
			Assert.IsFalse(KingdomScaffoldLabourWindowRules.TryEncode(
				Window(0L, 100, 3, true), out _));
			Assert.IsFalse(KingdomScaffoldLabourWindowRules.TryEncode(
				Window(0L, 50, 1, false), out _));
			Assert.IsFalse(KingdomScaffoldLabourWindowRules.TryDecode(
				"s1|01|50|1|1", out _));
		}

		[Test]
		public void MissingMalformedOrWrongAnchorNeverPricesInterval()
		{
			Assert.IsFalse(KingdomScaffoldLabourWindowRules.TryForInterval(
				null, 10L, out _));
			Assert.IsFalse(KingdomScaffoldLabourWindowRules.TryForInterval(
				"bad", 10L, out _));
			Assert.IsTrue(KingdomScaffoldLabourWindowRules.TryEncode(
				Window(9L, 100, 2, true), out string stale));
			Assert.IsFalse(KingdomScaffoldLabourWindowRules.TryForInterval(
				stale, 10L, out _));
		}

		[Test]
		public void MissingWindowSpendsOldIntervalThenCurrentWitnessWorksOnlyForward()
		{
			Assert.IsFalse(KingdomScaffoldLabourWindowRules.TryForInterval(
				null, 10L, out _));
			KingdomScaffoldLabourStep wake = KingdomScaffoldLabourRules.Advance(
				10L, 60L, 100L, 0);
			Assert.AreEqual(0L, wake.WorkedTicks);
			Assert.AreEqual(100L, wake.RemainingTicks);
			Assert.AreEqual(60L, wake.NextTick);

			KingdomScaffoldLabourWindow current = Window(60L, 100, 2, true);
			Assert.IsTrue(KingdomScaffoldLabourWindowRules.TryEncode(
				current, out string encoded));
			Assert.IsTrue(KingdomScaffoldLabourWindowRules.TryForInterval(
				encoded, wake.NextTick, out var prior));
			KingdomScaffoldLabourStep later = KingdomScaffoldLabourRules.Advance(
				wake.NextTick, 110L, wake.RemainingTicks, prior.EffectivenessPercent);
			Assert.AreEqual(50L, later.WorkedTicks);
			Assert.AreEqual(50L, later.RemainingTicks);
		}

		[Test]
		public void SameTickWitnessAnchorsOnlyTheFollowingInterval()
		{
			KingdomScaffoldLabourStep sameTick = KingdomScaffoldLabourRules.Advance(
				50L, 50L, 100L, 100);
			Assert.AreEqual(50L, sameTick.NextTick);
			Assert.AreEqual(100L, sameTick.RemainingTicks);
			Assert.AreEqual(0L, sameTick.WorkedTicks);

			KingdomScaffoldLabourStep later = Advance(
				Window(50L, 100, 2, true), 50L, 100L, 80L);
			Assert.AreEqual(30L, later.WorkedTicks);
			Assert.AreEqual(70L, later.RemainingTicks);
		}

		[Test]
		public void SamePassArrivalCannotWorkAbsenceButWorksFollowingInterval()
		{
			KingdomScaffoldLabourStep wake = Advance(
				Window(0L, 50, 1, true), 0L, 200L, 100L);
			Assert.AreEqual(50L, wake.WorkedTicks);
			Assert.AreEqual(150L, wake.RemainingTicks);
			KingdomScaffoldLabourStep later = Advance(
				Window(100L, 100, 2, true), wake.NextTick,
				wake.RemainingTicks, 200L);
			Assert.AreEqual(100L, later.WorkedTicks);
			Assert.AreEqual(50L, later.RemainingTicks);
		}

		[Test]
		public void CompletionCeilingIsExactAndDoesNotOverflow()
		{
			KingdomScaffoldLabourStep quantised = KingdomScaffoldLabourRules.Advance(
				100L, 102L, 1L, 50);
			Assert.IsTrue(quantised.Complete);
			Assert.AreEqual(102L, quantised.CompletionTick);

			long half = long.MaxValue / 2L;
			KingdomScaffoldLabourStep huge = KingdomScaffoldLabourRules.Advance(
				0L, long.MaxValue, half, 50);
			Assert.IsTrue(huge.Complete);
			Assert.AreEqual(half, huge.WorkedTicks);
			Assert.AreEqual(long.MaxValue - 1L, huge.CompletionTick);
		}

		[Test]
		public void ZeroEffectivenessConsumesIntervalWithoutBankingIt()
		{
			KingdomScaffoldLabourStep idle = KingdomScaffoldLabourRules.Advance(
				100L, 200L, 100L, 0);
			Assert.AreEqual(0L, idle.WorkedTicks);
			Assert.AreEqual(200L, idle.NextTick);
			KingdomScaffoldLabourStep resumed = KingdomScaffoldLabourRules.Advance(
				idle.NextTick, 250L, idle.RemainingTicks, 100);
			Assert.AreEqual(50L, resumed.WorkedTicks);
			Assert.AreEqual(50L, resumed.RemainingTicks);
		}

		private static KingdomScaffoldLabourStep Advance(KingdomScaffoldLabourWindow Window,
			long LastTick, long RemainingTicks, long Now)
		{
			Assert.IsTrue(KingdomScaffoldLabourWindowRules.TryEncode(
				Window, out string encoded));
			Assert.IsTrue(KingdomScaffoldLabourWindowRules.TryForInterval(
				encoded, LastTick, out var prior));
			return KingdomScaffoldLabourRules.Advance(LastTick, Now, RemainingTicks,
				prior.EffectivenessPercent);
		}

		private static KingdomScaffoldLabourWindow Window(long Tick, int Effectiveness,
			int Hands, bool Selected)
		{
			return new KingdomScaffoldLabourWindow
			{
				Tick = Tick,
				EffectivenessPercent = Effectiveness,
				Hands = Hands,
				Selected = Selected
			};
		}
	}
}
#endif
