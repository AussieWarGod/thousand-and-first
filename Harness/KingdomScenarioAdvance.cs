using System.Globalization;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The scripted long wait: <c>advance &lt;turns&gt;</c> runs N game turns with nobody at the
	/// keyboard, so a persona can test behaviour that only happens on a clock - settlement
	/// simulation, growth, decay, scheduled tasks - instead of only what one verb does immediately.
	/// <para>
	/// MECHANISM, and why not <c>AutoAct</c>. The engine's attended long waits
	/// (<c>XRLCore</c> CmdWait20 / CmdWaitN / CmdWait100) set <c>AutoAct.Setting = "." + N</c> and
	/// then call <c>GameObject.PassTurn</c>; <c>ActionManager.RunSegment</c> counts that setting down
	/// one turn at a time. Every one of those settings is deliberately INTERRUPTIBLE - RunSegment
	/// calls <c>AutoAct.Interrupt</c> on <c>Keyboard.kbhit()</c>, on a hostile, and at a zone edge -
	/// because their whole purpose is to hand control back to a human. There is no human in an
	/// unattended run, so an interrupt there is a silent stall, not a courtesy. This verb therefore
	/// uses the OTHER half of the same engine mechanism directly: one <c>PassTurn()</c> - the exact
	/// <c>UseEnergy(1000, "Pass", Passive: true)</c> call CmdWait itself makes - per player action
	/// opportunity, driven from <see cref="KingdomScenarioAutoRunner" />'s
	/// <c>BeginTakeActionEvent</c> handler.
	/// </para>
	/// <para>
	/// YIELDS TO THE GAME LOOP, never spins. Spending the turn inside <c>BeginTakeActionEvent</c>
	/// drops the player's energy below the threshold RunSegment's inner action loop needs, so that
	/// loop does not run and the engine never reaches <c>XRLCore.PlayerTurn</c>'s input wait. The
	/// segment finishes normally, every other actor acts, and the per-turn work RunSegment does at
	/// the ten-segment boundary - <c>EndTurnEvent</c>, <c>ProcessSingleTurn</c>,
	/// <c>ZoneManager.Tick</c>, <c>game.Turns++</c> - happens exactly as it does in ordinary play.
	/// This is a real wait, not a fast-forward.
	/// </para>
	/// <para>
	/// COUNTED IN GAME TURNS, read from <c>XRLGame.Turns</c>, not in handler calls: the engine
	/// increments that counter once per ten segments regardless of who acted, so it is the same turn
	/// a player would count. A faster-than-normal player simply gets more action opportunities per
	/// turn and the elapsed count stays honest.
	/// </para>
	/// <para>
	/// FAIL-CLOSED, with STABLE REASON CODES. Every refusal carries a machine-readable code beside
	/// its prose so an expectation can bind to the code and never to the wording.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioAdvance
	{
		/// <summary>The verb word. The argument is a plain decimal turn count.</summary>
		internal const string Verb = "advance";

		/// <summary>
		/// Hard cap per verb line. A persona that needs longer says so in more than one line, which
		/// also lands more progress rows; one line that could ask for a million turns would let a
		/// sealed script hang a run for hours with no way to see where it got to.
		/// </summary>
		internal const int MaxTurns = 10000;

		/// <summary>Progress cadence, so a stalled or slow advance is visible in the journal.</summary>
		internal const int ProgressTurns = 100;

		/// <summary>
		/// Consecutive action opportunities allowed without the game's turn counter moving. Ten
		/// segments make a turn and a normal-speed actor gets one opportunity per turn, so this is
		/// an order of magnitude of slack before an advance that is going nowhere is called stalled.
		/// </summary>
		internal const int MaxIdlePumps = 200;

		/// <summary>
		/// Passes allowed in one action opportunity. See <see cref="Spend" />: an actor can hold
		/// enough energy for a second action in the same segment, and leaving it unspent is what
		/// would drop the engine into its input wait.
		/// </summary>
		internal const int MaxPassesPerOpportunity = 8;

		internal const string ProgressRow = "advance-progress";
		internal const string CompleteRow = "advance-complete";

		internal const string CodeMalformed = "taf-advance-malformed-count";
		internal const string CodeRange = "taf-advance-count-out-of-range";
		internal const string CodeNoDriver = "taf-advance-no-driver";
		internal const string CodeNoGame = "taf-advance-no-live-game";
		internal const string CodeBusy = "taf-advance-already-running";
		internal const string CodeStalled = "taf-advance-stalled";
		internal const string CodeLostPlayer = "taf-advance-lost-player";

		/// <summary>
		/// Session state, deliberately NOT durable. A half-finished wait means nothing after a
		/// reload: the turns it was counting have either passed or been rolled back with the save,
		/// and resuming a countdown against a different clock would journal a number that never
		/// happened. A reload simply leaves no advance pending.
		/// </summary>
		private static bool DriverPresent;

		private static int Requested;
		private static int Remaining;
		private static int Elapsed;
		private static int NextProgress;
		private static int IdlePumps;
		private static long LastTurn;

		/// <summary>True while turns are still owed. The runner suspends its script while this holds.</summary>
		internal static bool Pending
		{
			get { return Remaining > 0; }
		}

		/// <summary>
		/// Announces that a turn pump exists for this game and clears any state a previous game in
		/// the same process left behind. Called from the auto-runner's <c>OnAdded</c>, which the
		/// engine runs once per <c>XRLGame.AddSystem</c>.
		/// </summary>
		internal static void ArmDriver()
		{
			DriverPresent = true;
			Cancel();
		}

		internal static void Cancel()
		{
			Requested = 0;
			Remaining = 0;
			Elapsed = 0;
			NextProgress = ProgressTurns;
			IdlePumps = 0;
			LastTurn = -1L;
		}

		/// <summary>
		/// Arms a wait and spends this action opportunity on it. Returns the report;
		/// <paramref name="Ok" /> is false only when the verb refused, and every refusal names a
		/// stable code.
		/// </summary>
		internal static string Run(string Argument, out bool Ok)
		{
			Ok = false;
			string raw = (Argument ?? "").Trim();
			if (!DriverPresent)
				return Refuse(CodeNoDriver, "this game runs no scenario auto-runner, so nothing "
					+ "would spend the turns; advance is available only in a game booted in the "
					+ "developer scenario mode");
			if (Pending)
				return Refuse(CodeBusy, "an advance of " + Requested + " turn(s) is already running "
					+ "with " + Elapsed + " elapsed");
			int turns;
			if (raw.Length == 0 || raw.Length > 10 || !AllDigits(raw)
				|| !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out turns))
				return Refuse(CodeMalformed, "'" + KingdomScenarioRules.Bounded(raw)
					+ "' is not a plain decimal turn count; write 'advance <turns>'");
			if (turns < 1 || turns > MaxTurns)
				return Refuse(CodeRange, turns + " is outside the accepted range 1.." + MaxTurns);
			GameObject player = The.Player;
			XRLGame game = The.Game;
			if (player == null || game == null || player.CurrentZone == null)
				return Refuse(CodeNoGame, "there is no live player zone whose turns could pass");
			Requested = turns;
			Remaining = turns;
			Elapsed = 0;
			NextProgress = ProgressTurns;
			IdlePumps = 0;
			LastTurn = game.Turns;
			// This opportunity is the wait's first turn. Spending it here is what keeps the engine
			// out of its input wait; see the class remarks.
			Spend(player);
			Ok = true;
			return "Advancing " + turns + " game turn(s) with no player input. A "
				+ ProgressRow + " row lands every " + ProgressTurns + " turns and the script resumes "
				+ "at its next verb when the wait completes.";
		}

		/// <summary>
		/// One action opportunity of a pending wait.
		/// <para>
		/// Returns true while the wait still owes turns, in which case the caller must return
		/// immediately: this call has already spent the opportunity. Returns false when the wait is
		/// over - with <paramref name="Faulted" /> false when it completed and the script may run
		/// on, or true when it was abandoned and the script must stop. A faulted wait deliberately
		/// does NOT spend the turn: the game is meant to come to rest where an operator can look at
		/// it.
		/// </para>
		/// </summary>
		internal static bool Pump(out bool Faulted)
		{
			Faulted = false;
			if (!Pending) return false;
			GameObject player = The.Player;
			XRLGame game = The.Game;
			if (player == null || game == null)
			{
				Faulted = true;
				Stop(CodeLostPlayer, "the player left the world after " + Elapsed + " of "
					+ Requested + " turn(s)");
				return false;
			}
			long turns = game.Turns;
			int passed = turns > LastTurn ? (int)(turns - LastTurn) : 0;
			LastTurn = turns;
			if (passed <= 0)
			{
				IdlePumps++;
				if (IdlePumps > MaxIdlePumps)
				{
					Faulted = true;
					Stop(CodeStalled, "the game clock did not move across " + MaxIdlePumps
						+ " action opportunities, with " + Elapsed + " of " + Requested
						+ " turn(s) elapsed");
					return false;
				}
			}
			else
			{
				IdlePumps = 0;
				Elapsed += passed;
				Remaining -= passed;
				while (Elapsed >= NextProgress && NextProgress <= Requested)
				{
					KingdomScenarioJournal.Append(ProgressRow, true, NextProgress + " of "
						+ Requested + " turn(s) elapsed");
					NextProgress += ProgressTurns;
				}
			}
			if (Remaining <= 0)
			{
				KingdomScenarioJournal.Append(CompleteRow, true, Elapsed + " turn(s) elapsed of "
					+ Requested + " requested");
				Cancel();
				return false;
			}
			Spend(player);
			return true;
		}

		/// <summary>
		/// Spends the action opportunity, and any further one the same segment would grant.
		/// <para>
		/// <c>GameObject.UseEnergy</c> deducts a RANDOMISED 900..1100 for a 1000-point pass, and
		/// <c>ActionManager.RunSegment</c> re-enters its inner action loop while the actor still
		/// holds 1000 or more - which for an unusually fast actor is where
		/// <c>XRLCore.PlayerTurn</c>'s input wait lives. Passing until the actor is below that
		/// threshold is what makes "no keypress" true for any speed rather than only for the
		/// normal-speed pregen this harness embarks with. The loop is bounded because a pass that
		/// somehow removed no energy must not become a spin.
		/// </para>
		/// </summary>
		private static void Spend(GameObject Player)
		{
			for (int i = 0; i < MaxPassesPerOpportunity; i++)
			{
				Player.PassTurn();
				if (Player.Energy == null || Player.Energy.Value < 1000) return;
			}
		}

		/// <summary>Whether every character is an ASCII digit. Rejects signs, spaces, and separators.</summary>
		private static bool AllDigits(string Value)
		{
			for (int i = 0; i < Value.Length; i++)
				if (Value[i] < '0' || Value[i] > '9') return false;
			return Value.Length > 0;
		}

		private static string Refuse(string Code, string Detail)
		{
			return "{{R|Advance refused}} [" + Code + "]: " + Detail + ".";
		}

		/// <summary>Abandons a pending wait and records why, under the same codes the verb uses.</summary>
		private static void Stop(string Code, string Detail)
		{
			KingdomScenarioJournal.Append(Verb, false, Refuse(Code, Detail));
			Cancel();
		}
	}
}
