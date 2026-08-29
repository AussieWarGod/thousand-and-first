#if !TAF_TESTS
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	internal static partial class KingdomSaveSystemRosterRuntime
	{
		/// <summary>A markerless empty registry is not proof of freshness. First install is accepted
		/// only when the save header positively says this manifest id was absent and no exact
		/// save-global TAF footprint exists. If header evidence was unavailable, this returns false.</summary>
		internal static bool CleanFirstInstall(XRLGame Game,
			bool SavedModEvidenceKnown, bool SavedModWasPresent, bool MarkerPresent,
			KingdomSaveSystemRosterCounts Counts)
		{
			return Game != null && SavedModEvidenceKnown && !SavedModWasPresent
				&& !MarkerPresent && KingdomSaveSystemRosterRuntimePlan.Empty(Counts)
				&& !HasKnownFootprint(Game);
		}

		private static bool HasKnownFootprint(XRLGame Game)
		{
			if (Game == null) return true;
			if (HasStringFootprint(Game.StringGameState)) return true;
			if (HasFootprint(Game.IntGameState) || HasFootprint(Game.Int64GameState)
				|| HasFootprint(Game.BooleanGameState) || HasFootprint(Game.ObjectGameState))
				return true;
			return false;
		}

		private static bool HasStringFootprint(Dictionary<string, string> States)
		{
			if (States == null) return false;
			foreach (string key in States.Keys)
				if (key == KingdomIdentityFenceRules.StateKey
					|| KingdomRemovalCoverage.IsOwnedGlobalState(key)) return true;
			return false;
		}

		private static bool HasFootprint<T>(Dictionary<string, T> States)
		{
			if (States == null) return false;
			foreach (string key in States.Keys)
				if (KingdomRemovalCoverage.IsOwnedGlobalState(key)) return true;
			return false;
		}
	}
}
#endif
