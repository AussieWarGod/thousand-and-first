using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.DevTests
{
	/// <summary>#244/#257 positive native witness: the polity-dispatch reading the harness journals
	/// after each daily pass, executed over receipts produced by production TryOpen/TryComplete.
	/// The persona host grammar (Tools/personas/persona_polity.py) judges these exact strings.</summary>
	[TestFixture]
	public sealed class KingdomPolityWindowWitnessTests
	{
		private const string B =
			"taf:settlement:v1:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
		private const string C =
			"taf:settlement:v1:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
		private const long Period = KingdomPolityDispatchRules.PeriodTicks;
		private const long Day = KingdomPolityDispatchRules.CalendarDayTicks;

		[Test]
		public void ReadsThePopulationZeroCampWindowExactly()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			Open(state, Camp(Period * 11L + 1L, 11L * Period + 1L), 0);
			ClassicAssert.AreEqual("window=11 revision=1 count=1 mask=1 intents=0", Read(state));
		}

		[Test]
		public void CountsOpenIntentsAndCompletedSlots()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			Open(state, Cities(Period * 60L), 3);
			ClassicAssert.AreEqual("window=60 revision=1 count=3 mask=0 intents=3", Read(state));
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryComplete(state, 60UL, 0,
				out string failure), failure);
			ClassicAssert.AreEqual("window=60 revision=2 count=3 mask=1 intents=2", Read(state));
		}

		[Test]
		public void LaterInWindowDailyPassesKeepTheReadingAndTheBoundaryAdvancesIt()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			Open(state, Camp(Period * 11L + Day, Period * 11L + Day), 0);
			string opened = Read(state);
			for (long day = 2L; day < 7L; day++)
			{
				long tick = Period * 11L + day * Day;
				Open(state, Camp(tick, tick), 0);
				ClassicAssert.AreEqual(opened, Read(state), "day " + day + " drifted the receipt");
			}
			Open(state, Camp(Period * 12L, Period * 12L), 0);
			ClassicAssert.AreEqual("window=12 revision=2 count=1 mask=1 intents=0", Read(state));
		}

		[Test]
		public void RefusesBeforeAnyWindowOpens()
		{
			ClassicAssert.IsNull(KingdomPolityWindowReading.Describe(new KingdomPolityDispatchState(),
				out string failure));
			ClassicAssert.AreEqual("the polity dispatch receipt has not opened a window", failure);
		}

		[Test]
		public void RefusesAMissingOrForgedReceipt()
		{
			ClassicAssert.IsNull(KingdomPolityWindowReading.Describe(null, out string failure));
			StringAssert.StartsWith("the polity dispatch receipt is invalid (", failure);
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			Open(state, Camp(Period * 11L, Period * 11L), 0);
			state.EndpointCount = KingdomPolityDispatchRules.MaximumEndpoints + 1;
			ClassicAssert.IsNull(KingdomPolityWindowReading.Describe(state, out failure));
			StringAssert.StartsWith("the polity dispatch receipt is invalid (", failure);
		}

		[Test]
		public void ObservationRowNameMatchesTheHostAllowlist()
		{
			ClassicAssert.AreEqual("polity-dispatch", KingdomPolityWindowReading.Row);
		}

		private static string Read(KingdomPolityDispatchState state)
		{
			string reading = KingdomPolityWindowReading.Describe(state, out string failure);
			ClassicAssert.IsNotNull(reading, failure);
			ClassicAssert.IsNull(failure);
			return reading;
		}

		private static void Open(KingdomPolityDispatchState state, KingdomPolityDispatchOffer offer,
			int expectedWork)
		{
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out List<KingdomPolityDueWork> work, out string failure), failure);
			ClassicAssert.AreEqual(expectedWork, work.Count);
		}

		/// <summary>A single founded pop-0 camp whose patrol observation carries the read tick of
		/// its rite ground, as production publishes it (EndpointFactRuntime.cs:112-130), so every
		/// daily pass changes its facts.</summary>
		private static KingdomPolityDispatchOffer Camp(long tick, long readTick)
		{
			KingdomPolityEndpointFacts camp = Endpoint(KingdomPolityTestData.Settlement, true);
			camp.Population = 0; camp.Stage = 0; camp.ShopTier = 0; camp.KnownStorageSpace = 16;
			camp.GuardCauseRef = null; camp.CourierCauseRef = null; camp.TraderCauseRef = null;
			camp.MigrantCauseRef = null;
			camp.PatrolCauseRef = "taf:fact:route-condition:read-" + readTick;
			return new KingdomPolityDispatchOffer { RealmId = KingdomPolityTestData.Realm,
				Tick = tick, Endpoints = new List<KingdomPolityEndpointFacts> { camp } };
		}

		private static KingdomPolityDispatchOffer Cities(long tick)
		{
			return new KingdomPolityDispatchOffer { RealmId = KingdomPolityTestData.Realm,
				Tick = tick, Endpoints = new List<KingdomPolityEndpointFacts>
				{ Endpoint(KingdomPolityTestData.Settlement, true), Endpoint(B, false),
					Endpoint(C, false) } };
		}

		private static KingdomPolityEndpointFacts Endpoint(string id, bool seat)
		{
			return new KingdomPolityEndpointFacts { SettlementId = id, IsSeat = seat,
				Population = 4, Stage = 4, ShopTier = 8, KnownStorageSpace = 10,
				SettlementName = "Reedwake", ZoneId = "JoppaWorld.8.22.1.1.10",
				GuardCauseRef = "taf:fact:watch:" + id, PatrolCauseRef = "taf:fact:patrol:" + id,
				CourierCauseRef = "taf:fact:courier:" + id, TraderCauseRef = "taf:fact:market:" + id,
				MigrantCauseRef = "taf:fact:room:" + id };
		}
	}
}
