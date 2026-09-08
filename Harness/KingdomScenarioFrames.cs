using System;
using System.Diagnostics;
using System.Globalization;

using XRL;
using XRL.Core;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The scripted frame yield: <c>yield-frames &lt;count&gt;</c> hands the engine back its own
	/// render loop until it has drawn at least N real frames, so an unattended observer can assert
	/// against state only a rendered frame produces - lighting, visibility, anything whose
	/// production code answers <c>BeforeRenderEvent</c>.
	/// <para>
	/// WHY <c>advance</c> CANNOT DO THIS, learned live. <see cref="KingdomScenarioAdvance" /> spends
	/// the opportunity from inside <c>BeginTakeActionEvent</c> precisely so the engine never reaches
	/// <c>XRLCore.PlayerTurn</c>'s input wait - but the per-frame render path lives INSIDE it.
	/// <c>RunSegment</c> calls <c>PlayerTurn</c> only while the player still holds 1000 energy
	/// (<c>D/XRL/Core/ActionManager.cs:1602</c>), and <c>PlayerTurn</c> is where <c>RenderBase</c> -
	/// and so <c>RenderBaseToBuffer</c>'s <c>BeforeRenderEvent.Send(activeZone)</c>
	/// (<c>D/XRL/Core/XRLCore.cs:2503</c>) - runs. A 2400-turn advance therefore moves the clock and
	/// renders NOTHING; two native runs proved exactly that. The engine's other long waits do not
	/// help: <c>RenderBase</c> returns early for <c>AutoAct.Setting</c> "r", "z" and "."
	/// (<c>D/XRL/Core/XRLCore.cs:2632</c>), which is every attended rest and CmdWaitN.
	/// </para>
	/// <para>
	/// MECHANISM: DO NOTHING, WHICH IS THE POINT. The verb arms a counter and returns WITHOUT
	/// spending the opportunity, so <c>RunSegment</c> walks on into <c>PlayerTurn</c>, whose loop is
	/// the ordinary idle render loop a human sees while standing still: render a frame,
	/// <c>Keyboard.IdleWait()</c> for the throttle interval, render again.
	/// </para>
	/// <para>
	/// AND HOW THE SCRIPT GETS CONTROL BACK. Two seams, both public extension points, no engine
	/// behaviour replaced. <see cref="KingdomScenarioFrameObserver" /> is a VOID postfix on
	/// <c>BeforeRenderEvent.Send</c>, counting the dispatch the observer cares about rather than a
	/// proxy for it. <see cref="Yield" /> is registered through
	/// <c>XRLCore.RegisterOnEndPlayerTurnCallback</c>, which fires once per <c>PlayerTurn</c>
	/// iteration on the game thread; when the count is met it spends the opportunity with the same
	/// <c>PassTurn()</c> CmdWait makes. Energy below the threshold ends <c>PlayerTurn</c>'s loop and
	/// <c>RunSegment</c>'s, and the next segment brings the <c>BeginTakeActionEvent</c> the runner
	/// resumes on - the SAME continuation <c>advance</c> already uses.
	/// </para>
	/// <para>
	/// COUNTED IN DISPATCHES OF THE OBSERVED ZONE, never in iterations or turns: a frame sent for
	/// another zone is not this zone's frame, and an iteration <c>bDraw</c> suppressed drew nothing.
	/// </para>
	/// <para>
	/// FAIL-CLOSED, with STABLE REASON CODES, and ONE state it cannot cover. The wall-clock deadline
	/// catches a stalled render loop - the engine parks <c>PlayerTurn</c> on
	/// <c>while (!GameManager.focused)</c> (<c>D/XRL/Core/XRLCore.cs:763</c>), so an unfocused window
	/// stalls for real - and the idle-opportunity counter catches a segment loop that never enters
	/// <c>PlayerTurn</c>. Neither reaches an inner loop that both skips <c>PlayerTurn</c> and never
	/// spends the turn: no seam fires there, so only the persona's TIMEOUT ends it - a visible
	/// timeout, never a silent pass.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioFrames
	{
		/// <summary>The verb word. The argument is a plain decimal frame count.</summary>
		internal const string Verb = "yield-frames";

		/// <summary>Hard cap per verb line: frames arrive at the engine's animation throttle, so
		/// a large count is wall-clock spent drawing rather than asserting.</summary>
		internal const int MaxFrames = 240;

		/// <summary>Action opportunities allowed without the count moving. A segment loop running
		/// while the render loop is not is a stall, never patience.</summary>
		internal const int MaxIdleOpportunities = 200;

		/// <summary>Wall-clock bound on one yield. The engine's idle throttle is tens of
		/// milliseconds, so this is orders of magnitude of slack before slow becomes stalled.</summary>
		internal const int DeadlineSeconds = 120;

		/// <summary>Bookkeeping row naming how many real frames the yield actually observed.</summary>
		internal const string CompleteRow = "yield-frames-complete";

		internal const string CodeMalformed = "taf-frames-malformed-count";
		internal const string CodeRange = "taf-frames-count-out-of-range";
		internal const string CodeNoDriver = "taf-frames-no-driver";
		internal const string CodeNoGame = "taf-frames-no-live-game";
		internal const string CodeBusy = "taf-frames-already-running";
		internal const string CodeAdvancing = "taf-frames-advance-pending";
		internal const string CodeStalled = "taf-frames-stalled";
		internal const string CodeLostPlayer = "taf-frames-lost-player";

		/// <summary>Session state, deliberately NOT durable: frames already drawn cannot be resumed
		/// against, and a count carried across a reload would journal a number that never
		/// happened.</summary>
		private static bool DriverPresent;

		private static bool Hooked;
		private static bool Waiting;
		private static int Requested;
		private static int Observed;
		private static int Marked;
		private static int IdlePumps;
		private static string ZoneId;
		private static string Abandoned;
		private static string Fault;
		private static int Delivered;
		private static readonly Stopwatch Clock = new Stopwatch();

		/// <summary>True while frames are owed. The runner suspends its script while it holds.</summary>
		internal static bool Pending { get { return Waiting; } }

		/// <summary>Real dispatches counted for the observed zone: the live yield's while one is
		/// pending, otherwise what the last completed yield delivered.</summary>
		internal static int Observations { get { return Waiting ? Observed : Delivered; } }

		/// <summary>Announces the render loop for this game, clears what a previous game in the
		/// process left behind, and registers the resume seam ONCE: the engine's callback list is
		/// static and never empties.</summary>
		internal static void ArmDriver()
		{
			DriverPresent = true;
			Cancel();
			Delivered = 0;
			if (Hooked) return;
			Hooked = true;
			XRLCore.RegisterOnEndPlayerTurnCallback(Yield);
		}

		internal static void Cancel()
		{
			Waiting = false;
			Requested = 0;
			Observed = 0;
			Marked = 0;
			IdlePumps = 0;
			ZoneId = null;
			Abandoned = null;
			Fault = null;
			Clock.Reset();
		}

		/// <summary>Arms a yield and deliberately does NOT spend this action opportunity: leaving
		/// the player's energy alone is the whole mechanism. Every refusal names a code.</summary>
		internal static string Run(string Argument, out bool Ok)
		{
			Ok = false;
			string raw = (Argument ?? "").Trim();
			if (!DriverPresent)
				return Refuse(CodeNoDriver, "this game runs no scenario auto-runner, so nothing "
					+ "would resume the script; yield-frames is available only in a game booted in "
					+ "the developer scenario mode");
			if (Waiting)
				return Refuse(CodeBusy, "a yield of " + Requested + " frame(s) is already running "
					+ "with " + Observed + " observed");
			if (KingdomScenarioAdvance.Pending)
				return Refuse(CodeAdvancing, "an advance still owes turns; a yield that let the "
					+ "engine render while the turn pump was spending opportunities would be two "
					+ "mechanisms fighting over the same energy");
			int frames;
			if (raw.Length == 0 || raw.Length > 10 || !AllDigits(raw)
				|| !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out frames))
				return Refuse(CodeMalformed, "'" + KingdomScenarioRules.Bounded(raw)
					+ "' is not a plain decimal frame count; write 'yield-frames <frames>'");
			if (frames < 1 || frames > MaxFrames)
				return Refuse(CodeRange, frames + " is outside the accepted range 1.." + MaxFrames);
			GameObject player = The.Player;
			if (player == null || The.Game == null || player.CurrentZone == null)
				return Refuse(CodeNoGame, "there is no live player zone the engine could render");
			Requested = frames;
			Observed = 0;
			Marked = 0;
			IdlePumps = 0;
			ZoneId = player.CurrentZone.ZoneID;
			Abandoned = null;
			Fault = null;
			Waiting = true;
			Clock.Restart();
			Ok = true;
			return "Yielding to the engine's own render loop until " + frames + " real "
				+ "BeforeRenderEvent frame(s) of " + ZoneId + " have been dispatched. This "
				+ "opportunity is deliberately not spent; a " + CompleteRow + " row lands with the "
				+ "observed count and the script resumes after it.";
		}

		/// <summary>
		/// One action opportunity of a pending yield.
		/// <para>
		/// Returns true while frames are still owed, in which case the caller MUST return
		/// immediately WITHOUT spending the opportunity - the unspent energy is what lets the
		/// engine reach its render loop at all. Returns false when the yield is over, with
		/// <paramref name="Faulted" /> false when it completed and the script may run on, or true
		/// when it was abandoned and the script must stop.
		/// </para>
		/// </summary>
		internal static bool Pump(out bool Faulted)
		{
			Faulted = false;
			if (!Waiting) return false;
			if (The.Player == null || The.Game == null)
			{
				Faulted = true;
				Stop(CodeLostPlayer, "the player left the world after " + Observed + " of "
					+ Requested + " frame(s)");
				return false;
			}
			if (Abandoned != null)
			{
				string code = Abandoned;
				Faulted = true;
				Stop(code, "the render-loop seam abandoned the yield after " + Observed + " of "
					+ Requested + " frame(s)" + (Fault == null ? "" : ": " + Fault));
				return false;
			}
			if (Observed >= Requested)
			{
				KingdomScenarioJournal.Append(CompleteRow, true, Observed
					+ " real BeforeRenderEvent frame(s) of " + ZoneId + ", of " + Requested
					+ " requested");
				Delivered = Observed;
				Cancel();
				return false;
			}
			if (Observed > Marked)
			{
				Marked = Observed;
				IdlePumps = 0;
			}
			else if (++IdlePumps > MaxIdleOpportunities)
			{
				Faulted = true;
				Stop(CodeStalled, "the engine drew no frame across " + MaxIdleOpportunities
					+ " action opportunities, with " + Observed + " of " + Requested
					+ " frame(s) observed");
				return false;
			}
			if (Clock.Elapsed.TotalSeconds > DeadlineSeconds)
			{
				Faulted = true;
				Stop(CodeStalled, "the render loop did not deliver " + Requested + " frame(s) "
					+ "within " + DeadlineSeconds + "s; " + Observed + " were observed");
				return false;
			}
			return true;
		}

		/// <summary>Void witness of one real dispatch. Counts only the observed zone's frames: a
		/// dispatch sent for another zone is not evidence about this one.</summary>
		internal static void Observe(Zone Rendered)
		{
			if (!Waiting || Rendered == null) return;
			if (!string.Equals(Rendered.ZoneID, ZoneId, StringComparison.Ordinal)) return;
			Observed++;
		}

		/// <summary>The resume seam, one call per <c>PlayerTurn</c> iteration on the game thread.
		/// Spends the opportunity the engine holds open for a keypress once the count is met, or
		/// once the deadline passed - the yield is then abandoned, never asserted on.</summary>
		private static void Yield(XRLCore Core)
		{
			if (!Waiting) return;
			try
			{
				GameObject player = The.Player;
				if (player == null) return;
				if (Observed < Requested && Clock.Elapsed.TotalSeconds <= DeadlineSeconds) return;
				if (Observed < Requested && Abandoned == null) Abandoned = CodeStalled;
				player.PassTurn();
			}
			catch (Exception error)
			{
				// A throw escaping an engine render-loop callback would end the game loop for the
				// rest of the session, so it is recorded and answered on the next opportunity.
				if (Abandoned == null)
				{
					Abandoned = CodeStalled;
					Fault = KingdomScenarioRules.Bounded(
						error.GetType().Name + ": " + error.Message);
				}
			}
		}

		/// <summary>Whether every character is an ASCII digit. Rejects signs and separators.</summary>
		private static bool AllDigits(string Value)
		{
			for (int i = 0; i < Value.Length; i++)
				if (Value[i] < '0' || Value[i] > '9') return false;
			return Value.Length > 0;
		}

		private static string Refuse(string Code, string Detail)
		{
			return "{{R|Frame yield refused}} [" + Code + "]: " + Detail + ".";
		}

		/// <summary>Abandons a pending yield and records why, under the codes the verb uses.</summary>
		private static void Stop(string Code, string Detail)
		{
			KingdomScenarioJournal.Append(Verb, false, Refuse(Code, Detail));
			Cancel();
		}
	}
}
