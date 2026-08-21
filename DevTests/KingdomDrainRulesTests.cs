#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The drain order. LIVING-CITY-ARCHITECTURE §3.9, invariant I4: deficits drain real containers
	/// in a stated deterministic order — oldest dedication first — and every mismatch is attributed
	/// and told rather than silently repaired.
	/// </summary>
	public class KingdomDrainRulesTests
	{
		private static KingdomVesselRow Cask(int id, int dedication, long level, bool fresh = true)
		{
			return new KingdomVesselRow(id, dedication, KingdomStockKind.Water, level, 200L, fresh);
		}

		private static int[] Order(KingdomVesselRow[] vessels)
		{
			int[] order = new int[vessels.Length];
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDrainRules.TryOrder(vessels, vessels.Length, order, out fault), fault.ToString());
			int[] ids = new int[vessels.Length];
			for (int i = 0; i < vessels.Length; i++)
			{
				ids[i] = vessels[order[i]].VesselId;
			}
			return ids;
		}

		[Test]
		public void TheOldestDedicationGoesFirst()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[3]
			{
				Cask(30, 9, 100L),
				Cask(10, 1, 100L),
				Cask(20, 5, 100L)
			};
			CollectionAssert.AreEqual(new int[3] { 10, 20, 30 }, Order(vessels));
		}

		/// <summary>
		/// The reason §3.9 refuses "smallest first": the smallest REMAINING vessel changes as the
		/// drain proceeds, so a reload resuming from a slightly different intermediate state could
		/// pick a different urn. Dedication order does not move when the contents do.
		/// </summary>
		[Test]
		public void TheOrderDoesNotMoveWhenTheContentsDo()
		{
			KingdomVesselRow[] full = new KingdomVesselRow[3] { Cask(30, 9, 200L), Cask(10, 1, 5L), Cask(20, 5, 90L) };
			KingdomVesselRow[] drained = new KingdomVesselRow[3] { Cask(30, 9, 1L), Cask(10, 1, 200L), Cask(20, 5, 0L) };
			CollectionAssert.AreEqual(Order(full), Order(drained));
		}

		[Test]
		public void TiesBreakOnTheLowerVesselIdAndNothingElse()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[3] { Cask(9, 4, 1L), Cask(2, 4, 999L), Cask(7, 4, 50L) };
			CollectionAssert.AreEqual(new int[3] { 2, 7, 9 }, Order(vessels));
		}

		[Test]
		public void ANullOrOverlongOrderIsRefused()
		{
			KingdomCityFault fault;
			Assert.IsFalse(KingdomDrainRules.TryOrder(null, 0, new int[1], out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.IsFalse(KingdomDrainRules.TryOrder(new KingdomVesselRow[1], 1, null, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.IsFalse(KingdomDrainRules.TryOrder(new KingdomVesselRow[1], 2, new int[2], out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}

		/// <summary>The founder who opens the cistern after a season finds exactly the model's
		/// remainder — drained out of the vessels the city was actually given first.</summary>
		[Test]
		public void ADemandDrainsTheOldestVesselsUntilItIsMet()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[3] { Cask(3, 3, 100L), Cask(1, 1, 40L), Cask(2, 2, 30L) };
			long[] drawn = new long[3];
			long shortfall;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 3, KingdomStockKind.Water, 90L, drawn, out shortfall, out fault));
			Assert.AreEqual(0L, shortfall);
			Assert.AreEqual(40L, drawn[1], "the oldest cask was not drained first");
			Assert.AreEqual(30L, drawn[2]);
			Assert.AreEqual(20L, drawn[0], "the newest dedication is the reserve and went last");
		}

		[Test]
		public void WhatTheVesselsCannotCoverComesBackNamedRatherThanForgiven()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[2] { Cask(1, 1, 10L), Cask(2, 2, 5L) };
			long[] drawn = new long[2];
			long shortfall;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 2, KingdomStockKind.Water, 100L, drawn, out shortfall, out fault));
			Assert.AreEqual(85L, shortfall);
			Assert.AreEqual(10L, drawn[0]);
			Assert.AreEqual(5L, drawn[1]);
		}

		/// <summary>Qud's salt pools are water-600,salt-400. A drain may never launder brine into
		/// the books — STANDARDS §1 and LIVING-CITY-ARCHITECTURE §3.9.</summary>
		[Test]
		public void ABrineVesselIsPassedOverRatherThanPartlyDrained()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[2] { Cask(1, 1, 100L, fresh: false), Cask(2, 2, 100L) };
			long[] drawn = new long[2];
			long shortfall;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 2, KingdomStockKind.Water, 50L, drawn, out shortfall, out fault));
			Assert.AreEqual(0L, drawn[0], "brine was drunk");
			Assert.AreEqual(50L, drawn[1]);
			Assert.AreEqual(0L, shortfall);
		}

		/// <summary>The fresh-water rule is a water rule. A larder is not brine and is not skipped
		/// for carrying the flag false.</summary>
		[Test]
		public void TheFreshnessRuleAppliesToWaterAndNotToEveryStock()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[1]
			{
				new KingdomVesselRow(1, 1, KingdomStockKind.Food, 100L, 200L, fresh: false)
			};
			long[] drawn = new long[1];
			long shortfall;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 1, KingdomStockKind.Food, 30L, drawn, out shortfall, out fault));
			Assert.AreEqual(30L, drawn[0]);
			Assert.AreEqual(0L, shortfall);
		}

		[Test]
		public void AVesselOfAnotherKindIsNeverTouched()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[2]
			{
				new KingdomVesselRow(1, 1, KingdomStockKind.Food, 100L, 200L, true),
				Cask(2, 2, 100L)
			};
			long[] drawn = new long[2];
			long shortfall;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 2, KingdomStockKind.Water, 40L, drawn, out shortfall, out fault));
			Assert.AreEqual(0L, drawn[0]);
			Assert.AreEqual(40L, drawn[1]);
		}

		[Test]
		public void AZeroDemandDrainsNothingAndANegativeOneIsRefused()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[1] { Cask(1, 1, 100L) };
			long[] drawn = new long[1];
			long shortfall;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 1, KingdomStockKind.Water, 0L, drawn, out shortfall, out fault));
			Assert.AreEqual(0L, drawn[0]);
			Assert.AreEqual(0L, shortfall);
			Assert.IsFalse(KingdomDrainRules.TryApportion(vessels, 1, KingdomStockKind.Water, -1L, drawn, out shortfall, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidRate, fault);
		}

		/// <summary>The same demand over the same vessels apportions identically, every time. That
		/// is the half of step 90g a reload actually tests.</summary>
		[Test]
		public void TheApportionmentIsReproducible()
		{
			KingdomVesselRow[] vessels = new KingdomVesselRow[4] { Cask(4, 7, 25L), Cask(1, 2, 25L), Cask(3, 5, 25L), Cask(2, 2, 25L) };
			long[] first = new long[4];
			long[] second = new long[4];
			long shortfall;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 4, KingdomStockKind.Water, 60L, first, out shortfall, out fault));
			Assert.IsTrue(KingdomDrainRules.TryApportion(vessels, 4, KingdomStockKind.Water, 60L, second, out shortfall, out fault));
			CollectionAssert.AreEqual(first, second);
			Assert.AreEqual(25L, first[1], "vessel 1, dedicated first, was not drained first");
			Assert.AreEqual(25L, first[3], "vessel 2 tied on dedication and lost the lower-id tiebreak");
			Assert.AreEqual(10L, first[2]);
			Assert.AreEqual(0L, first[0]);
		}

		[Test]
		public void ANullApportionmentIsRefusedAndWritesNothing()
		{
			long shortfall;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomDrainRules.TryApportion(null, 0, KingdomStockKind.Water, 1L, new long[1], out shortfall, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.IsFalse(KingdomDrainRules.TryApportion(new KingdomVesselRow[1], 1, KingdomStockKind.Water, 1L, null, out shortfall, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
		}
	}
}
#endif
