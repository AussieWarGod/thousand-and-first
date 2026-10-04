#if TAF_TESTS
using System.Collections.Generic;
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

		[TestCase("exact", true)]
		[TestCase("water-extra", false)]
		[TestCase("stone-extra", false)]
		[TestCase("timber-unpaid", false)]
		[TestCase("unasked-brush", false)]
		public void HeartBillRequiresExactCompositeDebit(string fault, bool accepted)
		{
			var materials = new KingdomMaterialTally();
			materials.Set(KingdomMaterial.Stone, 24);
			materials.Set(KingdomMaterial.Timber, 1);
			var price = new KingdomMaterialDebitCost(materials);
			var claim = KingdomConstructionRules.NewClaims(18, price);
			ClassicAssert.IsTrue(KingdomConstructionRules.TryApplyWaterAttempt(
				claim, 18, 18, 0, 18, true, out claim));
			claim.MaterialSpent = claim.MaterialLost = price.ToClaimString();
			claim.MaterialOutstanding = new KingdomMaterialDebitCost().ToClaimString();
			if (fault == "water-extra") claim.WaterLost++;
			if (fault == "stone-extra")
			{
				materials.Set(KingdomMaterial.Stone, 25);
				claim.MaterialLost = new KingdomMaterialDebitCost(materials).ToClaimString();
			}
			if (fault == "timber-unpaid")
			{
				materials.Set(KingdomMaterial.Timber, 0);
				claim.MaterialSpent = claim.MaterialLost = new KingdomMaterialDebitCost(materials).ToClaimString();
				var remaining = new KingdomMaterialTally(); remaining.Set(KingdomMaterial.Timber, 1);
				claim.MaterialOutstanding = new KingdomMaterialDebitCost(remaining).ToClaimString();
			}
			if (fault == "unasked-brush")
			{
				materials.Set(KingdomMaterial.Brush, 1);
				claim.MaterialRequested = claim.MaterialSpent = claim.MaterialLost =
					new KingdomMaterialDebitCost(materials).ToClaimString();
			}
			ClassicAssert.AreEqual(accepted,
				KingdomQuickstartBuildClaims.CleanFirstPayment(claim, 18, price));
		}

		// The arcology's own price shape (RuntimeData/KingdomBuildings.xml:1409): materials beside
		// its Bits="00346" and Exotics="ingot:3,gem:2" (#264 review).
		private static KingdomMaterialDebitCost Composite()
		{
			var materials = new KingdomMaterialTally();
			materials.Set(KingdomMaterial.Stone, 40);
			materials.Set(KingdomMaterial.Timber, 1);
			var bits = new KingdomBitTally();
			bits.Set(0, 2); bits.Set(3, 1); bits.Set(4, 1); bits.Set(6, 1);
			var exotics = new KingdomExoticTally();
			exotics.Set(KingdomExotic.Ingot, 3); exotics.Set(KingdomExotic.Gem, 2);
			return new KingdomMaterialDebitCost(materials, bits, exotics);
		}

		private static KingdomBitTally Bits(KingdomBitTally Start, params int[] TierThenCount)
		{
			var bits = Start.Copy();
			for (int i = 0; i + 1 < TierThenCount.Length; i += 2) bits.Add(TierThenCount[i], TierThenCount[i + 1]);
			return bits;
		}

		/// <summary>A first composite payment answered in full whose physical loss carries
		/// <paramref name="LostBits"/> - more than the priced bits when the bodies broken up held a
		/// surplus, as Growth/KingdomMaterialDebitRules.Planning.cs AddLost records.</summary>
		private static KingdomConstructionClaims PaidComposite(KingdomMaterialDebitCost Price,
			KingdomBitTally LostBits)
		{
			var claim = KingdomConstructionRules.NewClaims(94, Price);
			ClassicAssert.IsTrue(KingdomConstructionRules.TryApplyWaterAttempt(
				claim, 94, 94, 0, 94, true, out claim));
			claim.MaterialSpent = Price.ToClaimString();
			claim.MaterialLost = new KingdomMaterialDebitCost(Price.Materials, LostBits, Price.Exotics)
				.ToClaimString();
			claim.MaterialOutstanding = new KingdomMaterialDebitCost().ToClaimString();
			ClassicAssert.IsTrue(KingdomConstructionRules.ValidateClaims(claim));
			return claim;
		}

		[Test]
		public void ACompositeClaimIsCleanOnlyAtItsCompositePriceNeverThePlainTally()
		{
			var price = Composite();
			var claim = PaidComposite(price, price.Bits);
			ClassicAssert.IsTrue(KingdomQuickstartBuildClaims.CleanFirstPayment(claim, 94, price));
			var plain = new KingdomMaterialDebitCost(price.Materials);
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstPayment(claim, 94, plain));
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(claim, 94, plain,
				price.Bits));
		}

		[Test]
		public void CompositePaymentAdmitsExactlyTheWitnessedBitSurplus()
		{
			var price = Composite();
			var surplus = Bits(price.Bits, 0, 3, 1, 2, 2, 1, 5, 1);
			var claim = PaidComposite(price, surplus);
			// Whole bodies carry surplus, so the plain clean predicate can never pass this payment.
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstPayment(claim, 94, price));
			ClassicAssert.IsTrue(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(claim, 94, price,
				surplus));
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(claim, 94, price,
				price.Bits));
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(claim, 94, price,
				Bits(surplus, 6, 1)));
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(claim, 94,
				new KingdomMaterialDebitCost(price.Materials), surplus));
		}

		[TestCase("material-lost-extra")]
		[TestCase("exotic-lost-extra")]
		[TestCase("bits-outstanding")]
		[TestCase("water-extra")]
		[TestCase("uncertain")]
		[TestCase("plain-requested")]
		public void CompositePaymentNeverAdmitsAnUnansweredOrForeignLoss(string fault)
		{
			var price = Composite();
			var surplus = Bits(price.Bits, 0, 3);
			var claim = PaidComposite(price, surplus);
			switch (fault)
			{
				case "material-lost-extra":
					var stone = price.Materials.Copy(); stone.Set(KingdomMaterial.Stone, 41);
					claim.MaterialLost = new KingdomMaterialDebitCost(stone, surplus, price.Exotics).ToClaimString(); break;
				case "exotic-lost-extra":
					var gems = price.Exotics.Copy(); gems.Set(KingdomExotic.Gem, 3);
					claim.MaterialLost = new KingdomMaterialDebitCost(price.Materials, surplus, gems).ToClaimString(); break;
				case "bits-outstanding":
					claim.MaterialSpent = claim.MaterialLost = new KingdomMaterialDebitCost(price.Materials, null,
						price.Exotics).ToClaimString();
					claim.MaterialOutstanding = new KingdomMaterialDebitCost(null, price.Bits).ToClaimString(); break;
				case "water-extra": claim.WaterLost++; break;
				case "uncertain": claim.Exact = false; break;
				case "plain-requested":
					claim.MaterialRequested = new KingdomMaterialDebitCost(price.Materials).ToClaimString(); break;
			}
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(claim, 94, price,
				surplus), fault);
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(null, 94, price, surplus));
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(PaidComposite(price,
				surplus), 94, null, surplus));
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(PaidComposite(price,
				surplus), 94, price, null));
			ClassicAssert.IsFalse(KingdomQuickstartBuildClaims.CleanFirstCompositePayment(PaidComposite(price,
				surplus), -1, price, surplus));
		}

		[Test]
		public void WithThePricedBitsWitnessedTheCompositePredicateIsCleanFirstPayment()
		{
			var price = Composite();
			var claims = new List<KingdomConstructionClaims> { PaidComposite(price, price.Bits),
				PaidComposite(price, Bits(price.Bits, 4, 2)), Paid() };
			var extra = PaidComposite(price, price.Bits); extra.WaterLost++; claims.Add(extra);
			var plain = Paid(); plain.Exact = false; claims.Add(plain);
			int accepted = 0;
			foreach (var claim in claims)
				foreach (var cost in new[] { price, Price() })
					foreach (int water in new[] { 2, 94 })
					{
						bool clean = KingdomQuickstartBuildClaims.CleanFirstPayment(claim, water, cost);
						ClassicAssert.AreEqual(clean,
							KingdomQuickstartBuildClaims.CleanFirstCompositePayment(claim, water, cost, cost.Bits));
						if (clean) accepted++;
					}
			// Not vacuous: the exact composite and the exact plain payments are both accepted.
			ClassicAssert.AreEqual(2, accepted);
		}
	}
}
#endif
