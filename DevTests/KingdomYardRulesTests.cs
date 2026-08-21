#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using Rect = ThousandAndFirst.KingdomPlotRules.PlotRect;
using Size = ThousandAndFirst.KingdomPlotRules.PlotSize;
using Spec = ThousandAndFirst.KingdomYardRules.YardWorkSpec;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The yard-trades geometry, eligibility, parsing, and prose. Every number and every substring
	/// is asserted directly, so deleting a bound, flipping the "Open" gate, widening the shading
	/// cap, or dropping a refusal's own reason fails a test here rather than only showing up as a
	/// silent stall in play (STANDARDS 7b).
	/// </summary>
	public class KingdomYardRulesTests
	{
		private static Rect R(int X1, int Y1, int X2, int Y2)
		{
			return new Rect(X1, Y1, X2, Y2);
		}

		private static Rect At(int X, int Y, Size Size)
		{
			Assert.IsTrue(KingdomPlotRules.TryRectAt(X, Y, Size, out var rect), "expected a rect for " + Size);
			return rect;
		}

		// --- Yard geometry: inside the rect, outside the walls -----------------------------

		[TestCase(Size.Small, 5, 4, 3, 2)]
		[TestCase(Size.Medium, 8, 6, 6, 4)]
		[TestCase(Size.Large, 12, 9, 10, 7)]
		[TestCase(Size.Huge, 20, 14, 18, 12)]
		public void YardInteriorIsTheRectWithoutItsWalls(Size Size, int Width, int Height, int InteriorWidth, int InteriorHeight)
		{
			Rect rect = At(10, 10, Size);
			Assert.AreEqual(Width, rect.Width);
			Assert.AreEqual(Height, rect.Height);
			Assert.IsTrue(KingdomYardRules.TryYardInterior(rect, out var interior));
			Assert.AreEqual(InteriorWidth, interior.Width);
			Assert.AreEqual(InteriorHeight, interior.Height);
			// Every interior cell is inside the rect and never one of its border (wall) cells.
			for (int y = interior.Y1; y <= interior.Y2; y++)
			{
				for (int x = interior.X1; x <= interior.X2; x++)
				{
					Assert.IsTrue(rect.Contains(x, y));
					Assert.IsFalse(rect.IsBorder(x, y));
				}
			}
		}

		[Test]
		public void ARectTooThinHasNoYardAtAll()
		{
			Assert.IsFalse(KingdomYardRules.TryYardInterior(R(0, 0, 1, 5), out _), "width 2 has no cell that is not a wall");
			Assert.IsFalse(KingdomYardRules.TryYardInterior(R(0, 0, 5, 1), out _), "height 2 has no cell that is not a wall");
			Assert.IsFalse(KingdomYardRules.TryYardInterior(R(0, 0, 0, 0), out _), "a single cell is all wall");
		}

		[Test]
		public void ARectExactlyThreeWideHasOneYardColumn()
		{
			Assert.IsTrue(KingdomYardRules.TryYardInterior(R(0, 0, 2, 5), out var interior));
			Assert.AreEqual(1, interior.Width);
		}

		// --- Eligibility: only a small or middling roofed house ----------------------------

		[TestCase(Size.Small, false, "housing", true)]
		[TestCase(Size.Medium, false, "housing", true)]
		[TestCase(Size.Medium, false, "Housing", true)]
		[TestCase(Size.Medium, false, " HOUSING ", true)]
		[TestCase(Size.Large, false, "housing", false)]
		[TestCase(Size.Huge, false, "housing", false)]
		[TestCase(Size.None, false, "housing", false)]
		[TestCase(Size.Small, true, "housing", false)]
		[TestCase(Size.Medium, false, "civic", false)]
		[TestCase(Size.Medium, false, "craft", false)]
		[TestCase(Size.Medium, false, null, false)]
		public void OnlyASmallOrMiddlingRoofedHouseIsEligible(Size Size, bool Open, string Category, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomYardRules.IsEligibleDesign(Size, Open, Category));
		}

		// --- Parsing: authorable from XML like everything else ------------------------------

		[Test]
		public void ParsingRefusesAMissingKey()
		{
			Assert.IsFalse(KingdomYardRules.TryParseYardWorkAttributes(null, "vine lattice", "r_KingdomVineLattice", null, "food:1", null, out var spec, out var error));
			Assert.IsNull(spec);
			StringAssert.Contains("Key", error);
		}

		[Test]
		public void ParsingRefusesAMissingDisplayName()
		{
			Assert.IsFalse(KingdomYardRules.TryParseYardWorkAttributes("vinelattice", null, "r_KingdomVineLattice", null, "food:1", null, out var spec, out var error));
			Assert.IsNull(spec);
			StringAssert.Contains("DisplayName", error);
		}

		[Test]
		public void ParsingRefusesAMissingBlueprint()
		{
			Assert.IsFalse(KingdomYardRules.TryParseYardWorkAttributes("vinelattice", "vine lattice", null, null, "food:1", null, out var spec, out var error));
			Assert.IsNull(spec);
			StringAssert.Contains("Blueprint", error);
		}

		[Test]
		public void ParsingRefusesABadGoodsFlag()
		{
			Assert.IsFalse(KingdomYardRules.TryParseYardWorkAttributes("dyevat", "dye vat", "r_KingdomDyeVat", null, null, "maybe", out var spec, out var error));
			Assert.IsNull(spec);
			StringAssert.Contains("Goods", error);
		}

		[Test]
		public void ParsingRefusesABadShadesTally()
		{
			Assert.IsFalse(KingdomYardRules.TryParseYardWorkAttributes("hiderack", "hide rack", "r_KingdomHideRack", null, "craft", null, out var spec, out var error));
			Assert.IsNull(spec);
			StringAssert.Contains("Shades", error);
		}

		[Test]
		public void ParsingRefusesShadingOverTheCap()
		{
			string shades = "craft:" + (KingdomYardRules.MaxShadePerWork + 1);
			Assert.IsFalse(KingdomYardRules.TryParseYardWorkAttributes("hiderack", "hide rack", "r_KingdomHideRack", null, shades, null, out var spec, out var error));
			Assert.IsNull(spec);
			StringAssert.Contains("hiderack", error);
		}

		[Test]
		public void ParsingAcceptsShadingExactlyAtTheCap()
		{
			string shades = "craft:" + KingdomYardRules.MaxShadePerWork;
			Assert.IsTrue(KingdomYardRules.TryParseYardWorkAttributes("hiderack", "hide rack", "r_KingdomHideRack", null, shades, null, out var spec, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(KingdomYardRules.MaxShadePerWork, spec.Shades[0].Amount);
		}

		[Test]
		public void ParsingSumsMultiplePairsAgainstTheCap()
		{
			// craft:1,food:1 sums to two, which is at the cap and must pass; three must not.
			Assert.IsTrue(KingdomYardRules.TryParseYardWorkAttributes("k1", "k1", "bp", null, "craft:1,food:1", null, out _, out var okError));
			Assert.IsNull(okError);
			Assert.IsFalse(KingdomYardRules.TryParseYardWorkAttributes("k2", "k2", "bp", null, "craft:1,food:1,learning:1", null, out var spec, out var badError));
			Assert.IsNull(spec);
			Assert.IsNotNull(badError);
		}

		[Test]
		public void ParsingFallsBackTheTradeToTheDisplayNameWhenNoneIsGiven()
		{
			Assert.IsTrue(KingdomYardRules.TryParseYardWorkAttributes("vinelattice", "vine lattice", "r_KingdomVineLattice", null, "food:1", null, out var spec, out var error));
			Assert.IsNull(error);
			Assert.AreEqual("vine lattice", spec.Trade);
		}

		[Test]
		public void ParsingKeepsAnExplicitTradeOverTheDisplayName()
		{
			Assert.IsTrue(KingdomYardRules.TryParseYardWorkAttributes("hiderack", "hide rack", "r_KingdomHideRack", "tanning", "craft:1", null, out var spec, out var error));
			Assert.IsNull(error);
			Assert.AreEqual("tanning", spec.Trade);
			Assert.AreEqual("hide rack", spec.DisplayName);
		}

		[Test]
		public void ParsingAcceptsAWorkThatShadesNothingAtAll()
		{
			// Flavor-only third-party work: no Shades, no Goods. Legal, not an error.
			Assert.IsTrue(KingdomYardRules.TryParseYardWorkAttributes("kitchengarden", "kitchen garden", "r_KingdomKitchenGarden", null, null, null, out var spec, out var error));
			Assert.IsNull(error);
			Assert.IsNotNull(spec.Shades);
			Assert.AreEqual(0, spec.Shades.Count);
			Assert.IsFalse(spec.FeedsGoods);
		}

		[Test]
		public void ParsingReadsTheGoodsFlag()
		{
			Assert.IsTrue(KingdomYardRules.TryParseYardWorkAttributes("dyevat", "dye vat", "r_KingdomDyeVat", "dyeing", null, "yes", out var spec, out var error));
			Assert.IsNull(error);
			Assert.IsTrue(spec.FeedsGoods);
			Assert.AreEqual(0, spec.Shades.Count);
		}

		// --- Prose: nothing stalls in silence -----------------------------------------------

		[Test]
		public void RefuseNotEligibleNamesTheHouse()
		{
			StringAssert.Contains("the great hall", KingdomYardRules.RefuseNotEligible("the great hall"));
		}

		[Test]
		public void RefuseNoRoomNamesTheHouse()
		{
			StringAssert.Contains("the stone house", KingdomYardRules.RefuseNoRoom("the stone house"));
		}

		[Test]
		public void RefuseAlreadyWorkingNamesBothTheHouseAndTheExistingTrade()
		{
			string refusal = KingdomYardRules.RefuseAlreadyWorking("the stone house", "tanning");
			StringAssert.Contains("the stone house", refusal);
			StringAssert.Contains("tanning", refusal);
		}

		[Test]
		public void RefuseUnknownWorkNamesTheBadKey()
		{
			StringAssert.Contains("nonesuch", KingdomYardRules.RefuseUnknownWork("nonesuch"));
		}

		private static Spec Work(string Key = "hiderack", string DisplayName = "hide rack", string Trade = "tanning")
		{
			return new Spec { Key = Key, DisplayName = DisplayName, Blueprint = "r_KingdomHideRack", Trade = Trade, Shades = new List<KindAmount>(), FeedsGoods = false };
		}

		[Test]
		public void TakeUpLineNamesTheHouseTheTradeAndTheObject()
		{
			string line = KingdomYardRules.TakeUpLine("the stone house", Work());
			StringAssert.Contains("the stone house", line);
			StringAssert.Contains("tanning", line);
			StringAssert.Contains("hide rack", line);
		}

		[Test]
		public void ReleaseLineSaysNothingIsRecovered()
		{
			string line = KingdomYardRules.ReleaseLine("the stone house", Work());
			StringAssert.Contains("the stone house", line);
			StringAssert.Contains("hide rack", line);
			StringAssert.Contains("Nothing is recovered", line);
		}

		[Test]
		public void DescriptionLineNamesTheTradeAndTheObject()
		{
			string line = KingdomYardRules.DescriptionLine(Work());
			StringAssert.Contains("tanning", line);
			StringAssert.Contains("hide rack", line);
		}

		// --- Shade summary --------------------------------------------------------------------

		[Test]
		public void ShadeSummaryNamesAGoodsWorkByItsGoods()
		{
			Spec spec = new Spec { Key = "dyevat", DisplayName = "dye vat", Trade = "dyeing", Shades = new List<KindAmount>(), FeedsGoods = true };
			StringAssert.Contains("caravan", KingdomYardRules.ShadeSummary(spec));
		}

		[Test]
		public void ShadeSummaryNamesAFlavorOnlyWorkAsShadingNothing()
		{
			Spec spec = new Spec { Key = "kitchengarden", DisplayName = "kitchen garden", Trade = "gardening", Shades = new List<KindAmount>(), FeedsGoods = false };
			StringAssert.Contains("nothing", KingdomYardRules.ShadeSummary(spec));
		}

		[Test]
		public void ShadeSummaryNamesTheSupportAndTheAmount()
		{
			Spec spec = new Spec { Key = "vellumpress", DisplayName = "vellum press", Trade = "scrivening", Shades = new List<KindAmount> { new KindAmount("learning", 1) }, FeedsGoods = false };
			string summary = KingdomYardRules.ShadeSummary(spec);
			StringAssert.Contains("learning", summary);
			StringAssert.Contains("1", summary);
		}

		[Test]
		public void ShadeSummaryListsEveryPairWhenAWorkShadesMoreThanOneSupport()
		{
			Spec spec = new Spec { Key = "k", DisplayName = "k", Trade = "k", Shades = new List<KindAmount> { new KindAmount("craft", 1), new KindAmount("food", 1) }, FeedsGoods = false };
			string summary = KingdomYardRules.ShadeSummary(spec);
			StringAssert.Contains("craft", summary);
			StringAssert.Contains("food", summary);
		}
	}
}
#endif
