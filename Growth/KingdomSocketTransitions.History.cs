using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomSocketTransitions
	{
		private static readonly Dictionary<string, KingdomSocketTransition> byHistoricalDigest =
			new Dictionary<string, KingdomSocketTransition>(StringComparer.Ordinal);

		private static void ReadRetained(XmlDataHelper Xml)
		{
			bool valid = KingdomSocketTransitionRules.TryParse(Xml.GetAttribute("Key"),
				Xml.GetAttribute("From"), Xml.GetAttribute("To"), Xml.GetAttribute("Type"),
				Xml.GetAttribute("Size"), Xml.GetAttribute("Mode"), Xml.GetAttribute("Water"),
				Xml.GetAttribute("Materials"), Xml.GetAttribute("Ticks"), out var declaration,
				out string failure);
			Xml.DoneWithElement();
			if (!valid || !KingdomSocketTransitionRules.TryDeclarationDigest(declaration, out string digest))
			{
				MetricsManager.LogError("ThousandAndFirst retained transition: " + failure);
				return;
			}
			if (byHistoricalDigest.ContainsKey(digest)) return;
			if (byHistoricalDigest.Count >= KingdomSocketTransitionRules.MaxTransitions)
			{
				MetricsManager.LogError("ThousandAndFirst retained transition limit exceeded");
				return;
			}
			byHistoricalDigest.Add(digest, declaration);
		}

		private static bool TryRetained(string Digest, KingdomArchitectureIntent Before,
			KingdomArchitectureIntent After, out KingdomSocketTransition Declaration)
		{
			Declaration = null;
			if (Digest == null || !byHistoricalDigest.TryGetValue(Digest, out var retained)
				|| retained.FromBuildKey != Before.BuildKey || retained.ToBuildKey != After.BuildKey
				|| retained.LotType != Before.LotType || retained.LotSize != Before.LotSize)
				return false;
			return KingdomSocketTransitionRules.TrySnapshot(retained, out Declaration);
		}
	}
}
