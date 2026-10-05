#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Mark = ThousandAndFirst.KingdomLayoutRules.LayoutMark;
using Rect = ThousandAndFirst.KingdomPlotRules.PlotRect;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Issue #282, main suite only: production's own siting, subsidence and authored-lane rules
	/// asked about the chain fixture around the Medium source tent. The native run still owes the
	/// real ground: the fixture locks the exact production quote before it pays, so a ground or
	/// mark difference refuses at the phase-2 boundary rather than after the tent is bought.
	/// </summary>
	public class KingdomCampHeartMediumTentSitingTests
	{
		private static Rect Choose(int FounderX, int FounderY, int HeartX, int HeartY, out KingdomLayoutRules.LayoutOutcome Outcome)
		{
			Assert.That(KingdomPlotRules.TryInterior(80, 25, out var interior), Is.True);
			KingdomCampHeartMediumTentTests.Rite(out int riteX, out int riteY);
			KingdomCampHeartMediumTentTests.Hearts(out var survey, out var rung2, out _);
			Assert.That(KingdomPlotRules.TryDimensions(KingdomPlotRules.PlotSize.Medium, out int w, out int h), Is.True);
			var candidates = KingdomPlotPoseSitingRules.Enumerate(interior, w, h).Select(pose => pose.Rect)
				.Where(rect => !KingdomPlotRules.CrowdsExisting(rect, new[] { rung2 })
					&& !rect.Contains(FounderX, FounderY)).ToList();
			var marks = new List<Mark> { new Mark(HeartX, HeartY, KingdomLayoutRules.LayoutPurpose.Civic) };
			var edges = KingdomRules.Frontier.North | KingdomRules.Frontier.South
				| KingdomRules.Frontier.West | KingdomRules.Frontier.East;
			Outcome = KingdomPlotRules.ChooseRect(KingdomLayoutRules.LayoutPurpose.Housing,
				KingdomPlotRules.PlotSize.Medium, 80, 25, edges, marks, candidates, true, FounderX, FounderY,
				true, riteX, riteY, out int index, true, survey);
			Assert.That(index, Is.GreaterThanOrEqualTo(0));
			return candidates[index];
		}

		[Test]
		public void ProductionSitingPutsTheChainTentOnTheFounderGroundBelowTheNorthCourt()
		{
			KingdomCampHeartMediumTentTests.Hearts(out _, out _, out var rung4);
			KingdomCampHeartMediumTentTests.CommissionCell(out int x, out int y);
			// A housing plot scores by its distance from the marks' settled heart. Every settled
			// heart between the survey's west edge and just east of the rite ground gives the same
			// founder-ground answer; the quote lock refuses any other answer before payment.
			for (int heartX = 32; heartX <= 41; heartX++)
				for (int heartY = 10; heartY <= 13; heartY++)
				{
					Rect chosen = Choose(x, y, heartX, heartY, out var outcome);
					Assert.That(outcome, Is.EqualTo(KingdomLayoutRules.LayoutOutcome.Founder));
					Assert.That(chosen, Is.EqualTo(KingdomCampHeartMediumTentTests.SourceTent), heartX + "," + heartY);
				}
			Assert.That(KingdomPlotRules.Reach(KingdomCampHeartMediumTentTests.SourceTent, x, y),
				Is.EqualTo(KingdomPlotRules.FounderReachCells));
			// The pre-#282 Small-width approach lands the Medium tent's reserved lane on the
			// final heart, which RequireChainCommissionClear refused only after payment.
			Rect old = Choose(rung4.X1 - KingdomPlotRules.SmallWidth - KingdomPlotRules.RoadMargin - 2, 12, 40, 12, out _);
			Assert.That(KingdomPlotRules.Overlaps(rung4, KingdomPlotRules.Reserved(old)), Is.True);
		}

		[Test]
		public void TheFixtureFounderReachIsProductions()
		{
			Assert.That(KingdomCampHeartMediumTentTests.FounderReach, Is.EqualTo(KingdomPlotRules.FounderReachCells));
		}

		[Test]
		public void EightCourtsAndEighteenRowsAreAtOrAboveTheCityMinimum()
		{
			var catalogue = XDocument.Parse(TestMain.ReadRepositoryText("RuntimeData/KingdomBuildings.xml"));
			int water = Carried(catalogue, "airwellcourt", "water:");
			int courts = KingdomCampHeartMediumTentTests.Courts().Count;
			Assert.That(KingdomSubsidenceRules.LevelFromWater(courts * water, GrowthStage.City), Is.GreaterThanOrEqualTo(50));
			Assert.That(KingdomSubsidenceRules.LevelFromWater((courts - 1) * water, GrowthStage.City), Is.LessThan(50),
				"eight legacy courts are the fewest that sustain fifty residents");
			int rows = KingdomCampHeartMediumTentTests.ChainHomes().Count;
			int beds = Carried(catalogue, "tentrow", "roof:"), tentBeds = Carried(catalogue, "tent", "roof:");
			Assert.That(rows * beds + tentBeds, Is.GreaterThanOrEqualTo(50));
			Assert.That((rows - 2) * beds + tentBeds, Is.LessThan(50), "seventeen rows are the fewest; Native30 kept eighteen");
		}

		private static int Carried(XDocument Catalogue, string Key, string Term)
		{
			string carries = (string)Catalogue.Descendants("building").Single(b => (string)b.Attribute("Key") == Key)
				.Attribute("Carries");
			return int.Parse(carries.Split(',').Single(value => value.StartsWith(Term, StringComparison.Ordinal))
				.Substring(Term.Length));
		}

		[Test]
		public void EveryAuthoredLaneOfTheChainLayoutStaysOpen()
		{
			var corpus = KingdomArchitectureCorpusFixture.Load();
			var tent = KingdomCampHeartMediumTentTests.SourceTent;
			KingdomCampHeartMediumTentTests.Hearts(out _, out _, out var rung4);
			var homes = KingdomCampHeartMediumTentTests.ChainHomes();
			var plots = new List<Rect>(homes) { tent, rung4 };
			var roots = KingdomCampHeartMediumTentTests.Courts().Select(c => (c.X1 + 3, c.Y1 + 2)).ToList();
			AssertLanes(corpus, "tentrow", ArchitectureLotSize.Small, homes, plots, roots);
			AssertLanes(corpus, "tent", ArchitectureLotSize.Medium, new[] { tent }, plots, roots);
			AssertLanes(corpus, "heartcourt", ArchitectureLotSize.Huge, new[] { rung4 }, plots, roots);
		}

		/// <summary>Every public DoorToLane route of every pose production's heart-facing rule can
		/// choose for a settled heart in the survey's middle band stays in the zone, off every
		/// other plot, off every water producer root and off the founder's commission cell.</summary>
		private static void AssertLanes(ArchitectureCorpus Corpus, string BuildKey, ArchitectureLotSize Size,
			IEnumerable<Rect> Lots, List<Rect> Plots, List<(int, int)> Roots)
		{
			var cases = Corpus.Cases.Where(c => c.Tier.BuildKey == BuildKey && c.Binding.Size == Size).ToList();
			Assert.That(cases.Count, Is.GreaterThan(0), BuildKey + " " + Size);
			Assert.That(KingdomArchitectureRules.TryCanonicalDimensions(Size, out int canonicalWidth, out _), Is.True);
			KingdomCampHeartMediumTentTests.CommissionCell(out int founderX, out int founderY);
			int routes = 0;
			foreach (var lot in Lots)
			{
				var facings = new HashSet<ArchitectureFacing>();
				for (int heartX = 30; heartX <= 44; heartX++)
					for (int heartY = 9; heartY <= 14; heartY++)
						facings.Add(lot.Width == canonicalWidth
							? (heartY <= lot.CenterY ? ArchitectureFacing.North : ArchitectureFacing.South)
							: (heartX >= lot.CenterX ? ArchitectureFacing.East : ArchitectureFacing.West));
				foreach (var item in cases)
					foreach (var facing in facings)
					{
						var request = KingdomArchitectureCorpusFixture.Request(Corpus, item, facing);
						Assert.That(KingdomArchitectureRules.TryCompile(request, out var snapshot, out string failure), Is.True, failure);
						foreach (var entrance in snapshot.Anchors.Where(a => a.Key == "entrance:public"
							|| a.Key.StartsWith("entrance:public@", StringComparison.Ordinal)))
						{
							var route = new List<ArchitecturePoint>();
							Assert.That(KingdomRoadRules.TryAuthoredLane(snapshot, lot, entrance, route,
								out _, out _, out int laneX, out int laneY), Is.True, BuildKey + " " + facing);
							route.Add(new ArchitecturePoint(laneX, laneY));
							foreach (var cell in route)
							{
								string at = BuildKey + " " + lot.X1 + "," + lot.Y1 + " " + facing + " lane " + cell.X + "," + cell.Y;
								Assert.That(KingdomRoadRules.InBounds(cell.X, cell.Y, 80, 25), Is.True, at);
								Assert.That(Plots.Any(plot => !plot.Equals(lot) && plot.Contains(cell.X, cell.Y)), Is.False, at);
								Assert.That(Roots.Contains((cell.X, cell.Y)), Is.False, at);
								Assert.That(cell.X == founderX && cell.Y == founderY, Is.False, at);
							}
							routes++;
						}
					}
			}
			Assert.That(routes, Is.GreaterThan(0), BuildKey);
		}
	}
}
#endif
