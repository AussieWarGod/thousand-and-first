#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The memory formula. LIVING-CITY-ARCHITECTURE §0.0 says the thing these tests exist to
	/// enforce: <i>"the formula is the contract, not the constant"</i>. So the table's line items
	/// are pinned by value, the total is composed from them rather than asserted as a number, and
	/// the row widths are checked against what the row types actually declare — which is what makes
	/// a budget falsifiable by adding a field.
	/// </summary>
	public class KingdomCityMemoryTests
	{
		private const int KiB = 1024;

		/// <summary>The caps are not this file's to choose. Each is a copy of a constant that lives
		/// somewhere else in the mod, and a copy that stops agreeing with its source is exactly the
		/// defect the ladder idiom in KingdomExileRules guards against.</summary>
		[Test]
		public void EveryCapStillAgreesWithTheConstantItWasCopiedFrom()
		{
			Assert.AreEqual(KingdomSettlement.MaxSettlements, KingdomCityMemoryRules.CitiesPerRealm);
			Assert.AreEqual(KingdomRules.MaxBuildings, KingdomCityState.MaxWorks);
			Assert.AreEqual(KingdomRules.MaxPopulation, KingdomCityState.MaxResidents);
			Assert.AreEqual(KingdomZoningRules.ZonesForStage(GrowthStage.City), KingdomCityState.MaxZones);
		}

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(c), the widths, by value.</summary>
		[TestCase(96, KingdomCityMemoryRules.ZoneRowBytes)]
		[TestCase(64, KingdomCityMemoryRules.WorkRowBytes)]
		[TestCase(104, KingdomCityMemoryRules.ResidentRowStructBytes)]
		[TestCase(168, KingdomCityMemoryRules.ResidentRowBytes)]
		[TestCase(16, KingdomCityMemoryRules.ClockRowBytes)]
		[TestCase(32, KingdomCityMemoryRules.ToldRowBytes)]
		[TestCase(256, KingdomCityMemoryRules.CityHeaderBytes)]
		[TestCase(32, KingdomCityMemoryRules.BindingRowBytes)]
		[TestCase(36, KingdomCityMemoryRules.LegBytes)]
		[TestCase(280, KingdomCityMemoryRules.JobRowBytes)]
		[TestCase(48, KingdomCityMemoryRules.ResearchHeaderBytes)]
		[TestCase(12, KingdomCityMemoryRules.ResearchShelfRowBytes)]
		public void TheTablesWidthsAreWhatTheConstitutionWroteDown(int expected, int actual)
		{
			Assert.AreEqual(expected, actual);
		}

		/// <summary>
		/// Every row type is measured against the width §0.0(c) bought it. Padding is not modelled,
		/// so the declared sum is a lower bound and must fit inside the budget; a row that grows
		/// past its budget fails here, and either the field or the table has to give.
		/// </summary>
		[Test]
		public void EveryRowFitsInsideTheWidthTheConstitutionBudgetedForIt()
		{
			AssertRowFits(typeof(KingdomZoneRow), KingdomCityMemoryRules.ZoneRowBytes);
			AssertRowFits(typeof(KingdomWorkRow), KingdomCityMemoryRules.WorkRowBytes);
			AssertRowFits(typeof(KingdomResidentRow), KingdomCityMemoryRules.ResidentRowStructBytes);
			AssertRowFits(typeof(KingdomClockRow), KingdomCityMemoryRules.ClockRowBytes);
			AssertRowFits(typeof(KingdomToldRow), KingdomCityMemoryRules.ToldRowBytes);
			AssertRowFits(typeof(KingdomLeg), KingdomCityMemoryRules.LegBytes);
			AssertRowFits(typeof(KingdomWorkRunState), 16);
			// The registry's own row, budgeted at §0.0(c) and shipped in W2: key 4 + kind 1 +
			// zone ref 8 + object ref 8 + minted tick 8, against the thirty-two the table bought it.
			AssertRowFits(typeof(KingdomBinding), KingdomCityMemoryRules.BindingRowBytes);
		}

		/// <summary>
		/// The brink half of the resident row, by value. W2 moved two windows off the settler's
		/// property bag and into the row, and the row had to stay inside the ninety-six §0.0(c)
		/// budgets it — which it did, at ninety-one, with the two windows costing seventeen bytes
		/// each. This pins the window itself so that widening it fails here rather than silently
		/// eating the row's remaining headroom.
		/// <para>
		/// The creed-gate wave then spent the last of that headroom and more: Addendum 16 put the
		/// creeds a person has HELD AND LEFT into the row, as one shared string reference, and
		/// ninety-one plus eight is ninety-nine against a ninety-six budget. So the budget moved to
		/// a hundred and four and the table moved with it — which is the whole point of measuring
		/// the row rather than trusting the number.
		/// </para>
		/// </summary>
		[Test]
		public void ABrinkWindowIsAStandingFlagAndTwoTicks()
		{
			int bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(typeof(KingdomBrinkWindow), out bytes));
			Assert.AreEqual(17, bytes);
			int row;
			Assert.IsTrue(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(typeof(KingdomResidentRow), out row));
			Assert.AreEqual(99, row, "the resident row moved; if it grew past 104, §0.0(c) needs the same edit");
		}

		/// <summary>
		/// The creed history is BOUNDED, and this is what makes that falsifiable rather than
		/// promised. Addendum 16 wants a recorded fact, not a growing one: the record takes
		/// <c>MaxKeptCreeds</c> names and then stops, and a fourth conversion changes nothing —
		/// including, deliberately, not evicting the first. See
		/// <c>KingdomCreedRules.RememberKept</c> for why the record never rotates.
		/// </summary>
		[Test]
		public void ASettlersCreedHistoryIsBoundedAndNeverRotates()
		{
			string kept = "";
			bool added;
			for (int i = 0; i < KingdomCreedRules.MaxKeptCreeds + 3; i++)
			{
				kept = KingdomCreedRules.RememberKept(kept, "Creed" + i, out added);
				Assert.AreEqual(i < KingdomCreedRules.MaxKeptCreeds, added, "creed " + i);
			}
			Assert.AreEqual(KingdomCreedRules.MaxKeptCreeds, KingdomCreedRules.DecodeKept(kept).Count);
			Assert.IsTrue(KingdomCreedRules.KeptHolds(kept, "Creed0"), "the first creed a settler left is never evicted");
			Assert.IsFalse(KingdomCreedRules.KeptHolds(kept, "Creed" + KingdomCreedRules.MaxKeptCreeds));
		}

		/// <summary>Six stock/capacity longs, forty-eight bytes, on the city and on every zone row.
		/// LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		[Test]
		public void TheStockBlockIsExactlySixLongs()
		{
			int bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(typeof(KingdomStocks), out bytes));
			Assert.AreEqual(48, bytes);
		}

		/// <summary>
		/// The keepers' header is the fields the settlement actually serializes, not a number that
		/// says so. Seven named fields — roster ref 8 + subject ref 8 + accrued 4 + taken-up tick 8
		/// + stalled flag 1 + shelf ref 8 + best mind 4 — measured off the settlement type through
		/// the same declared-width sizer every row above uses. Add a <c>long</c> to the lane and
		/// this passes forty-eight and something has to give: the field, or the table.
		/// </summary>
		[Test]
		public void TheKeepersHeaderIsTheFieldsTheSettlementActuallySerialises()
		{
			int bytes;
			Assert.IsTrue(
				KingdomCityMemoryRules.TryMeasureDeclaredFieldBytes(typeof(KingdomSettlement), KingdomCityMemoryRules.ResearchFields, out bytes),
				"a field the receipt names is no longer on the settlement, so the lane moved and nothing said so");
			Assert.AreEqual(7, KingdomCityMemoryRules.ResearchFields.Length);
			Assert.AreEqual(41, bytes, "the keepers' fields moved; if they grew past 48, §0.0(c) needs the same edit");
			Assert.LessOrEqual(bytes, KingdomCityMemoryRules.ResearchHeaderBytes);
		}

		/// <summary>A name the type does not declare is refused rather than counted as nothing —
		/// which is the only reason measuring by name is worth more than restating the width.</summary>
		[Test]
		public void AFieldTheTypeNoLongerDeclaresIsRefusedRatherThanCountedAsZero()
		{
			int bytes;
			Assert.IsFalse(KingdomCityMemoryRules.TryMeasureDeclaredFieldBytes(
				typeof(KingdomSettlement), new string[1] { "ResearchFieldThatWasRenamed" }, out bytes));
			Assert.AreEqual(0, bytes);
			Assert.IsFalse(KingdomCityMemoryRules.TryMeasureDeclaredFieldBytes(typeof(KingdomSettlement), null, out bytes));
			Assert.IsFalse(KingdomCityMemoryRules.TryMeasureDeclaredFieldBytes(null, KingdomCityMemoryRules.ResearchFields, out bytes));
		}

		/// <summary>The shelf's row count is the rule's own, not a copy of it: a ninth shelving
		/// drops a row rather than widening the receipt behind its back.</summary>
		[Test]
		public void TheShelfIsPricedAtTheCapTheRuleEnforces()
		{
			Assert.AreEqual(KingdomResearchRules.ShelfRows, KingdomCityMemoryRules.ResearchShelfRows);
			long bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryResearchBytes(1, out bytes));
			Assert.AreEqual(
				KingdomCityMemoryRules.ResearchHeaderBytes + (long)KingdomResearchRules.ShelfRows * KingdomCityMemoryRules.ResearchShelfRowBytes,
				bytes);
			Assert.IsFalse(KingdomCityMemoryRules.TryResearchBytes(-1, out bytes));
			Assert.AreEqual(0L, bytes);
		}

		private static void AssertRowFits(Type row, int budget)
		{
			int bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(row, out bytes), row.Name + " could not be measured");
			Assert.LessOrEqual(bytes, budget, row.Name + " declares " + bytes + " bytes against a budget of " + budget);
		}

		/// <summary>14,496 bytes ≈ 14.2 KiB, exactly as §0.0(c) computes it row by row with the
		/// zone row at the ninety-six bytes W1 widened it to and the resident row at the hundred
		/// and four the creed history widened it to.</summary>
		[Test]
		public void OneCityAtTodaysCapsIsTheTablesOwnFigure()
		{
			long bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryCityModelBytes(
				KingdomCityState.MaxZones, KingdomCityState.MaxWorks, KingdomCityState.MaxResidents, KingdomCityState.MaxClocks, out bytes));
			Assert.AreEqual(14496L, bytes);
		}

		[Test]
		public void EachOtherLineOfTheTableComposesToItsOwnFigure()
		{
			long registry;
			Assert.IsTrue(KingdomCityMemoryRules.TryRegistryBytes(KingdomCityState.MaxResidents, KingdomCityMemoryRules.CitiesPerRealm, KingdomCityMemoryRules.MaxOpenJobs, out registry));
			Assert.AreEqual(4480L, registry, "binding registry, realm-scope");

			long jobs;
			Assert.IsTrue(KingdomCityMemoryRules.TryJobBytes(KingdomCityMemoryRules.MaxOpenJobs, out jobs));
			Assert.AreEqual(4480L, jobs, "job rows with itineraries");

			long distance;
			Assert.IsTrue(KingdomCityMemoryRules.TryDistanceMatrixBytes(1, out distance));
			Assert.AreEqual(3042L, distance, "distance matrix, per city");

			long networks;
			Assert.IsTrue(KingdomCityMemoryRules.TryNetworkBytes(1, out networks));
			// W7 re-pin, justified in KingdomCityMemoryRules: 5,248 was four networks of
			// 32x16 + 48x16 + a 32-byte header, priced before anything had been built to sit in
			// the header. The header is 64 (four array references do not fit in 32) and each
			// network now also stores its traversal order, two bytes a node, which is what holds
			// the SOLVE inside §3.11's O(nodes + edges) ceiling instead of nodes x edges. The
			// realm total moves 768 bytes and stays under the advisory rung; the ceiling has not
			// moved and is what a regression is measured against.
			Assert.AreEqual(5632L, networks, "network graphs, per city");

			long research;
			Assert.IsTrue(KingdomCityMemoryRules.TryResearchBytes(1, out research));
			// The keepers, per city: the seven-field header at 48 plus eight shelf rows at 12.
			// Its own line because the state hangs off the settlement container and not off the
			// city's book -- Addendum 22 B1's siting, priced where it actually lives.
			Assert.AreEqual(144L, research, "the keepers' state, per city");
		}

		/// <summary>
		/// The row §0.0's table is actually answerable for: model + registry + itineraries +
		/// distance matrix + network graphs, per realm, under the 64 KiB ceiling.
		/// </summary>
		[Test]
		public void TheRealmFitsUnderTheCeilingAtTodaysCaps()
		{
			long bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryRealmBytesAtTodaysCaps(out bytes));
			// 55,300 + 288: the research wave put the keepers' seven fields and their eight-row
			// shelf on each of the realm's two settlement containers, and a lane the receipt
			// cannot see is a lane with no owner.
			Assert.AreEqual(55588L, bytes, "the composed realm total moved");
			Assert.Less(bytes, KingdomBudgetRules.ModelBytesCeiling, "the realm broke the 64 KiB ceiling");
			// W0 recorded this rather than asserting it away: the composed total (52.3 KiB) sat
			// ABOVE §0.0's own 48 KiB warn rung, so the design shipped permanently inside its own
			// warning. §0.0 settled it in W1 by raising the ADVISORY rung to 56 KiB and leaving the
			// ceiling untouched at 64 -- warn is advice, the ceiling is the contract. The verdict
			// is pinned at Within so that a width change which genuinely eats the new headroom
			// fails here rather than being read as the old permanent warning.
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.ModelBytes, bytes),
				"the realm total crossed the 56 KiB advisory rung -- if the widths changed, §0.0(c) needs the same edit");
			Assert.Less(bytes, KingdomBudgetRules.ModelBytesWarn, "the advisory rung is only advice if the total is under it");
		}

		/// <summary>
		/// The same formula at one whole parasang, caps scaled with it. §0.0(f)'s claim is that
		/// nothing here changes when the city grows — only R does — and this is the figure that
		/// claim is worth: about 88 KiB, over today's ceiling and still under a tenth of a megabyte,
		/// which is why the ceiling is checked against the formula at the live caps rather than
		/// against a frozen number.
		/// </summary>
		[Test]
		public void TheSameFormulaAnswersForANineZoneCity()
		{
			long bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryRealmBytesAtFullParasang(out bytes));
			Assert.AreEqual(92948L, bytes);
			Assert.Greater(bytes, KingdomBudgetRules.ModelBytesCeiling, "a nine-zone realm is over TODAY's ceiling by design");
			Assert.Less(bytes, 100L * KiB, "still under a tenth of a megabyte");
		}

		/// <summary>Cost is O(rows) and nothing else: doubling the residents moves the total by
		/// exactly the residents' own width, never by a term in the elapsed or in the zone count.</summary>
		[Test]
		public void TheModelIsLinearInRowsAndInNothingElse()
		{
			long baseline;
			long doubled;
			Assert.IsTrue(KingdomCityMemoryRules.TryCityModelBytes(4, 40, 30, 12, out baseline));
			Assert.IsTrue(KingdomCityMemoryRules.TryCityModelBytes(4, 40, 60, 12, out doubled));
			Assert.AreEqual(30L * KingdomCityMemoryRules.ResidentRowBytes, doubled - baseline);
		}

		[TestCase(-1, 0, 0, 0)]
		[TestCase(0, -1, 0, 0)]
		[TestCase(0, 0, -1, 0)]
		[TestCase(0, 0, 0, -1)]
		public void ANegativeCountIsRefusedRatherThanUnderReported(int zones, int works, int residents, int clocks)
		{
			long bytes;
			Assert.IsFalse(KingdomCityMemoryRules.TryCityModelBytes(zones, works, residents, clocks, out bytes));
			Assert.AreEqual(0L, bytes);
		}

		[Test]
		public void AnUnmeasurableTypeIsRefusedRatherThanCountedAsZero()
		{
			int bytes;
			Assert.IsFalse(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(null, out bytes));
		}
	}
}
#endif
