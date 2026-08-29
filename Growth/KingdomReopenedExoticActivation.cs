using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Ephemeral catalogue keys for reopened exotic works. These keys are observations of current
	/// seated ground, never knowledge written into a city's permanent keepers' roll.
	/// </summary>
	internal static partial class KingdomReopenedExoticActivation
	{
		internal const string StasisVaultKey = "node:stasisvault-runtime-v1";
		internal const string AssentingMootKey = "node:assentingmoot-runtime-v1";

		internal static void AppendDerivedKeys(KingdomSystem System, List<string> Roster)
		{
			if (Roster == null) return;
			if (StasisVaultEligible(System, Roster) && !Roster.Contains(StasisVaultKey))
				Roster.Add(StasisVaultKey);
			AppendAssentingMoot(System, Roster);
		}

		static partial void AppendAssentingMoot(KingdomSystem System, List<string> Roster);
	}
}
