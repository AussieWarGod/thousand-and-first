using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One stock and the ceiling it fills toward, as the city book holds it.</summary>
	public readonly struct KingdomStockReading
	{
		/// <summary>What the civic share holds. Player-carried and undedicated stock is not in
		/// here and never will be (LIVING-CITY-ARCHITECTURE &sect;1.2(a)): the model speaks only
		/// for what the founder designated, which is what keeps the protection law simple.</summary>
		public readonly long Level;

		/// <summary>What the dedicated vessels could hold.</summary>
		public readonly long Capacity;

		/// <summary>Builds a stock reading.</summary>
		public KingdomStockReading(long Level, long Capacity)
		{
			this.Level = Level;
			this.Capacity = Capacity;
		}

		/// <summary>Room left, floored at zero.</summary>
		public long Room
		{
			get { return (Capacity > Level) ? (Capacity - Level) : 0L; }
		}
	}

	/// <summary>What a work is, for the one slot of run-state its row carries. Mirrors the model's
	/// own vocabulary; ordinals are stable API.</summary>
	public enum KingdomWorkClass : byte
	{
		/// <summary>Anything with no run-state of its own.</summary>
		Other = 0,

		/// <summary>A field or row that ripens.</summary>
		Growing = 1,

		/// <summary>A store.</summary>
		Store = 2,

		/// <summary>Something that makes a good.</summary>
		Producer = 3,

		/// <summary>Something that turns one good into a better one.</summary>
		Refiner = 4,

		/// <summary>Something that carries charge.</summary>
		Power = 5,

		/// <summary>A plot or scaffold actively being raised.</summary>
		Construction = 6
	}

	/// <summary>Where a person's day puts them. Derived, never authored per settler.</summary>
	public enum KingdomDayPlace : byte
	{
		/// <summary>At home.</summary>
		Hearth = 0,

		/// <summary>In the fields.</summary>
		Field = 1,

		/// <summary>In a yard.</summary>
		Yard = 2,

		/// <summary>At the market.</summary>
		Market = 3,

		/// <summary>At a craft.</summary>
		Craft = 4,

		/// <summary>On the watch.</summary>
		Watch = 5,

		/// <summary>At the shrine.</summary>
		Shrine = 6
	}

	/// <summary>What the roll says about one settler.</summary>
	public enum KingdomRollStanding : byte
	{
		/// <summary>Lives here.</summary>
		Resident = 0,

		/// <summary>On the roll, somewhere else, doing no work.</summary>
		Abroad = 1,

		/// <summary>Off the roll.</summary>
		Dead = 2,

		/// <summary>On a dated civic expedition, still bound to one body and doing no city work.</summary>
		Expedition = 3
	}

	/// <summary>One claimed zone as the model last read it.</summary>
	public readonly struct KingdomZoneReading
	{
		/// <summary>The engine's zone id.</summary>
		public readonly string ZoneId;

		/// <summary>Water held on this ground.</summary>
		public readonly KingdomStockReading Water;

		/// <summary>Food held on this ground.</summary>
		public readonly KingdomStockReading Food;

		/// <summary>Materials held on this ground.</summary>
		public readonly KingdomStockReading Materials;

		/// <summary>Roofs counted here.</summary>
		public readonly int Roofs;

		/// <summary>Crewed defence here.</summary>
		public readonly int Defence;

		/// <summary>Water the model owes this ground and has not been able to land on real
		/// vessels yet. Signed: negative is a draw still to take (Addendum 12(d)).</summary>
		public readonly int OwedWater;

		/// <summary>Food owed, on the same terms.</summary>
		public readonly int OwedFood;

		/// <summary>Materials owed, on the same terms.</summary>
		public readonly int OwedMaterials;

		/// <summary>When the ground was last actually looked at.</summary>
		public readonly long LastReadTick;

		/// <summary>Builds a zone reading.</summary>
		public KingdomZoneReading(string ZoneId, KingdomStockReading Water, KingdomStockReading Food,
			KingdomStockReading Materials, int Roofs, int Defence, int OwedWater, int OwedFood,
			int OwedMaterials, long LastReadTick)
		{
			this.ZoneId = ZoneId;
			this.Water = Water;
			this.Food = Food;
			this.Materials = Materials;
			this.Roofs = Roofs;
			this.Defence = Defence;
			this.OwedWater = OwedWater;
			this.OwedFood = OwedFood;
			this.OwedMaterials = OwedMaterials;
			this.LastReadTick = LastReadTick;
		}
	}

	/// <summary>One work, and what the model knows about how it is running.</summary>
	public readonly struct KingdomWorkReading
	{
		/// <summary>The model's own id for this work. Stable across a save.</summary>
		public readonly int WorkId;

		/// <summary>Which ground it stands on.</summary>
		public readonly string ZoneId;

		/// <summary>The catalogue key it was raised from.</summary>
		public readonly string DesignKey;

		/// <summary>Wear, as a percentage of sound.</summary>
		public readonly int ConditionPercent;

		/// <summary>Hands set on it.</summary>
		public readonly int CrewAssigned;

		/// <summary>What kind of run-state it carries.</summary>
		public readonly KingdomWorkClass Class;

		/// <summary>Growth stage for a growing ground; unread for every other class.</summary>
		public readonly int Stage;

		/// <summary>Progress for a producer or refiner; charge for a power work.</summary>
		public readonly int Progress;

		/// <summary>The next breakpoint, never a countdown.</summary>
		public readonly long NextTick;

		/// <summary>Builds a work reading.</summary>
		public KingdomWorkReading(int WorkId, string ZoneId, string DesignKey, int ConditionPercent,
			int CrewAssigned, KingdomWorkClass Class, int Stage, int Progress, long NextTick)
		{
			this.WorkId = WorkId;
			this.ZoneId = ZoneId;
			this.DesignKey = DesignKey;
			this.ConditionPercent = ConditionPercent;
			this.CrewAssigned = CrewAssigned;
			this.Class = Class;
			this.Stage = Stage;
			this.Progress = Progress;
			this.NextTick = NextTick;
		}
	}

	/// <summary>One settler, as the roll holds them.</summary>
	public readonly struct KingdomResidentReading
	{
		/// <summary>The model's own id. One identity, at most one body (&sect;3.8).</summary>
		public readonly int ResidentId;

		/// <summary>Their name.</summary>
		public readonly string Name;

		/// <summary>The ground their body was last bound in, or null.</summary>
		public readonly string ZoneId;

		/// <summary>Where their day puts them.</summary>
		public readonly KingdomDayPlace Day;

		/// <summary>Whether they live here.</summary>
		public readonly KingdomRollStanding Standing;

		/// <summary>When they arrived.</summary>
		public readonly long ArrivedTick;

		/// <summary>The work they sleep in, or zero.</summary>
		public readonly int HomeWorkId;

		/// <summary>The work they are set on, or zero.</summary>
		public readonly int JobWorkId;

		/// <summary>Builds a resident reading.</summary>
		public KingdomResidentReading(int ResidentId, string Name, string ZoneId, KingdomDayPlace Day,
			KingdomRollStanding Standing, long ArrivedTick, int HomeWorkId, int JobWorkId)
		{
			this.ResidentId = ResidentId;
			this.Name = Name;
			this.ZoneId = ZoneId;
			this.Day = Day;
			this.Standing = Standing;
			this.ArrivedTick = ArrivedTick;
			this.HomeWorkId = HomeWorkId;
			this.JobWorkId = JobWorkId;
		}
	}

	/// <summary>
	/// One city's book, frozen, as an extension reads it. LIVING-CITY-ARCHITECTURE &sect;6.6
	/// clause 2: <b>frozen snapshot in, frozen result out</b> &mdash; an extension cannot reach the
	/// ground, the clock, or another extension's rows, and there is nothing on this type it could
	/// write to if it tried.
	/// <para>
	/// This is a PROJECTION of the model, not the model. It carries what a reading surface and a
	/// generator need and no more: the machinery the model keeps for its own arithmetic &mdash;
	/// brink windows, binding registry, itinerary legs, told-log ordinals &mdash; is deliberately
	/// absent, because publishing it would freeze the first draft of five internal shapes as API.
	/// </para>
	/// </summary>
	public sealed class KingdomCityReading
	{
		private readonly KingdomZoneReading[] zones;

		private readonly KingdomWorkReading[] works;

		private readonly KingdomResidentReading[] residents;

		private readonly KingdomBehaviourReading behaviour;

		/// <summary>The city's display name, as the founder knows it.</summary>
		public readonly string CityName;

		/// <summary>The model's own id for this settlement. Domain-separates every draw an
		/// extension makes.</summary>
		public readonly string SettlementId;

		/// <summary>How far the model has been advanced, in <c>The.Game.TimeTicks</c>.</summary>
		public readonly long ProcessedThroughTick;

		/// <summary>The whole city's water.</summary>
		public readonly KingdomStockReading Water;

		/// <summary>The whole city's food.</summary>
		public readonly KingdomStockReading Food;

		/// <summary>The whole city's materials.</summary>
		public readonly KingdomStockReading Materials;

		/// <summary>
		/// Builds a reading. The three arrays are COPIED, so nothing the caller keeps can reach
		/// inside afterwards &mdash; the same contract every frozen row in the model keeps.
		/// </summary>
		public KingdomCityReading(
			string CityName,
			string SettlementId,
			long ProcessedThroughTick,
			KingdomStockReading Water,
			KingdomStockReading Food,
			KingdomStockReading Materials,
			KingdomZoneReading[] Zones,
			KingdomWorkReading[] Works,
			KingdomResidentReading[] Residents)
			: this(CityName, SettlementId, ProcessedThroughTick, Water, Food, Materials,
				Zones, Works, Residents, null)
		{
		}

		/// <summary>Builds a reading with the API-v3 durable behaviour projection. This overload is
		/// additive: the original constructor remains binary-compatible for v1/v2 sources.</summary>
		public KingdomCityReading(
			string CityName,
			string SettlementId,
			long ProcessedThroughTick,
			KingdomStockReading Water,
			KingdomStockReading Food,
			KingdomStockReading Materials,
			KingdomZoneReading[] Zones,
			KingdomWorkReading[] Works,
			KingdomResidentReading[] Residents,
			KingdomBehaviourReading Behaviour)
		{
			this.CityName = CityName ?? "";
			this.SettlementId = SettlementId ?? "";
			this.ProcessedThroughTick = ProcessedThroughTick;
			this.Water = Water;
			this.Food = Food;
			this.Materials = Materials;
			zones = Copy(Zones);
			works = Copy(Works);
			residents = Copy(Residents);
			behaviour = Behaviour ?? new KingdomBehaviourReading(null, null, null, null);
		}

		/// <summary>Frozen API-v3 resource/job/network/work sidecar. Never null.</summary>
		public KingdomBehaviourReading Behaviour
		{
			get { return behaviour; }
		}

		/// <summary>Claimed zones the model holds a row for.</summary>
		public int ZoneCount
		{
			get { return zones.Length; }
		}

		/// <summary>Works the model holds a row for.</summary>
		public int WorkCount
		{
			get { return works.Length; }
		}

		/// <summary>Settlers the model holds a row for, living and otherwise.</summary>
		public int ResidentCount
		{
			get { return residents.Length; }
		}

		/// <summary>One zone row by index. False out of range, and the caller gets a default
		/// rather than an exception: an extension is not obliged to bounds-check us.</summary>
		public bool TryZone(int Index, out KingdomZoneReading Zone)
		{
			if (Index < 0 || Index >= zones.Length)
			{
				Zone = default(KingdomZoneReading);
				return false;
			}
			Zone = zones[Index];
			return true;
		}

		/// <summary>One work row by index.</summary>
		public bool TryWork(int Index, out KingdomWorkReading Work)
		{
			if (Index < 0 || Index >= works.Length)
			{
				Work = default(KingdomWorkReading);
				return false;
			}
			Work = works[Index];
			return true;
		}

		/// <summary>One resident row by index.</summary>
		public bool TryResident(int Index, out KingdomResidentReading Resident)
		{
			if (Index < 0 || Index >= residents.Length)
			{
				Resident = default(KingdomResidentReading);
				return false;
			}
			Resident = residents[Index];
			return true;
		}

		/// <summary>Settlers whose standing is <see cref="KingdomRollStanding.Resident"/>.</summary>
		public int LivingCount
		{
			get
			{
				int count = 0;
				for (int i = 0; i < residents.Length; i++)
				{
					if (residents[i].Standing == KingdomRollStanding.Resident)
					{
						count++;
					}
				}
				return count;
			}
		}

		private static T[] Copy<T>(T[] source)
		{
			if (source == null || source.Length == 0)
			{
				return Array.Empty<T>();
			}
			T[] copy = new T[source.Length];
			Array.Copy(source, copy, source.Length);
			return copy;
		}
	}
}
