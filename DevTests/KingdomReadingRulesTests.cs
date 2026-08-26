#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Api;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The one seam between the internal model and the published reading.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE §6.6 publishes contracts, not rows. These cases pin the two
	/// properties that makes true: every published value is the model's own, and the two enum
	/// vocabularies are MAPPED rather than cast, so a model-side insertion cannot silently
	/// renumber somebody else's API.
	/// </para>
	/// </summary>
	internal class KingdomReadingRulesTests
	{
		private const string Here = "taf:zone:here";

		private static KingdomCityState Book(KingdomZoneRow[] zones, KingdomWorkRow[] works, KingdomResidentRow[] residents, KingdomStocks stocks)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 900L, stocks, zones, works, residents, null, out state, out fault), fault.ToString());
			return state;
		}

		private static KingdomStocks Stocks(long water, long waterCap, long food, long foodCap, long mats, long matsCap)
		{
			return new KingdomStocks(new KingdomStockPair(water, waterCap),
				new KingdomStockPair(food, foodCap), new KingdomStockPair(mats, matsCap));
		}

		/// <summary>Every figure on the reading is the figure on the row. Checked field by field
		/// rather than by count, because a projection that drops a field is exactly the bug a
		/// count would pass.</summary>
		[Test]
		public void Project_CarriesEveryPublishedFigure()
		{
			KingdomZoneRow zone = new KingdomZoneRow(Here, 3, 800L, Stocks(10L, 20L, 4L, 8L, 1L, 2L), 5, 2, 0, 0, -6, 7, 0);
			KingdomWorkRow work = new KingdomWorkRow(11, Here, 4, 9, "mill", 62, 2, 700L,
				new KingdomWorkRunState(KingdomWorkKind.Producer, 3, 44, 1200L));
			KingdomResidentRow settler = new KingdomResidentRow(21, "Ptoh", 2, 0, 300L, 11, 12, 0,
				KingdomDayShape.Watch, KingdomResidentStanding.Abroad, KingdomStandingCause.None, Here,
				KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);

			KingdomCityReading reading = KingdomReadingRules.Project("Kavvat",
				Book(new KingdomZoneRow[1] { zone }, new KingdomWorkRow[1] { work }, new KingdomResidentRow[1] { settler },
					Stocks(100L, 240L, 12L, 60L, 3L, 40L)));

			Assert.AreEqual("Kavvat", reading.CityName);
			Assert.AreEqual("taf:city:kavvat", reading.SettlementId);
			Assert.AreEqual(900L, reading.ProcessedThroughTick);
			Assert.AreEqual(100L, reading.Water.Level);
			Assert.AreEqual(240L, reading.Water.Capacity);
			Assert.AreEqual(48L, reading.Food.Room);

			KingdomZoneReading z;
			Assert.IsTrue(reading.TryZone(0, out z));
			Assert.AreEqual(Here, z.ZoneId);
			Assert.AreEqual(10L, z.Water.Level);
			Assert.AreEqual(5, z.Roofs);
			Assert.AreEqual(2, z.Defence);
			Assert.AreEqual(-6, z.OwedWater);
			Assert.AreEqual(7, z.OwedFood);
			Assert.AreEqual(800L, z.LastReadTick);

			KingdomWorkReading w;
			Assert.IsTrue(reading.TryWork(0, out w));
			Assert.AreEqual(11, w.WorkId);
			Assert.AreEqual("mill", w.DesignKey);
			Assert.AreEqual(62, w.ConditionPercent);
			Assert.AreEqual(2, w.CrewAssigned);
			Assert.AreEqual(KingdomWorkClass.Producer, w.Class);
			Assert.AreEqual(3, w.Stage);
			Assert.AreEqual(44, w.Progress);
			Assert.AreEqual(1200L, w.NextTick);

			KingdomResidentReading r;
			Assert.IsTrue(reading.TryResident(0, out r));
			Assert.AreEqual(21, r.ResidentId);
			Assert.AreEqual("Ptoh", r.Name);
			Assert.AreEqual(Here, r.ZoneId);
			Assert.AreEqual(KingdomDayPlace.Watch, r.Day);
			Assert.AreEqual(KingdomRollStanding.Abroad, r.Standing);
			Assert.AreEqual(300L, r.ArrivedTick);
			Assert.AreEqual(11, r.HomeWorkId);
			Assert.AreEqual(12, r.JobWorkId);
		}

		/// <summary>A null book is an empty reading, never null: every consumer is a loop over
		/// counts, and an empty city is a legal city.</summary>
		[Test]
		public void Project_ANullBookIsAnEmptyReading()
		{
			KingdomCityReading reading = KingdomReadingRules.Project("Kavvat", null);
			Assert.IsNotNull(reading);
			Assert.AreEqual(0, reading.ZoneCount);
			Assert.AreEqual(0, reading.WorkCount);
			Assert.AreEqual(0, reading.ResidentCount);
			Assert.AreEqual(0, reading.LivingCount);
			Assert.AreEqual("", reading.SettlementId);
		}

		/// <summary>Out-of-range reads answer false and a default, never an exception: an
		/// extension is not obliged to bounds-check us.</summary>
		[Test]
		public void Reading_RefusesOutOfRangeWithoutThrowing()
		{
			KingdomCityReading reading = KingdomReadingRules.Project("Kavvat", null);
			KingdomZoneReading z;
			KingdomWorkReading w;
			KingdomResidentReading r;
			Assert.IsFalse(reading.TryZone(0, out z));
			Assert.IsFalse(reading.TryWork(-1, out w));
			Assert.IsFalse(reading.TryResident(9, out r));
		}

		/// <summary>Only living rows count as living. A row that is abroad or dead is on the book
		/// and is not somebody the city can put on a work.</summary>
		[Test]
		public void LivingCount_ExcludesAbroadAndDead()
		{
			KingdomResidentRow[] rows = new KingdomResidentRow[3]
			{
				Settler(1, KingdomResidentStanding.Resident),
				Settler(2, KingdomResidentStanding.Abroad),
				Settler(3, KingdomResidentStanding.Dead)
			};
			Assert.AreEqual(1, KingdomReadingRules.Project("Kavvat", Book(null, null, rows, default(KingdomStocks))).LivingCount);
		}

		/// <summary>The class mapping is a switch, so it is total and it round-trips. Every model
		/// kind has exactly one published class and back again.</summary>
		[TestCase(KingdomWorkKind.Other, KingdomWorkClass.Other)]
		[TestCase(KingdomWorkKind.Growing, KingdomWorkClass.Growing)]
		[TestCase(KingdomWorkKind.Store, KingdomWorkClass.Store)]
		[TestCase(KingdomWorkKind.Producer, KingdomWorkClass.Producer)]
		[TestCase(KingdomWorkKind.Refiner, KingdomWorkClass.Refiner)]
		[TestCase(KingdomWorkKind.Power, KingdomWorkClass.Power)]
		[TestCase(KingdomWorkKind.Construction, KingdomWorkClass.Construction)]
		public void Class_MapsBothWays(KingdomWorkKind kind, KingdomWorkClass expected)
		{
			Assert.AreEqual(expected, KingdomReadingRules.Class(kind));
			Assert.AreEqual(kind, KingdomReadingRules.Kind(expected));
		}

		/// <summary>Rows and posts share this exact pure priority table. Each engine-supported work
		/// class reaches a distinct row kind; mixed traits resolve deterministically.</summary>
		[TestCase(false, false, false, false, false, false, KingdomWorkKind.Other)]
		[TestCase(true, false, false, false, false, false, KingdomWorkKind.Growing)]
		[TestCase(false, true, false, false, false, false, KingdomWorkKind.Construction)]
		[TestCase(false, false, true, false, false, false, KingdomWorkKind.Store)]
		[TestCase(false, false, false, true, false, false, KingdomWorkKind.Power)]
		[TestCase(false, false, false, false, true, false, KingdomWorkKind.Refiner)]
		[TestCase(false, false, false, false, false, true, KingdomWorkKind.Producer)]
		[TestCase(true, true, true, true, true, true, KingdomWorkKind.Growing)]
		[TestCase(false, true, true, true, true, true, KingdomWorkKind.Construction)]
		[TestCase(false, false, false, true, true, true, KingdomWorkKind.Power)]
		[TestCase(false, false, false, false, true, true, KingdomWorkKind.Refiner)]
		public void Classifier_MapsEveryActualTraitThroughOnePriorityTable(bool growing,
			bool construction, bool store, bool power, bool refiner, bool producer,
			KingdomWorkKind expected)
		{
			Assert.AreEqual(expected, KingdomWorkRules.Classify(new KingdomWorkTraits(growing,
				construction, store, power, refiner, producer)));
		}

		/// <summary>A model kind the published vocabulary has never heard of reads as Other rather
		/// than as whatever integer it happens to share.</summary>
		[Test]
		public void Class_AnUnknownKindIsOther()
		{
			Assert.AreEqual(KingdomWorkClass.Other, KingdomReadingRules.Class((KingdomWorkKind)200));
			Assert.AreEqual(KingdomDayPlace.Hearth, KingdomReadingRules.Day((KingdomDayShape)200));
			Assert.AreEqual(KingdomRollStanding.Resident, KingdomReadingRules.Standing((KingdomResidentStanding)200));
		}

		/// <summary>Every day shape has a published place.</summary>
		[TestCase(KingdomDayShape.Hearth, KingdomDayPlace.Hearth)]
		[TestCase(KingdomDayShape.Field, KingdomDayPlace.Field)]
		[TestCase(KingdomDayShape.Yard, KingdomDayPlace.Yard)]
		[TestCase(KingdomDayShape.Market, KingdomDayPlace.Market)]
		[TestCase(KingdomDayShape.Craft, KingdomDayPlace.Craft)]
		[TestCase(KingdomDayShape.Watch, KingdomDayPlace.Watch)]
		[TestCase(KingdomDayShape.Shrine, KingdomDayPlace.Shrine)]
		public void Day_MapsEveryShape(KingdomDayShape shape, KingdomDayPlace expected)
		{
			Assert.AreEqual(expected, KingdomReadingRules.Day(shape));
		}

		private static KingdomResidentRow Settler(int id, KingdomResidentStanding standing)
		{
			return new KingdomResidentRow(id, "Ptoh-" + id, 2, 0, 100L, 0, 0, 0,
				KingdomDayShape.Hearth, standing, KingdomStandingCause.None, Here,
				KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);
		}
	}
}
#endif
