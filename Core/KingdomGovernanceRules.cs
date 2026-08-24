using System;

namespace ThousandAndFirst
{
	/// <summary>The semantic result of one founder-facing civic interaction.</summary>
	public enum KingdomGovernanceResult : byte
	{
		Read = 0,
		Cancelled = 1,
		Failed = 2,
		Bookkeeping = 3,
		Committed = 4
	}

	/// <summary>Engine-free rules for the Charter's one-commit action boundary.</summary>
	public static class KingdomGovernanceRules
	{
		/// <summary>Qud's nominal cost for one ordinary action. Engine energy modifiers still
		/// own the raw delta.</summary>
		public const int NominalEnergyCost = 1000;

		public const string EnergyPrefix = "TAF Governance ";

		public static bool Charges(KingdomGovernanceResult Result)
		{
			return Result == KingdomGovernanceResult.Committed;
		}

		public static bool ClosesInterface(KingdomGovernanceResult Result)
		{
			return Result == KingdomGovernanceResult.Committed;
		}

		public static string EnergyReason(string Verb)
		{
			string verb = (Verb ?? "act").Trim();
			if (verb.Length == 0)
			{
				verb = "act";
			}
			return EnergyPrefix + verb;
		}
	}
}
