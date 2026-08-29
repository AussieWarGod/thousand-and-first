using System;

namespace ThousandAndFirst
{
	public enum KingdomRemovalCarrierDisposition : byte
	{
		Unknown = 0,
		PreserveResidue = 1,
		ExactValueRelease = 2,
		PlayerTerminalCut = 3
	}

	public enum KingdomRemovalGlobalDisposition : byte
	{
		Unknown = 0,
		Preserve = 1,
		EmptyOnly = 2,
		ExactCurrentRealmClear = 3,
		TerminalMarkerCut = 4
	}

	public static partial class KingdomRemovalCoverage
	{
		/// <summary>No generic IPart removal is callback-safe. Only owner-specific receipts may cut.</summary>
		public static KingdomRemovalCarrierDisposition CarrierDisposition(string Name)
		{
			if (Name == "KingdomCharterPart")
				return KingdomRemovalCarrierDisposition.PlayerTerminalCut;
			if (Name == "r_KingdomRelocationFrame"
				|| Name == "r_KingdomStasisCustody"
				|| Name == "r_KingdomStasisFieldAnchor"
				|| Name == "r_KingdomStasisProjection"
				|| Name == "r_KingdomStasisVault"
				|| Name == "r_KingdomWitnessWorkProjection")
				return KingdomRemovalCarrierDisposition.ExactValueRelease;
			return IsCustomPart(Name)
				? KingdomRemovalCarrierDisposition.PreserveResidue
				: KingdomRemovalCarrierDisposition.Unknown;
		}

		/// <summary>Value-bearing namespaces preserve by default; no key-only deletion is lawful.</summary>
		public static KingdomRemovalGlobalDisposition GlobalDisposition(string Name)
		{
			if (Name == "r_TAF_SaveSystemRoster_v1")
				return KingdomRemovalGlobalDisposition.TerminalMarkerCut;
			if (Name == "r_TAF_Inheritance")
				return KingdomRemovalGlobalDisposition.EmptyOnly;
			if (Contains(HostedArcologyAuthorityStates, Name))
				return KingdomRemovalGlobalDisposition.ExactCurrentRealmClear;
			return IsOwnedGlobalState(Name)
				? KingdomRemovalGlobalDisposition.Preserve
				: KingdomRemovalGlobalDisposition.Unknown;
		}
	}
}
