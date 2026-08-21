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
		[TestCase(96, KingdomCityMemoryRules.ResidentRowStructBytes)]
		[TestCase(160, KingdomCityMemoryRules.ResidentRowBytes)]
		[TestCase(16, KingdomCityMemoryRules.ClockRowBytes)]
		[TestCase(32, KingdomCityMemoryRules.ToldRowBytes)]
		[TestCase(256, KingdomCityMemoryRules.CityHeaderBytes)]
		[TestCase(32, KingdomCityMemoryRules.BindingRowBytes)]
		[TestCase(36, KingdomCityMemoryRules.LegBytes)]
		[TestCase(280, KingdomCityMemoryRules.JobRowBytes)]
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
		/// budgets it — which it does, at ninety-one, with the two windows costing seventeen bytes
		/// each. This pins the window itself so that widening it fails here rather than silently
		/// eating the row's remaining headroom.
		/// </summary>
		[Test]
		public void ABrinkWindowIsAStandingFlagAndTwoTicks()
		{
			int bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(typeof(KingdomBrinkWindow), out bytes));
			Assert.AreEqual(17, bytes);
			int row;
			Assert.IsTrue(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(typeof(KingdomResidentRow), out row));
			Assert.AreEqual(91, row, "the resident row moved; if it grew past 96, §0.0(c) needs the same edit");
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

		private static void AssertRowFits(Type row, int budget)
		{
			int bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(row, out bytes), row.Name + " could not be measured");
			Assert.LessOrEqual(bytes, budget, row.Name + " declares " + bytes + " bytes against a budget of " + budget);
		}

		/// <summary>14,016 bytes ≈ 13.7 KiB, exactly as §0.0(c) computes it row by row with the
		/// zone row at the ninety-six bytes W1 widened it to.</summary>
		[Test]
		public void OneCityAtTodaysCapsIsTheTablesOwnFigure()
		{
			long bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryCityModelBytes(
				KingdomCityState.MaxZones, KingdomCityState.MaxWorks, KingdomCityState.MaxResidents, KingdomCityState.MaxClocks, out bytes));
			Assert.AreEqual(14016L, bytes);
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
			Assert.AreEqual(5248L, networks, "network graphs, per city");
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
			Assert.AreEqual(53572L, bytes, "the composed realm total moved");
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
			Assert.AreEqual(89732L, bytes);
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
