using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomGovernanceRulesTests
	{
		[Test]
		public void OnlyCommittedChargesAndCloses()
		{
			foreach (KingdomGovernanceResult result in new KingdomGovernanceResult[]
			{
				KingdomGovernanceResult.Read,
				KingdomGovernanceResult.Cancelled,
				KingdomGovernanceResult.Failed,
				KingdomGovernanceResult.Bookkeeping
			})
			{
				ClassicAssert.IsFalse(KingdomGovernanceRules.Charges(result), result.ToString());
				ClassicAssert.IsFalse(KingdomGovernanceRules.ClosesInterface(result), result.ToString());
			}
			ClassicAssert.IsTrue(KingdomGovernanceRules.Charges(KingdomGovernanceResult.Committed));
			ClassicAssert.IsTrue(KingdomGovernanceRules.ClosesInterface(KingdomGovernanceResult.Committed));
			ClassicAssert.AreEqual(1000, KingdomGovernanceRules.NominalEnergyCost);
		}

		[TestCase(null, "TAF Governance act")]
		[TestCase("", "TAF Governance act")]
		[TestCase("  claim ground  ", "TAF Governance claim ground")]
		public void EnergyReasonIsStable(string verb, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomGovernanceRules.EnergyReason(verb));
		}
	}
}
