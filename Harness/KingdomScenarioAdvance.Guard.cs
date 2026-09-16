using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>The founder-guard half of the advance pump (KingdomScenarioFounderGuard.cs):
	/// the guarded entries that lower the engine flag when the pump body throws, and the guard's
	/// own end row. Split out only to keep the main shard under the house line cap.</summary>
	internal static partial class KingdomScenarioAdvance
	{
		/// <summary>
		/// The pump, with the guard lowered on the way out of an exception. The runner's Step runs
		/// under KingdomSystem.Guard, which logs and swallows, so a throwing pump would otherwise
		/// leave the flag raised until a later pump, stop, death or new game happened to lower it.
		/// A normal return keeps the flag exactly as the body left it: the end row and release on
		/// completion and on Stop are the body's own, so <c>settled</c> is the only thing this adds.
		/// </summary>
		internal static bool Pump(out bool Faulted)
		{
			bool settled = false;
			try
			{
				bool pending = PumpCore(out Faulted);
				settled = true;
				return pending;
			}
			finally
			{
				if (!settled) KingdomScenarioFounderGuard.Release(The.Player);
			}
		}

		/// <summary>The first spend after arming, same shape: a throw from PassTurn must not leave
		/// the flag raised behind a refusal the verb never reported.</summary>
		private static void GuardedSpend(GameObject Player)
		{
			bool settled = false;
			try
			{
				Spend(Player);
				settled = true;
			}
			finally
			{
				if (!settled) KingdomScenarioFounderGuard.Release(Player);
			}
		}

		/// <summary>The guard's end row, only when it was armed: the founder's cell and the
		/// restored state, before the row that ends the advance.</summary>
		private static void EndGuard(GameObject Player)
		{
			if (!KingdomScenarioFounderGuard.Armed) return;
			KingdomScenarioJournal.Append(KingdomScenarioFounderGuard.Row, true,
				"end; " + KingdomScenarioFounderGuard.Release(Player));
		}
	}
}
