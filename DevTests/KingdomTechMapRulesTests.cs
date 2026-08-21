#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The keepers' map.
	/// <para>
	/// <c>_notes/DIVERSITY-AND-TECH-TREES.md</c> §2.2's ruling governs: the map may show the
	/// dependency graph the catalogue already contains, and may not become a screen the founder
	/// pays into. These cases pin the arithmetic of "how close" and the prose of "why not", which
	/// are the two things a map has to get right to be worth drawing.
	/// </para>
	/// </summary>
	internal class KingdomTechMapRulesTests
	{
		/// <summary>Distance is the count of gates the SETTLEMENT has not met. Every missing
		/// knowledge key counts separately, because each is a separate errand.</summary>
		[TestCase(false, 0, false, false, 0)]
		[TestCase(true, 0, false, false, 1)]
		[TestCase(false, 2, false, false, 2)]
		[TestCase(true, 1, true, true, 4)]
		public void Distance_CountsEveryUnmetGate(bool tech, int knowledge, bool zones, bool stage, int expected)
		{
			Assert.AreEqual(expected, KingdomTechMapRules.Distance(tech, knowledge, zones, stage));
		}

		/// <summary>A district gate does not count as distance. It is answered by standing
		/// somewhere else, which is not an errand, and counting it would rank a design the founder
		/// could raise today below one they cannot raise at all.</summary>
		[Test]
		public void Distance_ADistrictIsNotDistance()
		{
			Assert.AreEqual(0, KingdomTechMapRules.Distance(false, 0, false, false));
			string missing = KingdomTechMapRules.Missing(null, null, "workshop", 0, 1, null, "a craft quarter");
			StringAssert.Contains("a craft quarter", missing);
		}

		/// <summary>What is in the way is listed in the gates' own refusal order: knowledge, then
		/// craft, then ground, then stage, then the district.</summary>
		[Test]
		public void Missing_ListsGatesInTheRefusalOrder()
		{
			string missing = KingdomTechMapRules.Missing(
				new List<string> { "solar still" }, "foundry", "workshop", 3, 1, "Town", "a craft quarter");
			int knowledge = missing.IndexOf("solar still");
			int craft = missing.IndexOf("foundry");
			int ground = missing.IndexOf("parasangs");
			int stage = missing.IndexOf("Town");
			int district = missing.IndexOf("craft quarter");
			Assert.Less(knowledge, craft);
			Assert.Less(craft, ground);
			Assert.Less(ground, stage);
			Assert.Less(stage, district);
		}

		/// <summary>Nothing in the way and no district is no clause at all, not an empty
		/// sentence.</summary>
		[Test]
		public void Missing_NothingInTheWayIsSilence()
		{
			Assert.AreEqual("", KingdomTechMapRules.Missing(null, null, "workshop", 0, 2, null, null));
		}

		/// <summary>The head names the level, the count, and what the next rung costs — the same
		/// three numbers the keepers' own readout uses, so the two surfaces cannot disagree.</summary>
		[Test]
		public void Header_NamesTheLevelAndTheNextRungsCost()
		{
			string header = KingdomTechMapRules.Header("Kavvat", 5, "workshop", 4, "foundry");
			StringAssert.Contains("Kavvat", header);
			StringAssert.Contains("workshop", header);
			StringAssert.Contains("5 things", header);
			StringAssert.Contains("4 more", header);
			StringAssert.Contains("foundry", header);
		}

		/// <summary>At the top of the ladder the head says so instead of promising a rung that does
		/// not exist.</summary>
		[Test]
		public void Header_AtTheTopSaysThereIsNoHigher()
		{
			StringAssert.Contains("no higher craft", KingdomTechMapRules.Header("Kavvat", 14, "arclight", 0, "arclight"));
		}

		/// <summary>Roads not taken names only the ways this city has never walked, and every one
		/// of them is a thing that happens out in the world rather than at a screen.</summary>
		[Test]
		public void RoadsNotTaken_NamesOnlyTheUnwalkedWays()
		{
			string all = KingdomTechMapRules.RoadsNotTaken(false, false, false);
			StringAssert.Contains("data disk", all);
			StringAssert.Contains("certified no machine", all);
			StringAssert.Contains("trade of their own", all);

			string some = KingdomTechMapRules.RoadsNotTaken(true, false, true);
			StringAssert.DoesNotContain("data disk", some);
			StringAssert.Contains("certified no machine", some);
		}

		/// <summary>A city that has walked all of them is told nothing, because there is nothing to
		/// tell.</summary>
		[Test]
		public void RoadsNotTaken_AllWalkedIsSilence()
		{
			Assert.AreEqual("", KingdomTechMapRules.RoadsNotTaken(true, true, true));
		}

		/// <summary>Nearest first, then by name. Fully determined, so a reload never reshuffles the
		/// map.</summary>
		[Test]
		public void Sort_IsNearestFirstThenStable()
		{
			List<TechMapRow> rows = new List<TechMapRow>
			{
				new TechMapRow("c", "cistern", 3, "x"),
				new TechMapRow("a", "arclight forge", 1, "x"),
				new TechMapRow("b", "bakehouse", 1, "x")
			};
			KingdomTechMapRules.Sort(rows);
			Assert.AreEqual("arclight forge", rows[0].Name);
			Assert.AreEqual("bakehouse", rows[1].Name);
			Assert.AreEqual("cistern", rows[2].Name);
		}

		/// <summary>Distance is said in words, so the number never has to be interpreted, and zero
		/// reads as within reach rather than as "0 things away".</summary>
		[Test]
		public void Reach_SaysItInWords()
		{
			StringAssert.Contains("within reach", KingdomTechMapRules.Reach(0));
			StringAssert.Contains("one thing away", KingdomTechMapRules.Reach(1));
			StringAssert.Contains("4 things away", KingdomTechMapRules.Reach(4));
		}

		/// <summary>A row with no unmet gate is open. The map's whole claim rests on that being
		/// the same question the gates ask.</summary>
		[Test]
		public void Row_OpenIsZeroDistance()
		{
			Assert.IsTrue(new TechMapRow("k", "n", 0, "").Open);
			Assert.IsFalse(new TechMapRow("k", "n", 1, "x").Open);
		}
	}
}
#endif
