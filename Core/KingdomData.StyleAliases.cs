using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomData
	{
		/// <summary>Canonical and compatibility keys accepted for one merged style.</summary>
		public static IList<string> StyleKeys(string Style)
		{
			EnsureLoaded();
			return KingdomStyleRules.KeysFor(_styleDefinitions, Style);
		}

		/// <summary>Alias-aware catalogue style-tag match.</summary>
		public static bool StyleTagAccepts(string Expression, string Style)
		{
			EnsureLoaded();
			return KingdomStyleRules.TagAccepts(_styleDefinitions, Expression, Style);
		}
	}
}
