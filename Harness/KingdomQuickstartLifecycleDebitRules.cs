using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free expectation for the material debit of one paid commission, proved by value in
	/// DevTests/KingdomQuickstartLifecycleDebitRulesTests.cs (both public projects).
	/// <para>
	/// RUN 46b (investigation-run46b-47.md A). lifecycle-next demanded an exact one-timber drop in
	/// the ONE store the harness was watching, but production's commission debit plans over EVERY
	/// dedicated stockpile's MaterialStock (Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs
	/// Stock(Z) -> StockInLocalPass adds every IsStockpile candidate; Growth/KingdomMaterialDebit.cs
	/// Reserve/TryPlan choose sources across that zone-wide row set). With two dedicated stores
	/// (the camp chest and the heart's own store) the payer can lawfully be the other one, and a
	/// thawed zone's row order after a cold load makes that likely. The honest census is the
	/// zone-wide total across every dedicated store: it must drop by exactly the design's bill,
	/// no store may gain, and the store set itself must not change across the call.
	/// </para>
	/// </summary>
	internal static class KingdomQuickstartLifecycleDebitRules
	{
		/// <summary>True when the per-store counts, taken before and after in the same store
		/// order, show exactly <paramref name="ExpectedDrop" /> units gone in total, spread across
		/// any stores, with no store gaining and no store added or removed.
		/// <para>PR #183 review: a store whose stock could not be read is NOT a zero. Its failure
		/// text (parallel lists <paramref name="BeforeFailures" /> / <paramref name="AfterFailures" />,
		/// null where the read succeeded) refuses FIRST, before any arithmetic, so an unmeasured
		/// store can neither fabricate a gain nor let a balanced total pass as proof.</para></summary>
		internal static bool Judge(IReadOnlyList<string> Ids, IReadOnlyList<int> Before,
			IReadOnlyList<int> After, IReadOnlyList<string> BeforeFailures,
			IReadOnlyList<string> AfterFailures, int ExpectedDrop, out string Failure)
		{
			Failure = null;
			if (Ids == null || Before == null || After == null
				|| Ids.Count != Before.Count || Before.Count != After.Count)
			{
				Failure = "the dedicated store set changed across the commission";
				return false;
			}
			for (int i = 0; i < Ids.Count; i++)
			{
				string unread = Unread(BeforeFailures, i) ?? Unread(AfterFailures, i);
				if (unread == null) continue;
				Failure = "dedicated store " + (string.IsNullOrEmpty(Ids[i]) ? "unassigned" : Ids[i])
					+ " could not be stocked: " + unread;
				return false;
			}
			if (Ids.Count == 0) { Failure = "no dedicated store stood to pay from"; return false; }
			int totalBefore = 0, totalAfter = 0;
			for (int i = 0; i < Ids.Count; i++)
			{
				if (After[i] > Before[i])
				{
					Failure = "store " + Ids[i] + " GAINED timber across the commission ("
						+ Before[i] + " -> " + After[i] + ")";
					return false;
				}
				totalBefore += Before[i];
				totalAfter += After[i];
			}
			if (totalBefore - totalAfter != ExpectedDrop)
			{
				Failure = "timber across " + Ids.Count + " dedicated store(s) moved by "
					+ (totalBefore - totalAfter) + ", not the design's exact " + ExpectedDrop
					+ " (" + Describe(Ids, Before, After) + ")";
				return false;
			}
			return true;
		}

		private static string Unread(IReadOnlyList<string> Failures, int Index)
		{
			if (Failures == null || Index >= Failures.Count) return null;
			return string.IsNullOrEmpty(Failures[Index]) ? null : Failures[Index];
		}

		/// <summary>Every store's count before and after, in one ASCII clause.</summary>
		internal static string Describe(IReadOnlyList<string> Ids, IReadOnlyList<int> Before,
			IReadOnlyList<int> After)
		{
			StringBuilder text = new StringBuilder();
			int totalBefore = 0, totalAfter = 0;
			for (int i = 0; i < Ids.Count; i++)
			{
				if (i > 0) text.Append(' ');
				text.Append("store=").Append(string.IsNullOrEmpty(Ids[i]) ? "unassigned" : Ids[i])
					.Append(" timber=").Append(Before[i]).Append("->").Append(After[i]);
				totalBefore += Before[i];
				totalAfter += After[i];
			}
			return "stores=" + Ids.Count + " timberBefore=" + totalBefore + " timberAfter="
				+ totalAfter + (Ids.Count > 0 ? " " : "") + text;
		}
	}
}
