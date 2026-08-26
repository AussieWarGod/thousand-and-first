#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomConstructionPresenceRulesTests
	{
		private static KingdomRaisingCandidate C(string id, long tick, int x, int y)
		{
			return new KingdomRaisingCandidate(id, tick, x, y);
		}

		[Test]
		public void ConcurrentRaisingsSelectOnlyTheOldest()
		{
			var candidates = new List<KingdomRaisingCandidate>
			{
				C("new", 200, 1, 1), C("old", 100, 70, 20), C("middle", 150, 2, 2)
			};
			KingdomRaisingPlan plan = KingdomConstructionPresenceRules.Plan(candidates, 20, 2);
			Assert.AreEqual(1, plan.SelectedIndex);
			Assert.AreEqual(2, plan.AssignedHands);
		}

		[Test]
		public void EnumerationOrderCannotChangeTheChosenRaising()
		{
			var forward = new List<KingdomRaisingCandidate>
			{
				C("a", 20, 1, 1), C("b", 10, 5, 5)
			};
			var reverse = new List<KingdomRaisingCandidate>
			{
				C("b", 10, 5, 5), C("a", 20, 1, 1)
			};
			Assert.AreEqual("b", forward[KingdomConstructionPresenceRules.Oldest(forward)].ObjectId);
			Assert.AreEqual("b", reverse[KingdomConstructionPresenceRules.Oldest(reverse)].ObjectId);
		}

		[Test]
		public void EqualStartTicksUseNorthWestThenStableIdentity()
		{
			var candidates = new List<KingdomRaisingCandidate>
			{
				C("z", 10, 3, 4), C("z2", 10, 2, 4), C("b", 10, 7, 3),
				C("a", 10, 7, 3)
			};
			Assert.AreEqual("a", candidates[KingdomConstructionPresenceRules.Oldest(candidates)].ObjectId);
		}

		[TestCase(-1, 2, 0)]
		[TestCase(0, 2, 0)]
		[TestCase(1, 2, 1)]
		[TestCase(9, 2, 2)]
		[TestCase(9, 0, 0)]
		public void AssignedBodiesAreBoundedByAvailabilityAndGangSize(int available, int wanted,
			int expected)
		{
			var candidates = new List<KingdomRaisingCandidate> { C("one", 1, 1, 1) };
			Assert.AreEqual(expected,
				KingdomConstructionPresenceRules.Plan(candidates, available, wanted).AssignedHands);
		}

		[Test]
		public void InvalidCandidatesNeverMintASelection()
		{
			var candidates = new List<KingdomRaisingCandidate> { C(null, 1, 1, 1), C("", 0, 0, 0) };
			Assert.AreEqual(-1, KingdomConstructionPresenceRules.Plan(candidates, 2, 2).SelectedIndex);
		}

		[Test]
		public void QueueTellingNamesBothWorksAndTheHandsSpentOnceLaw()
		{
			Assert.AreEqual("The kiln waits. The settlement's raising gang is committed first to "
				+ "the cistern; the same hands cannot stand at two frames.",
				KingdomConstructionPresenceRules.QueueLine("kiln", "cistern"));
		}
	}
}
#endif
