using System;
using ThousandAndFirst.Api;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The one place the internal model becomes the published reading, and the only place.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;6.6 publishes contracts, not rows: every type in
	/// <c>ThousandAndFirst.Api</c> is a projection, and the model's own shapes stay internal so
	/// that widening a row, adding a brink field or changing a told ordinal is not a breaking API
	/// change. This class is the seam that makes that true, and it is engine-free and total like
	/// every <c>*Rules</c> class beside it.
	/// </para>
	/// <para>
	/// <b>What is deliberately not projected.</b> Brink windows, creed codes, standing causes,
	/// itinerary legs, told-log ordinals and the binding registry. Each is machinery the model
	/// keeps for its own arithmetic; each would be frozen as API the moment it appeared here; and
	/// none is needed to ask the city for something or to say what happened in it.
	/// </para>
	/// </summary>
	internal static class KingdomReadingRules
	{
		/// <summary>
		/// Projects a frozen model state into the published reading.
		/// <para>
		/// Preconditions: none. A null state yields an empty reading rather than null, because
		/// every consumer of this is a loop over counts and an empty city is a legal city.
		/// Side effects: none. Failure mode: none &mdash; total over any representable state.
		/// </para>
		/// </summary>
		/// <param name="CityName">The display name. Never read by the model itself.</param>
		/// <param name="State">The frozen book.</param>
		internal static KingdomCityReading Project(string CityName, KingdomCityState State)
		{
			return Project(CityName, State, null);
		}

		/// <summary>Projects the ordinary frozen city plus its API-v3 durable behaviour sidecar.
		/// Malformed sidecar input yields an empty behaviour reading; authority retains and reports
		/// that wire elsewhere rather than letting presentation repair it silently.</summary>
		internal static KingdomCityReading Project(string CityName, KingdomCityState State,
			string ExtensionModel)
		{
			KingdomBehaviourState behaviourState;
			KingdomBehaviourReading behaviour = KingdomBehaviourRules.TryDecode(ExtensionModel,
				out behaviourState) ? behaviourState.Reading() : KingdomBehaviourState.Empty.Reading();
			if (State == null)
			{
				return new KingdomCityReading(CityName, "", 0L,
					default(KingdomStockReading), default(KingdomStockReading), default(KingdomStockReading),
					null, null, null, behaviour);
			}
			KingdomZoneReading[] zones = new KingdomZoneReading[State.ZoneCount];
			for (int i = 0; i < State.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (!State.TryZone(i, out row))
				{
					continue;
				}
				zones[i] = new KingdomZoneReading(
					row.ZoneId,
					Stock(row.Stocks.Water),
					Stock(row.Stocks.Food),
					Stock(row.Stocks.Materials),
					row.Roofs,
					row.Defence,
					row.OwedWater,
					row.OwedFood,
					row.OwedMaterials,
					row.LastReadTick);
			}
			KingdomWorkReading[] works = new KingdomWorkReading[State.WorkCount];
			for (int i = 0; i < State.WorkCount; i++)
			{
				KingdomWorkRow row;
				if (!State.TryWork(i, out row))
				{
					continue;
				}
				works[i] = new KingdomWorkReading(
					row.WorkId,
					row.ZoneId,
					row.DesignKey,
					row.ConditionPercent,
					row.CrewAssigned,
					Class(row.RunState.Kind),
					row.RunState.Stage,
					row.RunState.Progress,
					row.RunState.NextTick);
			}
			KingdomResidentReading[] residents = new KingdomResidentReading[State.ResidentCount];
			for (int i = 0; i < State.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!State.TryResident(i, out row))
				{
					continue;
				}
				residents[i] = new KingdomResidentReading(
					row.ResidentId,
					row.Name,
					row.BoundZoneId,
					Day(row.DayShape),
					Standing(row.Standing),
					row.ArrivedTick,
					row.HomeWorkId,
					row.JobWorkId);
			}
			return new KingdomCityReading(
				CityName,
				State.SettlementId,
				State.ProcessedThroughTick,
				Stock(State.Stocks.Water),
				Stock(State.Stocks.Food),
				Stock(State.Stocks.Materials),
				zones,
				works,
				residents,
				behaviour);
		}

		/// <summary>One stock pair, projected.</summary>
		internal static KingdomStockReading Stock(KingdomStockPair pair)
		{
			return new KingdomStockReading(pair.Level, pair.Capacity);
		}

		/// <summary>
		/// The published work class for a model work kind.
		/// <para>
		/// A switch and not a cast, deliberately: the two enums are separate vocabularies that
		/// happen to agree today, and a cast would make every future model-side insertion a silent
		/// API break. An unrecognised kind reads as <see cref="KingdomWorkClass.Other"/>, which is
		/// what an extension that has never heard of it should see.
		/// </para>
		/// </summary>
		internal static KingdomWorkClass Class(KingdomWorkKind kind)
		{
			switch (kind)
			{
			case KingdomWorkKind.Growing:
				return KingdomWorkClass.Growing;
			case KingdomWorkKind.Store:
				return KingdomWorkClass.Store;
			case KingdomWorkKind.Producer:
				return KingdomWorkClass.Producer;
			case KingdomWorkKind.Refiner:
				return KingdomWorkClass.Refiner;
			case KingdomWorkKind.Power:
				return KingdomWorkClass.Power;
			case KingdomWorkKind.Construction:
				return KingdomWorkClass.Construction;
			default:
				return KingdomWorkClass.Other;
			}
		}

		/// <summary>
		/// The model work kind for a published class &mdash; <see cref="Class"/> read backwards, so
		/// a rule written against the published reading can ask the model's own predicates (
		/// <c>KingdomHappeningRules.NeedsHands</c>) instead of restating them. Restating one would
		/// be two definitions of when a work has stopped, and they would drift.
		/// </summary>
		internal static KingdomWorkKind Kind(KingdomWorkClass workClass)
		{
			switch (workClass)
			{
			case KingdomWorkClass.Growing:
				return KingdomWorkKind.Growing;
			case KingdomWorkClass.Store:
				return KingdomWorkKind.Store;
			case KingdomWorkClass.Producer:
				return KingdomWorkKind.Producer;
			case KingdomWorkClass.Refiner:
				return KingdomWorkKind.Refiner;
			case KingdomWorkClass.Power:
				return KingdomWorkKind.Power;
			case KingdomWorkClass.Construction:
				return KingdomWorkKind.Construction;
			default:
				return KingdomWorkKind.Other;
			}
		}

		/// <summary>The published day place for a model day shape. A switch, for the reason
		/// <see cref="Class"/> gives.</summary>
		internal static KingdomDayPlace Day(KingdomDayShape shape)
		{
			switch (shape)
			{
			case KingdomDayShape.Field:
				return KingdomDayPlace.Field;
			case KingdomDayShape.Yard:
				return KingdomDayPlace.Yard;
			case KingdomDayShape.Market:
				return KingdomDayPlace.Market;
			case KingdomDayShape.Craft:
				return KingdomDayPlace.Craft;
			case KingdomDayShape.Watch:
				return KingdomDayPlace.Watch;
			case KingdomDayShape.Shrine:
				return KingdomDayPlace.Shrine;
			default:
				return KingdomDayPlace.Hearth;
			}
		}

		/// <summary>The published standing for a model standing.</summary>
		internal static KingdomRollStanding Standing(KingdomResidentStanding standing)
		{
			switch (standing)
			{
			case KingdomResidentStanding.Expedition:
				return KingdomRollStanding.Expedition;
			case KingdomResidentStanding.Abroad:
				return KingdomRollStanding.Abroad;
			case KingdomResidentStanding.Dead:
				return KingdomRollStanding.Dead;
			default:
				return KingdomRollStanding.Resident;
			}
		}
	}
}
