using System;
using System.Text;
using ThousandAndFirst.Api;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The city book as the founder reads it: the stores and their ceilings, the works and what
	/// each is waiting on, and the roll.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;5: <i>"the works board &hellip; this is where the simulation
	/// becomes legible. Without it the model is invisible and the whole wave is worthless."</i>
	/// Every line here is a projection of a row the model already keeps; nothing is stored for the
	/// sake of being read, which is BUILDING-CATALOGUE-BRIEF Addendum 13's mesh condition as a
	/// property of the type rather than a promise about it.
	/// </para>
	/// <para>
	/// Engine-free and total. Zone and design names arrive as delegates, because the model carries
	/// neither &mdash; appearance stays on the object (&sect;1.2(c)) &mdash; and a rules class that
	/// reached for the catalogue to find one would stop being testable.
	/// </para>
	/// </summary>
	internal static partial class KingdomBookRules
	{
		/// <summary>Works listed before the list is summarised instead. Forty is the model's own
		/// ceiling (<c>KingdomCityState.MaxWorks</c>); a screen is not.</summary>
		internal const int MaxWorksListed = 24;

		/// <summary>Settlers named on the roll chapter before the rest are counted.</summary>
		internal const int MaxNamesListed = 12;

		// ==================================================================================
		// The stores
		// ==================================================================================

		/// <summary>
		/// What the city holds, and where.
		/// <para>
		/// The city line and the zone lines are both the model's, and they are shown together on
		/// purpose: the whole point of the book is that the granary two zones away is a number the
		/// founder can see without walking to it.
		/// </para>
		/// </summary>
		/// <param name="City">The published reading.</param>
		/// <param name="ZoneName">Turns a zone id into ground a founder recognises, or null.</param>
		internal static string Stores(KingdomCityReading City, Func<string, string> ZoneName,
			Func<string, string> PresentName = null)
		{
			StringBuilder builder = new StringBuilder();
			if (City == null)
			{
				return "There is no book to read.";
			}
			builder.Append("{{C|").Append(Shown(City.CityName, PresentName)).Append("}} holds:")
				.Append("\n  Water      ").Append(Pair(City.Water))
				.Append("\n  Food       ").Append(Pair(City.Food))
				.Append("\n  Materials  ").Append(Pair(City.Materials));
			if (City.ZoneCount <= 0)
			{
				builder.Append("\n\n{{K|No ground is on the book yet.}}");
				return builder.ToString();
			}
			for (int i = 0; i < City.ZoneCount; i++)
			{
				KingdomZoneReading zone;
				if (!City.TryZone(i, out zone))
				{
					continue;
				}
				builder.Append("\n\n").Append(Ground(zone.ZoneId, ZoneName))
					.Append("\n  water ").Append(Pair(zone.Water))
					.Append(", food ").Append(Pair(zone.Food))
					.Append(", materials ").Append(Pair(zone.Materials))
					.Append("\n  {{K|").Append(zone.Roofs).Append((zone.Roofs == 1) ? " roof" : " roofs")
					.Append(", ").Append(zone.Defence).Append(" on the watch}}");
				string owed = Owed(zone);
				if (!string.IsNullOrEmpty(owed))
				{
					builder.Append("\n  {{W|").Append(owed).Append("}}");
				}
			}
			return builder.ToString();
		}

		/// <summary>
		/// What the model owes this ground and has not been able to land on real vessels yet, in
		/// plain words. Addendum 12(d): the debt is real, it is signed, and it is told rather than
		/// silently repaired.
		/// </summary>
		/// <returns>The clause, or empty when the book and the ground agree.</returns>
		internal static string Owed(KingdomZoneReading Zone)
		{
			StringBuilder builder = new StringBuilder();
			Owed(builder, Zone.OwedWater, "water");
			Owed(builder, Zone.OwedFood, "food");
			Owed(builder, Zone.OwedMaterials, "materials");
			if (builder.Length == 0)
			{
				return "";
			}
			return "The count and the vessels have not been squared here: " + builder.ToString() + ".";
		}

		private static void Owed(StringBuilder builder, int owed, string what)
		{
			if (owed == 0)
			{
				return;
			}
			if (builder.Length > 0)
			{
				builder.Append(", ");
			}
			builder.Append((owed > 0) ? (owed + " " + what + " still to land") : ((-owed) + " " + what + " still to draw"));
		}

		// ==================================================================================
		// The works
		// ==================================================================================

		/// <summary>
		/// Every work in the CITY, not in the zone: what it is, how worn, who is on it, and what
		/// it is waiting on.
		/// </summary>
		internal static string Works(KingdomCityReading City, Func<string, string> WorkName,
			Func<string, string> ZoneName, Func<string, string> PresentName = null)
		{
			if (City == null || City.WorkCount <= 0)
			{
				return "Nothing stands on the book yet. What you raise goes on it.";
			}
			int stopped = 0;
			for (int i = 0; i < City.WorkCount; i++)
			{
				KingdomWorkReading work;
				if (City.TryWork(i, out work) && !string.IsNullOrEmpty(Waiting(work)))
				{
					stopped++;
				}
			}
			StringBuilder builder = new StringBuilder();
			builder.Append("{{C|").Append(Shown(City.CityName, PresentName))
				.Append("}} keeps ").Append(City.WorkCount)
				.Append((City.WorkCount == 1) ? " work" : " works")
				.Append((stopped > 0) ? (", and {{W|" + stopped + " of them " + ((stopped == 1) ? "is" : "are") + " waiting on you}}.") : ", and none of them is waiting on you.");
			int listed = 0;
			for (int i = 0; i < City.WorkCount && listed < MaxWorksListed; i++)
			{
				KingdomWorkReading work;
				if (!City.TryWork(i, out work))
				{
					continue;
				}
				listed++;
				builder.Append("\n\n").Append(Named(work, WorkName)).Append(" — ").Append(Condition(work.ConditionPercent))
					.Append(", ").Append(Hands(work.CrewAssigned))
					.Append("\n  {{K|").Append(Ground(work.ZoneId, ZoneName)).Append(". ").Append(Doing(work)).Append("}}");
				string waiting = Waiting(work);
				if (!string.IsNullOrEmpty(waiting))
				{
					builder.Append("\n  {{W|").Append(waiting).Append("}}");
				}
			}
			if (City.WorkCount > listed)
			{
				builder.Append("\n\n{{K|And ").Append(City.WorkCount - listed).Append(" more.}}");
			}
			return builder.ToString();
		}

		/// <summary>
		/// What a work is waiting on, or empty when it is simply running.
		/// <para>
		/// The two clauses are <c>KingdomHappeningRules.Broken</c>'s own, asked forward instead of
		/// after the fact, so the board and the breakdown happening can never disagree about what
		/// "stopped" means.
		/// </para>
		/// </summary>
		internal static string Waiting(KingdomWorkReading Work)
		{
			if (Work.ConditionPercent <= KingdomHappeningRules.BreakdownConditionFloor)
			{
				return "Worn past mending itself. It waits on a crew, or on being taken down.";
			}
			if (Work.CrewAssigned <= 0 && KingdomHappeningRules.NeedsHands(KingdomReadingRules.Kind(Work.Class)))
			{
				return "Nobody is on it. It waits on hands.";
			}
			return "";
		}

		/// <summary>What a work is doing, read off the one slot of run-state its kind uses.</summary>
		internal static string Doing(KingdomWorkReading Work)
		{
			switch (Work.Class)
			{
			case KingdomWorkClass.Growing:
				return "Growing, at stage " + Work.Stage + ".";
			case KingdomWorkClass.Store:
				return "Holding what is put in it.";
			case KingdomWorkClass.Producer:
				return (Work.CrewAssigned > 0) ? "Making." : "Idle.";
			case KingdomWorkClass.Refiner:
				return (Work.CrewAssigned > 0) ? "Refining." : "Idle.";
			case KingdomWorkClass.Power:
				return "Carrying " + Work.Progress + " charge.";
			case KingdomWorkClass.Construction:
				return (Work.CrewAssigned > 0) ? "Being raised." : "Idle.";
			default:
				return "Standing.";
			}
		}

		/// <summary>Wear in words. The rungs are the wear lane's own reading of a percentage, not
		/// a new ladder.</summary>
		internal static string Condition(int Percent)
		{
			if (Percent >= 100)
			{
				return "sound";
			}
			if (Percent <= KingdomHappeningRules.BreakdownConditionFloor)
			{
				return "{{r|worn to " + Percent + "%}}";
			}
			return "worn to " + Percent + "%";
		}
	}
}
