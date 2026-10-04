using System;
using System.Collections.Generic;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	/// <summary>Offer builders and the drift-aware TryOpen adapter shared by the #244/#257
	/// window drift pins. Every endpoint is built field by field; nothing here re-implements
	/// production dispatch rules.</summary>
	public sealed partial class KingdomPolityDispatchWindowDriftTests
	{
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

		/// <summary>Production shape of a single founded pop-0 camp with a heart work row
		/// (EndpointFactRuntime.cs:47-76; EndpointObservationRules.cs:71-82): no eligible
		/// purpose, the read tick lives in the patrol observation.</summary>
		private static KingdomPolityDispatchOffer Camp(long tick)
		{
			KingdomPolityDispatchOffer o = Offer(1, tick);
			KingdomPolityEndpointFacts e = o.Endpoints[0];
			e.Population = 0; e.Stage = 0; e.ShopTier = 0; e.KnownStorageSpace = 16;
			e.GuardCauseRef = null; e.CourierCauseRef = null; e.TraderCauseRef = null;
			e.MigrantCauseRef = null;
			e.PatrolCauseRef = "taf:fact:route-condition:read-" + tick;
			return o;
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

		private static void Apply(KingdomPolityDispatchOffer offer, Drift change)
		{
			KingdomPolityEndpointFacts e = offer.Endpoints[0];
			switch (change)
			{
			case Drift.Storage: e.KnownStorageSpace += 2; e.CapacityFactRef = "taf:fact:capacity:12"; break;
			case Drift.Population: e.Population += 1; e.PopulationFactRef = "taf:fact:population:5"; break;
			case Drift.Stage: e.Stage -= 1; break;
			case Drift.Shop: e.ShopTier -= 1; e.MarketFactRef = "taf:fact:market-tier:7"; break;
			case Drift.Deed: e.DeedFactRef = "taf:fact:deed:fire"; e.DeedSummary = "the communal fire raised"; break;
			case Drift.GuardReading: e.GuardCauseRef = "taf:fact:witnessed:read-97200"; break;
			case Drift.PatrolReading: e.PatrolCauseRef = "taf:fact:route-condition:read-97200"; break;
			case Drift.Wear: e.PatrolConditionDetail = "condition 97"; break;
			case Drift.Name: e.SettlementName = "Reedwake Renamed"; break;
			case Drift.SeatExchange:
				offer.Endpoints[0].IsSeat = false; offer.Endpoints[1].IsSeat = true; break;
			default: throw new ArgumentOutOfRangeException(nameof(change));
			}
		}

		private static KingdomPolityDispatchOffer Pair(string first, string second, long tick)
		{
			return new KingdomPolityDispatchOffer { RealmId = KingdomPolityTestData.Realm,
				Tick = tick, Endpoints = new List<KingdomPolityEndpointFacts>
				{ Endpoint(first, true), Endpoint(second, false) } };
		}

		private static KingdomPolityDispatchOffer Offer(int count, long tick)
		{
			List<KingdomPolityEndpointFacts> rows = new List<KingdomPolityEndpointFacts>
				{ Endpoint(KingdomPolityTestData.Settlement, true) };
			if (count > 1) rows.Add(Endpoint(B, false)); if (count > 2) rows.Add(Endpoint(C, false));
			return new KingdomPolityDispatchOffer { RealmId = KingdomPolityTestData.Realm,
				Tick = tick, Endpoints = rows };
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
