#if !TAF_TESTS
using XRL;

namespace ThousandAndFirst
{
	internal static partial class KingdomSaveSystemRosterRuntime
	{
		/// <summary>Exact raw compare-and-swap used by the authorized removal transaction while
		/// every roster-named system is still registered. It says nothing about a later save loaded
		/// without this mod; no code can run while the mod is absent.</summary>
		internal static bool TryClearForPreparedRemoval(XRLGame Game, out string Failure)
		{
			Failure = null;
			KingdomSaveSystemRosterCounts counts = Snapshot(Game);
			bool present = Marker(Game, out int raw);
			KingdomSaveSystemRosterRuntimePlan plan =
				KingdomSaveSystemRosterRuntimePlan.Create(
					KingdomSaveSystemRosterContext.PreparedRemoval, present, raw, counts);
			if (plan.RecoveryRequired)
				return Fail(Describe(plan.Decision), out Failure);
			return TryCommit(Game, plan.Decision, out Failure);
		}

		internal static bool TryCommit(XRLGame Game,
			KingdomSaveSystemRosterDecision Decision, out string Failure)
		{
			Failure = null;
			if (Game == null)
				return Fail("save-system roster CAS has no game state", out Failure);
			bool present = Marker(Game, out int raw);
			if (!KingdomSaveSystemRosterRules.TryResolveCas(Decision, present, raw,
				out bool nextPresent, out int nextRaw,
				out KingdomSaveSystemRosterFault fault, out Failure))
				return Fail(Failure ?? ("save-system roster CAS failed with " + fault),
					out Failure);
			if (nextPresent != present || (nextPresent && nextRaw != raw))
			{
				if (nextPresent)
					Game.SetIntGameState(KingdomSaveSystemRosterRules.StateKey, nextRaw);
				else
					Game.RemoveIntGameState(KingdomSaveSystemRosterRules.StateKey);
			}
			bool retained = Marker(Game, out int retainedRaw);
			if (retained != nextPresent || (retained && retainedRaw != nextRaw))
				return Fail("save-system roster did not retain its compare-and-swap write",
					out Failure);
			return true;
		}

		internal static string Describe(KingdomSaveSystemRosterDecision Decision)
		{
			if (Decision == null) return "save-system roster produced no decision";
			string system = Decision.System == KingdomSaveSystemRosterSystem.None ? ""
				: " [" + Decision.System + ": expected " + Decision.ExpectedCount
					+ ", observed " + Decision.ActualCount + "]";
			return "save-system roster " + Decision.Fault + system + ": "
				+ (string.IsNullOrEmpty(Decision.Failure)
					? "the saved carrier set could not be proved" : Decision.Failure);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
#endif
