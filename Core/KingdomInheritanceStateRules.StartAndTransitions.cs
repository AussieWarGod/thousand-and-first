using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritanceStateRules
	{
		internal static bool ShouldOffer(string GameMode, bool TutorialActive)
		{
			return !TutorialActive
				&& !string.Equals(GameMode, "Tutorial", StringComparison.Ordinal)
				&& !string.Equals(GameMode, "Daily", StringComparison.Ordinal)
				&& !string.Equals(GameMode, KingdomQuickstartRules.ModeId,
					StringComparison.Ordinal);
		}

		internal static KingdomInheritanceStartFault ValidateStart(string TargetZoneId,
			string StartWorld, string StartZoneId)
		{
			if (string.IsNullOrEmpty(StartWorld) || string.IsNullOrEmpty(StartZoneId))
			{
				return KingdomInheritanceStartFault.MissingStart;
			}
			if (!string.Equals(StartWorld, KingdomInheritanceSiteRules.WorldId, StringComparison.Ordinal))
			{
				return KingdomInheritanceStartFault.AlternateWorld;
			}
			if (string.Equals(TargetZoneId, StartZoneId, StringComparison.Ordinal))
			{
				return KingdomInheritanceStartFault.TargetIsStart;
			}
			return KingdomInheritanceStartFault.None;
		}

		internal static bool CanTransition(KingdomInheritancePhase From, KingdomInheritancePhase To)
		{
			if (From == To)
			{
				return true;
			}
			switch (From)
			{
			case KingdomInheritancePhase.Empty:
				return To == KingdomInheritancePhase.Reserved
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.Reserved:
				return To == KingdomInheritancePhase.SiteSelected
					|| To == KingdomInheritancePhase.Refused
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.SiteSelected:
				return To == KingdomInheritancePhase.WorldValidated
					|| To == KingdomInheritancePhase.Refused
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.WorldValidated:
				return To == KingdomInheritancePhase.Installed
					|| To == KingdomInheritancePhase.Refused
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.Installed:
				return To == KingdomInheritancePhase.AppliedPendingDurability
					|| To == KingdomInheritancePhase.Committed
					|| To == KingdomInheritancePhase.Refused
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.AppliedPendingDurability:
				return To == KingdomInheritancePhase.Committed
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.RepairRequired:
				return To == KingdomInheritancePhase.AppliedPendingDurability
					|| To == KingdomInheritancePhase.Committed
					|| To == KingdomInheritancePhase.Refused;
			default:
				return false;
			}
		}
	}
}
