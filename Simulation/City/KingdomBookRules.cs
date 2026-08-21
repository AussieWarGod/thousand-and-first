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
	internal static class KingdomBookRules
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
		internal static string Stores(KingdomCityReading City, Func<string, string> ZoneName)
		{
			StringBuilder builder = new StringBuilder();
			if (City == null)
			{
				return "There is no book to read.";
			}
			builder.Append("{{C|").Append(City.CityName).Append("}} holds:")
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
		internal static string Works(KingdomCityReading City, Func<string, string> WorkName, Func<string, string> ZoneName)
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
			builder.Append("{{C|").Append(City.CityName).Append("}} keeps ").Append(City.WorkCount)
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

		// ==================================================================================
		// The roll
		// ==================================================================================

		/// <summary>
		/// Who lives here, where their day puts them, and who the city calls its own.
		/// </summary>
		/// <param name="City">The published reading.</param>
		/// <param name="OfficeTitle">The settlement's title for its office, or empty.</param>
		/// <param name="OfficeHolder">Who holds it, with their epithet, or empty.</param>
		internal static string Roll(KingdomCityReading City, string OfficeTitle, string OfficeHolder)
		{
			if (City == null || City.ResidentCount <= 0)
			{
				return "Nobody is on the roll yet.";
			}
			int living = 0;
			int abroad = 0;
			int dead = 0;
			int[] byDay = new int[7];
			for (int i = 0; i < City.ResidentCount; i++)
			{
				KingdomResidentReading row;
				if (!City.TryResident(i, out row))
				{
					continue;
				}
				switch (row.Standing)
				{
				case KingdomRollStanding.Abroad:
					abroad++;
					continue;
				case KingdomRollStanding.Dead:
					dead++;
					continue;
				}
				living++;
				int day = (int)row.Day;
				if (day >= 0 && day < byDay.Length)
				{
					byDay[day]++;
				}
			}
			StringBuilder builder = new StringBuilder();
			builder.Append("{{C|").Append(City.CityName).Append("}}: ").Append(living)
				.Append((living == 1) ? " lives here" : " live here")
				.Append((abroad > 0) ? (", " + abroad + " away with you") : "")
				.Append((dead > 0) ? (", " + dead + " buried") : "")
				.Append(".");
			if (!string.IsNullOrEmpty(OfficeHolder))
			{
				builder.Append("\n\n{{W|").Append(OfficeHolder).Append("}}")
					.Append(string.IsNullOrEmpty(OfficeTitle) ? "" : (", " + OfficeTitle)).Append(".");
			}
			string spread = Spread(byDay);
			if (!string.IsNullOrEmpty(spread))
			{
				builder.Append("\n\nBy day: ").Append(spread).Append(".");
			}
			int named = 0;
			for (int i = 0; i < City.ResidentCount && named < MaxNamesListed; i++)
			{
				KingdomResidentReading row;
				if (!City.TryResident(i, out row) || row.Standing != KingdomRollStanding.Resident || string.IsNullOrEmpty(row.Name))
				{
					continue;
				}
				named++;
				builder.Append("\n  ").Append(row.Name).Append(" {{K|— ").Append(Where(row.Day)).Append("}}");
			}
			if (living > named)
			{
				builder.Append("\n  {{K|and ").Append(living - named).Append(" more}}");
			}
			return builder.ToString();
		}

		/// <summary>Where a day shape puts somebody, in the founder's words.</summary>
		internal static string Where(KingdomDayPlace Day)
		{
			switch (Day)
			{
			case KingdomDayPlace.Field:
				return "in the fields";
			case KingdomDayPlace.Yard:
				return "in a yard";
			case KingdomDayPlace.Market:
				return "at the market";
			case KingdomDayPlace.Craft:
				return "at a craft";
			case KingdomDayPlace.Watch:
				return "on the watch";
			case KingdomDayPlace.Shrine:
				return "at the shrine";
			default:
				return "at the hearth";
			}
		}

		private static string Spread(int[] byDay)
		{
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < byDay.Length; i++)
			{
				if (byDay[i] <= 0)
				{
					continue;
				}
				if (builder.Length > 0)
				{
					builder.Append(", ");
				}
				builder.Append(byDay[i]).Append(" ").Append(Where((KingdomDayPlace)i));
			}
			return builder.ToString();
		}

		// ==================================================================================
		// Shared shapes
		// ==================================================================================

		/// <summary>A level against its ceiling, and nothing at all when there is no ceiling: a
		/// city with no dedicated vessels does not hold "0 of 0", it holds nothing.</summary>
		internal static string Pair(KingdomStockReading Stock)
		{
			if (Stock.Capacity <= 0L)
			{
				return "{{K|nothing dedicated}}";
			}
			return Stock.Level + " of " + Stock.Capacity;
		}

		/// <summary>Hands on a work, counted.</summary>
		internal static string Hands(int Crew)
		{
			if (Crew <= 0)
			{
				return "{{K|no hands}}";
			}
			return Crew + ((Crew == 1) ? " hand" : " hands");
		}

		private static string Named(KingdomWorkReading work, Func<string, string> workName)
		{
			string resolved = (workName == null || string.IsNullOrEmpty(work.DesignKey)) ? null : workName(work.DesignKey);
			if (!string.IsNullOrEmpty(resolved))
			{
				return resolved;
			}
			return string.IsNullOrEmpty(work.DesignKey) ? "a work" : work.DesignKey;
		}

		private static string Ground(string zoneId, Func<string, string> zoneName)
		{
			string resolved = (zoneName == null || string.IsNullOrEmpty(zoneId)) ? null : zoneName(zoneId);
			if (!string.IsNullOrEmpty(resolved))
			{
				return resolved;
			}
			return string.IsNullOrEmpty(zoneId) ? "somewhere on the claim" : zoneId;
		}
	}
}
