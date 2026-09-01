using System;

namespace ThousandAndFirst
{
	/// <summary>Pure vocabulary and admissibility laws for authored physical transitions.</summary>
	public static class KingdomArchitectureTransitionRules
	{
		public static bool TryParseMode(string Text, out ArchitectureTransitionMode Mode)
		{
			Mode = ArchitectureTransitionMode.None;
			if (string.IsNullOrWhiteSpace(Text)) return false;
			switch (Text.Trim().ToLowerInvariant())
			{
			case "none": Mode = ArchitectureTransitionMode.None; return true;
			case "additive": Mode = ArchitectureTransitionMode.Additive; return true;
			case "additive-expand":
				Mode = ArchitectureTransitionMode.AdditiveExpand; return true;
			case "renovate": Mode = ArchitectureTransitionMode.Renovate; return true;
			case "renovate-expand":
				Mode = ArchitectureTransitionMode.RenovateExpand; return true;
			case "replacement": Mode = ArchitectureTransitionMode.Replacement; return true;
			default: return false;
			}
		}

		public static string ModeKey(ArchitectureTransitionMode Mode)
		{
			switch (Mode)
			{
			case ArchitectureTransitionMode.None: return "none";
			case ArchitectureTransitionMode.Additive: return "additive";
			case ArchitectureTransitionMode.AdditiveExpand: return "additive-expand";
			case ArchitectureTransitionMode.Renovate: return "renovate";
			case ArchitectureTransitionMode.RenovateExpand: return "renovate-expand";
			case ArchitectureTransitionMode.Replacement: return "replacement";
			default: return null;
			}
		}

		public static bool IsKnown(ArchitectureTransitionMode Mode)
		{
			return ModeKey(Mode) != null;
		}

		public static bool IsInPlace(ArchitectureTransitionMode Mode)
		{
			return Mode == ArchitectureTransitionMode.Additive
				|| Mode == ArchitectureTransitionMode.AdditiveExpand
				|| Mode == ArchitectureTransitionMode.Renovate
				|| Mode == ArchitectureTransitionMode.RenovateExpand;
		}

		public static bool AllowsLotExpansion(ArchitectureTransitionMode Mode)
		{
			return Mode == ArchitectureTransitionMode.AdditiveExpand
				|| Mode == ArchitectureTransitionMode.RenovateExpand;
		}

		public static bool PreservesStandingFabric(ArchitectureTransitionMode Mode)
		{
			return Mode == ArchitectureTransitionMode.Additive
				|| Mode == ArchitectureTransitionMode.AdditiveExpand;
		}

		/// <summary>Base tiers are fresh commissions; every later tier names its incoming edge.</summary>
		public static bool ValidTierMode(int Level, ArchitectureTransitionMode Mode)
		{
			return Level == 0 ? Mode == ArchitectureTransitionMode.None
				: Level > 0 && Mode != ArchitectureTransitionMode.None && IsKnown(Mode);
		}
	}
}
