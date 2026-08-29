using System;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.World;
	using XRL.World.Parts;

	/// <summary>Removal-side custody for the realm mirror-gate register.</summary>
	internal static partial class KingdomMirrorGate
	{
		/// <summary>
		/// Proves that removing this work cannot leave a live register row pointing at empty ground.
		/// This is deliberately read-only: striking and conversion must refuse before publishing or
		/// spending, while an already-started strike may wait here until the founder unkeys the arch.
		/// </summary>
		internal static bool TryPreflightRemoval(GameObject Building, Zone Zone,
			out string Failure)
		{
			Failure = null;
			r_KingdomMirrorGate gate = Building?.GetPart<r_KingdomMirrorGate>();
			if (gate == null) return true;
			Cell cell = Building.CurrentCell;
			if (The.Game == null || !GameObject.Validate(Building) || Zone == null
				|| Building.CurrentZone != Zone || cell == null || cell.ParentZone != Zone)
			{
				Failure = KingdomMirrorGateRules.RemovalProofFailureLine;
				return false;
			}
			string key = KingdomMirrorGateRules.ComposeLocationKey(Zone.ZoneID, cell.X, cell.Y);
			string raw = The.Game.GetStringGameState(
				KingdomMirrorGateRules.RegisterStateKey, "");
			if (string.IsNullOrEmpty(key)
				|| !KingdomMirrorGateRules.TryParseRegister(raw,
					out KingdomGateRow[] rows, out int dropped)
				|| dropped != 0
				|| !string.Equals(raw, KingdomMirrorGateRules.FormatRegister(rows),
					StringComparison.Ordinal))
			{
				Failure = KingdomMirrorGateRules.RemovalProofFailureLine;
				return false;
			}
			if (!KingdomMirrorGateRules.MayRemove(rows, key)
				|| (!string.IsNullOrEmpty(gate.LocationKey)
					&& gate.LocationKey != key
					&& !KingdomMirrorGateRules.MayRemove(rows, gate.LocationKey)))
			{
				Failure = KingdomMirrorGateRules.KeyedRemovalFailureLine;
				return false;
			}
			return true;
		}
	}
}
