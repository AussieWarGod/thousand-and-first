using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Deterministic cap allocation and inspection order over normalized provider evidence.</summary>
	internal static partial class KingdomBenefitAllocationRules
	{
		// Covers one maximum normalized declaration plus anonymous object/holder/type framing.
		internal const int MaxStableKeyChars = 8192;
		internal const int MaxDetailChars = 512;
		internal const int MaxInspectionRows = KingdomBenefitEmbodimentRules.MaxProvidersPerZone
			+ KingdomDesignationRules.MaxSourceFaults
			+ 2 * KingdomDesignationRules.MaxDesignationsPerZone
			+ KingdomBenefitEmbodimentRules.MaxObservationLimitRowsPerZone;

		internal static bool TryAllocate(IReadOnlyList<KindAmount> Caps,
			IReadOnlyList<string> AcceptedTags, IReadOnlyList<KingdomBenefitAllocationClaim> Claims,
			out List<KingdomBenefitAllocationClaim> Ordered, out string Failure)
		{
			try { return TryAllocateBounded(Caps, AcceptedTags, Claims, out Ordered, out Failure); }
			catch (Exception exception)
			{
				Ordered = null;
				return Fail("benefit allocation observation threw "
					+ exception.GetType().Name, out Failure);
			}
		}

		private static bool TryAllocateBounded(IReadOnlyList<KindAmount> Caps,
			IReadOnlyList<string> AcceptedTags, IReadOnlyList<KingdomBenefitAllocationClaim> Claims,
			out List<KingdomBenefitAllocationClaim> Ordered, out string Failure)
		{
			Ordered = null; Failure = null;
			if (Caps == null || AcceptedTags == null || Claims == null)
				return Fail("benefit allocation roster is absent or over-bound", out Failure);
			int capCount = Caps.Count, tagCount = AcceptedTags.Count, claimCount = Claims.Count;
			if (capCount > KingdomDesignationRules.MaxCapsPerDesignation
				|| tagCount > KingdomDesignationRules.MaxTagsPerDesignation
				|| claimCount > KingdomBenefitEmbodimentRules.MaxProvidersPerZone)
				return Fail("benefit allocation roster is absent or over-bound", out Failure);
			List<KindAmount> capRows = new List<KindAmount>(capCount);
			List<string> acceptedRows = new List<string>(tagCount);
			List<KingdomBenefitAllocationClaim> claimRows =
				new List<KingdomBenefitAllocationClaim>(claimCount);
			for (int i = 0; i < capCount; i++) capRows.Add(Caps[i]);
			for (int i = 0; i < tagCount; i++) acceptedRows.Add(AcceptedTags[i]);
			for (int i = 0; i < claimCount; i++)
			{
				KingdomBenefitAllocationClaim claim = Claims[i]; claimRows.Add(claim);
				if (claim == null || claim.ActiveAmounts == null || claim.ActiveTags == null
					|| claim.Credited == null || claim.CreditedTags == null
					|| claim.ActiveAmounts.Count > KingdomBenefitProviderRules.MaxBenefitRows
					|| claim.ActiveTags.Count > KingdomBenefitProviderRules.MaxBenefitRows
					|| !Bounded(claim.StableKey) || !Token(claim.DesignationIdentity, 256))
					return Fail("benefit allocation claim is malformed or over-bound", out Failure);
			}
			Dictionary<string, int> caps = new Dictionary<string, int>(StringComparer.Ordinal);
			if (!TryAmounts(capRows, caps, out Failure)) return false;
			HashSet<string> accepted = new HashSet<string>(StringComparer.Ordinal);
			if (!TryTags(acceptedRows, accepted, out Failure)) return false;
			List<List<KindAmount>> normalizedAmounts = new List<List<KindAmount>>(claimCount);
			List<List<string>> normalizedTags = new List<List<string>>(claimCount);
			List<string> orderKeys = new List<string>(claimCount);
			for (int i = 0; i < claimCount; i++)
			{
				Dictionary<string, int> amounts = new Dictionary<string, int>(StringComparer.Ordinal);
				if (!TryAmounts(claimRows[i].ActiveAmounts, amounts, out Failure)) return false;
				HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);
				if (!TryTags(claimRows[i].ActiveTags, tags, out Failure)) return false;
				List<KindAmount> amountRows = AmountRows(amounts);
				List<string> tagRows = TagRows(tags);
				normalizedAmounts.Add(amountRows); normalizedTags.Add(tagRows);
				orderKeys.Add(ClaimKey(claimRows[i].StableKey,
					claimRows[i].DesignationIdentity, amountRows, tagRows));
			}
			for (int i = 0; i < claimCount; i++)
			{
				claimRows[i].ActiveAmounts = normalizedAmounts[i];
				claimRows[i].ActiveTags = normalizedTags[i];
				claimRows[i].OrderKey = orderKeys[i];
			}
			Ordered = new List<KingdomBenefitAllocationClaim>(claimRows);
			Ordered.Sort((a, b) => string.CompareOrdinal(a.OrderKey, b.OrderKey));
			Dictionary<string, int> used = new Dictionary<string, int>(StringComparer.Ordinal);
			HashSet<string> suppliedTags = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Ordered.Count; i++)
			{
				KingdomBenefitAllocationClaim claim = Ordered[i];
				claim.Credited.Clear(); claim.CreditedTags.Clear(); claim.Limited = false;
				claim.OutsideContract = false; claim.Saturated = false;
				for (int a = 0; a < claim.ActiveAmounts.Count; a++)
				{
					KindAmount offered = claim.ActiveAmounts[a];
					if (!caps.TryGetValue(offered.Kind, out int cap))
					{
						claim.OutsideContract = true; claim.Limited = true; continue;
					}
					used.TryGetValue(offered.Kind, out int prior);
					int room = cap > prior ? cap - prior : 0;
					int credited = offered.Amount < room ? offered.Amount : room;
					if (credited < offered.Amount)
					{
						claim.Saturated = true; claim.Limited = true;
					}
					if (credited <= 0) continue;
					used[offered.Kind] = prior + credited;
					claim.Credited.Add(new KindAmount(offered.Kind, credited));
				}
				for (int t = 0; t < claim.ActiveTags.Count; t++)
				{
					string tag = claim.ActiveTags[t];
					if (!accepted.Contains(tag))
					{
						claim.OutsideContract = true; claim.Limited = true;
					}
					else if (!suppliedTags.Add(tag))
					{
						claim.Saturated = true; claim.Limited = true;
					}
					else claim.CreditedTags.Add(tag);
				}
			}
			return true;
		}

		internal static string BoundDetail(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return Value;
			StringBuilder result = new StringBuilder(Math.Min(Value.Length, MaxDetailChars));
			for (int i = 0; i < Value.Length && result.Length < MaxDetailChars; i++)
				result.Append(char.IsControl(Value[i]) ? ' ' : Value[i]);
			return result.ToString();
		}

		internal static string DeclarationKey(KingdomBenefitProviderDeclaration Row)
		{
			if (Row == null) return "<malformed-declaration>";
			StringBuilder result = new StringBuilder(); Frame(result, Row.Key);
			result.Append(((int)Row.Scope).ToString(CultureInfo.InvariantCulture)).Append('|')
				.Append(((int)Row.Operation).ToString(CultureInfo.InvariantCulture)).Append('|');
			Frame(result, Row.NetworkKey); Append(result, Row.Carries); Append(result, Row.Provides);
			return result.ToString();
		}

		private static bool TryAmounts(IReadOnlyList<KindAmount> Rows,
			Dictionary<string, int> Result, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Rows.Count; i++)
			{
				string kind = (Rows[i].Kind ?? "").Trim().ToLowerInvariant();
				if (!Token(kind, 64) || Rows[i].Amount <= 0 || Result.ContainsKey(kind))
					return Fail("benefit amount rows are malformed or duplicated", out Failure);
				Result.Add(kind, Rows[i].Amount);
			}
			return true;
		}

		private static bool TryTags(IReadOnlyList<string> Rows, HashSet<string> Result,
			out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Rows.Count; i++)
			{
				string tag = (Rows[i] ?? "").Trim().ToLowerInvariant();
				if (!Token(tag, 128) || !Result.Add(tag))
					return Fail("benefit tag rows are malformed or duplicated", out Failure);
			}
			return true;
		}

		private static List<KindAmount> AmountRows(Dictionary<string, int> Source)
		{
			List<KindAmount> result = new List<KindAmount>();
			foreach (KeyValuePair<string, int> row in Source)
				result.Add(new KindAmount(row.Key, row.Value));
			result.Sort((a, b) => string.CompareOrdinal(a.Kind, b.Kind)); return result;
		}

		private static List<string> TagRows(HashSet<string> Source)
		{
			List<string> result = new List<string>(Source);
			result.Sort(StringComparer.Ordinal); return result;
		}

		private static string ClaimKey(string StableKey, string DesignationIdentity,
			IList<KindAmount> Amounts, IList<string> Tags)
		{
			StringBuilder result = new StringBuilder();
			Frame(result, StableKey); Frame(result, DesignationIdentity);
			for (int i = 0; i < Amounts.Count; i++)
			{
				Frame(result, Amounts[i].Kind);
				result.Append(Amounts[i].Amount.ToString(
					CultureInfo.InvariantCulture)).Append('|');
			}
			for (int i = 0; i < Tags.Count; i++) Frame(result, Tags[i]);
			return result.ToString();
		}

		private static void Append(StringBuilder Into, IList<KindAmount> Rows)
		{
			for (int i = 0; i < Rows.Count; i++)
			{
				Frame(Into, Rows[i].Kind); Into.Append(Rows[i].Amount.ToString(
					CultureInfo.InvariantCulture)).Append('|');
			}
		}
		private static void Append(StringBuilder Into, IList<string> Rows)
		{
			for (int i = 0; i < Rows.Count; i++) Frame(Into, Rows[i]);
		}
		private static void Frame(StringBuilder Into, string Value)
		{
			string value = Value ?? ""; Into.Append(value.Length.ToString(
				CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|');
		}
		private static bool Bounded(string Value) => !string.IsNullOrEmpty(Value)
			&& Value.Length <= MaxStableKeyChars;
		private static bool Token(string Value, int Maximum) =>
			KingdomDesignationRules.SafeToken(Value, Maximum);
		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
