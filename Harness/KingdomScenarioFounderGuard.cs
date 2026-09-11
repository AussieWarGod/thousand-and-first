using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Keeps the founder out of the wandering world's target lists for the duration of ONE
	/// scripted <c>advance</c>, and only on the quickstart-lifecycle road.
	/// <para>
	/// WHY (native run 36, 13122f0). During the persona's scripted <c>advance 7200</c> the founder
	/// - the player body, standing on the rite cell with nobody at the keyboard - was bitten to
	/// death by a wandering creature; succession refused (HeirUnreachable), the engine stopped the
	/// game loop, and the AutoRunner, which only ever wakes on the player's own
	/// BeginTakeActionEvent, could never journal a terminal row. The run sat for ~50 minutes.
	/// Death is now terminal in its own right (Harness/KingdomScenarioAutoRunner.Death.cs); this
	/// shard is the other half: an unattended founder should not be a combat target at all.
	/// </para>
	/// <para>
	/// MECHANISM: the engine's own <c>XRLCore.IgnoreMe</c> (decompile 2.0.211.51
	/// XRL/Core/XRLCore.cs:217, <c>[NonSerialized] public bool IgnoreMe</c>). It is consulted in
	/// exactly two places: <c>Brain.WantToKill</c> returns before pushing a Kill goal when the
	/// subject is the player (XRL/World/Parts/Brain.cs:1075-1077), and
	/// <c>GameObject.IsHostileTowards</c> answers false toward the player
	/// (XRL/World/GameObject.cs:11431-11433). It is the flag the engine's own <c>ignoreme</c> wish
	/// toggles (XRL/World/Capabilities/Wishing.cs:3396-3398). It is NOT invulnerability: nothing in
	/// <c>TakeDamage</c> or <c>Die</c> reads it, so anything that does attack still lands damage;
	/// it is not the invulnerability cheat, not <c>XRLCore.Calm</c> (which freezes every non-player actor), it
	/// disables no spawning, and it is never serialized, so a save taken under it carries nothing.
	/// </para>
	/// <para>
	/// SCOPE. Armed only when KingdomQuickstartBootTest.LifecycleRequested - the one command whose
	/// founder is the player - and only between an <c>advance</c> arming and that advance's end by
	/// any route (complete, stall, lost player, death, new game). Every other persona's advance is
	/// untouched. The previous value is restored, never assumed false.
	/// </para>
	/// <para>
	/// NO WALK. The brief preferred walking the founder into the staked camp tent or the heart's
	/// interior first. At Camp stage neither exists: the two tent rows are STAKED plots still to be
	/// walled (World/KingdomQuickstartBootstrap.cs:180, "walled over 1700 ticks") and the founding
	/// heart has no interior cell yet (Growth/KingdomPlot2.07.HeartGeometry.cs interior only for
	/// a yard-bearing rung). Moving the founder onto unbuilt ground would prove nothing and could
	/// put a body on a plot footprint (#163). So the founder stays where the boot left him and the
	/// row records that cell; <c>walk=none</c> names the limit rather than hiding it.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioFounderGuard
	{
		/// <summary>Bookkeeping journal row (Tools/personas/persona_matrix.py BOOKKEEPING): one at
		/// arming, one at release, each naming the founder's cell and the guard state.</summary>
		internal const string Row = "advance-guard";

		internal const string StateArmed = "ignoreme-armed";
		internal const string StateReleased = "ignoreme-released";
		internal const string StateNotRequested = "not-requested";

		private static bool Held;
		private static bool Previous;

		/// <summary>True while this guard holds the engine flag.</summary>
		internal static bool Armed { get { return Held; } }

		/// <summary>Raises the flag for a quickstart-lifecycle run; a no-op elsewhere. Returns the
		/// row text naming the founder's cell and the resulting state.</summary>
		internal static string Arm(GameObject Player)
		{
			if (!KingdomQuickstartBootTest.LifecycleRequested)
				return Describe(Player, StateNotRequested);
			if (!Held)
			{
				Previous = The.Core.IgnoreMe;
				The.Core.IgnoreMe = true;
				Held = true;
			}
			return Describe(Player, StateArmed);
		}

		/// <summary>Restores the flag to the value found at arming. Idempotent; safe to call from
		/// every end-of-advance route, including a new game in the same process.</summary>
		internal static string Release(GameObject Player)
		{
			if (!Held)
				return Describe(Player, KingdomQuickstartBootTest.LifecycleRequested
					? StateReleased : StateNotRequested);
			The.Core.IgnoreMe = Previous;
			Held = false;
			return Describe(Player, StateReleased);
		}

		/// <summary>The founder's cell and the guard state, read now, in one ASCII clause.</summary>
		internal static string Describe(GameObject Player, string State)
		{
			Cell cell = Player?.CurrentCell;
			string where = cell == null
				? "founderCell=absent; zone=absent"
				: "founderCell=" + cell.X + "," + cell.Y + "; zone="
					+ (cell.ParentZone?.ZoneID ?? "absent");
			XRL.Core.XRLCore core = The.Core;
			return where + "; guard=" + State + "; ignoreMe="
				+ (core == null ? "no-core" : core.IgnoreMe.ToString())
				+ "; walk=none; scope=quickstart-lifecycle";
		}
	}
}
