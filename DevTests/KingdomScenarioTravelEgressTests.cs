#if TAF_TESTS
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The away walker leaves the rite ground before its westward row. Everything here is read
	/// from the authored heart maps, never from a copied constant: the rung-1 camp is a canvas
	/// horseshoe open only to the south, so a straight westward step off the rite is blocked by
	/// design and the founder must walk the rite column south until clear of the heart ground.
	/// </summary>
	public class KingdomScenarioTravelEgressTests
	{
		private const string Faith = "Architecture/KingdomArchitectures-CivicFaith.xml";
		private const string Camp = "civic-heartbasin-s0";
		private const string Court = "civic-heartcourt-xl3";

		private static XElement Map(string key)
			=> XDocument.Parse(TestMain.ReadRepositoryText(Faith)).Descendants("map")
				.Single(map => (string)map.Attribute("Key") == key);

		private static string[] Rows(XElement map)
			=> map.Elements("row").Select(row => (string)row.Attribute("Cells")).ToArray();

		private static string Pass(XElement map, char glyph)
			=> (string)map.Elements("glyph").Single(g => ((string)g.Attribute("Char"))[0] == glyph).Attribute("Pass");

		/// <summary>x=(width/2)-1 is the rite cell on every heart map (the authored header rule);
		/// the row is the one carrying the first basin there.</summary>
		private static int RiteRow(XElement map, string[] rows, out int riteX)
		{
			int column = (int)map.Attribute("Width") / 2 - 1;
			riteX = column;
			int[] basin = Enumerable.Range(0, rows.Length).Where(y => rows[y][column] == 'B').ToArray();
			ClassicAssert.AreEqual(1, basin.Length, "one basin on the rite column");
			ClassicAssert.AreEqual("walk", Pass(map, 'B'));
			return basin[0];
		}

		[Test]
		public void TheCampBlocksWestOfTheRiteAndOpensSouthAlongTheRiteColumn()
		{
			XElement map = Map(Camp);
			string[] rows = Rows(map);
			int riteY = RiteRow(map, rows, out int riteX);
			ClassicAssert.AreEqual("blocked", Pass(map, rows[riteY][riteX - 1]), "west neighbour of the rite");
			ClassicAssert.AreEqual("blocked", Pass(map, rows[riteY][riteX - 2]), "canvas flank west of the rite");
			for (int y = riteY + 1; y < rows.Length; y++)
				ClassicAssert.AreEqual("walk", Pass(map, rows[y][riteX]), "rite column row " + y);
			ClassicAssert.AreEqual('E', rows[rows.Length - 1][riteX], "the approach entrance closes the column");
		}

		[Test]
		public void EgressRowsComeFromTheAuthoredCampAndSurveyFootprints()
		{
			XElement camp = Map(Camp);
			string[] campRows = Rows(camp);
			int riteY = RiteRow(camp, campRows, out int riteX);
			// The camp itself, stamped so that its rite cell lands on the founder at 40,12.
			int x1 = 40 - riteX, y1 = 12 - riteY;
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.TryEgress(40, 12, x1, y1,
				x1 + (int)camp.Attribute("Width") - 1, y1 + campRows.Length - 1, out int steps));
			ClassicAssert.AreEqual(campRows.Length - riteY, steps);
			ClassicAssert.AreEqual(3, steps, "rows B, 0, E then clear");
			// The surveyed heart ground is the largest heart footprint around the same rite cell.
			XElement court = Map(Court);
			string[] courtRows = Rows(court);
			int courtY = RiteRow(court, courtRows, out int courtX);
			int sx1 = 40 - courtX, sy1 = 12 - courtY, sx2 = sx1 + (int)court.Attribute("Width") - 1, sy2 = sy1 + courtRows.Length - 1;
			ClassicAssert.AreEqual(new[] { 31, 4, 50, 21 }, new[] { sx1, sy1, sx2, sy2 }, "matches the native survey line");
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.TryEgress(40, 12, sx1, sy1, sx2, sy2, out steps));
			ClassicAssert.AreEqual(courtRows.Length - courtY, steps);
			ClassicAssert.AreEqual(10, steps);
			ClassicAssert.IsTrue(KingdomScenarioTravelRules.TryEgress(40, 22, sx1, sy1, sx2, sy2, out steps));
			ClassicAssert.AreEqual(0, steps, "outside the survey nothing is owed");
		}

		[Test]
		public void WalkerUsesTheLegTableAndJournalsTheEgressCells()
		{
			string source = TestMain.ReadRepositoryText("Harness/KingdomScenarioTravel.cs");
			StringAssert.Contains("KingdomPlots.TrySurveyedHeart(zone, out KingdomPlotRules.PlotRect heart)", source);
			StringAssert.Contains("KingdomScenarioTravelRules.TryEgress(HomeX, HomeY, heart.X1, heart.Y1, heart.X2, heart.Y2", source);
			StringAssert.Contains("KingdomScenarioTravelRules.TryLeg(west, EgressSteps, EgressDone, OutSteps, BackSteps, IngressDone", source);
			StringAssert.Contains("Player.Move(current == KingdomScenarioTravelRules.Leg.Egress ? \"S\" : \"N\", AllowDashing: false, DoConfirmations: false)", source);
			StringAssert.Contains("KingdomScenarioJournal.Append(\"travel-egress\", true, \"travel-egress=\" + cells", source);
			StringAssert.Contains("Require(BackSteps == OutSteps && IngressDone == EgressDone, \"return route length differs\")", source);
			StringAssert.Contains("\"normal walking was blocked; no clearing or teleport fallback\"", source);
			StringAssert.DoesNotContain("Teleport", source);
			StringAssert.DoesNotContain("RemoveObject", source);
			StringAssert.Contains("\"travel-egress\",", TestMain.ReadRepositoryText("Tools/personas/persona_matrix.py"));
		}
	}
}
#endif
