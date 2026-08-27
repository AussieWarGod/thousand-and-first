using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomZoning
	{
		// The seated city's own rolls, with the one-time fold in front of them.
		//
		// THE FOLD IS A SHIM AND IS NAMED ONE. Before the knowledge siting the roster was a single
		// string on the game; a save written then carries it there and carries nothing on its
		// cities. This reads it into the seat once and retires the key, so the same save never
		// folds twice and a second city never inherits the first one's rolls by accident. It is not
		// a migration harness and it is not a policy: when the release-era harness lands
		// (Addendum 9) this is the first thing it should absorb.
		private static string Stored(KingdomSystem System)
		{
			if (System == null)
			{
				return "";
			}
			string legacy = The.Game?.GetStringGameState(RosterState, "") ?? "";
			if (legacy.Length > 0)
			{
				string canonical;
				if (string.IsNullOrEmpty(System.KeepersRoster)
					&& KingdomZoningRules.TryCanonicalRoster(legacy, out canonical))
				{
					System.KeepersRoster = canonical;
					KingdomLog.Log("zoning: folded the old game-held roster into " + System.SeatName + " and retired the key");
				}
				else if (string.IsNullOrEmpty(System.KeepersRoster))
				{
					MetricsManager.LogError("ThousandAndFirst zoning: old keeper roster exceeds its hard bound; it was not imported");
				}
				The.Game?.SetStringGameState(RosterState, "");
			}
			return System.KeepersRoster ?? "";
		}

		private static void Store(KingdomSystem System, string Roster)
		{
			if (System == null)
			{
				return;
			}
			string canonical;
			if (!KingdomZoningRules.TryCanonicalRoster(Roster, out canonical))
			{
				MetricsManager.LogError("ThousandAndFirst zoning: refused an unbounded keeper roster write");
				return;
			}
			System.KeepersRoster = canonical;
		}
	}
}
