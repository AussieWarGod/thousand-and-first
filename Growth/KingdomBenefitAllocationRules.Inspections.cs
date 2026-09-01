using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomBenefitAllocationRules
	{
		internal static bool TryOrderInspections(IReadOnlyList<KingdomBenefitInspectionOrderRow> Rows,
			out List<KingdomBenefitInspectionOrderRow> Ordered, out string Failure)
		{
			try { return TryOrderInspectionsBounded(Rows, out Ordered, out Failure); }
			catch (Exception exception)
			{
				Ordered = null;
				return Fail("benefit inspection observation threw "
					+ exception.GetType().Name, out Failure);
			}
		}

		private static bool TryOrderInspectionsBounded(
			IReadOnlyList<KingdomBenefitInspectionOrderRow> Rows,
			out List<KingdomBenefitInspectionOrderRow> Ordered, out string Failure)
		{
			Ordered = null; Failure = null;
			if (Rows == null) return Fail(
				"benefit inspection roster is absent or over-bound", out Failure);
			int rowCount = Rows.Count;
			if (rowCount > MaxInspectionRows)
				return Fail("benefit inspection roster is absent or over-bound", out Failure);
			List<KingdomBenefitInspectionOrderRow> snapshot =
				new List<KingdomBenefitInspectionOrderRow>(rowCount);
			for (int i = 0; i < rowCount; i++) snapshot.Add(Rows[i]);
			for (int i = 0; i < snapshot.Count; i++)
			{
				KingdomBenefitInspectionOrderRow row = snapshot[i];
				if (row == null || row.Inspection == null || !Bounded(row.IdentityBase)
					|| !Bounded(row.StableAnchor) || !ValidInspection(row.Inspection))
					return Fail("benefit inspection row is malformed or over-bound", out Failure);
				row.OrderKey = InspectionKey(row.StableAnchor, row.Inspection);
			}
			Ordered = new List<KingdomBenefitInspectionOrderRow>(snapshot);
			Ordered.Sort((a, b) =>
			{
				int key = string.CompareOrdinal(a.OrderKey, b.OrderKey);
				return key != 0 ? key : string.CompareOrdinal(a.IdentityBase, b.IdentityBase);
			});
			Dictionary<string, int> totals = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < Ordered.Count; i++)
				totals[Ordered[i].IdentityBase] = totals.TryGetValue(Ordered[i].IdentityBase,
					out int count) ? count + 1 : 1;
			Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < Ordered.Count; i++)
			{
				string identity = Ordered[i].IdentityBase;
				seen[identity] = seen.TryGetValue(identity, out int count) ? count + 1 : 1;
				Ordered[i].Inspection.ProviderIdentity = totals[identity] == 1 ? identity
					: identity + ":instance-" + seen[identity].ToString("D4",
						CultureInfo.InvariantCulture);
			}
			return true;
		}

		private static string InspectionKey(string Anchor, KingdomBenefitInspection Row)
		{
			StringBuilder result = new StringBuilder(); Frame(result, Anchor);
			Frame(result, Row.ProviderKey); Frame(result, Row.DesignationIdentity);
			result.Append(((int)Row.Fault).ToString(CultureInfo.InvariantCulture)).Append('|')
				.Append(Row.OperationPercent.ToString(CultureInfo.InvariantCulture)).Append('|')
				.Append(Row.LimitedByDesignation ? '1' : '0').Append('|')
				.Append(Row.OutsideDesignationContract ? '1' : '0').Append('|')
				.Append(Row.SaturatedByDesignation ? '1' : '0').Append('|');
			Frame(result, Row.Detail);
			Append(result, Row.Offered); Append(result, Row.Credited);
			Append(result, Row.Tags); Append(result, Row.CreditedTags); return result.ToString();
		}

		private static bool ValidInspection(KingdomBenefitInspection Row)
		{
			return Row.Offered != null && Row.Credited != null && Row.Tags != null
				&& Row.CreditedTags != null
				&& Row.Offered.Count <= KingdomBenefitProviderRules.MaxBenefitRows
				&& Row.Credited.Count <= KingdomBenefitProviderRules.MaxBenefitRows
				&& Row.Tags.Count <= KingdomBenefitProviderRules.MaxBenefitRows
				&& Row.CreditedTags.Count <= KingdomBenefitProviderRules.MaxBenefitRows
				&& (Row.Detail?.Length ?? 0) <= MaxDetailChars;
		}
	}
}
