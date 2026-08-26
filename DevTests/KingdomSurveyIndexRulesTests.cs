#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomSurveyIndexRulesTests
	{
		[Test]
		public void MutationClassificationIsTotalAndFailClosed()
		{
			for (int mask = 0; mask < 8; mask++)
			{
				bool known = (mask & 1) != 0;
				bool valid = (mask & 2) != 0;
				bool here = (mask & 4) != 0;
				KingdomSurveyIndexRules.Mutation expected = known
					? (valid && here ? KingdomSurveyIndexRules.Mutation.Refresh
						: KingdomSurveyIndexRules.Mutation.Remove)
					: (valid && here ? KingdomSurveyIndexRules.Mutation.Add
						: KingdomSurveyIndexRules.Mutation.Refuse);
				Assert.AreEqual(expected,
					KingdomSurveyIndexRules.Classify(known, valid, here), "mask " + mask);
			}
		}

		[Test]
		public void CallbackTopologyReproofUsesOnlyObservedPostCallbackState()
		{
			Assert.AreEqual(KingdomSurveyIndexRules.Mutation.Refresh,
				KingdomSurveyIndexRules.Classify(true, true, true));
			Assert.AreEqual(KingdomSurveyIndexRules.Mutation.Remove,
				KingdomSurveyIndexRules.Classify(true, false, false));
			Assert.AreEqual(KingdomSurveyIndexRules.Mutation.Remove,
				KingdomSurveyIndexRules.Classify(true, true, false));
			Assert.AreEqual(KingdomSurveyIndexRules.Mutation.Add,
				KingdomSurveyIndexRules.Classify(false, true, true));
			Assert.AreEqual(KingdomSurveyIndexRules.Mutation.Refuse,
				KingdomSurveyIndexRules.Classify(false, true, false));
		}

		[Test]
		public void StableInsertionPreservesOrderAcrossRefreshRemoveAndAdd()
		{
			List<long> order = new List<long> { 2, 5, 5, 9 };
			Assert.AreEqual(0, KingdomSurveyIndexRules.StableInsertionIndex(order, 1));
			Assert.AreEqual(1, KingdomSurveyIndexRules.StableInsertionIndex(order, 3));
			Assert.AreEqual(3, KingdomSurveyIndexRules.StableInsertionIndex(order, 5));
			Assert.AreEqual(4, KingdomSurveyIndexRules.StableInsertionIndex(order, 12));

			order.RemoveAt(1);
			int refreshed = KingdomSurveyIndexRules.StableInsertionIndex(order, 5);
			order.Insert(refreshed, 5);
			CollectionAssert.AreEqual(new long[] { 2, 5, 5, 9 }, order);
			Assert.IsTrue(KingdomSurveyIndexRules.ComesBeforeOrEqual(5, 5));
			Assert.IsFalse(KingdomSurveyIndexRules.ComesBeforeOrEqual(9, 5));
		}

		[Test]
		public void MissingOrderEvidenceIsRejected()
		{
			Assert.Throws<ArgumentNullException>(delegate
			{
				KingdomSurveyIndexRules.StableInsertionIndex(null, 1);
			});
		}
	}
}
#endif
