using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Pure fold for live designation carries plus attended hosted output.</summary>
	public static class KingdomObservedBenefitProjectionRules
	{
		public static bool TryProject(IList<KindAmount> Live, int HostedRoof,
			int HostedLuxury, int Effectiveness, out List<KindAmount> Projected,
			out string Failure)
		{
			Projected = null; Failure = null;
			if (Live == null || Live.Count > KingdomBenefitProviderRules.MaxBenefitRows
				|| HostedRoof < 0 || HostedLuxury < 0
				|| Effectiveness < 0 || Effectiveness > 100)
				return Fail("observed benefit projection input is invalid", out Failure);
			Dictionary<string, int> amounts = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < Live.Count; i++)
			{
				KindAmount row = Live[i];
				if (!KingdomDesignationRules.SafeToken(row.Kind, 64)
					|| row.Amount <= 0)
					return Fail("live benefit projection row is malformed", out Failure);
				Add(amounts, row.Kind, row.Amount);
			}
			Add(amounts, KingdomCatalogueRules.SupportRoof,
				KingdomCatalogueRules.Carried(HostedRoof, Effectiveness));
			Add(amounts, "luxury", KingdomReachRules.Scaled(HostedLuxury, Effectiveness));
			if (amounts.Count > KingdomBenefitProviderRules.MaxBenefitRows + 2)
				return Fail("observed benefit projection exceeds its row bound", out Failure);
			Projected = new List<KindAmount>();
			foreach (KeyValuePair<string, int> pair in amounts)
				if (pair.Value > 0) Projected.Add(new KindAmount(pair.Key, pair.Value));
			Projected.Sort((a, b) => string.CompareOrdinal(a.Kind, b.Kind));
			return true;
		}

		public static int Amount(IList<KindAmount> Rows, string Kind)
		{
			for (int i = 0; Rows != null && i < Rows.Count; i++)
				if (Rows[i].Kind == Kind) return Math.Max(0, Rows[i].Amount);
			return 0;
		}

		public static int PhysicalLift(IList<KindAmount> Rows)
		{
			long total = 0L;
			for (int i = 0; Rows != null && i < Rows.Count; i++)
				if (Rows[i].Amount > 0
					&& KingdomReachRules.IsPhysicalLift(Rows[i].Kind)) total += Rows[i].Amount;
			return total >= int.MaxValue ? int.MaxValue : (int)total;
		}

		private static void Add(Dictionary<string, int> Into, string Kind, int Amount)
		{
			if (Amount <= 0) return;
			Into.TryGetValue(Kind, out int prior);
			long sum = (long)prior + Amount;
			Into[Kind] = sum >= int.MaxValue ? int.MaxValue : (int)sum;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
