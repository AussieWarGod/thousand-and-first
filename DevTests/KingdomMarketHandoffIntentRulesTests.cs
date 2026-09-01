#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomMarketHandoffIntentRulesTests
	{
		[TestCase(null, null, KingdomMarketHandoffIntentState.None)]
		[TestCase("target", null, KingdomMarketHandoffIntentState.FirstOnly)]
		[TestCase(null, "target", KingdomMarketHandoffIntentState.SecondOnly)]
		[TestCase("target", "target", KingdomMarketHandoffIntentState.Paired)]
		[TestCase("foreign", null, KingdomMarketHandoffIntentState.Divergent)]
		[TestCase(null, "foreign", KingdomMarketHandoffIntentState.Divergent)]
		[TestCase("target", "foreign", KingdomMarketHandoffIntentState.Divergent)]
		[TestCase("foreign", "target", KingdomMarketHandoffIntentState.Divergent)]
		public void ClassifiesEveryPersistedPair(string first, string second,
			KingdomMarketHandoffIntentState expected)
		{
			Assert.That(KingdomMarketHandoffIntentRules.Classify(first, "target",
				second, "target"), Is.EqualTo(expected));
		}

		[TestCase(null, null, true)]
		[TestCase("intent", null, true)]
		[TestCase(null, "prior", true)]
		[TestCase("intent", "prior", true)]
		[TestCase("other", "prior", false)]
		[TestCase("intent", "other", false)]
		public void BodyFieldsMayHaveDifferentExactValues(string intent, string prior,
			bool expected)
		{
			Assert.That(KingdomMarketHandoffIntentRules.ExactOrRecoverable(intent,
				"intent", prior, "prior"), Is.EqualTo(expected));
		}

		[TestCase(null, "target")]
		[TestCase("target", null)]
		[TestCase(null, null)]
		public void EmptyExpectedAuthorityIsAlwaysDivergent(string firstExpected,
			string secondExpected)
		{
			Assert.That(KingdomMarketHandoffIntentRules.Classify(null, firstExpected,
				null, secondExpected), Is.EqualTo(KingdomMarketHandoffIntentState.Divergent));
		}
	}
}
#endif
