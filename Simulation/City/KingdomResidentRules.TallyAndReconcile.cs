using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentRules
	{
		/// <summary>The roll, counted off the rows.</summary>
		internal static bool TryTally(KingdomCityState state, out KingdomResidentTally tally)
		{
			tally = default(KingdomResidentTally);
			if (state == null)
			{
				return false;
			}
			int resident = 0;
			int abroad = 0;
			int dead = 0;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row))
				{
					return false;
				}
				switch (row.Standing)
				{
				case KingdomResidentStanding.Resident:
				case KingdomResidentStanding.Expedition:
					resident++;
					break;
				case KingdomResidentStanding.Abroad:
					abroad++;
					break;
				default:
					dead++;
					break;
				}
			}
			tally = new KingdomResidentTally(resident, abroad, dead);
			return true;
		}

		/// <summary>
		/// LIVING-CITY-ARCHITECTURE &sect;8.3 invariant 3, checked rather than assumed:
		/// <i>the roll == live bindings + <c>Abroad</c></i>.
		/// <para>
		/// Which unpacks into two facts this walks: every <c>Resident</c> row has exactly one live
		/// binding, and no <c>Abroad</c> or <c>Dead</c> row has one. Both directions matter — a
		/// resident with no binding is a person the city thinks is working and has no body for, and
		/// a dead row with a binding is a corpse the registry will still hand out as a place to
		/// move a settler to.
		/// </para>
		/// <para>
		/// The registry is realm-scope and this walks ONE city, so it counts only bindings whose
		/// key belongs to a row of this book. A key the other city minted is not this city's to
		/// reconcile, and treating it as one would fail the audit every time a realm held two
		/// cities.
		/// </para>
		/// </summary>
		internal static bool TryReconcile(KingdomCityState state, KingdomBindingTable table, out KingdomResidentTally tally, out KingdomCityFault fault)
		{
			tally = default(KingdomResidentTally);
			if (state == null || table == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (!TryTally(state, out tally))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int bound = 0;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				bool holds = table.Holds(row.ResidentId, KingdomBindingKind.Resident);
				if (holds != Bindable(row.Standing))
				{
					fault = holds ? KingdomCityFault.DuplicateBinding : KingdomCityFault.UnknownBinding;
					return false;
				}
				if (holds)
				{
					bound++;
				}
			}
			if (bound + tally.Abroad != tally.OnTheRoll)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			fault = KingdomCityFault.None;
			return true;
		}

	}
}
