using System;

namespace ThousandAndFirst
{
	public static partial class KingdomCharterMenuRules
	{
		/// <summary>Player wording for a founding stamp, without exposing engine ticks.</summary>
		public static string FoundedWhen(long Founded, long Now, long TicksPerDay)
		{
			if (Founded < 0L || Now < Founded || TicksPerDay <= 0L)
				return "founding date needs inspection";
			long days = (Now - Founded) / TicksPerDay;
			if (days <= 0L) return "founded today";
			if (days == 1L) return "founded yesterday";
			return "founded " + days + " days ago";
		}

		/// <summary>Player wording for a due stamp, preserving whether it has passed.</summary>
		public static string DueWhen(long Due, long Now, long TicksPerDay)
		{
			if (Due <= 0L) return "not yet scheduled";
			if (Now < 0L || TicksPerDay <= 0L) return "date needs inspection";
			if (Due == Now) return "due now";
			if (Due < Now)
			{
				long late = Now - Due;
				long days = late / TicksPerDay;
				long remainder = late % TicksPerDay;
				if (days == 0L) return "overdue by less than a day";
				if (remainder == 0L)
					return "overdue by " + days + (days == 1L ? " day" : " days");
				return "overdue by more than " + days + (days == 1L ? " day" : " days");
			}
			long span = Due - Now;
			long futureDays = span / TicksPerDay;
			long futureRemainder = span % TicksPerDay;
			if (futureDays == 0L) return "due within a day";
			if (futureRemainder == 0L)
				return "due in " + futureDays + (futureDays == 1L ? " day" : " days");
			return "due in less than " + (futureDays + 1L) + " days";
		}

		private static KingdomCharterMenuRoute[] Copy(KingdomCharterMenuRoute[] Source)
		{
			KingdomCharterMenuRoute[] copy = new KingdomCharterMenuRoute[Source.Length];
			Array.Copy(Source, copy, Source.Length); return copy;
		}
	}
}
