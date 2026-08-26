using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentRules
	{
		/// <summary>
		/// Where a person's day puts them, derived from the work they are posted to.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;1.2(d): the day shape is <b>derived from Job and the
		/// settlement's standing policy, never authored per settler</b>, and it holds no times. It
		/// is read against the calendar's own bands, and the mapping from band to PLACE is what W3
		/// adds on top of this; what a person's day is ABOUT is this.
		/// </para>
		/// <para>
		/// A settler with no post keeps the hearth. That is not a placeholder for a wave that has
		/// not shipped: somebody the works have no room for genuinely spends their day at home, and
		/// it is the same fact <c>KingdomGrowth.AssignWork</c> reports as an idle work.
		/// </para>
		/// </summary>
		internal static KingdomDayShape DayShapeFor(int jobWorkId, KingdomWorkKind jobKind)
		{
			if (jobWorkId == 0)
			{
				return KingdomDayShape.Hearth;
			}
			switch (jobKind)
			{
			case KingdomWorkKind.Growing:
				return KingdomDayShape.Field;
			case KingdomWorkKind.Store:
				return KingdomDayShape.Market;
			case KingdomWorkKind.Producer:
			case KingdomWorkKind.Refiner:
				return KingdomDayShape.Craft;
			case KingdomWorkKind.Power:
				return KingdomDayShape.Yard;
			case KingdomWorkKind.Construction:
				return KingdomDayShape.Yard;
			default:
				return KingdomDayShape.Hearth;
			}
		}

		/// <summary>The stable code for one of <c>KingdomRules.Origins</c>, or
		/// <see cref="NoOrigin"/>. The district idiom, applied to where a settler walked in
		/// from: the row carries a code and the name stays in one place.</summary>
		internal static int OriginCode(string origin)
		{
			if (string.IsNullOrEmpty(origin))
			{
				return NoOrigin;
			}
			for (int i = 0; i < KingdomRules.Origins.Length; i++)
			{
				if (string.Equals(KingdomRules.Origins[i], origin, StringComparison.Ordinal))
				{
					return i + 1;
				}
			}
			return NoOrigin;
		}

		/// <summary>The origin a code names, or null. The inverse of <see cref="OriginCode"/> over
		/// every representable input.</summary>
		internal static string OriginKey(int code)
		{
			int index = code - 1;
			if (index < 0 || index >= KingdomRules.Origins.Length)
			{
				return null;
			}
			return KingdomRules.Origins[index];
		}
	}
}
