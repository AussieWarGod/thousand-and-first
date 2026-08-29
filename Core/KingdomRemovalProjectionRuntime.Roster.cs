using System.Collections.Generic;
using System.Globalization;
using XRL;

namespace ThousandAndFirst
{
	internal static partial class KingdomRemovalProjectionRuntime
	{
		internal static bool TryInspectRoster(out List<string> Rows, out string Failure)
		{
			Rows = new List<string>(); Failure = null;
			if (The.Game == null)
				return Fail("save-system roster game state is absent", out Failure);
			bool present = KingdomSaveSystemRosterRuntime.Marker(The.Game, out int raw);
			KingdomSaveSystemRosterCounts counts =
				KingdomSaveSystemRosterRuntime.Snapshot(The.Game);
			Rows.Add("marker\u001f" + (present ? "1" : "0") + "\u001f"
				+ raw.ToString(CultureInfo.InvariantCulture));
			Rows.Add("systems\u001f" + counts.Realm + "\u001f" + counts.Seal + "\u001f"
				+ counts.CivicMemory + "\u001f" + counts.Succession + "\u001f"
				+ counts.Inheritance);
			KingdomSaveSystemRosterRuntimePlan plan = KingdomSaveSystemRosterRuntimePlan.Create(
				KingdomSaveSystemRosterContext.UnprovenAbsence, present, raw, counts);
			if (plan.RecoveryRequired)
				return Fail(KingdomSaveSystemRosterRuntime.Describe(plan.Decision), out Failure);
			return true;
		}
	}
}
