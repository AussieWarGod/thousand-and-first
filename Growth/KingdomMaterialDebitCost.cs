using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// One composite claim against the stockpiles. Each tally is copied on construction, so a
	/// caller cannot alter a reserved price by retaining and editing its original tally.
	/// </summary>
	public sealed class KingdomMaterialDebitCost
	{
		public readonly KingdomMaterialTally Materials;
		public readonly KingdomBitTally Bits;
		public readonly KingdomExoticTally Exotics;

		public KingdomMaterialDebitCost(
			KingdomMaterialTally Materials = null,
			KingdomBitTally Bits = null,
			KingdomExoticTally Exotics = null)
		{
			this.Materials = (Materials == null) ? new KingdomMaterialTally() : Materials.Copy();
			this.Bits = (Bits == null) ? new KingdomBitTally() : Bits.Copy();
			this.Exotics = (Exotics == null) ? new KingdomExoticTally() : Exotics.Copy();
		}

		public bool IsEmpty => Materials.IsEmpty() && Bits.IsEmpty() && Exotics.IsEmpty();

		public KingdomMaterialDebitCost Copy()
		{
			return new KingdomMaterialDebitCost(Materials, Bits, Exotics);
		}

		/// <summary>
		/// Stable primitive encoding for a durable construction or lab job. A live receipt contains
		/// engine references and is deliberately not serializable; this claim is what crosses a save.
		/// </summary>
		public string ToClaimString()
		{
			StringBuilder text = new StringBuilder("v1|m:");
			AppendMaterial(text, Materials);
			text.Append("|b:");
			AppendBits(text, Bits);
			text.Append("|e:");
			AppendExotics(text, Exotics);
			return text.ToString();
		}

		public static bool TryParseClaim(string Text, out KingdomMaterialDebitCost Cost)
		{
			Cost = null;
			if (string.IsNullOrEmpty(Text))
			{
				return false;
			}
			string[] fields = Text.Split('|');
			if (fields.Length != 4 || fields[0] != "v1" || !fields[1].StartsWith("m:", StringComparison.Ordinal)
				|| !fields[2].StartsWith("b:", StringComparison.Ordinal)
				|| !fields[3].StartsWith("e:", StringComparison.Ordinal))
			{
				return false;
			}
			int[] material;
			int[] bits;
			int[] exotics;
			if (!TryParseVector(fields[1].Substring(2), KingdomMaterialRules.MaterialCount, out material)
				|| !TryParseVector(fields[2].Substring(2), KingdomMaterialRules.BitTierCount, out bits)
				|| !TryParseVector(fields[3].Substring(2), KingdomMaterialRules.ExoticCount, out exotics))
			{
				return false;
			}
			KingdomMaterialTally materialTally = new KingdomMaterialTally();
			KingdomBitTally bitTally = new KingdomBitTally();
			KingdomExoticTally exoticTally = new KingdomExoticTally();
			for (int i = 0; i < material.Length; i++)
			{
				materialTally.Set((KingdomMaterial)i, material[i]);
			}
			for (int i = 0; i < bits.Length; i++)
			{
				bitTally.Set(i, bits[i]);
			}
			for (int i = 0; i < exotics.Length; i++)
			{
				exoticTally.Set((KingdomExotic)i, exotics[i]);
			}
			Cost = new KingdomMaterialDebitCost(materialTally, bitTally, exoticTally);
			return true;
		}

		private static void AppendMaterial(StringBuilder Into, KingdomMaterialTally Tally)
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				if (i > 0) Into.Append(',');
				Into.Append(Tally.Get((KingdomMaterial)i).ToString(CultureInfo.InvariantCulture));
			}
		}

		private static void AppendBits(StringBuilder Into, KingdomBitTally Tally)
		{
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				if (i > 0) Into.Append(',');
				Into.Append(Tally.Get(i).ToString(CultureInfo.InvariantCulture));
			}
		}

		private static void AppendExotics(StringBuilder Into, KingdomExoticTally Tally)
		{
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				if (i > 0) Into.Append(',');
				Into.Append(Tally.Get((KingdomExotic)i).ToString(CultureInfo.InvariantCulture));
			}
		}

		private static bool TryParseVector(string Text, int Count, out int[] Values)
		{
			Values = null;
			string[] terms = Text.Split(',');
			if (terms.Length != Count)
			{
				return false;
			}
			Values = new int[Count];
			for (int i = 0; i < terms.Length; i++)
			{
				int value;
				if (!int.TryParse(terms[i], NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 0)
				{
					Values = null;
					return false;
				}
				Values[i] = value;
			}
			return true;
		}
	}
}
