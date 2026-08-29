#if !TAF_TESTS
using XRL;

namespace ThousandAndFirst
{
	internal static partial class KingdomSaveSystemRosterRuntime
	{
		/// <summary>
		/// Succession is optional in the roster because only Kingdom mode adds it; the mode's
		/// gamesystem is installed before player mutators by QudGamemodeModule.cs:341-364 and
		/// QudGameBootModule.cs:256-270,303-307. Once that exact mode is saved, absence is loss.
		/// InheritanceLifecycle is optional because Initialize adds it only when an import is offered;
		/// a non-empty or unreadable inheritance singleton proves that its lifecycle may not vanish.
		/// </summary>
		internal static int MissingRequiredOptionalMask(XRLGame Game,
			KingdomSaveSystemRosterCounts Counts, bool InheritanceAuthorityUnreadable,
			out string Failure)
		{
			Failure = null;
			if (Game == null || Counts == null) return 0;
			int missing = 0;
			if (KingdomSuccessionRules.ModeOn(Game.gameMode,
				Game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey))
				&& Counts.Succession == 0)
			{
				missing |= KingdomSaveSystemRosterRules.SuccessionBit;
				Failure = "Kingdom mode lost its required Succession carrier";
			}
			if (Counts.Inheritance == 0 && RequiresInheritanceLifecycle(Game,
				InheritanceAuthorityUnreadable))
			{
				missing |= KingdomSaveSystemRosterRules.InheritanceBit;
				Failure = string.IsNullOrEmpty(Failure)
					? "inheritance authority lost its required lifecycle carrier"
					: Failure + "; inheritance authority also lost its lifecycle carrier";
			}
			return missing;
		}

		private static bool RequiresInheritanceLifecycle(XRLGame Game,
			bool InheritanceAuthorityUnreadable)
		{
			if (InheritanceAuthorityUnreadable) return true;
			if (Game?.ObjectGameState == null
				|| !Game.ObjectGameState.TryGetValue(KingdomInheritanceState.StateId,
					out object value)) return false;
			KingdomInheritanceState state = value as KingdomInheritanceState;
			return state == null || state.Phase != KingdomInheritancePhase.Empty;
		}
	}
}
#endif
