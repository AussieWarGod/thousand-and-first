using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// A count of rare finds: what a great work is short of, and what the stockpiles hold. Never
	/// serialized, for <see cref="KingdomMaterialTally"/>'s own reason.
	/// </summary>
	public sealed class KingdomExoticTally
	{
		private readonly int[] Amounts = new int[KingdomMaterialRules.ExoticCount];

		/// <summary>Units of one exotic held. Never negative.</summary>
		public int Get(KingdomExotic Exotic)
		{
			int index = (int)Exotic;
			return (index < 0 || index >= Amounts.Length) ? 0 : Amounts[index];
		}

		/// <summary>Adds units of one exotic, clamping at zero rather than going negative.
		/// </summary>
		public void Add(KingdomExotic Exotic, int Units)
		{
			int index = (int)Exotic;
			if (index < 0 || index >= Amounts.Length)
			{
				return;
			}
			long total = (long)Amounts[index] + Units;
			Amounts[index] = (total <= 0L) ? 0 : ((total >= int.MaxValue) ? int.MaxValue : (int)total);
		}

		/// <summary>Sets units of one exotic outright, clamping negatives to zero.</summary>
		public void Set(KingdomExotic Exotic, int Units)
		{
			int index = (int)Exotic;
			if (index < 0 || index >= Amounts.Length)
			{
				return;
			}
			Amounts[index] = (Units > 0) ? Units : 0;
		}

		/// <summary>Units of every exotic added together.</summary>
		public int Total()
		{
			long total = 0L;
			for (int i = 0; i < Amounts.Length; i++)
			{
				total += Amounts[i];
			}
			return (total >= int.MaxValue) ? int.MaxValue : (int)total;
		}

		/// <summary>True when nothing rare is held or wanted.</summary>
		public bool IsEmpty()
		{
			return Total() == 0;
		}

		/// <summary>An independent copy; mutating the copy never touches this tally.</summary>
		public KingdomExoticTally Copy()
		{
			KingdomExoticTally copy = new KingdomExoticTally();
			for (int i = 0; i < Amounts.Length; i++)
			{
				copy.Amounts[i] = Amounts[i];
			}
			return copy;
		}

		/// <summary>Player-facing prose: "2 gold nuggets and 1 rough gemstone", or null when
		/// empty.</summary>
		public string Describe()
		{
			List<string> parts = new List<string>();
			for (int i = 0; i < Amounts.Length; i++)
			{
				if (Amounts[i] > 0)
				{
					parts.Add(Amounts[i] + " " + KingdomMaterialRules.ExoticName((KingdomExotic)i, Amounts[i]));
				}
			}
			return KingdomMaterialRules.JoinPhrases(parts);
		}
	}
}
