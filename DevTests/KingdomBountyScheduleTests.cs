#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomBountyScheduleTests
	{
		private const string Settlement = "taf:settlement:schedule";

		private static readonly List<string> Roster = new List<string> { "Aeru", "Voss", "Kest" };

		[Test]
		public void FirstAttempt_IsOneAbsoluteQudDayAfterPosting()
		{
			long tick;
			Assert.IsTrue(KingdomBountyRules.TryFirstAttemptTick(5000L, out tick));
			Assert.AreEqual(6200L, tick);
			Assert.IsTrue(KingdomBountyRules.TryFirstAttemptTick(-9L, out tick));
			Assert.AreEqual(KingdomBountyRules.AttemptIntervalTicks, tick);
		}

		[Test]
		public void ReentryBeforeDue_HasNoAttemptToResolve()
		{
			for (int visits = 0; visits < 1000; visits++)
			{
				Assert.AreEqual(0, KingdomBountyRules.DueAttemptPrefix(6199L, 6200L,
					Exhausted: false, KingdomBountyRules.MaxAttemptsPerSettlementPass));
			}
		}

		[Test]
		public void LegacyMigration_StartsStrictlyAfterNowOnOriginalAlignment()
		{
			long tick;
			Assert.IsTrue(KingdomBountyRules.TryAttemptAfter(6200L, 5000L, out tick));
			Assert.AreEqual(7400L, tick);
			Assert.IsTrue(KingdomBountyRules.TryAttemptAfter(7000L, 5000L, out tick));
			Assert.AreEqual(7400L, tick);
			Assert.IsTrue(KingdomBountyRules.TryAttemptAfter(5000L, 5000L, out tick));
			Assert.AreEqual(6200L, tick);
		}

		[Test]
		public void AttemptArithmetic_RefusesLongOverflow()
		{
			long ignored;
			Assert.IsFalse(KingdomBountyRules.TryFirstAttemptTick(long.MaxValue, out ignored));
			Assert.IsFalse(KingdomBountyRules.TryAdvanceAttemptTick(long.MaxValue, out ignored));
			Assert.IsFalse(KingdomBountyRules.TryAttemptAfter(long.MaxValue, 0L, out ignored));
			Assert.AreEqual(long.MaxValue, KingdomBountyRules.WorkDueTick(long.MaxValue - 10L, 1));
			Assert.AreEqual(0L, KingdomBountyRules.WorkDueTick(5000L, 0));
		}

		[Test]
		public void DuePrefix_CapsComputationWithoutSkippingItsCursor()
		{
			long next = 1200L;
			long now = 1200L + 10000L * KingdomBountyRules.AttemptIntervalTicks;
			int count = KingdomBountyRules.DueAttemptPrefix(now, next, false, 7);
			Assert.AreEqual(7, count);
			for (int i = 0; i < count; i++)
			{
				Assert.IsTrue(KingdomBountyRules.TryAdvanceAttemptTick(next, out next));
			}
			Assert.AreEqual(1200L + 7L * KingdomBountyRules.AttemptIntervalTicks, next,
				"cap jumped over unresolved truth");
			Assert.Greater(KingdomBountyRules.DueAttemptPrefix(now, next, false, 7), 0,
				"unresolved suffix was burned");
		}

		[Test]
		public void LatestDueAttempt_SkipsHistoricalRosterOpportunities()
		{
			long latest;
			long skipped;
			Assert.IsTrue(KingdomBountyRules.TryLatestDueAttempt(1200L, 1200L, false,
				out latest, out skipped));
			Assert.AreEqual(1200L, latest);
			Assert.AreEqual(0L, skipped);

			Assert.IsTrue(KingdomBountyRules.TryLatestDueAttempt(1200L * 11L + 50L,
				1200L, false, out latest, out skipped));
			Assert.AreEqual(1200L * 11L, latest);
			Assert.AreEqual(10L, skipped);
			Assert.Less(1200L * 11L + 50L - latest, KingdomBountyRules.AttemptIntervalTicks);
		}

		[Test]
		public void LatestDueAttempt_RefusesFutureAndExhaustedCursors()
		{
			long latest;
			long skipped;
			Assert.IsFalse(KingdomBountyRules.TryLatestDueAttempt(1199L, 1200L, false,
				out latest, out skipped));
			Assert.IsFalse(KingdomBountyRules.TryLatestDueAttempt(1200L, 1200L, true,
				out latest, out skipped));
		}

		[Test]
		public void ScheduledOutcome_IsStableAtOneAbsoluteTick()
		{
			string stream = KingdomBountyRules.NoticeEventStream("42");
			for (int i = 0; i < 50; i++)
			{
				long tick = 1200L + i * KingdomBountyRules.AttemptIntervalTicks;
				KingdomBountyRules.BountyAttempt a = KingdomBountyRules.ResolveScheduled(
					Settlement, stream, tick, Roster, BountyTask.Fetch, 12);
				KingdomBountyRules.BountyAttempt b = KingdomBountyRules.ResolveScheduled(
					Settlement, stream, tick, Roster, BountyTask.Fetch, 12);
				Assert.IsTrue(a.Determined);
				Assert.AreEqual(a.Outcome, b.Outcome);
				Assert.AreEqual(a.Name, b.Name);
				Assert.AreEqual(a.TasteMatched, b.TasteMatched);
			}
		}

		[Test]
		public void PartitionedScheduleEnumeration_ProducesSameDeterministicDraws()
		{
			string stream = KingdomBountyRules.NoticeEventStream("77");
			List<string> whole = ResolveRange(stream, 1200L, 80, 80);
			List<string> partitioned = ResolveRange(stream, 1200L, 80, 3);
			CollectionAssert.AreEqual(whole, partitioned);
		}

		private static List<string> ResolveRange(string Stream, long First, int Count, int Chunk)
		{
			List<string> outcomes = new List<string>();
			long next = First;
			int left = Count;
			while (left > 0)
			{
				int take = (left < Chunk) ? left : Chunk;
				for (int i = 0; i < take; i++)
				{
					KingdomBountyRules.BountyAttempt attempt = KingdomBountyRules.ResolveScheduled(
						Settlement, Stream, next, Roster, BountyTask.Scouting, 8);
					Assert.IsTrue(attempt.Determined);
					outcomes.Add(attempt.Outcome + "/" + (attempt.Name ?? "-"));
					Assert.IsTrue(KingdomBountyRules.TryAdvanceAttemptTick(next, out next));
				}
				left -= take;
			}
			return outcomes;
		}

		[Test]
		public void DifferentNoticeIdentity_SeparatesSameTickDraws()
		{
			string aStream = KingdomBountyRules.NoticeEventStream("101");
			string bStream = KingdomBountyRules.NoticeEventStream("102");
			int differences = 0;
			for (int i = 0; i < 100; i++)
			{
				long tick = 1200L + i * KingdomBountyRules.AttemptIntervalTicks;
				KingdomBountyRules.BountyAttempt a = KingdomBountyRules.ResolveScheduled(
					Settlement, aStream, tick, Roster, BountyTask.Clearance, 8);
				KingdomBountyRules.BountyAttempt b = KingdomBountyRules.ResolveScheduled(
					Settlement, bStream, tick, Roster, BountyTask.Clearance, 8);
				if (a.Outcome != b.Outcome || a.Name != b.Name)
				{
					differences++;
				}
			}
			Assert.Greater(differences, 0);
		}

		[Test]
		public void KernelRefusal_IsUndeterminedSoCallerCannotBurnTruth()
		{
			KingdomBountyRules.BountyAttempt badSettlement = KingdomBountyRules.ResolveScheduled(
				"bad", KingdomBountyRules.NoticeEventStream("7"), 1200L, Roster,
				BountyTask.Fetch, 8);
			Assert.IsFalse(badSettlement.Determined);
			KingdomBountyRules.BountyAttempt badStream = KingdomBountyRules.ResolveScheduled(
				Settlement, "bad", 1200L, Roster, BountyTask.Fetch, 8);
			Assert.IsFalse(badStream.Determined);
		}

		[Test]
		public void EmptyRoster_IsDeterminedNobodyNotAKernelFailure()
		{
			KingdomBountyRules.BountyAttempt attempt = KingdomBountyRules.ResolveScheduled(
				Settlement, KingdomBountyRules.NoticeEventStream("8"), 1200L,
				new List<string>(), BountyTask.Fetch, 8);
			Assert.IsTrue(attempt.Determined);
			Assert.AreEqual(BountyOutcome.NobodyTried, attempt.Outcome);
		}

		[Test]
		public void NoticeStream_IsStableBoundedAndGrammarSafeForHostileIds()
		{
			string a = KingdomBountyRules.NoticeEventStream("ABC / 123");
			string b = KingdomBountyRules.NoticeEventStream("ABC / 123");
			Assert.AreEqual(a, b);
			Assert.IsTrue(a.StartsWith("taf:bounty:notice:v2:"));
			Assert.LessOrEqual(a.Length, 128);
			Assert.IsTrue(KingdomBountyRules.ResolveScheduled(Settlement, a, 1200L,
				Roster, BountyTask.Fetch, 8).Determined);
		}
	}
}
#endif
