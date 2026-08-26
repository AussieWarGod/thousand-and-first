using System;

namespace ThousandAndFirst.Api
{
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
}
