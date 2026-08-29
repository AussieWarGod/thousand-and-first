using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		/// <summary>
		/// The settler's identity, and the only thing about a person the body itself carries.
		/// Minted once, never re-minted, and never reused: the realm's counter only goes up.
		/// </summary>
		public const string ResidentIdProperty = "KingdomResidentId";

		/// <summary>
		/// The exact job a transient body renders. Production porters and other carriers stamp it
		/// only after durable job publication; the stale-transient sweep reads it before rendering
		/// so a closed model job and a leftover body cannot expose the same cargo twice.
		/// </summary>
		public const string JobIdProperty = "KingdomJobId";

		// ==================================================================================
		// The id
		// ==================================================================================

		/// <summary>This body's resident id, or zero for a body that has never been enrolled.</summary>
		public static int IdOf(GameObject Body)
		{
			return GameObject.Validate(Body) ? Body.GetIntProperty(ResidentIdProperty) : 0;
		}

		/// <summary>
		/// This body's resident id, minting one if it has none. Zero when there is no realm to mint
		/// against — an id from a counter nobody is keeping is not an id.
		/// </summary>
		public static int EnsureId(KingdomSystem System, GameObject Body)
		{
			int existing = IdOf(Body);
			if (existing != 0 || System == null || !GameObject.Validate(Body))
			{
				return existing;
			}
			System.ResidentCounter++;
			Body.SetIntProperty(ResidentIdProperty, System.ResidentCounter);
			return System.ResidentCounter;
		}

		// ==================================================================================
		// The resident-row authority
		// ==================================================================================

		/// <summary>Reads the seated city's bounded living roll. This row service is the production
		/// bridge from a realm or exact city book; historical parallel lists are projections only.</summary>
		internal static bool TryRoll(KingdomSystem System, out KingdomCityState State,
			out KingdomResidentRollProjection Roll)
		{
			return TryRoll(System?.City, out State, out Roll);
		}

		/// <summary>Reads any exact city book through the same bounded row authority as the seat.</summary>
		internal static bool TryRoll(KingdomCityBook Book, out KingdomCityState State,
			out KingdomResidentRollProjection Roll)
		{
			Roll = null;
			return TryState(Book, out State)
				&& KingdomResidentRules.TryProject(State, out Roll);
		}

		private static bool TryState(KingdomCityBook Book, out KingdomCityState State)
		{
			State = null;
			KingdomCityFault fault;
			return Book != null && Book.TryRead(out State, out fault);
		}

		internal static int OnRollCount(KingdomSystem System)
		{
			KingdomCityState state;
			KingdomResidentRollProjection roll;
			return TryRoll(System, out state, out roll) ? roll.Population : 0;
		}

		internal static List<KingdomResidentRow> RollRows(KingdomSystem System,
			bool LabourOnly = false)
		{
			return RollRows(System?.City, LabourOnly);
		}

		internal static List<KingdomResidentRow> RollRows(KingdomCityBook Book,
			bool LabourOnly = false)
		{
			List<KingdomResidentRow> rows = new List<KingdomResidentRow>();
			KingdomCityState state;
			KingdomResidentRollProjection ignored;
			if (!TryRoll(Book, out state, out ignored)) return rows;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row)) break;
				if (LabourOnly ? KingdomResidentRules.Labours(row)
					: KingdomResidentRules.OnTheRoll(row)) rows.Add(row);
			}
			return rows;
		}

		internal static bool TryResident(KingdomCityBook Book, int ResidentId,
			out KingdomResidentRow Row)
		{
			Row = default(KingdomResidentRow);
			KingdomCityState state;
			int index;
			return ResidentId > 0 && TryState(Book, out state)
				&& state.TryResidentIndex(ResidentId, out index)
				&& state.TryResident(index, out Row);
		}

		internal static bool TryFindByName(KingdomSystem System, string Name,
			out KingdomResidentRow Row)
		{
			Row = default(KingdomResidentRow);
			if (string.IsNullOrEmpty(Name)) return false;
			List<KingdomResidentRow> rows = RollRows(System);
			for (int i = 0; i < rows.Count; i++)
			{
				if (string.Equals(rows[i].Name, Name, StringComparison.Ordinal))
				{
					Row = rows[i];
					return true;
				}
			}
			return false;
		}

		internal static bool TryHead(KingdomSystem System, out KingdomResidentRow Row)
		{
			Row = default(KingdomResidentRow);
			List<KingdomResidentRow> rows = RollRows(System);
			if (rows.Count == 0) return false;
			KingdomResidentRow head = rows[0];
			for (int i = 1; i < rows.Count; i++)
			{
				KingdomResidentRow candidate = rows[i];
				// Unknown legacy arrival (zero) remains senior to dated rows. ResidentId breaks ties
				// without depending on the order a zone happened to be surveyed.
				if (candidate.ArrivedTick < head.ArrivedTick
					|| candidate.ArrivedTick == head.ArrivedTick
					&& candidate.ResidentId < head.ResidentId) head = candidate;
			}
			Row = head;
			return true;
		}

		internal static string HeadName(KingdomSystem System)
		{
			KingdomResidentRow head;
			return TryHead(System, out head) ? head.Name : null;
		}

		/// <summary>One-way compatibility projection after a row publish. Population is a cache of
		/// the on-roll count; the three public lists remain for save ABI/reflection only.</summary>
		// The parallel roster fields are frozen save ABI, not live authority. This adapter is
		// the sole deliberate internal user; keep its obsolete-warning scope narrow and visible.
#pragma warning disable 618
		internal static bool ProjectCompatibility(KingdomSystem System)
		{
			if (System == null) return false;
			bool unresolvedSeat = System.City != null && System.City.ResidentCount == 0
				&& (System.RosterNames?.Count > 0 || System.RosterOrigins?.Count > 0
					|| System.RosterArrived?.Count > 0);
			KingdomResidentRollProjection seatRoll = null;
			bool seat = !unresolvedSeat && ProjectCompatibility(System.City, out seatRoll);
			if (seat)
			{
				System.RosterNames = seatRoll.Names;
				System.RosterOrigins = seatRoll.Origins;
				System.RosterArrived = seatRoll.Arrived;
				System.Population = seatRoll.Population;
				System.WaterCrew = Math.Min(System.WaterCrew, seatRoll.Labour);
				System.AssignedCrew = Math.Min(System.AssignedCrew, seatRoll.Labour);
				System.OriginCounts = Counts(seatRoll.Origins);
			}
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
			{
				KingdomSettlement row = nonSeat[i];
				bool unresolved = row.City != null && row.City.ResidentCount == 0
					&& (row.RosterNames?.Count > 0 || row.RosterOrigins?.Count > 0
						|| row.RosterArrived?.Count > 0);
				if (!unresolved) ProjectCompatibility(row);
			}
			return seat;
		}

		internal static bool ProjectCompatibility(KingdomSettlement Settlement)
		{
			if (Settlement == null || !ProjectCompatibility(Settlement.City,
				out KingdomResidentRollProjection roll)) return false;
			Settlement.RosterNames = roll.Names;
			Settlement.RosterOrigins = roll.Origins;
			Settlement.RosterArrived = roll.Arrived;
			Settlement.Population = roll.Population;
			Settlement.WaterCrew = Math.Min(Settlement.WaterCrew, roll.Labour);
			Settlement.AssignedCrew = Math.Min(Settlement.AssignedCrew, roll.Labour);
			Settlement.OriginCounts = Counts(roll.Origins);
			return true;
		}

		internal static bool ProjectCompatibility(KingdomCityBook Book,
			out KingdomResidentRollProjection Roll)
		{
			Roll = null;
			KingdomCityState state;
			KingdomCityFault fault;
			return Book != null && Book.TryRead(out state, out fault)
				&& KingdomResidentRules.TryProject(state, out Roll);
		}

		/// <summary>Load boundary. A complete old parallel roll seeds an empty book exactly once as
		/// Abroad claims; a real body later adopts the claim's id. Existing rows always win and are
		/// projected outward. Ragged evidence is retained and logged.</summary>
		internal static void AdoptLegacyAuthority(KingdomSystem System)
		{
			if (System == null) return;
			int counter = Math.Max(0, System.ResidentCounter);
			counter = Math.Max(counter, MaxResidentId(System.City));
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				counter = Math.Max(counter, MaxResidentId(nonSeat[i]?.City));
			System.ResidentCounter = counter;
			Adopt(System.City, System.RosterNames, System.RosterOrigins, System.RosterArrived,
				ref System.ResidentCounter, "seat");
			for (int i = 0; i < nonSeat.Count; i++)
			{
				KingdomSettlement row = nonSeat[i];
				Adopt(row.City, row.RosterNames, row.RosterOrigins,
					row.RosterArrived, ref System.ResidentCounter, "non-seat");
			}
			ProjectCompatibility(System);
		}
#pragma warning restore 618

		private static void Adopt(KingdomCityBook Book, List<string> Names,
			List<string> Origins, List<string> Arrived, ref int Counter, string Label)
		{
			KingdomCityState state;
			KingdomCityState next;
			KingdomCityFault fault;
			int nextCounter;
			if (Book == null || !Book.TryRead(out state, out fault)) return;
			if (!KingdomResidentRules.TryAdoptLegacy(state, Names, Origins, Arrived, Counter,
				out next, out nextCounter, out fault))
			{
				KingdomLog.Log("resident: " + Label + " legacy roll retained unresolved (" + fault + ")");
				return;
			}
			if (!ReferenceEquals(next, state) && !Book.TryPublish(next, out fault))
			{
				KingdomLog.Log("resident: " + Label + " legacy adoption refused (" + fault + ")");
				return;
			}
			Counter = nextCounter;
		}

		private static int MaxResidentId(KingdomCityBook Book)
		{
			if (Book == null) return 0;
			Book.Normalize();
			int max = 0;
			for (int i = 0; i < Book.ResidentIds.Count; i++)
				if (Book.ResidentIds[i] > max) max = Book.ResidentIds[i];
			return max;
		}

		private static Dictionary<string, int> Counts(List<string> Values)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			for (int i = 0; Values != null && i < Values.Count; i++)
			{
				string value = Values[i] ?? "";
				counts.TryGetValue(value, out int count);
				counts[value] = count + 1;
			}
			return counts;
		}
	}
}
