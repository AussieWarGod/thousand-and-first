using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// A count of each material: what a stockpile holds, what a design costs, what a cleared
	/// rect will yield. Never serialized and never persisted &mdash; a stockpile's contents are
	/// real items in a real container, and a tally is only ever the reading taken of them, the
	/// same way <c>KingdomSurvey.FoodStored</c> is a reading and not a store.
	/// </summary>
	public sealed class KingdomMaterialTally
	{
		private readonly int[] Amounts = new int[KingdomMaterialRules.MaterialCount];

		/// <summary>Units of one material held. Never negative.</summary>
		public int Get(KingdomMaterial Material)
		{
			int index = (int)Material;
			if (index < 0 || index >= Amounts.Length)
			{
				return 0;
			}
			return Amounts[index];
		}

		/// <summary>
		/// Adds units of one material, clamping the result at zero rather than going negative.
		/// </summary>
		/// <param name="Material">Which material.</param>
		/// <param name="Units">May be negative, to take units away.</param>
		public void Add(KingdomMaterial Material, int Units)
		{
			int index = (int)Material;
			if (index < 0 || index >= Amounts.Length)
			{
				return;
			}
			long total = (long)Amounts[index] + Units;
			Amounts[index] = (total <= 0L) ? 0 : ((total >= int.MaxValue) ? int.MaxValue : (int)total);
		}

		/// <summary>Sets units of one material outright, clamping negatives to zero.</summary>
		public void Set(KingdomMaterial Material, int Units)
		{
			int index = (int)Material;
			if (index < 0 || index >= Amounts.Length)
			{
				return;
			}
			Amounts[index] = (Units > 0) ? Units : 0;
		}

		/// <summary>Adds every material of Other into this tally. Null is a no-op.</summary>
		public void AddAll(KingdomMaterialTally Other)
		{
			if (Other == null)
			{
				return;
			}
			for (int i = 0; i < Amounts.Length; i++)
			{
				Add((KingdomMaterial)i, Other.Amounts[i]);
			}
		}

		/// <summary>Units of every material added together.</summary>
		public int Total()
		{
			long total = 0L;
			for (int i = 0; i < Amounts.Length; i++)
			{
				total += Amounts[i];
			}
			return (total >= int.MaxValue) ? int.MaxValue : (int)total;
		}

		/// <summary>True when nothing at all is held. An absent material cost is empty, and an
		/// empty cost is what makes every design written before materials existed still buildable
		/// for water alone.</summary>
		public bool IsEmpty()
		{
			return Total() == 0;
		}

		/// <summary>An independent copy; mutating the copy never touches this tally.</summary>
		public KingdomMaterialTally Copy()
		{
			KingdomMaterialTally copy = new KingdomMaterialTally();
			for (int i = 0; i < Amounts.Length; i++)
			{
				copy.Amounts[i] = Amounts[i];
			}
			return copy;
		}

		/// <summary>
		/// This tally with every material multiplied by Percent and rounded down. Used for
		/// partial salvage; a percentage that would round a single unit away really does round
		/// it away, because a struck hut does not return the beam it was built from whole.
		/// </summary>
		public KingdomMaterialTally Scaled(int Percent)
		{
			KingdomMaterialTally scaled = new KingdomMaterialTally();
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

		/// <summary>Player-facing prose: "8 timber and 4 cut stone", or null when empty.</summary>
		public string Describe()
		{
			List<string> parts = new List<string>();
			for (int i = 0; i < Amounts.Length; i++)
			{
				if (Amounts[i] > 0)
				{
					parts.Add(Amounts[i] + " " + KingdomMaterialRules.MaterialName((KingdomMaterial)i));
				}
			}
			return KingdomMaterialRules.JoinPhrases(parts);
		}

		/// <summary>Units of every REFINED material held together &mdash; what the yards have
		/// made, as against what the ground gave up.</summary>
		public int RefinedTotal()
		{
			int total = 0;
			for (int i = 0; i < Amounts.Length; i++)
			{
				if (KingdomMaterialRules.IsRefined((KingdomMaterial)i))
				{
					total += Amounts[i];
				}
			}
			return total;
		}
	}
}
