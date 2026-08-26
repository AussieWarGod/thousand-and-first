using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomRaidDefenceReservation
	{
		/// <summary>Stable FNV identity of the exact built work. This is the same WorkId used by
		/// city rows and settler posts; object-list position and a display name are not authority.</summary>
		public int WorkId;
		public int FrozenScore;
		/// <summary>Exact resident-row identities reserved at this work. Whole people, globally
		/// exclusive within the muster; empty only for a work whose design needs no crew.</summary>
		public List<int> CrewSemanticIds = new List<int>();
	}
}
