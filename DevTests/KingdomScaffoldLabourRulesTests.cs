#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomScaffoldLabourRulesTests
	{
		[Test]
		public void CanonicalWindowRoundTripsExactBoundedWitness()
		{
			KingdomScaffoldLabourWindow expected = Window(25L, 50, 1, true);
			ClassicAssert.IsTrue(KingdomScaffoldLabourWindowRules.TryEncode(
				expected, out string encoded));
			ClassicAssert.AreEqual("s1|25|50|1|1", encoded);
			ClassicAssert.IsTrue(KingdomScaffoldLabourWindowRules.TryDecode(encoded, out var actual));
			ClassicAssert.AreEqual(expected.Tick, actual.Tick);
			ClassicAssert.AreEqual(expected.EffectivenessPercent, actual.EffectivenessPercent);
			ClassicAssert.AreEqual(expected.Hands, actual.Hands);
			ClassicAssert.AreEqual(expected.Selected, actual.Selected);
		}

		[Test]
		public void CodecRejectsUnboundedContradictoryAndNoncanonicalWitnesses()
		{
			ClassicAssert.IsFalse(KingdomScaffoldLabourWindowRules.TryEncode(
				Window(0L, 101, 2, true), out _));
			ClassicAssert.IsFalse(KingdomScaffoldLabourWindowRules.TryEncode(
				Window(0L, 100, 3, true), out _));
			ClassicAssert.IsFalse(KingdomScaffoldLabourWindowRules.TryEncode(
				Window(0L, 50, 1, false), out _));
			ClassicAssert.IsFalse(KingdomScaffoldLabourWindowRules.TryDecode(
				"s1|01|50|1|1", out _));
		}

		[Test]
		public void MissingMalformedOrWrongAnchorNeverPricesInterval()
		{
			ClassicAssert.IsFalse(KingdomScaffoldLabourWindowRules.TryForInterval(
				null, 10L, out _));
			ClassicAssert.IsFalse(KingdomScaffoldLabourWindowRules.TryForInterval(
				"bad", 10L, out _));
			ClassicAssert.IsTrue(KingdomScaffoldLabourWindowRules.TryEncode(
				Window(9L, 100, 2, true), out string stale));
			ClassicAssert.IsFalse(KingdomScaffoldLabourWindowRules.TryForInterval(
				stale, 10L, out _));
		}

		[Test]
		public void MissingWindowSpendsOldIntervalThenCurrentWitnessWorksOnlyForward()
		{
			ClassicAssert.IsFalse(KingdomScaffoldLabourWindowRules.TryForInterval(
				null, 10L, out _));
			KingdomScaffoldLabourStep wake = KingdomScaffoldLabourRules.Advance(
				10L, 60L, 100L, 0);
			ClassicAssert.AreEqual(0L, wake.WorkedTicks);
			ClassicAssert.AreEqual(100L, wake.RemainingTicks);
			ClassicAssert.AreEqual(60L, wake.NextTick);

			KingdomScaffoldLabourWindow current = Window(60L, 100, 2, true);
			ClassicAssert.IsTrue(KingdomScaffoldLabourWindowRules.TryEncode(
				current, out string encoded));
			ClassicAssert.IsTrue(KingdomScaffoldLabourWindowRules.TryForInterval(
				encoded, wake.NextTick, out var prior));
			KingdomScaffoldLabourStep later = KingdomScaffoldLabourRules.Advance(
				wake.NextTick, 110L, wake.RemainingTicks, prior.EffectivenessPercent);
			ClassicAssert.AreEqual(50L, later.WorkedTicks);
			ClassicAssert.AreEqual(50L, later.RemainingTicks);
		}

		[Test]
		public void SameTickWitnessAnchorsOnlyTheFollowingInterval()
		{
			KingdomScaffoldLabourStep sameTick = KingdomScaffoldLabourRules.Advance(
				50L, 50L, 100L, 100);
			ClassicAssert.AreEqual(50L, sameTick.NextTick);
			ClassicAssert.AreEqual(100L, sameTick.RemainingTicks);
			ClassicAssert.AreEqual(0L, sameTick.WorkedTicks);

			KingdomScaffoldLabourStep later = Advance(
				Window(50L, 100, 2, true), 50L, 100L, 80L);
			ClassicAssert.AreEqual(30L, later.WorkedTicks);
			ClassicAssert.AreEqual(70L, later.RemainingTicks);
		}

		[Test]
		public void SamePassArrivalCannotWorkAbsenceButWorksFollowingInterval()
		{
			KingdomScaffoldLabourStep wake = Advance(
				Window(0L, 50, 1, true), 0L, 200L, 100L);
			ClassicAssert.AreEqual(50L, wake.WorkedTicks);
			ClassicAssert.AreEqual(150L, wake.RemainingTicks);
			KingdomScaffoldLabourStep later = Advance(
				Window(100L, 100, 2, true), wake.NextTick,
				wake.RemainingTicks, 200L);
			ClassicAssert.AreEqual(100L, later.WorkedTicks);
			ClassicAssert.AreEqual(50L, later.RemainingTicks);
		}

		[Test]
		public void CompletionCeilingIsExactAndDoesNotOverflow()
		{
			KingdomScaffoldLabourStep quantised = KingdomScaffoldLabourRules.Advance(
				100L, 102L, 1L, 50);
			ClassicAssert.IsTrue(quantised.Complete);
			ClassicAssert.AreEqual(102L, quantised.CompletionTick);

			long half = long.MaxValue / 2L;
			KingdomScaffoldLabourStep huge = KingdomScaffoldLabourRules.Advance(
				0L, long.MaxValue, half, 50);
			ClassicAssert.IsTrue(huge.Complete);
			ClassicAssert.AreEqual(half, huge.WorkedTicks);
			ClassicAssert.AreEqual(long.MaxValue - 1L, huge.CompletionTick);
		}

		[Test]
		public void ZeroEffectivenessConsumesIntervalWithoutBankingIt()
		{
			KingdomScaffoldLabourStep idle = KingdomScaffoldLabourRules.Advance(
				100L, 200L, 100L, 0);
			ClassicAssert.AreEqual(0L, idle.WorkedTicks);
			ClassicAssert.AreEqual(200L, idle.NextTick);
			KingdomScaffoldLabourStep resumed = KingdomScaffoldLabourRules.Advance(
				idle.NextTick, 250L, idle.RemainingTicks, 100);
			ClassicAssert.AreEqual(50L, resumed.WorkedTicks);
			ClassicAssert.AreEqual(50L, resumed.RemainingTicks);
		}

		private static KingdomScaffoldLabourStep Advance(KingdomScaffoldLabourWindow Window,
			long LastTick, long RemainingTicks, long Now)
		{
			ClassicAssert.IsTrue(KingdomScaffoldLabourWindowRules.TryEncode(
				Window, out string encoded));
			ClassicAssert.IsTrue(KingdomScaffoldLabourWindowRules.TryForInterval(
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
