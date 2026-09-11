#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Value tests for the generation-aware component census
	/// (<see cref="KingdomArchitectureComponentCensusRules"/>), over the exact tuple matrix in the
	/// retag census spec. These are EXECUTABLE decisions, not source pins: each case states what a
	/// surveyed candidate carries and what the census must make of it.
	/// <para>
	/// The case that motivated the rule: during the retag pass of an authored upgrade, the
	/// predecessor's component and the successor's retagged component share the lot and the
	/// layout-local slot name while standing on different world cells. Counting by lot and slot
	/// alone made a correct settlement look like a duplicate.
	/// </para>
	/// </summary>
	public class KingdomArchitectureComponentCensusTests
	{
		private const string Lot = "lot-1";
		private const string Slot = "g:01:01";
		private const string After = "token-after";
		private const string Before = "token-before";

		private static int Census(string Token, params string[][] Candidates)
		{
			int count = 0;
			foreach (string[] candidate in Candidates)
				if (KingdomArchitectureComponentCensusRules.Counts(Lot, Slot, Token,
					candidate[0], candidate[1], candidate[2])) count++;
			return count;
		}

		private static string[] Candidate(string CandidateLot, string CandidateSlot, string Token)
		{
			return new[] { CandidateLot, CandidateSlot, Token };
		}

		/// <summary>The retag case: both generations stand, and each generation's own census is
		/// one. This is the count that used to read as two and quarantine the settlement.</summary>
		[Test]
		public void BothGenerationsAtOneSlotEachCountExactlyOne()
		{
			string[] successor = Candidate(Lot, Slot, After);
			string[] predecessor = Candidate(Lot, Slot, Before);
			ClassicAssert.AreEqual(1, Census(After, successor, predecessor));
			ClassicAssert.AreEqual(1, Census(Before, successor, predecessor));
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.Settled(
				Census(After, successor, predecessor)));
		}

		/// <summary>A single component of the generation being settled.</summary>
		[Test]
		public void ASingleComponentOfThisGenerationSettles()
		{
			ClassicAssert.AreEqual(1, Census(After, Candidate(Lot, Slot, After)));
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.Settled(1));
		}

		/// <summary>Two copies of the SAME generation are still a duplicate, and still refuse.
		/// This is the whole point of the census and nothing here relaxes it.</summary>
		[Test]
		public void TwoComponentsOfOneGenerationStillCountTwoAndRefuse()
		{
			ClassicAssert.AreEqual(2,
				Census(After, Candidate(Lot, Slot, After), Candidate(Lot, Slot, After)));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Settled(2));
			ClassicAssert.AreEqual(2,
				Census(Before, Candidate(Lot, Slot, Before), Candidate(Lot, Slot, Before)));
		}

		/// <summary>An absent component is zero, which does not settle either.</summary>
		[Test]
		public void AnAbsentComponentDoesNotSettle()
		{
			ClassicAssert.AreEqual(0, Census(After, Candidate(Lot, Slot, Before)));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Settled(0));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Settled(-1));
		}

		/// <summary>Another lot's component, and another slot's, are not this census's subject.</summary>
		[Test]
		public void AnotherLotOrAnotherSlotIsNotCounted()
		{
			ClassicAssert.AreEqual(0, Census(After, Candidate("lot-2", Slot, After)));
			ClassicAssert.AreEqual(0, Census(After, Candidate(Lot, "g:02:01", After)));
		}

		/// <summary>A candidate missing any term is not counted -- and is refused elsewhere, by the
		/// element checks and the layout-slot cell guard, which this rule never replaces.</summary>
		[Test]
		public void AMissingTermIsNeverCounted()
		{
			foreach (string[] candidate in new[]
			{
				Candidate(null, Slot, After), Candidate("", Slot, After),
				Candidate(Lot, null, After), Candidate(Lot, "", After),
				Candidate(Lot, Slot, null), Candidate(Lot, Slot, ""),
			})
				ClassicAssert.AreEqual(0, Census(After, candidate));
		}

		/// <summary>The census refuses to run at all without its own three terms, so a caller
		/// cannot obtain a count from an incomplete question.</summary>
		[Test]
		public void ACensusWithoutItsOwnTermsCountsNothing()
		{
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Counts(
				null, Slot, After, Lot, Slot, After));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Counts(
				Lot, null, After, Lot, Slot, After));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Counts(
				Lot, Slot, null, Lot, Slot, After));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Counts(
				Lot, Slot, "", Lot, Slot, ""));
		}

		/// <summary>A foreign token on this lot and slot -- a third object, or one whose placement
		/// changed under it -- is not counted. It is refused by the checks that own that question,
		/// never excused by this one.</summary>
		[Test]
		public void AForeignTokenIsNotCountedAndIsNotTherebyExcused()
		{
			ClassicAssert.AreEqual(0, Census(After, Candidate(Lot, Slot, "token-foreign")));
			ClassicAssert.AreEqual(1,
				Census(After, Candidate(Lot, Slot, After), Candidate(Lot, Slot, "token-foreign")));
		}
	}
}
#endif
