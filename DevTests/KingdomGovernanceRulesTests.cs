using NUnit.Framework;

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
				Assert.IsFalse(KingdomGovernanceRules.Charges(result), result.ToString());
				Assert.IsFalse(KingdomGovernanceRules.ClosesInterface(result), result.ToString());
			}
			Assert.IsTrue(KingdomGovernanceRules.Charges(KingdomGovernanceResult.Committed));
			Assert.IsTrue(KingdomGovernanceRules.ClosesInterface(KingdomGovernanceResult.Committed));
			Assert.AreEqual(1000, KingdomGovernanceRules.NominalEnergyCost);
		}

		[TestCase(null, "TAF Governance act")]
		[TestCase("", "TAF Governance act")]
		[TestCase("  claim ground  ", "TAF Governance claim ground")]
		public void EnergyReasonIsStable(string verb, string expected)
		{
			Assert.AreEqual(expected, KingdomGovernanceRules.EnergyReason(verb));
		}
	}
}
