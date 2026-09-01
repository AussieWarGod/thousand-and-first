using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCatalogueRules
	{
		// --- The Carries list -------------------------------------------------------------------

		/// <summary>
		/// Reads a <c>support:settlers</c> comma list. Whitespace anywhere is ignored, kinds are
		/// folded to lower case, and an empty attribute is an empty list rather than a fault.
		/// <para>
		/// Deliberately more forgiving than <c>KingdomMaterialRules.TryParseMaterialCost</c>, which
		/// refuses a kind it does not know. A material the settlement cannot hold is a cost nobody
		/// can ever pay; a support this file has not heard of is somebody else's good, and it
		/// lifts.
		/// </para>
		/// </summary>
		/// <param name="Source">The raw attribute, or null.</param>
		/// <param name="Tally">The pairs, in the order written. Never null; empty when
		/// <paramref name="Source"/> was.</param>
		/// <param name="Error">Null on success, else the first thing wrong. On failure the tally
		/// holds every pair that parsed before the bad one, so a caller that logs and carries on
		/// is not silently credited with nothing.</param>
		public static bool TryParseTally(string Source, out List<KindAmount> Tally, out string Error)
		{
			Tally = new List<KindAmount>();
			Error = null;
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return true;
			}
			string[] parts = Source.Split(ListSeparator);
			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i].Trim();
				if (part.Length == 0)
				{
					continue;
				}
				int split = part.IndexOf(AmountSeparator);
				if (split <= 0 || split >= part.Length - 1)
				{
					Error = "\"" + part + "\" is not a support and an amount";
					return false;
				}
				string kind = Fold(part.Substring(0, split));
				string amount = part.Substring(split + 1).Trim();
				if (kind == null)
				{
					Error = "\"" + part + "\" names no support";
					return false;
				}
				if (!int.TryParse(amount, out var value) || value < 0)
				{
					Error = "\"" + part + "\" has a bad amount";
					return false;
				}
				Tally.Add(new KindAmount(kind, value));
			}
			return true;
		}

		/// <summary>How much of one support a parsed tally holds. Repeats add, so
		/// <c>water:2,water:3</c> is five.</summary>
		public static int AmountOf(List<KindAmount> Tally, string Kind)
		{
			if (Tally == null)
			{
				return 0;
			}
			string kind = Fold(Kind);
			int total = 0;
			for (int i = 0; i < Tally.Count; i++)
			{
				if (Tally[i].Kind == kind)
				{
					total = SaturatingCounterAdd(total, Tally[i].Amount);
				}
			}
			return total;
		}

		/// <summary>Everything in a parsed tally that is not a binding support, summed &mdash; the
		/// <c>Lift</c> argument to <see cref="Equilibrium"/>. An unknown kind lifts.</summary>
		public static int LiftOf(List<KindAmount> Tally)
		{
			if (Tally == null)
			{
				return 0;
			}
			int total = 0;
			for (int i = 0; i < Tally.Count; i++)
			{
				if (!IsBindingSupport(Tally[i].Kind))
				{
					total = SaturatingCounterAdd(total, Tally[i].Amount);
				}
			}
			return total;
		}

	}
}
