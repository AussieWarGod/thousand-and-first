#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomSocketRulesTests
	{
		private static KingdomMaterialTally Tally(params int[] Amounts)
		{
			KingdomMaterialTally tally = new KingdomMaterialTally();
			for (int i = 0; i < Amounts.Length && i < KingdomMaterialRules.MaterialCount; i++)
			{
				tally.Set((KingdomMaterial)i, Amounts[i]);
			}
			return tally;
		}

		// --- ClassifyChange: Addendum 2's (type x size) split ----------------------------------

		[TestCase("civic", KingdomPlotRules.PlotSize.Small, "civic", KingdomPlotRules.PlotSize.Small, KingdomSocketRules.ChangeKind.SameSet)]
		[TestCase("civic", KingdomPlotRules.PlotSize.Small, "Civic", KingdomPlotRules.PlotSize.Small, KingdomSocketRules.ChangeKind.SameSet)]
		[TestCase("  civic  ", KingdomPlotRules.PlotSize.Small, "civic", KingdomPlotRules.PlotSize.Small, KingdomSocketRules.ChangeKind.SameSet)]
		[TestCase("civic", KingdomPlotRules.PlotSize.Small, "craft", KingdomPlotRules.PlotSize.Small, KingdomSocketRules.ChangeKind.Retype)]
		[TestCase("civic", KingdomPlotRules.PlotSize.Small, "civic", KingdomPlotRules.PlotSize.Medium, KingdomSocketRules.ChangeKind.Retype)]
		[TestCase("civic", KingdomPlotRules.PlotSize.Medium, "civic", KingdomPlotRules.PlotSize.Small, KingdomSocketRules.ChangeKind.Retype)]
		[TestCase("civic", KingdomPlotRules.PlotSize.Small, "craft", KingdomPlotRules.PlotSize.Medium, KingdomSocketRules.ChangeKind.Retype)]
		[TestCase(null, KingdomPlotRules.PlotSize.Small, null, KingdomPlotRules.PlotSize.Small, KingdomSocketRules.ChangeKind.SameSet)]
		[TestCase(null, KingdomPlotRules.PlotSize.Small, "", KingdomPlotRules.PlotSize.Small, KingdomSocketRules.ChangeKind.SameSet)]
		[TestCase("civic", KingdomPlotRules.PlotSize.Small, null, KingdomPlotRules.PlotSize.Small, KingdomSocketRules.ChangeKind.Retype)]
		public void ClassifyChange_MatchesTypeAndSizeExactly(string currentCategory, KingdomPlotRules.PlotSize currentSize, string targetCategory, KingdomPlotRules.PlotSize targetSize, KingdomSocketRules.ChangeKind expected)
		{
			Assert.AreEqual(expected, KingdomSocketRules.ClassifyChange(currentCategory, currentSize, targetCategory, targetSize));
		}

		[Test]
		public void VerbFor_NamesEachKindDifferently()
		{
			string sameSet = KingdomSocketRules.VerbFor(KingdomSocketRules.ChangeKind.SameSet);
			string retype = KingdomSocketRules.VerbFor(KingdomSocketRules.ChangeKind.Retype);
			Assert.IsFalse(string.IsNullOrEmpty(sameSet));
			Assert.IsFalse(string.IsNullOrEmpty(retype));
			Assert.AreNotEqual(sameSet, retype);
		}

		[TestCase(5, 4, KingdomPlotRules.PlotSize.Small)]
		[TestCase(4, 5, KingdomPlotRules.PlotSize.Small)]
		[TestCase(8, 6, KingdomPlotRules.PlotSize.Medium)]
		[TestCase(9, 12, KingdomPlotRules.PlotSize.Large)]
		[TestCase(20, 14, KingdomPlotRules.PlotSize.Huge)]
		public void ActualSizeComesFromStakedRectangleNotDesignMinimum(int width,
			int height, KingdomPlotRules.PlotSize expected)
		{
			Assert.IsTrue(KingdomSocketRules.TryActualSize(width, height, out var actual));
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void SameSetMayUseLargerActualLotButNeverSmallerOne()
		{
			Assert.IsTrue(KingdomSocketRules.FitsSameSet("craft",
				KingdomPlotRules.PlotSize.Large, "CRAFT", KingdomPlotRules.PlotSize.Small));
			Assert.IsFalse(KingdomSocketRules.FitsSameSet("craft",
				KingdomPlotRules.PlotSize.Small, "craft", KingdomPlotRules.PlotSize.Medium));
			Assert.IsFalse(KingdomSocketRules.FitsSameSet("craft",
				KingdomPlotRules.PlotSize.Large, "civic", KingdomPlotRules.PlotSize.Small));
		}

		// --- FootprintFits: "footprint <= plot", applied a second time at the socket -----------

		[TestCase(5, 4, 5, 4, true)]
		[TestCase(8, 6, 5, 4, true)]
		[TestCase(5, 4, 8, 6, false)]
		[TestCase(5, 4, 6, 4, false)]
		[TestCase(5, 4, 5, 5, false)]
		[TestCase(0, 0, 1, 1, false)]
		public void FootprintFits_NeverAllowsTheDesignToOutgrowThePlot(int plotWidth, int plotHeight, int needWidth, int needHeight, bool expected)
		{
			Assert.AreEqual(expected, KingdomSocketRules.FootprintFits(plotWidth, plotHeight, needWidth, needHeight));
		}

		// --- Refusals: STANDARDS 7b, never silent, always name what would lift it --------------

		[Test]
		public void RefuseTooSmall_NamesTheDesignAndBothDimensions()
		{
			string message = KingdomSocketRules.RefuseTooSmall("great hall", 5, 4, 12, 9);
			Assert.IsTrue(message.Contains("great hall"));
			Assert.IsTrue(message.Contains("12"));
			Assert.IsTrue(message.Contains("9"));
			Assert.IsTrue(message.Contains("5"));
			Assert.IsTrue(message.Contains("4"));
		}

		[Test]
		public void RefuseAdopted_NamesTheBuilding()
		{
			Assert.IsTrue(KingdomSocketRules.RefuseAdopted("bathhouse").Contains("bathhouse"));
		}

		[Test]
		public void RefuseNotAPlot_NamesTheDesign()
		{
			Assert.IsTrue(KingdomSocketRules.RefuseNotAPlot("cask rack").Contains("cask rack"));
		}

		[Test]
		public void RefuseAlreadyThat_NamesTheBuilding()
		{
			Assert.IsTrue(KingdomSocketRules.RefuseAlreadyThat("scriptorium").Contains("scriptorium"));
		}

		[Test]
		public void RefuseCondemned_NamesTheBuilding()
		{
			Assert.IsTrue(KingdomSocketRules.RefuseCondemned("bathhouse").Contains("bathhouse"));
		}

		[Test]
		public void RefuseImproving_NamesTheBuilding()
		{
			Assert.IsTrue(KingdomSocketRules.RefuseImproving("cistern").Contains("cistern"));
		}

		[Test]
		public void RefuseUnknownSkin_NamesBothTheKeyAndTheBuilding()
		{
			string message = KingdomSocketRules.RefuseUnknownSkin("verdant-roof", "scriptorium");
			Assert.IsTrue(message.Contains("verdant-roof"));
			Assert.IsTrue(message.Contains("scriptorium"));
		}

		[Test]
		public void RefuseUnknownDesign_NamesTheBuilding()
		{
			Assert.IsTrue(KingdomSocketRules.RefuseUnknownDesign("scriptorium").Contains("scriptorium"));
		}

		// --- RedressCost: trivial, and zero for a water-only design ----------------------------

		[Test]
		public void RedressCost_IsAFractionOfTheFullBuildCost()
		{
			KingdomMaterialTally full = Tally(0, 0, 100, 100);
			KingdomMaterialTally redress = KingdomSocketRules.RedressCost(full);
			Assert.Less(redress.Total(), full.Total());
			Assert.AreEqual(full.Get(KingdomMaterial.Timber) * KingdomSocketRules.RedressCostPercent / 100, redress.Get(KingdomMaterial.Timber));
		}

		[Test]
		public void RedressCost_OfAWaterOnlyDesignIsNothing()
		{
			Assert.IsTrue(KingdomSocketRules.RedressCost(new KingdomMaterialTally()).IsEmpty());
			Assert.IsTrue(KingdomSocketRules.RedressCost(null).IsEmpty());
		}

		[Test]
		public void RedressCost_NeverExceedsAOnePercentSliverEvenRoundedUpToOne()
		{
			// A cheap design (single-digit units of any one material) redresses for free: a
			// tenth of a small number floors to zero, which is exactly the "trivial" the
			// addendum asks for and never a hidden minimum charge nobody authored.
			KingdomMaterialTally cheap = Tally(0, 0, 4);
			Assert.IsTrue(KingdomSocketRules.RedressCost(cheap).IsEmpty());
		}

		// --- AssessConversion: strike effort + new cost - salvage, composed once ---------------

		[Test]
		public void AssessConversion_StrikeEffortMatchesTheOrdinaryStrikeFormula()
		{
			KingdomMaterialTally oldCost = Tally(0, 0, 8, 4);
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(oldCost, 50, new KingdomMaterialTally(), 0);
			Assert.AreEqual(KingdomMaterialRules.StrikeEffort(oldCost.Total(), 50), quote.StrikeEffort);
			Assert.AreEqual(KingdomMaterialRules.DaysForOneHand(quote.StrikeEffort), quote.EffortDays);
		}

		[Test]
		public void AssessConversion_SalvageMatchesTheOrdinaryStrikeSalvage()
		{
			KingdomMaterialTally oldCost = Tally(0, 0, 8, 4);
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(oldCost, 50, new KingdomMaterialTally(), 0);
			KingdomMaterialTally expected = KingdomMaterialRules.StrikeSalvage(oldCost);
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial material = (KingdomMaterial)i;
				Assert.AreEqual(expected.Get(material), quote.Salvage.Get(material));
			}
		}

		[Test]
		public void AssessConversion_NewDramsIsTheNewDesignsOwnFullCost()
		{
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(new KingdomMaterialTally(), 0, new KingdomMaterialTally(), 240);
			Assert.AreEqual(240, quote.NewDrams);
		}

		[Test]
		public void AssessConversion_NegativeDramsClampToZero()
		{
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(new KingdomMaterialTally(), -5, new KingdomMaterialTally(), -5);
			Assert.AreEqual(0, quote.NewDrams);
			Assert.AreEqual(KingdomMaterialRules.StrikeEffort(0, 0), quote.StrikeEffort);
		}

		[Test]
		public void AssessConversion_NetMaterialsCreditsSalvageAgainstTheNewCost()
		{
			// Old cost 8 timber -> salvage is StrikeSalvagePercent of that. New cost 10 timber.
			// Net must be exactly newCost - salvage, per material, not merely "less than new".
			KingdomMaterialTally oldCost = Tally(0, 0, 8);
			KingdomMaterialTally newCost = Tally(0, 0, 10);
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(oldCost, 0, newCost, 0);
			int expectedNet = 10 - quote.Salvage.Get(KingdomMaterial.Timber);
			Assert.AreEqual(expectedNet, quote.NetMaterials.Get(KingdomMaterial.Timber));
		}

		[Test]
		public void AssessConversion_NetMaterialsNeverGoesNegativeWhenSalvageOutweighsTheNewCost()
		{
			// Old cost is lavish (a lot to salvage); new cost is cheap. The net must floor at
			// zero per material rather than reading as a negative price.
			KingdomMaterialTally oldCost = Tally(0, 0, 100);
			KingdomMaterialTally newCost = Tally(0, 0, 1);
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(oldCost, 0, newCost, 0);
			Assert.AreEqual(0, quote.NetMaterials.Get(KingdomMaterial.Timber));
		}

		[Test]
		public void AssessConversion_ANullMaterialCostOnEitherSideReadsAsEmpty()
		{
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(null, 10, null, 20);
			Assert.AreEqual(0, quote.Salvage.Total());
			Assert.AreEqual(0, quote.NetMaterials.Total());
			Assert.AreEqual(20, quote.NewDrams);
		}

		[Test]
		public void AssessConversion_DoesNotMutateEitherInputTally()
		{
			KingdomMaterialTally oldCost = Tally(0, 0, 8);
			KingdomMaterialTally newCost = Tally(0, 0, 10);
			KingdomSocketRules.AssessConversion(oldCost, 0, newCost, 0);
			Assert.AreEqual(8, oldCost.Get(KingdomMaterial.Timber));
			Assert.AreEqual(10, newCost.Get(KingdomMaterial.Timber));
		}

		// --- DescribeConversion: the one disclosed figure, composed before anything moves ------

		[Test]
		public void DescribeConversion_NamesBothDesignsAndTheDramCost()
		{
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(Tally(0, 0, 8), 40, Tally(0, 0, 10), 120);
			string text = KingdomSocketRules.DescribeConversion("bathhouse", "scriptorium", KingdomSocketRules.ChangeKind.SameSet, quote);
			Assert.IsTrue(text.Contains("bathhouse"));
			Assert.IsTrue(text.Contains("scriptorium"));
			Assert.IsTrue(text.Contains("120"));
		}

		[Test]
		public void DescribeConversion_MentionsSalvageOnlyWhenThereIsAny()
		{
			KingdomSocketRules.ConversionQuote withSalvage = KingdomSocketRules.AssessConversion(Tally(0, 0, 8), 0, new KingdomMaterialTally(), 0);
			KingdomSocketRules.ConversionQuote withoutSalvage = KingdomSocketRules.AssessConversion(new KingdomMaterialTally(), 0, new KingdomMaterialTally(), 0);
			string withText = KingdomSocketRules.DescribeConversion("hut", "hall", KingdomSocketRules.ChangeKind.Retype, withSalvage);
			string withoutText = KingdomSocketRules.DescribeConversion("hut", "hall", KingdomSocketRules.ChangeKind.Retype, withoutSalvage);
			Assert.IsTrue(withText.Contains("comes back"));
			Assert.IsFalse(withoutText.Contains("comes back"));
		}

		[Test]
		public void DescribeConversion_NeverRefundsWaterInItsOwnWords()
		{
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(Tally(0, 0, 8), 10, Tally(0, 0, 4), 20);
			string text = KingdomSocketRules.DescribeConversion("hut", "hall", KingdomSocketRules.ChangeKind.Retype, quote);
			Assert.IsTrue(text.Contains("No water is ever refunded"));
		}

		[Test]
		public void DescribeConversion_DistinguishesSameSetFromRetypeInWording()
		{
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessConversion(new KingdomMaterialTally(), 0, new KingdomMaterialTally(), 0);
			string sameSet = KingdomSocketRules.DescribeConversion("hut", "cabin", KingdomSocketRules.ChangeKind.SameSet, quote);
			string retype = KingdomSocketRules.DescribeConversion("hut", "hall", KingdomSocketRules.ChangeKind.Retype, quote);
			Assert.AreNotEqual(sameSet.Substring(0, 9), retype.Substring(0, 9));
		}
	}
}
#endif
