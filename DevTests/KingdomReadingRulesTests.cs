#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
		[Test]
		public void PublishedReadingEnumsKeepByteAbiAndExactValues()
		{
			ClassicAssert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomWorkClass)));
			CollectionAssert.AreEqual(new[] { "Other", "Growing", "Store", "Producer",
				"Refiner", "Power", "Construction" }, Enum.GetNames(typeof(KingdomWorkClass)));
			CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, Array.ConvertAll(
				(KingdomWorkClass[])Enum.GetValues(typeof(KingdomWorkClass)), value => (byte)value));

			ClassicAssert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomDayPlace)));
			CollectionAssert.AreEqual(new[] { "Hearth", "Field", "Yard", "Market", "Craft",
				"Watch", "Shrine" }, Enum.GetNames(typeof(KingdomDayPlace)));
			CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, Array.ConvertAll(
				(KingdomDayPlace[])Enum.GetValues(typeof(KingdomDayPlace)), value => (byte)value));

			ClassicAssert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomRollStanding)));
			CollectionAssert.AreEqual(new[] { "Resident", "Abroad", "Dead", "Expedition" },
				Enum.GetNames(typeof(KingdomRollStanding)));
			CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, Array.ConvertAll(
				(KingdomRollStanding[])Enum.GetValues(typeof(KingdomRollStanding)),
				value => (byte)value));
		}

		private const string Here = "taf:zone:here";

		private static KingdomCityState Book(KingdomZoneRow[] zones, KingdomWorkRow[] works, KingdomResidentRow[] residents, KingdomStocks stocks)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
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

			ClassicAssert.AreEqual("Kavvat", reading.CityName);
			ClassicAssert.AreEqual("taf:city:kavvat", reading.SettlementId);
			ClassicAssert.AreEqual(900L, reading.ProcessedThroughTick);
			ClassicAssert.AreEqual(100L, reading.Water.Level);
			ClassicAssert.AreEqual(240L, reading.Water.Capacity);
			ClassicAssert.AreEqual(48L, reading.Food.Room);

			KingdomZoneReading z;
			ClassicAssert.IsTrue(reading.TryZone(0, out z));
			ClassicAssert.AreEqual(Here, z.ZoneId);
			ClassicAssert.AreEqual(10L, z.Water.Level);
			ClassicAssert.AreEqual(5, z.Roofs);
			ClassicAssert.AreEqual(2, z.Defence);
			ClassicAssert.AreEqual(-6, z.OwedWater);
			ClassicAssert.AreEqual(7, z.OwedFood);
			ClassicAssert.AreEqual(800L, z.LastReadTick);

			KingdomWorkReading w;
			ClassicAssert.IsTrue(reading.TryWork(0, out w));
			ClassicAssert.AreEqual(11, w.WorkId);
			ClassicAssert.AreEqual("mill", w.DesignKey);
			ClassicAssert.AreEqual(62, w.ConditionPercent);
			ClassicAssert.AreEqual(2, w.CrewAssigned);
			ClassicAssert.AreEqual(KingdomWorkClass.Producer, w.Class);
			ClassicAssert.AreEqual(3, w.Stage);
			ClassicAssert.AreEqual(44, w.Progress);
			ClassicAssert.AreEqual(1200L, w.NextTick);

			KingdomResidentReading r;
			ClassicAssert.IsTrue(reading.TryResident(0, out r));
			ClassicAssert.AreEqual(21, r.ResidentId);
			ClassicAssert.AreEqual("Ptoh", r.Name);
			ClassicAssert.AreEqual(Here, r.ZoneId);
			ClassicAssert.AreEqual(KingdomDayPlace.Watch, r.Day);
			ClassicAssert.AreEqual(KingdomRollStanding.Abroad, r.Standing);
			ClassicAssert.AreEqual(300L, r.ArrivedTick);
			ClassicAssert.AreEqual(11, r.HomeWorkId);
			ClassicAssert.AreEqual(12, r.JobWorkId);
		}

		/// <summary>A null book is an empty reading, never null: every consumer is a loop over
		/// counts, and an empty city is a legal city.</summary>
		[Test]
		public void Project_ANullBookIsAnEmptyReading()
		{
			KingdomCityReading reading = KingdomReadingRules.Project("Kavvat", null);
			ClassicAssert.IsNotNull(reading);
			ClassicAssert.AreEqual(0, reading.ZoneCount);
			ClassicAssert.AreEqual(0, reading.WorkCount);
			ClassicAssert.AreEqual(0, reading.ResidentCount);
			ClassicAssert.AreEqual(0, reading.LivingCount);
			ClassicAssert.AreEqual("", reading.SettlementId);
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
			ClassicAssert.IsFalse(reading.TryZone(0, out z));
			ClassicAssert.IsFalse(reading.TryWork(-1, out w));
			ClassicAssert.IsFalse(reading.TryResident(9, out r));
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
			ClassicAssert.AreEqual(1, KingdomReadingRules.Project("Kavvat", Book(null, null, rows, default(KingdomStocks))).LivingCount);
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
			ClassicAssert.AreEqual(expected, KingdomReadingRules.Class(kind));
			ClassicAssert.AreEqual(kind, KingdomReadingRules.Kind(expected));
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
			ClassicAssert.AreEqual(expected, KingdomWorkRules.Classify(new KingdomWorkTraits(growing,
				construction, store, power, refiner, producer)));
		}

		/// <summary>A model kind the published vocabulary has never heard of reads as Other rather
		/// than as whatever integer it happens to share.</summary>
		[Test]
		public void Class_AnUnknownKindIsOther()
		{
			ClassicAssert.AreEqual(KingdomWorkClass.Other, KingdomReadingRules.Class((KingdomWorkKind)200));
			ClassicAssert.AreEqual(KingdomDayPlace.Hearth, KingdomReadingRules.Day((KingdomDayShape)200));
			ClassicAssert.AreEqual(KingdomRollStanding.Resident, KingdomReadingRules.Standing((KingdomResidentStanding)200));
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
			ClassicAssert.AreEqual(expected, KingdomReadingRules.Day(shape));
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
