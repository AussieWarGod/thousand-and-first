using System;
using Qud.API;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomChronicle
	{
		/// <summary>TAF deliberately occupies at most three of vanilla's scarce weighted
		/// mural candidates in one run. Vanilla may still choose none of them.</summary>
		internal const int MaxCodaEligibleAccomplishments = 3;

		internal static int CountJournalAccomplishments(string EventId)
		{
			if (string.IsNullOrEmpty(EventId) || JournalAPI.Accomplishments == null) return 0;
			int count = 0;
			foreach (JournalAccomplishment row in JournalAPI.Accomplishments)
			{
				if (row != null && string.Equals(row.ID, EventId,
					StringComparison.Ordinal)) count++;
			}
			return count;
		}

		internal static bool TryPrepareJournalProjection(string EventId, string MuralText,
			out string ProjectedMural, out string GospelText, out MuralWeight Weight)
		{
			ProjectedMural = null;
			GospelText = null;
			Weight = MuralWeight.Nil;
			if (string.IsNullOrEmpty(EventId) || EventId.Length > 160 ||
				!EventId.StartsWith("taf:", StringComparison.Ordinal)) return false;
			if (string.IsNullOrEmpty(MuralText)) return true;
			if (MuralText.Length > KingdomChronicleReceiptRules.MaxEntryChars) return false;
			if (CountCodaEligibleAccomplishments() >= MaxCodaEligibleAccomplishments)
				return true;
			string clause = MuralText.Trim();
			if (clause.Length == 0) return false;
			clause = char.ToLowerInvariant(clause[0]) + clause.Substring(1).TrimEnd('.');
			ProjectedMural = MuralText;
			GospelText = "In =year=, =name= " + clause + ".";
			Weight = MuralWeight.Medium;
			return true;
		}

		private static int CountCodaEligibleAccomplishments()
		{
			if (JournalAPI.Accomplishments == null) return 0;
			int count = 0;
			foreach (JournalAccomplishment row in JournalAPI.Accomplishments)
			{
				if (row != null && row.ID != null &&
					row.ID.StartsWith("taf:", StringComparison.Ordinal) &&
					row.MuralWeight != MuralWeight.Nil &&
					!string.IsNullOrEmpty(row.MuralText) &&
					!string.IsNullOrEmpty(row.GospelText)) count++;
			}
			return count;
		}
	}
}
