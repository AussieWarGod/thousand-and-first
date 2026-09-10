#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomQuickstartBuildClaimsTests
	{
		private static KingdomMaterialDebitCost Price()
		{
			var tally = new KingdomMaterialTally();
			tally.Set(KingdomMaterial.Timber, 1);
			return new KingdomMaterialDebitCost(tally);
		}

		private static KingdomConstructionClaims Paid()
		{
			var claim = KingdomConstructionRules.NewClaims(2, Price());
			ClassicAssert.IsTrue(KingdomConstructionRules.TryApplyWaterAttempt(
				claim, 2, 2, 0, 2, true, out claim));
			claim.MaterialSpent = claim.MaterialRequested;
			claim.MaterialLost = claim.MaterialRequested;
			claim.MaterialOutstanding = new KingdomMaterialDebitCost().ToClaimString();
			ClassicAssert.IsTrue(KingdomConstructionRules.ValidateClaims(claim));
			return claim;
		}

		[Test]
		public void ExactPaymentHasNetLossEqualToSpentNotZero()
		{
			var claim = Paid();
			ClassicAssert.AreEqual(2, claim.WaterLost);
			ClassicAssert.IsTrue(KingdomQuickstartBuildClaims.CleanFirstPayment(claim, 2, Price()));
		}

		[TestCase("water-loss-zero")]
		[TestCase("water-loss-extra")]
		[TestCase("water-outstanding")]
		[TestCase("water-price")]
		[TestCase("material-loss-zero")]
		[TestCase("material-loss-extra")]
		[TestCase("material-outstanding")]
		[TestCase("material-spent-zero")]
		[TestCase("material-price")]
		[TestCase("uncertain")]
		public void IncompleteContradictoryOrExcessDebitNeverPasses(string fault)
		{
			var claim = Paid();
			string empty = new KingdomMaterialDebitCost().ToClaimString();
			switch (fault)
			{
				case "water-loss-zero": claim.WaterLost = 0; break;
				case "water-loss-extra": claim.WaterLost = 3; break;
				case "water-outstanding": claim.WaterSpent = 1; claim.WaterOutstanding = 1; break;
				case "water-price": claim.WaterRequested = claim.WaterSpent = claim.WaterLost = 3; break;
				case "material-loss-zero": claim.MaterialLost = empty; break;
				case "material-loss-extra":
					var extra = new KingdomMaterialTally(); extra.Set(KingdomMaterial.Timber, 2);
					claim.MaterialLost = new KingdomMaterialDebitCost(extra).ToClaimString(); break;
				case "material-outstanding": claim.MaterialSpent = empty; claim.MaterialOutstanding = claim.MaterialRequested; break;
				case "material-spent-zero": claim.MaterialSpent = empty; break;
				case "material-price": claim.MaterialRequested = claim.MaterialSpent = claim.MaterialLost = empty; break;
				case "uncertain": claim.Exact = false; break;
			}
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstPayment(claim, 2, Price()));
		}

		[Test]
		public void MissingClaimOrQuoteAndNegativeCostRefuse()
		{
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstPayment(null, 2, Price()));
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstPayment(Paid(), 2, null));
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstPayment(Paid(), -1, Price()));
		}
	}
}
#endif
