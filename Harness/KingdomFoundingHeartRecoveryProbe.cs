using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		// Failure-only observation. Acceptance still calls the production recovery operation.
		internal static string FoundingHeartRecoveryFailureForHarness(Zone Z)
		{
			if (!KingdomFoundingHeartRules.TryDecode(Z?.GetZoneProperty(FoundingHeartReceiptProperty, null),
				out var plan) || !KingdomFoundingHeartRules.Complete(plan)) return "recovery-witness=plan";
			if (!ExactFoundingHeartSeal(Z, plan)) return "recovery-witness=seal";
			if (!ExactFoundingHeartReservations(plan)) return "recovery-witness=reservations";
			if (!TryReadFoundingHeartContext(Z, plan, out var context)) return "recovery-witness=context";
			if (!ExactFoundingHeartMarkerRoster(Z, plan, false)) return "recovery-witness=marker-roster";
			if (!ExactFoundingHeartRetiredCustody(plan))
			{
				string result = "recovery-witness=retired-custody";
				if (!TryLoadedPlotTombstones(out var tombstones)) return result + "; tombstones=unreadable";
				foreach (var item in tombstones)
					if (item != null && !GameObject.Validate(item)
						&& TryClassifyFoundingHeartTombstone(item, plan, out bool relevant, out bool exact)
						&& relevant && !exact)
						result += "; foreign-tombstone=" + item.IDIfAssigned;
				return result;
			}
			if (!KingdomFoundingHeartTerminalRules.TryDecode(
				Z.GetZoneProperty(FoundingHeartTerminalProperty, null), out var terminal))
				return "recovery-witness=terminal";
			return "recovery-witness=retirement-proof; proved="
				+ ExactFoundingHeartRetirementProof(Z, context, terminal.FinalId,
					KingdomFoundingHeartRetiredGeneration.Final);
		}
	}
}
