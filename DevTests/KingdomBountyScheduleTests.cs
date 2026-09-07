#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
			ClassicAssert.IsTrue(KingdomBountyRules.TryFirstAttemptTick(5000L, out tick));
			ClassicAssert.AreEqual(6200L, tick);
			ClassicAssert.IsTrue(KingdomBountyRules.TryFirstAttemptTick(-9L, out tick));
			ClassicAssert.AreEqual(KingdomBountyRules.AttemptIntervalTicks, tick);
		}

		[Test]
		public void ReentryBeforeDue_HasNoAttemptToResolve()
		{
			for (int visits = 0; visits < 1000; visits++)
			{
				ClassicAssert.AreEqual(0, KingdomBountyRules.DueAttemptPrefix(6199L, 6200L,
					Exhausted: false, KingdomBountyRules.MaxAttemptsPerSettlementPass));
			}
		}

		[Test]
		public void LegacyMigration_StartsStrictlyAfterNowOnOriginalAlignment()
		{
			long tick;
			ClassicAssert.IsTrue(KingdomBountyRules.TryAttemptAfter(6200L, 5000L, out tick));
			ClassicAssert.AreEqual(7400L, tick);
			ClassicAssert.IsTrue(KingdomBountyRules.TryAttemptAfter(7000L, 5000L, out tick));
			ClassicAssert.AreEqual(7400L, tick);
			ClassicAssert.IsTrue(KingdomBountyRules.TryAttemptAfter(5000L, 5000L, out tick));
			ClassicAssert.AreEqual(6200L, tick);
		}

		[Test]
		public void AttemptArithmetic_RefusesLongOverflow()
		{
			long ignored;
			ClassicAssert.IsFalse(KingdomBountyRules.TryFirstAttemptTick(long.MaxValue, out ignored));
			ClassicAssert.IsFalse(KingdomBountyRules.TryAdvanceAttemptTick(long.MaxValue, out ignored));
			ClassicAssert.IsFalse(KingdomBountyRules.TryAttemptAfter(long.MaxValue, 0L, out ignored));
			ClassicAssert.AreEqual(long.MaxValue, KingdomBountyRules.WorkDueTick(long.MaxValue - 10L, 1));
			ClassicAssert.AreEqual(0L, KingdomBountyRules.WorkDueTick(5000L, 0));
		}

		[Test]
		public void DuePrefix_CapsComputationWithoutSkippingItsCursor()
		{
			long next = 1200L;
			long now = 1200L + 10000L * KingdomBountyRules.AttemptIntervalTicks;
			int count = KingdomBountyRules.DueAttemptPrefix(now, next, false, 7);
			ClassicAssert.AreEqual(7, count);
			for (int i = 0; i < count; i++)
			{
				ClassicAssert.IsTrue(KingdomBountyRules.TryAdvanceAttemptTick(next, out next));
			}
			ClassicAssert.AreEqual(1200L + 7L * KingdomBountyRules.AttemptIntervalTicks, next,
				"cap jumped over unresolved truth");
			ClassicAssert.Greater(KingdomBountyRules.DueAttemptPrefix(now, next, false, 7), 0,
				"unresolved suffix was burned");
		}

		[Test]
		public void LatestDueAttempt_SkipsHistoricalRosterOpportunities()
		{
			long latest;
			long skipped;
			ClassicAssert.IsTrue(KingdomBountyRules.TryLatestDueAttempt(1200L, 1200L, false,
				out latest, out skipped));
			ClassicAssert.AreEqual(1200L, latest);
			ClassicAssert.AreEqual(0L, skipped);

			ClassicAssert.IsTrue(KingdomBountyRules.TryLatestDueAttempt(1200L * 11L + 50L,
				1200L, false, out latest, out skipped));
			ClassicAssert.AreEqual(1200L * 11L, latest);
			ClassicAssert.AreEqual(10L, skipped);
			ClassicAssert.Less(1200L * 11L + 50L - latest, KingdomBountyRules.AttemptIntervalTicks);
		}

		[Test]
		public void LatestDueAttempt_RefusesFutureAndExhaustedCursors()
		{
			long latest;
			long skipped;
			ClassicAssert.IsFalse(KingdomBountyRules.TryLatestDueAttempt(1199L, 1200L, false,
				out latest, out skipped));
			ClassicAssert.IsFalse(KingdomBountyRules.TryLatestDueAttempt(1200L, 1200L, true,
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
				ClassicAssert.IsTrue(a.Determined);
				ClassicAssert.AreEqual(a.Outcome, b.Outcome);
				ClassicAssert.AreEqual(a.Name, b.Name);
				ClassicAssert.AreEqual(a.TasteMatched, b.TasteMatched);
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
					ClassicAssert.IsTrue(attempt.Determined);
					outcomes.Add(attempt.Outcome + "/" + (attempt.Name ?? "-"));
					ClassicAssert.IsTrue(KingdomBountyRules.TryAdvanceAttemptTick(next, out next));
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
			ClassicAssert.Greater(differences, 0);
		}

		[Test]
		public void KernelRefusal_IsUndeterminedSoCallerCannotBurnTruth()
		{
			KingdomBountyRules.BountyAttempt badSettlement = KingdomBountyRules.ResolveScheduled(
				"bad", KingdomBountyRules.NoticeEventStream("7"), 1200L, Roster,
				BountyTask.Fetch, 8);
			ClassicAssert.IsFalse(badSettlement.Determined);
			KingdomBountyRules.BountyAttempt badStream = KingdomBountyRules.ResolveScheduled(
				Settlement, "bad", 1200L, Roster, BountyTask.Fetch, 8);
			ClassicAssert.IsFalse(badStream.Determined);
		}

		[Test]
		public void EmptyRoster_IsDeterminedNobodyNotAKernelFailure()
		{
			KingdomBountyRules.BountyAttempt attempt = KingdomBountyRules.ResolveScheduled(
				Settlement, KingdomBountyRules.NoticeEventStream("8"), 1200L,
				new List<string>(), BountyTask.Fetch, 8);
			ClassicAssert.IsTrue(attempt.Determined);
			ClassicAssert.AreEqual(BountyOutcome.NobodyTried, attempt.Outcome);
		}

		[Test]
		public void NoticeStream_IsStableBoundedAndGrammarSafeForHostileIds()
		{
			string a = KingdomBountyRules.NoticeEventStream("ABC / 123");
			string b = KingdomBountyRules.NoticeEventStream("ABC / 123");
			ClassicAssert.AreEqual(a, b);
			ClassicAssert.IsTrue(a.StartsWith("taf:bounty:notice:v2:"));
			ClassicAssert.LessOrEqual(a.Length, 128);
			ClassicAssert.IsTrue(KingdomBountyRules.ResolveScheduled(Settlement, a, 1200L,
				Roster, BountyTask.Fetch, 8).Determined);
		}
	}
}
#endif
