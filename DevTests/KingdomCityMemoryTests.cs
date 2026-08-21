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
		[TestCase(80, KingdomCityMemoryRules.ZoneRowBytes)]
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

		/// <summary>13,952 bytes ≈ 13.6 KiB, exactly as §0.0(c) computes it row by row.</summary>
		[Test]
		public void OneCityAtTodaysCapsIsTheTablesOwnFigure()
		{
			long bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryCityModelBytes(
				KingdomCityState.MaxZones, KingdomCityState.MaxWorks, KingdomCityState.MaxResidents, KingdomCityState.MaxClocks, out bytes));
			Assert.AreEqual(13952L, bytes);
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
			Assert.AreEqual(53444L, bytes, "the composed realm total moved");
			Assert.Less(bytes, KingdomBudgetRules.ModelBytesCeiling, "the realm broke the 64 KiB ceiling");
			Assert.AreNotEqual(KingdomBudgetVerdict.Over, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.ModelBytes, bytes));
			// Recorded rather than asserted away: §0.0(c)'s own byte-by-byte total (≈ 51.9 KiB,
			// composed here as 52.2 KiB -- the table's figure is the sum of its rounded KiB
			// column) sits ABOVE the same table's 48 KiB warn rung. The design ships in its own
			// warn band at today's caps. That is the constitution's business to settle, not this
			// test's to hide, so the verdict is pinned as Warn and named.
			Assert.AreEqual(KingdomBudgetVerdict.Warn, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.ModelBytes, bytes),
				"the realm total left the warn band -- if the widths changed, §0.0(c) needs the same edit");
		}

		/// <summary>
		/// The same formula at one whole parasang, caps scaled with it. §0.0(f)'s claim is that
		/// nothing here changes when the city grows — only R does — and this is the figure that
		/// claim is worth: about 87 KiB, over today's ceiling and still under a tenth of a megabyte,
		/// which is why the ceiling is checked against the formula at the live caps rather than
		/// against a frozen number.
		/// </summary>
		[Test]
		public void TheSameFormulaAnswersForANineZoneCity()
		{
			long bytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryRealmBytesAtFullParasang(out bytes));
			Assert.AreEqual(89444L, bytes);
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
