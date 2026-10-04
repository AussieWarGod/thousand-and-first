using System;
using System.Collections.Generic;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.DevTests
{
	/// <summary>Production-shaped offers and the drift-aware TryOpen adapter shared by the
	/// #244/#257 window drift pins. Every endpoint is built by production
	/// KingdomPolityEndpointFactRules (the engine-free half of
	/// KingdomPolityEndpointFactRuntime.TryOffer) from a settlement's carrier values and a city book
	/// published through KingdomCityBook.TryPublish, so guard and patrol causes carry the zone read
	/// tick and the work's ran-through tick, and courier, trader and migrant causes carry the owned
	/// topology and their source settlement's facts exactly as in the game. Disclosed synthetic
	/// setup: one zone and one work row per settlement, written without an engine survey.</summary>
	public sealed partial class KingdomPolityDispatchWindowDriftTests
	{
		/// <summary>The persisted inputs TryOffer reads for one owned settlement.</summary>
		private sealed class Site
		{
			internal string Id;
			internal bool Seat;
			internal string Name = "Reedwake";
			internal string Zone = "JoppaWorld.8.22.1.1.10";
			internal int Population = 4, Stage = 4, ShopTier = 8, Storage = 10;
			internal string Deed = "the hut and yard raised at Reedwake";
			internal long DeedTick = 100L;
			internal long ReadTick = 200L;
			internal int Defence = 3;
			internal int Condition = 100;
			internal long RanThroughTick = 200L;

			internal Site Copy()
			{
				return (Site)MemberwiseClone();
			}
		}

		private static Site CityA()
		{
			return new Site { Id = KingdomPolityTestData.Settlement, Seat = true };
		}

		private static Site CityB()
		{
			return new Site { Id = B, Name = "Saltgate", Zone = "JoppaWorld.11.22.1.1.10",
				Deed = "the cistern raised at Saltgate" };
		}

		private static Site CityC()
		{
			return new Site { Id = C, Name = "Duneharrow", Zone = "JoppaWorld.14.22.1.1.10",
				Deed = "the market raised at Duneharrow" };
		}

		/// <summary>The offer production TryOffer would build for these settlements at Tick.</summary>
		private static KingdomPolityDispatchOffer Realm(long Tick, params Site[] Sites)
		{
			List<KingdomPolityEndpointFacts> endpoints = new List<KingdomPolityEndpointFacts>();
			for (int i = 0; i < Sites.Length; i++)
			{
				Site s = Sites[i];
				ClassicAssert.IsTrue(KingdomPolityEndpointFactRules.TryBuild(
					KingdomPolityTestData.Realm, s.Id, s.Name, s.Zone, s.Seat, s.Population, s.Stage,
					s.ShopTier, s.Storage, KingdomRules.GatePolicy.Open, s.Deed, s.DeedTick, Book(s),
					out KingdomPolityEndpointFacts facts, out string failure), failure);
				endpoints.Add(facts);
			}
			KingdomPolityEndpointFactRules.LinkSources(KingdomPolityTestData.Realm, endpoints);
			return new KingdomPolityDispatchOffer { RealmId = KingdomPolityTestData.Realm,
				Tick = Tick, Endpoints = endpoints };
		}

		/// <summary>One zone row and one work row, published the way a check-in publishes them.</summary>
		private static KingdomCityBook Book(Site S)
		{
			KingdomStocks none = new KingdomStocks(new KingdomStockPair(0L, 0L),
				new KingdomStockPair(0L, 0L), new KingdomStockPair(0L, 0L));
			KingdomZoneRow[] zones =
				{ new KingdomZoneRow(S.Zone, 0, S.ReadTick, none, 0, S.Defence, 0, 0, 0, 0, 0) };
			KingdomWorkRow[] works = { new KingdomWorkRow(1, S.Zone, 0, 0, "r_KingdomRiteGround",
				S.Condition, 0, S.RanThroughTick, new KingdomWorkRunState(KingdomWorkKind.Other, 0, 0,
				0L)) };
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion,
				KingdomCityRules.RulesVersion, S.Id, Math.Max(S.ReadTick, S.RanThroughTick), none,
				zones, works, new KingdomResidentRow[0], new KingdomClockRow[0],
				out KingdomCityState state, out KingdomCityFault fault), fault.ToString());
			KingdomCityBook book = new KingdomCityBook();
			ClassicAssert.IsTrue(book.TryPublish(state, out fault), fault.ToString());
			book.Normalize();
			return book;
		}

		/// <summary>A founded population-0 camp whose rite ground is its only work row
		/// (#257): no defence, so no guard cause; its patrol observation carries the day's zone
		/// read tick and the work's ran-through tick, so every daily check-in changes it.</summary>
		private static KingdomPolityDispatchOffer Camp(long Tick)
		{
			Site camp = new Site { Id = KingdomPolityTestData.Settlement, Seat = true,
				Name = "Kavvat", Population = 0, Stage = 0, ShopTier = 0, Storage = 16,
				Deed = "the rite ground raised at Kavvat", DeedTick = 96001L, ReadTick = Tick,
				Defence = 0, RanThroughTick = Tick };
			return Realm(Tick, camp);
		}

		private static bool Open(KingdomPolityDispatchState state, KingdomPolityDispatchOffer offer,
			out List<KingdomPolityDueWork> work, out bool drifted, out List<string> withdrawn,
			out string failure)
		{
			return Open(state, offer, true, out work, out drifted, out withdrawn, out failure);
		}

		private static bool Open(KingdomPolityDispatchState state, KingdomPolityDispatchOffer offer,
			bool admit, out List<KingdomPolityDueWork> work, out bool drifted,
			out List<string> withdrawn, out string failure)
		{
			return KingdomPolityDispatchRules.TryOpen(state, state.Revision, offer, admit,
				out work, out drifted, out withdrawn, out failure);
		}

		private static KingdomPolityDispatchState OpenAndComplete(KingdomPolityDispatchOffer offer)
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out List<KingdomPolityDueWork> work, out string failure), failure);
			for (int i = 0; i < work.Count; i++)
				ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryComplete(state,
					work[i].WindowOrdinal, work[i].EndpointOrdinal, out failure), failure);
			ClassicAssert.AreEqual((1 << offer.Endpoints.Count) - 1, state.CompletedMask);
			return state;
		}

		/// <summary>Opens a window whose intents all stay open (a crash cut before freezing) and
		/// checks that each slot holds the purpose the pin relies on.</summary>
		private static KingdomPolityDispatchState OpenIntents(KingdomPolityDispatchOffer offer,
			out List<KingdomPolityDueWork> work, params KingdomPolityCohortPurpose[] purposes)
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer, out work,
				out string failure), failure);
			ClassicAssert.AreEqual(purposes.Length, work.Count);
			for (int i = 0; i < purposes.Length; i++)
				ClassicAssert.AreEqual(purposes[i], work[i].Purpose, "slot " + i);
			return state;
		}

		/// <summary>One ordinary production input change on the first site, or a seat exchange
		/// between the first two.</summary>
		private static void Apply(Site[] sites, Drift change, long tick)
		{
			Site e = sites[0];
			switch (change)
			{
			case Drift.Storage: e.Storage += 2; break;
			case Drift.Population: e.Population += 1; break;
			case Drift.Stage: e.Stage -= 1; break;
			case Drift.Shop: e.ShopTier -= 1; break;
			case Drift.Deed: e.Deed = "the communal fire raised at Reedwake"; e.DeedTick = tick; break;
			case Drift.ZoneRead: e.ReadTick = tick; break;
			case Drift.WorkRun: e.RanThroughTick = tick; break;
			case Drift.Defence: e.Defence += 2; break;
			case Drift.Wear: e.Condition = 97; break;
			case Drift.Name: e.Name = "Reedwake Renamed"; break;
			case Drift.SeatExchange: sites[0].Seat = false; sites[1].Seat = true; break;
			default: throw new ArgumentOutOfRangeException(nameof(change));
			}
		}

		private static Site[] Copies(params Site[] sites)
		{
			Site[] result = new Site[sites.Length];
			for (int i = 0; i < sites.Length; i++) result[i] = sites[i].Copy();
			return result;
		}
	}
}
