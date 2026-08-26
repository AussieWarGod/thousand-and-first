using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// A count of bits by tier: what a design costs in tinkering stock, and what the stockpiles
	/// hold of it. Never serialized, for the same reason <see cref="KingdomMaterialTally"/> is not
	/// &mdash; the settlement's bits are real items in a real container, and this is only ever the
	/// reading taken of them. A founder donates bits by putting the scrap in the stockpile.
	/// </summary>
	public sealed class KingdomBitTally
	{
		private readonly int[] Amounts = new int[KingdomMaterialRules.BitTierCount];

		/// <summary>Bits of one tier held. Never negative; a tier outside the ladder is zero.
		/// </summary>
		public int Get(int Tier)
		{
			return (Tier < 0 || Tier >= Amounts.Length) ? 0 : Amounts[Tier];
		}

		/// <summary>Adds bits of one tier, clamping at zero rather than going negative.</summary>
		public void Add(int Tier, int Count)
		{
			if (Tier < 0 || Tier >= Amounts.Length)
			{
				return;
			}
			long total = (long)Amounts[Tier] + Count;
			Amounts[Tier] = (total <= 0L) ? 0 : ((total >= int.MaxValue) ? int.MaxValue : (int)total);
		}

		/// <summary>Sets bits of one tier outright, clamping negatives to zero.</summary>
		public void Set(int Tier, int Count)
		{
			if (Tier < 0 || Tier >= Amounts.Length)
			{
				return;
			}
			Amounts[Tier] = (Count > 0) ? Count : 0;
		}

		/// <summary>Adds every tier of Other into this tally. Null is a no-op.</summary>
		public void AddAll(KingdomBitTally Other)
		{
			if (Other == null)
			{
				return;
			}
			for (int i = 0; i < Amounts.Length; i++)
			{
				Add(i, Other.Amounts[i]);
			}
		}

		/// <summary>Bits of every tier added together.</summary>
		public int Total()
		{
			long total = 0L;
			for (int i = 0; i < Amounts.Length; i++)
			{
				total += Amounts[i];
			}
			return (total >= int.MaxValue) ? int.MaxValue : (int)total;
		}

		/// <summary>True when no bits at all are held. An absent bit cost is empty, and an empty
		/// cost is what every design written before bits existed goes on costing.</summary>
		public bool IsEmpty()
		{
			return Total() == 0;
		}

		/// <summary>An independent copy; mutating the copy never touches this tally.</summary>
		public KingdomBitTally Copy()
		{
			KingdomBitTally copy = new KingdomBitTally();
			for (int i = 0; i < Amounts.Length; i++)
			{
				copy.Amounts[i] = Amounts[i];
			}
			return copy;
		}

		/// <summary>This tally with every tier multiplied by Percent and rounded down. Used for
		/// the share of a design's bits a repair puts back.</summary>
		public KingdomBitTally Scaled(int Percent)
		{
			KingdomBitTally scaled = new KingdomBitTally();
			if (Percent <= 0)
			{
				return scaled;
			}
			for (int i = 0; i < Amounts.Length; i++)
			{
				long amount = (long)Amounts[i] * Percent / 100L;
				scaled.Amounts[i] = (amount >= int.MaxValue) ? int.MaxValue : (int)amount;
			}
			return scaled;
		}

		/// <summary>Player-facing prose: "2 of scrap and 1 of pure alloy", or null when empty.
		/// </summary>
		public string Describe()
		{
			List<string> parts = new List<string>();
			for (int i = 0; i < Amounts.Length; i++)
			{
				if (Amounts[i] > 0)
				{
					parts.Add(Amounts[i] + " of " + KingdomMaterialRules.BitTierName(i));
				}
			}
			return KingdomMaterialRules.JoinPhrases(parts);
		}
	}
}
