using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		// ---- Resident rows -------------------------------------------------------------------

		public List<int> ResidentIds = new List<int>();

		public List<string> ResidentNames = new List<string>();

		/// <summary>Exact open provenance; the code column is only its built-in catalogue view.</summary>
		public List<string> ResidentOrigins = new List<string>();

		public List<int> ResidentOriginCodes = new List<int>();

		public List<int> ResidentCreedCodes = new List<int>();

		/// <summary>The creeds each resident has HELD AND LEFT, as
		/// <c>KingdomCreedRules.EncodeKept</c> stores them. Addendum 16: the alignment gate reads
		/// what a person once believed, and a fact the city is asked about has to be written in the
		/// city's own book.</summary>
		public List<string> ResidentKeptCreeds = new List<string>();

		public List<long> ResidentArrivedTicks = new List<long>();

		/// <summary>Frozen dated label. This is presentation evidence, never a second clock.</summary>
		public List<string> ResidentArrived = new List<string>();

		public List<int> ResidentHomeWorkIds = new List<int>();

		public List<int> ResidentJobWorkIds = new List<int>();

		public List<int> ResidentJobRoles = new List<int>();

		public List<int> ResidentDayShapes = new List<int>();

		public List<int> ResidentStandings = new List<int>();

		/// <summary>Why a row left <c>Resident</c>. LIVING-CITY-ARCHITECTURE &sect;8.3: a body the
		/// player killed reads back as Dead <b>with a cause</b>, and a cause nobody wrote down is
		/// the half of that sentence that would have gone missing.</summary>
		public List<int> ResidentCauses = new List<int>();

		public List<string> ResidentBoundZoneIds = new List<string>();

		/// <summary>One when a roof brink stands over this settler at all. Kept apart from the
		/// warned tick so that "recorded, and the word has not gone out yet" and "no brink" are
		/// different states rather than the same zero &mdash; <c>KingdomBrink</c>'s own rule, and
		/// the reason the property it replaced existed.</summary>
		public List<int> ResidentRoofStanding = new List<int>();

		public List<long> ResidentRoofTicks = new List<long>();

		/// <summary>The tick the founder was warned, and the anchor the whole window runs from.
		/// A <c>long</c>, not a flag: <c>KingdomBrinkRules.WindowSpent</c> counts world-days from
		/// this number.</summary>
		public List<long> ResidentRoofWarnedTicks = new List<long>();

		public List<int> ResidentCreedStanding = new List<int>();

		public List<long> ResidentCreedTicks = new List<long>();

		public List<long> ResidentCreedWarnedTicks = new List<long>();

		/// <summary>The creed a brink pulls toward, by faction name. A name and not a code: creeds
		/// are open-ended faction names, and the conversion that fires at the end of the window
		/// needs the one it was recorded with.</summary>
		public List<string> ResidentCreedToward = new List<string>();

		public List<int> ResidentCreedChannels = new List<int>();

		// ---- Clocks --------------------------------------------------------------------------

		public List<int> ClockKinds = new List<int>();

		public List<long> ClockNextDueTicks = new List<long>();

		public List<int> ClockOrdinals = new List<int>();

		// ---- The told-log ring, written oldest first ------------------------------------------

		public List<int> ToldKinds = new List<int>();

		public List<long> ToldTicks = new List<long>();

		public List<int> ToldSubjectsA = new List<int>();

		public List<int> ToldSubjectsB = new List<int>();

		public List<string> ToldPlaceZoneIds = new List<string>();

		public List<int> ToldOutcomes = new List<int>();
	}
}
