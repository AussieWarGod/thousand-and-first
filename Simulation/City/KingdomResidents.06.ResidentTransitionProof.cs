using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		/// <summary>Non-normalizing exact row proof for a destructive transition. Every owned
		/// book is inspected so duplicate resident IDs cannot select the first convenient row.</summary>
		internal static bool TryProveResidentTransitionRow(KingdomSystem System,
			GameObject Body, int ResidentId, string ZoneId, string Name, bool AllowMissing,
			string RepairSettlementId, string RepairName, out int Matches)
		{
			Matches = 0;
			List<KingdomCityBook> books = System?.OwnedCityBooks();
			if (books == null || books.Count == 0
				|| books.Count > KingdomIdentityRules.MaxSettlements) return false;
			HashSet<KingdomCityBook> seenBooks = new HashSet<KingdomCityBook>();
			HashSet<string> settlements = new HashSet<string>(StringComparer.Ordinal);
			for (int b = 0; b < books.Count; b++)
			{
				KingdomCityBook book = books[b];
				if (book == null || !seenBooks.Add(book)
					|| !KingdomIdentityRules.IsSettlementId(book.SettlementId)
					|| !settlements.Add(book.SettlementId)
					|| !ResidentColumnsSquareForTransition(book, out int count)) return false;
				for (int i = 0; i < count; i++)
				{
					if (book.ResidentIds[i] != ResidentId) continue;
					Matches++;
					if (book.ResidentStandings[i]
						!= (int)KingdomResidentStanding.Resident
						|| !string.Equals(book.ResidentBoundZoneIds[i], ZoneId,
							StringComparison.Ordinal)
						|| !string.Equals(book.ResidentNames[i], Name,
							StringComparison.Ordinal)
						|| !string.Equals(book.ResidentOrigins[i] ?? "",
							Body?.GetStringProperty("KingdomOrigin") ?? "",
							StringComparison.Ordinal)
						|| AllowMissing && (!string.Equals(book.SettlementId,
							RepairSettlementId, StringComparison.Ordinal)
							|| !string.Equals(book.ResidentNames[i], RepairName,
								StringComparison.Ordinal))) return false;
				}
			}
			return !AllowMissing || Matches != 0
				|| KingdomIdentityRules.IsSettlementId(RepairSettlementId)
					&& string.Equals(Name, RepairName, StringComparison.Ordinal)
					&& settlements.Contains(RepairSettlementId);
		}

		private static bool ResidentColumnsSquareForTransition(KingdomCityBook Book,
			out int Count)
		{
			Count = Book?.ResidentIds?.Count ?? -1;
			if (Count < 0 || Count > KingdomCityState.MaxResidents) return false;
			int[] counts =
			{
				Book.ResidentNames?.Count ?? -1, Book.ResidentOrigins?.Count ?? -1,
				Book.ResidentOriginCodes?.Count ?? -1, Book.ResidentCreedCodes?.Count ?? -1,
				Book.ResidentKeptCreeds?.Count ?? -1, Book.ResidentArrivedTicks?.Count ?? -1,
				Book.ResidentArrived?.Count ?? -1, Book.ResidentHomeWorkIds?.Count ?? -1,
				Book.ResidentJobWorkIds?.Count ?? -1, Book.ResidentJobRoles?.Count ?? -1,
				Book.ResidentDayShapes?.Count ?? -1, Book.ResidentStandings?.Count ?? -1,
				Book.ResidentCauses?.Count ?? -1, Book.ResidentBoundZoneIds?.Count ?? -1,
				Book.ResidentRoofStanding?.Count ?? -1, Book.ResidentRoofTicks?.Count ?? -1,
				Book.ResidentRoofWarnedTicks?.Count ?? -1,
				Book.ResidentCreedStanding?.Count ?? -1, Book.ResidentCreedTicks?.Count ?? -1,
				Book.ResidentCreedWarnedTicks?.Count ?? -1,
				Book.ResidentCreedToward?.Count ?? -1,
				Book.ResidentCreedChannels?.Count ?? -1
			};
			for (int i = 0; i < counts.Length; i++) if (counts[i] != Count) return false;
			return true;
		}
	}
}
