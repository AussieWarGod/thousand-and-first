using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentRules
	{
		/// <summary>
		/// Whether this row's person is doing the city's work today.
		/// <para>
		/// &sect;8.3: a body the player took away is <i>"still on the roll, contributing no labour,
		/// and honestly reported as such"</i>. This is that sentence as a predicate. W2's roster
		/// authority and crew selection both consume it; placement remains W3's separate concern.
		/// </para>
		/// </summary>
		internal static bool Labours(KingdomResidentRow row)
		{
			return row.Standing == KingdomResidentStanding.Resident;
		}

		/// <summary>Hands assigned to one exact work on one exact ground. Resident rows are the
		/// authority: object staffing flags and legacy roster projections cannot contribute.</summary>
		internal static int CrewAssigned(KingdomCityState state, string zoneId, int workId)
		{
			if (state == null || string.IsNullOrEmpty(zoneId) || workId == 0) return 0;
			int crew = 0;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (state.TryResident(i, out row) && Labours(row)
					&& row.JobWorkId == workId
					&& string.Equals(row.BoundZoneId, zoneId, StringComparison.Ordinal))
				{
					crew++;
				}
			}
			return crew;
		}

		/// <summary>Whether the city still counts this person as one of its own. The dead are off
		/// the roll and the abroad are on it.</summary>
		internal static bool OnTheRoll(KingdomResidentRow row)
		{
			return row.Standing != KingdomResidentStanding.Dead;
		}

		/// <summary>Builds the living roll, in durable row order. Dead rows stay in the city book for
		/// memorials but are not projected into the living compatibility fields.</summary>
		internal static bool TryProject(KingdomCityState state,
			out KingdomResidentRollProjection projection)
		{
			projection = null;
			if (state == null) return false;
			KingdomResidentRollProjection built = new KingdomResidentRollProjection();
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row)) return false;
				if (Labours(row)) built.Labour++;
				if (!OnTheRoll(row)) continue;
				built.ResidentIds.Add(row.ResidentId);
				built.Names.Add(row.Name ?? "");
				built.Origins.Add(row.Origin ?? "");
				built.Arrived.Add(row.Arrived ?? "");
				built.Population++;
			}
			projection = built;
			return true;
		}

		/// <summary>Adopts a complete legacy parallel roll into an empty resident book. Claims are
		/// Abroad until a real body rebinds by id, so migration never fabricates labour or a body.
		/// Ragged evidence is refused whole and remains available at the migration boundary.</summary>
		internal static bool TryAdoptLegacy(KingdomCityState state, IList<string> names,
			IList<string> origins, IList<string> arrived, int counter,
			out KingdomCityState next, out int nextCounter, out KingdomCityFault fault)
		{
			next = null;
			nextCounter = counter;
			if (state == null || names == null || origins == null || arrived == null || counter < 0)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (state.ResidentCount != 0 || names.Count == 0)
			{
				next = state;
				fault = KingdomCityFault.None;
				return true;
			}
			if (names.Count != origins.Count || names.Count != arrived.Count
				|| names.Count > KingdomCityState.MaxResidents
				|| counter > int.MaxValue - names.Count)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomResidentRow[] rows = new KingdomResidentRow[names.Count];
			for (int i = 0; i < rows.Length; i++)
			{
				string name = names[i] ?? "";
				string origin = origins[i] ?? "";
				string date = arrived[i] ?? "";
				int id = counter + i + 1;
				rows[i] = new KingdomResidentRow(id, name, OriginCode(origin), 0, 0L,
					0, 0, 0, KingdomDayShape.Hearth, KingdomResidentStanding.Abroad,
					KingdomStandingCause.Astray, null, KingdomBrinkWindow.None,
					KingdomBrinkWindow.None, null, 0, null, origin, date);
			}
			if (!state.TryWithResidents(rows, out next, out fault)) return false;
			nextCounter = counter + rows.Length;
			return true;
		}

		/// <summary>Removes one exact resident row in one copy-on-write state transition.</summary>
		internal static bool TryRemove(KingdomCityState state, int residentId,
			out KingdomCityState next, out KingdomResidentRow removed, out KingdomCityFault fault)
		{
			next = null;
			removed = default(KingdomResidentRow);
			if (state == null || residentId <= 0)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			int index;
			if (!state.TryResidentIndex(residentId, out index)
				|| !state.TryResident(index, out removed))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KingdomResidentRow[] rows = new KingdomResidentRow[state.ResidentCount - 1];
			for (int read = 0, write = 0; read < state.ResidentCount; read++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(read, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				if (read != index) rows[write++] = row;
			}
			return state.TryWithResidents(rows, out next, out fault);
		}

	}
}
