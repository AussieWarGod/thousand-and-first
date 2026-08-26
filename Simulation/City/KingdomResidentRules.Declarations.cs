using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What the ground says about one bound body, on the pass that stood in its zone. The witness
	/// the transition rules are total over.
	/// <para>
	/// Deliberately not "alive / dead": the interesting case is a body that is standing right there
	/// and no longer belongs to the city, which is what <see cref="Led"/> is. That is the case
	/// LIVING-CITY-ARCHITECTURE &sect;8.3 calls the honest residual risk &mdash; a founder who
	/// charms half the settlement and walks them across Qud &mdash; and the design's answer is to
	/// SAY SO rather than to prevent it.
	/// </para>
	/// </summary>
	internal enum KingdomBodyWitness : byte
	{
		/// <summary>A live body carrying this id stands in the zone, on the city's roll.</summary>
		Present = 0,

		/// <summary>A live body carrying this id stands in the zone and follows the founder. It is
		/// no longer one of the city's own hands, however close it is standing.</summary>
		Led = 1,

		/// <summary>A body carrying this id is in the zone and dead.</summary>
		Killed = 2,

		/// <summary>No body carrying this id is in the zone at all, and the zone is attended, so
		/// there is nowhere in it left to look.</summary>
		Missing = 3
	}

	/// <summary>The roll, counted. LIVING-CITY-ARCHITECTURE &sect;8.3's third invariant is an
	/// equation between these numbers and the registry's.</summary>
	internal readonly struct KingdomResidentTally
	{
		internal readonly int Resident;

		internal readonly int Abroad;

		internal readonly int Dead;

		internal KingdomResidentTally(int resident, int abroad, int dead)
		{
			Resident = resident;
			Abroad = abroad;
			Dead = dead;
		}

		/// <summary>Everybody the city still counts as one of its people. The dead are off the
		/// roll; the abroad are on it and doing nothing, which is the whole point of having the
		/// word.</summary>
		internal int OnTheRoll
		{
			get { return Resident + Abroad; }
		}
	}

	/// <summary>A detached compatibility/report view built only from resident rows. Callers may
	/// mutate these lists without reaching the frozen city state.</summary>
	internal sealed class KingdomResidentRollProjection
	{
		internal readonly List<int> ResidentIds = new List<int>();
		internal readonly List<string> Names = new List<string>();
		internal readonly List<string> Origins = new List<string>();
		internal readonly List<string> Arrived = new List<string>();
		internal int Population;
		internal int Labour;
	}

	internal enum KingdomAccessionOutcome : byte
	{
		RefusedClean = 0,
		Committed = 1,
		RepairRequired = 2
	}

	internal enum KingdomAccessionCarrierState : byte
	{
		Original = 0,
		Committed = 1,
		CityAdvanced = 2,
		BindingAdvanced = 3,
		Unknown = 4
	}

}
