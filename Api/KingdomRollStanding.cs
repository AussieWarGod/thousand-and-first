using System;

namespace ThousandAndFirst.Api
{
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
}
