#if TAF_CONSTRUCTION_INPUT_PORTABLE
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Portable-harness display helpers omitted from the routed-input dependency slice.</summary>
	public static partial class KingdomMaterialRules
	{
		public static bool IsRefined(KingdomMaterial material)
		{
			return material == KingdomMaterial.ShapedTimber
				|| material == KingdomMaterial.ShapedStone
				|| material == KingdomMaterial.WorkedMetal;
		}

		public static string JoinPhrases(List<string> parts)
		{
			if (parts == null || parts.Count == 0) return null;
			if (parts.Count == 1) return parts[0];
			if (parts.Count == 2) return parts[0] + " and " + parts[1];
			return string.Join(", ", parts.GetRange(0, parts.Count - 1).ToArray())
				+ ", and " + parts[parts.Count - 1];
		}
	}
}
#endif
