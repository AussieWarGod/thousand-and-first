using System;

namespace ThousandAndFirst
{
	public static partial class KingdomFaithRules
	{
		// ==================================================================================
		// Education (Addendum 5, channel 3) -- softens, converts nobody.
		// ==================================================================================

		/// <summary>
		/// One band gentler: the closeness a staffed knowledge building lets its zone's residents
		/// read the ambient grudge as, for cohabitation and osmosis alike. Education never
		/// changes what quarters a home actually has &mdash; it changes how forgiving the
		/// quarters' own rung is read as being, exactly one rung roomier, capped at
		/// <see cref="KingdomLodgingRules.Closeness.Private"/> because there is nothing gentler
		/// than a house of one's own to soften toward.
		/// </summary>
		public static KingdomLodgingRules.Closeness SoftenedCloseness(KingdomLodgingRules.Closeness Quarters)
		{
			return (Quarters >= KingdomLodgingRules.Closeness.Private) ? Quarters : (KingdomLodgingRules.Closeness)((int)Quarters + 1);
		}

		/// <summary>
		/// STANDARDS 7b's once-only line for a knowledge building built to be staffed &mdash;
		/// carries a <c>Staff</c> requirement of its own &mdash; that presently has nobody at it:
		/// a room of vellum, and honestly said to be one.
		/// </summary>
		public static string EducationLapsedLine(string BuildingName,
			bool StaffedButUnsupplied = false)
		{
			string building = string.IsNullOrEmpty(BuildingName) ? "The scriptorium" : ("The " + BuildingName);
			if (StaffedButUnsupplied)
				return "{{K|" + building + " has hands, but no usable desk, shelf, or place of instruction. Restore one inside its designated ground.}}";
			return "{{K|" + building + " stands empty of hands: a room of vellum, and nothing more, until somebody keeps it.}}";
		}
	}
}
