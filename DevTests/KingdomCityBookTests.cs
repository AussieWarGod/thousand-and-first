#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The carrier. LIVING-CITY-ARCHITECTURE §1.3: the rules layer stays frozen and total, the
	/// carrier is written by exactly one publisher in one assignment, and a book read out of a save
	/// is repaired rather than trusted. These tests are the round trip and the repairs.
	/// </summary>
	public class KingdomCityBookTests
	{
		private static KingdomStocks Stocks(long water, long waterCap, long food, long foodCap)
		{
			return new KingdomStocks(
				new KingdomStockPair(water, waterCap),
				new KingdomStockPair(food, foodCap),
				new KingdomStockPair(0L, 0L));
		}

		private static KingdomCityState Peopled()
		{
			KingdomZoneRow[] zones = new KingdomZoneRow[2]
			{
				new KingdomZoneRow("taf:zone:a", 3, 700L, Stocks(40L, 120L, 8L, 30L), 5, 2, 6, 7, -12, 3, 0),
				new KingdomZoneRow("taf:zone:b", 0, 900L, Stocks(11L, 60L, 0L, 12L), 1, 0, 2, 0, 0, 0, 0)
			};
			KingdomWorkRow[] works = new KingdomWorkRow[1]
			{
				new KingdomWorkRow(4242, "taf:zone:a", 12, 34, "taf:design:cistern", 87, 0, 800L,
					new KingdomWorkRunState(KingdomWorkKind.Growing, 2, 15, 1500L))
			};
			KingdomResidentRow[] residents = new KingdomResidentRow[1]
			{
				new KingdomResidentRow(9, "Ptoh", 2, 3, 400L, 4242, 4242, 1, KingdomDayShape.Field,
					KingdomResidentStanding.Abroad, "taf:zone:b", 410L, true, 420L, false, 2, 1)
			};
			KingdomClockRow[] clocks = new KingdomClockRow[1]
			{
				new KingdomClockRow(KingdomClockKind.Harvest, 1800L, 7)
			};
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 950L, Stocks(51L, 180L, 8L, 42L), zones, works, residents, clocks, out state, out fault), fault.ToString());
			KingdomCityState told;
			Assert.IsTrue(state.TryTell(new KingdomToldRow(KingdomToldKind.Harvest, 640L, 1, 2, "taf:zone:a", 3), out told, out fault));
			return told;
		}

		/// <summary>Every row family the model holds survives the round trip. A carrier that
		/// silently drops a family the state already carries is how a wave loses a city.</summary>
		[Test]
		public void EveryRowFamilyRoundTripsThroughTheColumns()
		{
			KingdomCityState before = Peopled();
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(before, out fault), fault.ToString());
			KingdomCityState after;
			Assert.IsTrue(book.TryRead(out after, out fault), fault.ToString());

			Assert.AreEqual(before.SettlementId, after.SettlementId);
			Assert.AreEqual(before.ProcessedThroughTick, after.ProcessedThroughTick);
			Assert.AreEqual(before.Stocks.Water.Level, after.Stocks.Water.Level);
			Assert.AreEqual(before.Stocks.Food.Capacity, after.Stocks.Food.Capacity);
			Assert.AreEqual(before.ZoneCount, after.ZoneCount);
			Assert.AreEqual(before.WorkCount, after.WorkCount);
			Assert.AreEqual(before.ResidentCount, after.ResidentCount);
			Assert.AreEqual(before.ClockCount, after.ClockCount);
			Assert.AreEqual(before.ToldCount, after.ToldCount);

			KingdomZoneRow zoneBefore;
			KingdomZoneRow zoneAfter;
			Assert.IsTrue(before.TryZone(0, out zoneBefore));
			Assert.IsTrue(after.TryZone(0, out zoneAfter));
			Assert.AreEqual(zoneBefore.ZoneId, zoneAfter.ZoneId);
			Assert.AreEqual(zoneBefore.DistrictCode, zoneAfter.DistrictCode);
			Assert.AreEqual(zoneBefore.LastReadTick, zoneAfter.LastReadTick);
			Assert.AreEqual(zoneBefore.Stocks.Water.Level, zoneAfter.Stocks.Water.Level);
			Assert.AreEqual(zoneBefore.Stocks.Food.Capacity, zoneAfter.Stocks.Food.Capacity);
			Assert.AreEqual(zoneBefore.Roofs, zoneAfter.Roofs);
			Assert.AreEqual(zoneBefore.Defence, zoneAfter.Defence);
			Assert.AreEqual(zoneBefore.WaterCarry, zoneAfter.WaterCarry);
			Assert.AreEqual(zoneBefore.FoodCarry, zoneAfter.FoodCarry);
			Assert.AreEqual(-12, zoneAfter.OwedWater, "a standing draw must survive the save; that is what makes it a debt");
			Assert.AreEqual(3, zoneAfter.OwedFood, "a landing and a draw stand at once on one row");

			KingdomWorkRow workBefore;
			KingdomWorkRow workAfter;
			Assert.IsTrue(before.TryWork(0, out workBefore));
			Assert.IsTrue(after.TryWork(0, out workAfter));
			Assert.AreEqual(workBefore.WorkId, workAfter.WorkId);
			Assert.AreEqual(workBefore.AnchorX, workAfter.AnchorX);
			Assert.AreEqual(workBefore.DesignKey, workAfter.DesignKey);
			Assert.AreEqual(workBefore.ConditionPercent, workAfter.ConditionPercent);
			Assert.AreEqual(workBefore.RunState.Kind, workAfter.RunState.Kind);
			Assert.AreEqual(workBefore.RunState.Stage, workAfter.RunState.Stage);
			Assert.AreEqual(workBefore.RunState.NextTick, workAfter.RunState.NextTick);

			KingdomResidentRow personBefore;
			KingdomResidentRow personAfter;
			Assert.IsTrue(before.TryResident(0, out personBefore));
			Assert.IsTrue(after.TryResident(0, out personAfter));
			Assert.AreEqual(personBefore.Name, personAfter.Name);
			Assert.AreEqual(personBefore.Standing, personAfter.Standing);
			Assert.AreEqual(personBefore.DayShape, personAfter.DayShape);
			Assert.AreEqual(personBefore.RoofWarned, personAfter.RoofWarned);
			Assert.AreEqual(personBefore.CreedWarned, personAfter.CreedWarned);
			Assert.AreEqual(personBefore.BoundZoneId, personAfter.BoundZoneId);

			KingdomToldRow toldBefore;
			KingdomToldRow toldAfter;
			Assert.IsTrue(before.TryTold(0, out toldBefore));
			Assert.IsTrue(after.TryTold(0, out toldAfter));
			Assert.AreEqual(toldBefore.Kind, toldAfter.Kind);
			Assert.AreEqual(toldBefore.Tick, toldAfter.Tick);
			Assert.AreEqual(toldBefore.PlaceZoneId, toldAfter.PlaceZoneId);
		}

		/// <summary>A book nobody has written to is an empty city, not a fault.</summary>
		[Test]
		public void AFreshBookReadsAsACityWithNothingInIt()
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(new KingdomCityBook().TryRead(out state, out fault), fault.ToString());
			Assert.AreEqual(0, state.ZoneCount);
			Assert.AreEqual(0, state.RowCount);
			Assert.AreEqual(0L, state.ProcessedThroughTick);
		}

		/// <summary>Publishing twice leaves the book holding the SECOND state and nothing of the
		/// first. The columns are rewritten, never appended to.</summary>
		[Test]
		public void PublishingRewritesTheColumnsRatherThanAppendingToThem()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			Assert.AreEqual(2, book.ZoneCount);
			Assert.AreEqual(1, book.WorkCount);
			Assert.AreEqual(1, book.ToldCount);
		}

		[Test]
		public void PublishingNothingIsRefusedAndTheBookIsUntouched()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			Assert.IsFalse(book.TryPublish(null, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.AreEqual(2, book.ZoneCount, "a refused publish must leave the book byte-identical");
		}

		/// <summary>An absent named field arrives as a null column. It becomes an empty one rather
		/// than throwing inside the engine's block-skip recovery, which would cost the city.</summary>
		[Test]
		public void ANullColumnIsRepairedRatherThanThrown()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			book.ZoneRoofs = null;
			book.ToldTicks = null;
			book.Normalize();
			Assert.AreEqual(0, book.ZoneCount, "a zone row missing a field is not a zone row");
			Assert.AreEqual(0, book.ToldCount);
		}

		/// <summary>
		/// Ragged columns are truncated to the shortest. A reader that trusted the longest column
		/// would invent a zone out of a default id, and nothing is invented for ground the game has
		/// never looked at.
		/// </summary>
		[Test]
		public void RaggedColumnsAreTruncatedToTheShortest()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			book.ZoneIds.Add("taf:zone:ghost");
			book.ZoneDistrictCodes.Add(1);
			book.Normalize();
			Assert.AreEqual(2, book.ZoneCount);
			Assert.AreEqual("taf:zone:b", book.ZoneIds[1], "the half-written row is the one that goes");
		}

		/// <summary>No dimension of this model grows (§1.4). Rows past a cap are dropped on the
		/// way in rather than refused on the way out.</summary>
		[Test]
		public void RowsPastTheirCapAreDropped()
		{
			KingdomCityBook book = new KingdomCityBook();
			for (int i = 0; i < KingdomCityState.MaxZones + 3; i++)
			{
				book.ZoneIds.Add("taf:zone:" + i);
				book.ZoneDistrictCodes.Add(0);
				book.ZoneLastReadTicks.Add(100L + i);
				book.ZoneWaterLevels.Add(1L);
				book.ZoneWaterCapacities.Add(2L);
				book.ZoneFoodLevels.Add(0L);
				book.ZoneFoodCapacities.Add(0L);
				book.ZoneMaterialsLevels.Add(0L);
				book.ZoneMaterialsCapacities.Add(0L);
				book.ZoneRoofs.Add(0);
				book.ZoneDefences.Add(0);
				book.ZoneWaterCarries.Add(0);
				book.ZoneFoodCarries.Add(0);
				book.ZoneOwedWater.Add(0);
				book.ZoneOwedFood.Add(0);
				book.ZoneOwedMaterials.Add(0);
			}
			book.Normalize();
			Assert.AreEqual(KingdomCityState.MaxZones, book.ZoneCount);
		}

		/// <summary>The ring forgets its OLDEST lines, never its newest: a book that came back with
		/// more than the ring holds keeps the end of the story.</summary>
		[Test]
		public void AnOverlongToldLogKeepsItsNewestLines()
		{
			KingdomCityBook book = new KingdomCityBook();
			for (int i = 0; i < KingdomCityState.MaxToldEntries + 5; i++)
			{
				book.ToldKinds.Add((int)KingdomToldKind.Harvest);
				book.ToldTicks.Add(1000L + i);
				book.ToldSubjectsA.Add(i);
				book.ToldSubjectsB.Add(0);
				book.ToldPlaceZoneIds.Add("taf:zone:a");
				book.ToldOutcomes.Add(0);
			}
			book.Normalize();
			Assert.AreEqual(KingdomCityState.MaxToldEntries, book.ToldCount);
			Assert.AreEqual(1005L, book.ToldTicks[0], "the ring dropped the wrong end");
			Assert.AreEqual(1000L + KingdomCityState.MaxToldEntries + 4, book.ToldTicks[book.ToldCount - 1]);
		}

		/// <summary>A stamp below zero is a corrupt reading and not a model in debt: the book fails
		/// closed to "nothing reckoned yet" rather than refusing a whole city.</summary>
		[Test]
		public void ACorruptStampFailsClosedRatherThanRefusingTheCity()
		{
			KingdomCityBook book = new KingdomCityBook();
			book.ProcessedThroughTick = -5L;
			book.SettlementId = null;
			book.Normalize();
			Assert.AreEqual(0L, book.ProcessedThroughTick);
			Assert.AreEqual("", book.SettlementId);
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(book.TryRead(out state, out fault));
		}

		/// <summary>The lookup every re-plumbed sighting reader goes through.</summary>
		[Test]
		public void AZoneRowIsFoundByItsIdAndNotByPosition()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			int index;
			Assert.IsTrue(book.TryZoneRow("taf:zone:b", out index));
			Assert.AreEqual(1, index);
			Assert.IsFalse(book.TryZoneRow("taf:zone:never", out index));
			Assert.IsFalse(book.TryZoneRow(null, out index));
		}
	}
}
#endif
