#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Issue #282. Since 561bffd3 the tent is a Medium lot billed canvas:12,timber:1, so the
	/// camp-heart fixtures derive the bill, the sentinel and the chain layout from the catalogue
	/// and production's plot rules instead of the old Small tent. VALUE cases: they execute the
	/// engine-free harness rules against the authored catalogue. None is native evidence.
	/// </summary>
	public class KingdomCampHeartMediumTentTests
	{
		private const string Checks = "Harness/KingdomCampHeartNativeChecks.cs";

		private static XElement Building(string Key) => XDocument.Parse(
			TestMain.ReadRepositoryText("RuntimeData/KingdomBuildings.xml")).Descendants("building")
			.Single(value => (string)value.Attribute("Key") == Key);

		private static KingdomMaterialTally Bill(string Key, string Attribute)
		{
			Assert.That(KingdomMaterialRules.TryParseMaterialCost((string)Building(Key).Attribute(Attribute),
				out var tally, out string error), Is.True, Key + " " + Attribute + ": " + error);
			return tally;
		}

		private static int Constant(string Name)
		{
			string text = TestMain.ReadRepositoryText(Checks);
			int at = text.IndexOf("internal const int " + Name + " = ", StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThan(-1), Name);
			int open = text.IndexOf("= ", at, StringComparison.Ordinal) + 2;
			return int.Parse(text.Substring(open, text.IndexOf(';', open) - open).Trim());
		}

		private static int StoreCapacity()
		{
			string xml = TestMain.ReadRepositoryText("RuntimeData/ObjectBlueprints.xml");
			int at = xml.IndexOf("<object Name=\"r_KingdomHeartStockpile\"", StringComparison.Ordinal);
			int tag = xml.IndexOf("<tag Name=\"r_KingdomStockpileCapacity\" Value=\"", at, StringComparison.Ordinal);
			Assert.That(at > -1 && tag > at && tag < xml.IndexOf("</object>", at, StringComparison.Ordinal), Is.True);
			int open = xml.IndexOf("Value=\"", tag, StringComparison.Ordinal) + 7;
			return int.Parse(xml.Substring(open, xml.IndexOf('"', open) - open));
		}

		private static string[] Script(string Name) => TestMain.ReadRepositoryText("Tools/personas/" + Name + ".persona")
			.Split('\n').Single(line => line.StartsWith("SCRIPT=", StringComparison.Ordinal)).Substring(7).Split(';');

		internal static void Rite(out int X, out int Y)
		{
			string start = TestMain.ReadRepositoryText("Tools/personas/camp-heart-chain.persona").Split('\n')
				.Single(line => line.StartsWith("START=", StringComparison.Ordinal)).Trim();
			string[] cell = start.Substring(start.IndexOf('@') + 1).Split(',');
			X = int.Parse(cell[0]);
			Y = int.Parse(cell[1]);
		}

		internal static void Hearts(out KingdomPlotRules.PlotRect Survey, out KingdomPlotRules.PlotRect Rung2,
			out KingdomPlotRules.PlotRect Rung4)
		{
			Rite(out int x, out int y);
			Assert.That(KingdomPlotRules.TrySurveyedHeart(x, y, 80, 25, out Survey), Is.True);
			Assert.That(KingdomPlotRules.TryHeartRect(Survey, x, y, KingdomPlotRules.HeartSizeForRung(2), out Rung2), Is.True);
			Assert.That(KingdomPlotRules.TryHeartRect(Survey, x, y, KingdomPlotRules.HeartSizeForRung(4), out Rung4), Is.True);
		}

		// Views of the engine-free chain fixture for the main-suite siting cases, which also
		// need production rules the portable suite does not compile.
		internal static KingdomPlotRules.PlotRect SourceTent => KingdomCampHeartChainGrid.SourceTent;
		internal static int FounderReach => KingdomCampHeartChainGrid.FounderReach;
		internal static List<KingdomPlotRules.PlotRect> Courts() => KingdomCampHeartChainGrid.WaterCourts().ToList();
		internal static List<KingdomPlotRules.PlotRect> ChainHomes() =>
			Homes(KingdomCampHeartChainGrid.SourceTent, KingdomCampHeartChainGrid.Candidates());

		internal static void CommissionCell(out int X, out int Y)
		{
			Hearts(out _, out _, out var rung4);
			Assert.That(KingdomPlotRules.TryDimensions(KingdomPlotRules.PlotSize.Medium, out int width, out int height), Is.True);
			X = KingdomCampHeartChainGrid.CommissionX(rung4, width);
			Y = KingdomCampHeartChainGrid.CommissionY(height);
		}

		/// <summary>The fixture's own home loop (KingdomCampHeartChainSupport.SeedChainHomes):
		/// candidate order, interior fit, both paid approaches and every laid plot's lane.</summary>
		internal static List<KingdomPlotRules.PlotRect> Homes(KingdomPlotRules.PlotRect Tent,
			IEnumerable<KingdomPlotRules.PlotRect> Candidates, int Wanted = 18)
		{
			Assert.That(KingdomPlotRules.TryInterior(80, 25, out var interior), Is.True);
			Hearts(out _, out var rung2, out var rung4);
			var laid = new List<KingdomPlotRules.PlotRect> { rung2, Tent };
			var homes = new List<KingdomPlotRules.PlotRect>();
			foreach (var rect in Candidates)
			{
				if (homes.Count == Wanted) break;
				if (!KingdomPlotRules.Fits(rect, interior) || !KingdomCampHeartChainGrid.ClearsPaidApproach(rect, Tent)
					|| !KingdomCampHeartChainGrid.ClearsPaidApproach(rect, rung4)
					|| KingdomPlotRules.CrowdsExisting(rect, laid.Concat(homes).ToList())) continue;
				homes.Add(rect);
			}
			return homes;
		}

		[Test]
		public void TheTentBillIsTheMediumRowAndOneSentinelOutlivesItsOwnUpgradeGate()
		{
			XElement tent = Building("tent");
			Assert.That((string)tent.Attribute("Plot"), Is.EqualTo("M"));
			KingdomMaterialTally bill = Bill("tent", "Materials"), upgrade = Bill("tent", "UpgradeMaterials");
			int tentBrush = bill.Get(KingdomMaterial.Brush), upgradeBrush = upgrade.Get(KingdomMaterial.Brush);
			int fill = Constant("MintedStoneUnits") + Constant("MintedTimberUnits");
			Assert.That(KingdomCampHeartTentRules.Lawful(tentBrush, upgradeBrush, fill, StoreCapacity()), Is.True);
			int minted = KingdomCampHeartTentRules.PaidTentBrush(tentBrush, upgradeBrush);
			int saved = KingdomCampHeartTentRules.SavedBrush(tentBrush, upgradeBrush);
			Assert.That(minted - tentBrush, Is.EqualTo(saved), "the tent takes exactly its own catalogue brush");
			Assert.That(saved, Is.GreaterThanOrEqualTo(1), "a sentinel unit must survive the paid tent");
			Assert.That(saved, Is.LessThan(upgradeBrush), "the survivors must not pay the tent's own upgrade");
			Assert.That(minted, Is.LessThan(Constant("MintedBrushUnits")),
				"the plain 1 -> 2 regression keeps its own capacity fill; paid-tent scripts mint fewer");
			// Minting the plain run's capacity fill would leave enough survivors after the Medium
			// tent to pay its canvas-only upgrade by itself (the 499357a8 failure mode).
			Assert.That(Constant("MintedBrushUnits") - tentBrush, Is.GreaterThanOrEqualTo(upgradeBrush));
			// After the rung-2 bill takes the minted timber, the tent's other inputs are minted.
			KingdomMaterialTally rung2 = Bill("heartbasin", "UpgradeMaterials");
			Assert.That(rung2.Get(KingdomMaterial.Timber), Is.EqualTo(Constant("MintedTimberUnits")));
			Assert.That(bill.Get(KingdomMaterial.Timber), Is.GreaterThan(0));
			Assert.That(minted + bill.Total() - tentBrush, Is.LessThanOrEqualTo(StoreCapacity()),
				"the phase-2 store holds the unasked brush plus the tent's other inputs");
			Assert.That(KingdomCampHeartTentRules.Lawful(tentBrush, 1, fill, StoreCapacity()), Is.False,
				"a one-brush upgrade would leave no sentinel");
			Assert.That(KingdomCampHeartTentRules.Lawful(tentBrush, upgradeBrush, fill, fill + minted - 1), Is.False);
			Assert.That(KingdomCampHeartTentRules.Lawful(0, upgradeBrush, fill, StoreCapacity()), Is.False);
		}

		[Test]
		public void TheSavePersonaExpectsTheCatalogueSentinel()
		{
			int saved = KingdomCampHeartTentRules.SavedBrush(Bill("tent", "Materials").Get(KingdomMaterial.Brush),
				Bill("tent", "UpgradeMaterials").Get(KingdomMaterial.Brush));
			string expect = TestMain.ReadRepositoryText("Tools/personas/camp-heart-save.persona").Split('\n')
				.Single(line => line.StartsWith("EXPECT=", StringComparison.Ordinal));
			Assert.That(expect, Does.Contain(",camp-heart-save-custody:OK~before=r_KingdomBrush=" + saved + ","));
		}

		[Test]
		public void OnlyTheSaveAndChainFormsPayTheTent()
		{
			Assert.That(KingdomCampHeartScript.PaysTent(Script("camp-heart-save")), Is.True);
			Assert.That(KingdomCampHeartScript.PaysTent(Script("camp-heart-chain")), Is.True);
			Assert.That(KingdomCampHeartScript.PaysTent(Script("camp-heart-native-checks")), Is.False);
			Assert.That(KingdomCampHeartScript.PaysTent(Script("camp-heart-rung3-native-check")), Is.False);
			Assert.That(KingdomCampHeartScript.PaysTent(null), Is.False);
			var changed = Script("camp-heart-save");
			changed[changed.Length - 1] += " ";
			Assert.That(KingdomCampHeartScript.PaysTent(changed), Is.False);
		}

		[Test]
		public void ChainHomesTakeTwoTransposedSlotsBesideTheMediumSourceTent()
		{
			var tent = KingdomCampHeartChainGrid.SourceTent;
			var all = KingdomCampHeartChainGrid.Candidates().ToList();
			var grid = all.Where(rect => rect.Width == 6 && rect.Height == 4).ToList();
			Assert.That(grid.Count, Is.EqualTo(19), "the accepted Native30 grid is unchanged");
			Assert.That(Homes(tent, grid).Count, Is.EqualTo(16), "the Medium tent's approach takes two grid lots");
			var homes = Homes(tent, all);
			Assert.That(homes.Count, Is.EqualTo(18));
			var extras = homes.Except(grid).ToList();
			Assert.That(extras, Is.EqualTo(new[] { new KingdomPlotRules.PlotRect(16, 3, 19, 8),
				new KingdomPlotRules.PlotRect(74, 2, 77, 7) }));
			Assert.That(extras.All(rect => rect.Width == 4 && rect.Height == 6), Is.True,
				"transposed poses of the retained S canvas binding");
			Assert.That(Homes(tent, all, 19).Count, Is.EqualTo(19), "the last slot remains a lawful spare");
			// The old Small tent still houses eighteen on the grid alone.
			Assert.That(Homes(new KingdomPlotRules.PlotRect(24, 7, 29, 10), all).Except(grid), Is.Empty);
			int bedsNeeded = 50 - Roof("tent");
			int homesNeeded = (bedsNeeded + Roof("tentrow") - 1) / Roof("tentrow");
			Assert.That(homesNeeded, Is.LessThanOrEqualTo(homes.Count));
			Assert.That(KingdomRules.StageFor(50, 1024), Is.EqualTo(GrowthStage.City));
		}

		private static void Ordered(string Path, params string[] Tokens)
		{
			string text = TestMain.ReadRepositoryText(Path);
			int at = -1;
			foreach (string token in Tokens)
			{
				int next = text.IndexOf(token, at + 1, StringComparison.Ordinal);
				Assert.That(next, Is.GreaterThan(at), Path + " / " + token);
				at = next;
			}
		}

		/// <summary>WIRING pins only: the engine-touching shards meet a compiler in Tools/gate.sh
		/// and are executed only by the owed native runs.</summary>
		[Test]
		public void ThePaidTentIsQuotedLockedThenPaidAndHeldOnlyIdle()
		{
			Ordered("Harness/KingdomCampHeartNativeClaim.cs", "PrepareChainCommission();",
				"KingdomPlotQuote quote = PaysTent ? QuotePaidTent(entry) : null;",
				"KingdomPlotRules.PlotSize.None, quote, out failure);", "Same(laid, quote.Rect)",
				"RequireChainCommissionClear(laid);", "taf-camp-claim-paid-refused");
			Ordered("Harness/KingdomCampHeartPaidTent.cs", "KingdomMaterials.CostFor(ClaimKey)",
				"KingdomMaterials.UpgradeCostFor(ClaimKey)", "KingdomCampHeartTentRules.Lawful(",
				"KingdomPlots.TryQuoteCommission(System, Zone, Entry, null,",
				"quote.MaterialClaim.ToClaimString() == claim.ToClaimString()", "RequireChainQuote(quote);",
				"MintTentShortfall(claim);", "improvement == null || !improvement.Working",
				"CleanFirstPayment(found.Claims, entry.CostDrams, TentClaim)");
			Ordered("Harness/KingdomCampHeartChainObservation.cs", "KingdomPlots.TryGetSpec(ClaimKey, out var spec)",
				"KingdomCampHeartChainGrid.CommissionX(outer, width)", "KingdomCampHeartChainGrid.CommissionY(height)",
				"Game.TimeTicks == tick", "Same(Quote.Rect, tent)", "Quote.LabourTicks <= KingdomRules.TicksPerDay");
			Ordered("Harness/KingdomCampHeartChain.cs", "units.Count == SavedBrushUnits", "IdlePaidTent(jobs);",
				"SeedChainSupport();", "\"; brush=\" + SavedBrushUnits");
			Ordered("Harness/KingdomCampHeartChainSupport.cs", "var tent = IdlePaidTent(jobs);",
				"KingdomPhysicalPhase.EffectsSettled", "Held = true");
			Ordered("Harness/KingdomCampHeartSave.cs", "IdlePaidTent(jobs);", "PaysTent && ClaimOutcome",
				"RetainedBrush.Count == PaidTentBrushUnits");
			Ordered("Harness/KingdomCampHeartNativeFixture.cs", "RequirePaidTentArithmetic();",
				"Mint(KingdomMaterial.Brush, Unasked, MintedBrush);");
			Ordered(Checks, "PaysTent = SealedPaysTent();", "MintStoreContents();");
			foreach (string shard in new[] { "Chain", "Save", "SaveWitness", "LoadChecks", "PaidTent" })
			{
				string text = TestMain.ReadRepositoryText("Harness/KingdomCampHeart" + shard + ".cs");
				foreach (string stale in new[] { "brush=21", "== 21", "21 brush", "two brush", "MintedBrushUnits - 2" })
					Assert.That(text, Does.Not.Contain(stale), shard + " / " + stale);
			}
		}

		private static int Roof(string Key)
		{
			string carries = (string)Building(Key).Attribute("Carries");
			string term = carries.Split(',').Single(value => value.StartsWith("roof:", StringComparison.Ordinal));
			return int.Parse(term.Substring(5));
		}

		[Test]
		public void CourtsAndTheSourceTentKeepEveryProductionReservation()
		{
			var tent = KingdomCampHeartChainGrid.SourceTent;
			Hearts(out _, out _, out var rung4);
			var homes = Homes(tent, KingdomCampHeartChainGrid.Candidates());
			var plots = new List<KingdomPlotRules.PlotRect>(homes) { tent, rung4 };
			Assert.That(KingdomPlotRules.Overlaps(rung4, KingdomPlotRules.Reserved(tent)), Is.False,
				"the source tent's reserved lane stays outside the final heart");
			Assert.That(tent.Width * tent.Height, Is.EqualTo(48));
			var courts = KingdomCampHeartChainGrid.WaterCourts().ToList();
			Assert.That(courts.Count, Is.EqualTo(8));
			foreach (var court in courts)
			{
				var root = new KingdomPlotRules.PlotRect(court.X1 + 3, court.Y1 + 2, court.X1 + 3, court.Y1 + 2);
				Assert.That(KingdomPlotRules.ValidZoneRect(court, 80, 25), Is.True);
				Assert.That(plots.All(plot => !KingdomPlotRules.Overlaps(court, plot)), Is.True,
					"no plot stands on a persisted water footprint");
				Assert.That(plots.All(plot => KingdomCampHeartChainGrid.ClearsPaidApproach(root, plot)), Is.True,
					"no producer root stands on a paid approach");
			}
			Assert.That(KingdomCampHeartChainGrid.Candidates().All(KingdomCampHeartChainGrid.ClearsWaterFootprints), Is.True);
		}

		[Test]
		public void TheCommissionCellIsDerivedFromTheTentLotAndTheNorthCourt()
		{
			Hearts(out _, out _, out var rung4);
			Assert.That(KingdomPlotRules.TryDimensions(KingdomPlotRules.PlotSize.Medium, out int width, out int height), Is.True);
			int x = KingdomCampHeartChainGrid.CommissionX(rung4, width), y = KingdomCampHeartChainGrid.CommissionY(height);
			var tent = KingdomCampHeartChainGrid.SourceTent;
			Assert.That(tent.Width == width && tent.Height == height, Is.True);
			Assert.That(tent.Y1, Is.EqualTo(KingdomCampHeartChainGrid.NorthCourt.Y2 + 1));
			Assert.That(tent.X1 - x, Is.EqualTo(KingdomCampHeartChainGrid.FounderReach));
			Assert.That(y - tent.Y2, Is.EqualTo(KingdomCampHeartChainGrid.FounderReach));
			Assert.That(tent.X2 + KingdomPlotRules.RoadMargin, Is.LessThan(rung4.X1));
			Assert.That(tent.Contains(x, y), Is.False, "the founder cannot stand on its own commission");
			// The pre-#282 Small-width approach put the founder two columns further east.
			Assert.That(rung4.X1 - KingdomPlotRules.SmallWidth - KingdomPlotRules.RoadMargin - 2, Is.EqualTo(x + 2));
		}
	}
}
#endif
