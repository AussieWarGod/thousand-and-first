#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomChargingRulesTests
	{
		[TestCase(-100, 0)]
		[TestCase(0, 0)]
		[TestCase(1, 1)]
		[TestCase(50, 75)]
		[TestCase(99, 148)]
		[TestCase(100, 150)]
		[TestCase(250, 150)]
		public void OutputIsStaffedAndBounded(int effectiveness, int expected)
		{
			Assert.AreEqual(expected, KingdomChargingRules.Output(effectiveness));
		}
	}
}
#endif
