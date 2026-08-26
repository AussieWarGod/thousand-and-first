using System;

namespace ThousandAndFirst.Api
{
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
