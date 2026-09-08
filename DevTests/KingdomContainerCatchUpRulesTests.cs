#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Container-level catch-up receipts. LIVING-CITY-ARCHITECTURE §0.0(b), §3.5, §3.9:
	/// one medium unit is one real container touched, visible cells precede dedication order, and
	/// only a measured physical delta clears signed debt.
	/// </summary>
	public class KingdomContainerCatchUpRulesTests
	{
		private static KingdomContainerCatchUpRow Row(int id, int dedication,
			KingdomStockKind kind, bool visible, int room, int contents)
		{
			return new KingdomContainerCatchUpRow(id, dedication, kind, visible, room, contents);
		}

		[Test]
		public void AllWaterDebtCountsEveryPhysicalVesselItNeeds()
		{
			KingdomContainerCatchUpRow[] rows =
			{
				Row(1, 1, KingdomStockKind.Water, false, 0, 10),
				Row(2, 2, KingdomStockKind.Water, false, 0, 10),
				Row(3, 3, KingdomStockKind.Water, false, 0, 10)
			};
			KingdomContainerDemandReceipt receipt;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TryMeasure(rows, rows.Length,
				-25, 0, 0, out receipt, out fault), fault.ToString());
			ClassicAssert.AreEqual(3, receipt.RestUnits);
			ClassicAssert.AreEqual(25, receipt.WaterMovable);
			ClassicAssert.AreEqual(0, receipt.WaterBlocked);
			ClassicAssert.AreEqual(9, receipt.OwedThirds);
		}

		[Test]
		public void AllFoodLandingCountsEachLarderRatherThanOneStockKind()
		{
			KingdomContainerCatchUpRow[] rows =
			{
				Row(1, 1, KingdomStockKind.Food, false, 4, 0),
				Row(2, 2, KingdomStockKind.Food, false, 4, 0),
				Row(3, 3, KingdomStockKind.Food, false, 4, 0)
			};
			KingdomContainerDemandReceipt receipt;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TryMeasure(rows, rows.Length,
				0, 9, 0, out receipt, out fault));
			ClassicAssert.AreEqual(3, receipt.Units);
			ClassicAssert.AreEqual(9, receipt.FoodMovable);
		}

		[Test]
		public void MixedDebtKeepsKindsAndDirectionsSeparate()
		{
			KingdomContainerCatchUpRow[] rows =
			{
				Row(1, 1, KingdomStockKind.Water, true, 0, 7),
				Row(2, 2, KingdomStockKind.Food, false, 6, 0),
				Row(3, 3, KingdomStockKind.Water, false, 0, 8),
				Row(4, 4, KingdomStockKind.Food, true, 4, 0)
			};
			KingdomContainerDemandReceipt receipt;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TryMeasure(rows, rows.Length,
				-12, 9, 0, out receipt, out fault));
			ClassicAssert.AreEqual(2, receipt.VisibleUnits);
			ClassicAssert.AreEqual(2, receipt.RestUnits);
			ClassicAssert.AreEqual(12, receipt.WaterMovable);
			ClassicAssert.AreEqual(9, receipt.FoodMovable);
		}

		[Test]
		public void VisibilityPrecedesOldestDedicationThenStableId()
		{
			KingdomContainerCatchUpRow[] rows =
			{
				Row(30, 1, KingdomStockKind.Water, false, 0, 1),
				Row(20, 9, KingdomStockKind.Water, true, 0, 1),
				Row(10, 2, KingdomStockKind.Water, true, 0, 1)
			};
			List<int> order = new List<int>();
			KingdomContainerSettlementReceipt receipt;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TrySettle(rows, rows.Length,
				-3, 0, 0, 2, 1,
				delegate(int source, KingdomStockKind kind, KingdomUnitDirection direction,
					int offered, out int applied)
				{
					order.Add(rows[source].ContainerId);
					applied = offered;
					return true;
				}, out receipt, out fault));
			CollectionAssert.AreEqual(new[] { 10, 20, 30 }, order);
			ClassicAssert.AreEqual(0, receipt.OwedWater);
			ClassicAssert.AreEqual(2, receipt.VisibleSpent);
		}

		[Test]
		public void PartialAllowanceAcrossCallsAndZonesNeverBecomesPerCallSiteBudget()
		{
			KingdomReifyDemand demand = new KingdomReifyDemand(0, 3, 0, 0, 20, 0);
			KingdomReifySpend first;
			KingdomReifySpend second;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomCatchUpRules.TryPlanTurn(demand, 15, 4, out first, out fault));
			ClassicAssert.AreEqual(5, first.Medium);
			ClassicAssert.IsTrue(KingdomCatchUpRules.TryPlanTurn(demand,
				15 - first.ThirdsSpent, 4, out second, out fault));
			ClassicAssert.AreEqual(0, second.Units);
			ClassicAssert.AreEqual(5, first.Units + second.Units);
		}

		[Test]
		public void BlockedContainersLeaveQuantityHonestAndConsumeNoUnit()
		{
			KingdomContainerCatchUpRow[] rows =
			{
				Row(1, 1, KingdomStockKind.Water, true, 0, 0),
				Row(2, 2, KingdomStockKind.Food, false, 0, 0)
			};
			KingdomContainerDemandReceipt demand;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TryMeasure(rows, rows.Length,
				-8, 6, 0, out demand, out fault));
			ClassicAssert.AreEqual(0, demand.Units);
			ClassicAssert.AreEqual(8, demand.WaterBlocked);
			ClassicAssert.AreEqual(6, demand.FoodBlocked);
			int callbacks = 0;
			KingdomContainerSettlementReceipt settled;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TrySettle(rows, rows.Length,
				-8, 6, 0, 8, 8,
				delegate(int source, KingdomStockKind kind, KingdomUnitDirection direction,
					int offered, out int applied)
				{
					callbacks++;
					applied = 0;
					return false;
				}, out settled, out fault));
			ClassicAssert.AreEqual(0, callbacks);
			ClassicAssert.AreEqual(-8, settled.OwedWater);
			ClassicAssert.AreEqual(6, settled.OwedFood);
			ClassicAssert.AreEqual(0, settled.UnitsSpent);
		}

		[Test]
		public void CallbackFailureClearsOnlyItsMeasuredDeltaAndStopsTheOrder()
		{
			KingdomContainerCatchUpRow[] rows =
			{
				Row(1, 1, KingdomStockKind.Water, false, 0, 5),
				Row(2, 2, KingdomStockKind.Water, false, 0, 5)
			};
			int callbacks = 0;
			KingdomContainerSettlementReceipt receipt;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TrySettle(rows, rows.Length,
				-10, 0, 0, 0, 8,
				delegate(int source, KingdomStockKind kind, KingdomUnitDirection direction,
					int offered, out int applied)
				{
					callbacks++;
					applied = 3;
					return false;
				}, out receipt, out fault));
			ClassicAssert.AreEqual(1, callbacks, "a later reserve may not leapfrog a failed oldest vessel");
			ClassicAssert.AreEqual(-7, receipt.OwedWater);
			ClassicAssert.AreEqual(1, receipt.UnitsSpent);
			ClassicAssert.IsTrue(receipt.CallbackFailed);
		}

		[Test]
		public void SaveAndAbsenceResumeFromPersistedQuantityWithoutDoubleApplying()
		{
			KingdomContainerCatchUpRow[] rows = new KingdomContainerCatchUpRow[12];
			for (int i = 0; i < rows.Length; i++)
				rows[i] = Row(i + 1, i + 1, KingdomStockKind.Water, false, 0, 1);
			KingdomContainerSettlementReceipt first;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TrySettle(rows, rows.Length,
				-12, 0, 0, 0, 8,
				delegate(int source, KingdomStockKind kind, KingdomUnitDirection direction,
					int offered, out int applied)
				{
					applied = offered;
					rows[source] = Row(source + 1, source + 1, kind, false, 0, 0);
					return true;
				}, out first, out fault));
			ClassicAssert.AreEqual(-4, first.OwedWater);
			// Persisted signed debt is the resume token; already-empty vessels cannot pay twice.
			KingdomContainerSettlementReceipt reloaded;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TrySettle(rows, rows.Length,
				first.OwedWater, 0, 0, 0, 8,
				delegate(int source, KingdomStockKind kind, KingdomUnitDirection direction,
					int offered, out int applied)
				{
					applied = offered;
					rows[source] = Row(source + 1, source + 1, kind, false, 0, 0);
					return true;
				}, out reloaded, out fault));
			ClassicAssert.AreEqual(0, reloaded.OwedWater);
			ClassicAssert.AreEqual(12, first.UnitsSpent + reloaded.UnitsSpent);
		}

		[Test]
		public void DenseLegalZoneDerivesTrueContainerEnvelopeAndThirtyNineTurns()
		{
			int containers = KingdomRules.MaxCivicContainersPerZone;
			KingdomContainerCatchUpRow[] rows = new KingdomContainerCatchUpRow[containers];
			for (int i = 0; i < rows.Length; i++)
				rows[i] = Row(i + 1, i + 1, KingdomStockKind.Water, false, 0, 1);
			KingdomContainerDemandReceipt receipt;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomContainerCatchUpRules.TryMeasure(rows, rows.Length,
				-containers, 0, 0, out receipt, out fault));
			int units = receipt.Units + KingdomRules.MaxPopulation;
			ClassicAssert.AreEqual(KingdomCatchUpRules.WorstBacklogUnits, units);
			int turns;
			ClassicAssert.IsTrue(KingdomCatchUpRules.TryTurnsToDrain(
				units * KingdomCatchUpRules.ThirdsPerUnit, out turns, out fault));
			ClassicAssert.AreEqual(39, turns);
			ClassicAssert.Less(turns, KingdomCatchUpRules.GraceWindowTurns);
		}
	}
}
#endif
