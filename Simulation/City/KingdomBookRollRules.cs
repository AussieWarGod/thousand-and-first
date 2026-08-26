using System;
using System.Text;
using ThousandAndFirst.Api;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomBookRules
	{
		// ==================================================================================
		// The roll
		// ==================================================================================

		/// <summary>
		/// Who lives here, where their day puts them, and who the city calls its own.
		/// </summary>
		/// <param name="City">The published reading.</param>
		/// <param name="OfficeTitle">The settlement's title for its office, or empty.</param>
		/// <param name="OfficeHolder">Who holds it, with their epithet, or empty.</param>
		internal static string Roll(KingdomCityReading City, string OfficeTitle,
			string OfficeHolder, Func<string, string> PresentName = null)
		{
			if (City == null || City.ResidentCount <= 0)
			{
				return "Nobody is on the roll yet.";
			}
			int living = 0;
			int abroad = 0;
			int expedition = 0;
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
				case KingdomRollStanding.Expedition:
					expedition++;
					continue;
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
			builder.Append("{{C|").Append(Shown(City.CityName, PresentName))
				.Append("}}: ").Append(living)
				.Append((living == 1) ? " lives here" : " live here")
				.Append((expedition > 0) ? (", " + expedition + " on expedition") : "")
				.Append((abroad > 0) ? (", " + abroad + " away with you") : "")
				.Append((dead > 0) ? (", " + dead + " buried") : "")
				.Append(".");
			if (!string.IsNullOrEmpty(OfficeHolder))
			{
				builder.Append("\n\n{{W|").Append(Shown(OfficeHolder, PresentName)).Append("}}")
					.Append(string.IsNullOrEmpty(OfficeTitle) ? "" : (", " + OfficeTitle)).Append(".");
			}
			string spread = Spread(byDay);
			if (!string.IsNullOrEmpty(spread))
			{
				builder.Append("\n\nBy day: ").Append(spread).Append(".");
			}
			int named = 0;
			int namedLiving = 0;
			for (int i = 0; i < City.ResidentCount && named < MaxNamesListed; i++)
			{
				KingdomResidentReading row;
				if (!City.TryResident(i, out row)
					|| (row.Standing != KingdomRollStanding.Resident
						&& row.Standing != KingdomRollStanding.Expedition)
					|| string.IsNullOrEmpty(row.Name))
				{
					continue;
				}
				named++;
				if (row.Standing == KingdomRollStanding.Resident) namedLiving++;
				builder.Append("\n  ").Append(Shown(row.Name, PresentName)).Append(" {{K|— ")
					.Append(row.Standing == KingdomRollStanding.Expedition
						? "on expedition" : Where(row.Day)).Append("}}");
			}
			if (living > namedLiving)
			{
				builder.Append("\n  {{K|and ").Append(living - namedLiving).Append(" more}}");
			}
			return builder.ToString();
		}

		/// <summary>Optional runtime-only rich projection; pure tests leave names unchanged.</summary>
		private static string Shown(string plain, Func<string, string> present)
		{
			return present == null ? (plain ?? "") : (present(plain) ?? "");
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
