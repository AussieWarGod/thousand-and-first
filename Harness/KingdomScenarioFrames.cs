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
	/// against state only a rendered frame produces: lighting, visibility, anything a frame makes.
	/// <para>
	/// WHY <c>advance</c> CANNOT DO THIS, learned live. <see cref="KingdomScenarioAdvance" /> spends
	/// the opportunity from inside <c>BeginTakeActionEvent</c> precisely so the engine never reaches
	/// <c>XRLCore.PlayerTurn</c>'s input wait - but the per-frame render path lives INSIDE it.
	/// <c>RunSegment</c> calls <c>PlayerTurn</c> only while the player still holds 1000 energy
	/// (<c>D/XRL/Core/ActionManager.cs:1602</c>), and <c>PlayerTurn</c> is where <c>RenderBase</c>,
	/// and so <c>XRLCore.RenderBaseToBuffer</c> (<c>D/XRL/Core/XRLCore.cs:2455</c>), runs. A
	/// 2400-turn advance therefore moves the clock and renders NOTHING, as two native runs proved;
	/// nor do the attended long waits help, since <c>RenderBase</c> returns early for
	/// <c>AutoAct.Setting</c> "r", "z" and "." (<c>D/XRL/Core/XRLCore.cs:2632</c>).
	/// </para>
	/// <para>
	/// MECHANISM: DO NOTHING, WHICH IS THE POINT. The verb arms a counter and returns WITHOUT
	/// spending the opportunity, so <c>RunSegment</c> walks on into <c>PlayerTurn</c>, whose loop is
	/// the ordinary idle render loop a human sees while standing still: render a frame,
	/// <c>Keyboard.IdleWait()</c> for the throttle interval, render again. Two void seams give the
	/// script back control. <see cref="KingdomScenarioFrameObserver" /> counts DRAWN frames from a
	/// postfix on <c>RenderBaseToBuffer</c>, installed lazily on the first yield and never on
	/// <c>BeforeRenderEvent.Send</c>, whose patched form crashed the game for every persona; that
	/// file owns the whole account. <see cref="Yield" /> is registered through
	/// <c>XRLCore.RegisterOnEndPlayerTurnCallback</c>, fires once per <c>PlayerTurn</c> iteration on
	/// the game thread, and spends the opportunity with the same <c>PassTurn()</c> CmdWait makes
	/// once the count is met. Energy below the threshold ends both loops, and the next segment
	/// brings the <c>BeginTakeActionEvent</c> the runner resumes on: <c>advance</c>'s continuation.
	/// </para>
	/// <para>
	/// FOCUS, THEN FAIL-CLOSED. <c>PlayerTurn</c> parks its whole energy loop on
	/// <c>while (!GameManager.focused &amp;&amp; Game.Running) Thread.Sleep(200)</c>
	/// (<c>D/XRL/Core/XRLCore.cs:756</c>) - at the HEAD of the loop, upstream of the render call and
	/// the end-of-turn callbacks - and the launcher leaves a scripted window unfocused on purpose.
	/// <see cref="KingdomScenarioFocus" /> therefore ASSERTS that one flag for the hold and never
	/// <c>XRLCore.bThreadFocus</c>, so the loop runs while every input gate stays shut, and the verb
	/// refuses when the override does not read back. (<c>advance</c> never meets the park: spending
	/// the energy keeps it out of <c>PlayerTurn</c>.) That it TOOK is proved by a real frame: the
	/// deadline is checked in the frame seam and again in the resume seam, and the idle counter
	/// catches a segment loop that never enters <c>PlayerTurn</c> at all.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioFrames
	{
		/// <summary>The verb word. The argument is a plain decimal frame count.</summary>
		internal const string Verb = "yield-frames";

		/// <summary>Hard cap per verb line: frames arrive at the engine's animation throttle.</summary>
		internal const int MaxFrames = 240;

		/// <summary>Action opportunities allowed without the count moving. A segment loop running
		/// while the render loop is not is a stall, never patience.</summary>
		internal const int MaxIdleOpportunities = 200;

		/// <summary>Wall-clock bound on one yield, orders of magnitude above the idle throttle.</summary>
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
		internal const string CodeUnfocused = "taf-frames-window-unfocused";
		internal const string CodeNoSeam = "taf-frames-no-frame-seam";

		/// <summary>Session state, deliberately NOT durable: a count carried across a reload would
		/// journal a number that never happened.</summary>
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

		/// <summary>Real drawn frames counted for the observed zone: the live yield's while one is
		/// pending, else what the last completed yield delivered.</summary>
		internal static int Observations { get { return Waiting ? Observed : Delivered; } }

		/// <summary>Announces the render loop for this game, clears what a previous game in the
		/// process left behind, and registers the resume seam ONCE: the engine's callback list is
		/// static and never empties. The FRAME seam is deliberately NOT installed here - it arms on
		/// first use, so a game that never yields runs unpatched.</summary>
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
			KingdomScenarioFocus.Release();
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
		/// the player's energy alone is the whole mechanism. Every refusal names a code, and the
		/// frame seam is installed HERE - on the first yield of the process, and nowhere else.</summary>
		internal static string Run(string Argument, out bool Ok)
		{
			Ok = false;
			string raw = (Argument ?? "").Trim();
			if (!DriverPresent)
				return Refuse(CodeNoDriver, "this game runs no scenario auto-runner, so nothing "
					+ "would resume the script; yield-frames needs the developer scenario mode");
			if (Waiting)
				return Refuse(CodeBusy, "a yield of " + Requested + " frame(s) already runs with "
					+ Observed + " observed");
			if (KingdomScenarioAdvance.Pending)
				return Refuse(CodeAdvancing, "an advance still owes turns; rendering while the turn "
					+ "pump spends opportunities is two mechanisms fighting over one energy pool");
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
			string failure;
			if (!KingdomScenarioFrameObserver.TryInstall(out failure))
				return Refuse(CodeNoSeam, "no drawn-frame seam, so nothing counts a frame: " + failure);
			if (!KingdomScenarioFocus.TryHold(out failure))
				return Refuse(CodeUnfocused, "PlayerTurn would park its whole loop, drawing no "
					+ "frame and firing no seam: " + failure);
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
			return "Yielding to the engine's own render loop until " + frames + " real drawn "
				+ "frame(s) of " + ZoneId + " have been rendered. This opportunity is deliberately "
				+ "not spent; a " + CompleteRow + " row lands with the observed count.";
		}

		/// <summary>
		/// One action opportunity of a pending yield. Returns true while frames are still owed, in
		/// which case the caller MUST return immediately WITHOUT spending the opportunity - the
		/// unspent energy is what lets the engine reach its render loop at all. Returns false when
		/// the yield is over, with <paramref name="Faulted" /> false when it completed and the
		/// script may run on, or true when it was abandoned and the script must stop.</summary>
		internal static bool Pump(out bool Faulted)
		{
			Faulted = false;
			if (!Waiting) return false;
			if (The.Player == null || The.Game == null)
			{
				Faulted = true;
				Stop(CodeLostPlayer, "the player left the world after " + Observed + "/" + Requested);
				return false;
			}
			if (Abandoned != null)
			{
				string code = Abandoned;
				Faulted = true;
				Stop(code, "the render-loop seam abandoned the yield after " + Observed + "/"
					+ Requested + " frame(s)" + (Fault == null ? "" : ": " + Fault));
				return false;
			}
			if (Observed >= Requested)
			{
				KingdomScenarioJournal.Append(CompleteRow, true, Observed
					+ " real drawn frame(s) of " + ZoneId + ", of " + Requested + " requested");
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
					+ " action opportunities, with " + Observed + "/" + Requested + " observed");
				return false;
			}
			if (Clock.Elapsed.TotalSeconds > DeadlineSeconds)
			{
				Faulted = true;
				Stop(CodeStalled, "the render loop did not deliver " + Requested + " frame(s) within "
					+ DeadlineSeconds + "s; " + Observed + " were observed");
				return false;
			}
			return true;
		}

		/// <summary>Void witness of one drawn frame, and the deadline check a render loop that is
		/// slow rather than parked can actually reach. Counts only the observed zone's frames, and
		/// a suppressed draw arrives as no zone at all.</summary>
		internal static void Observe(Zone Rendered)
		{
			if (!Waiting) return;
			try
			{
				if (Abandoned == null && Observed < Requested
					&& Clock.Elapsed.TotalSeconds > DeadlineSeconds)
					Abandoned = CodeStalled;
				if (Abandoned != null || Rendered == null) return;
				if (!string.Equals(Rendered.ZoneID, ZoneId, StringComparison.Ordinal)) return;
				Observed++;
			}
			catch (Exception error) { Fail(error); }
		}

		/// <summary>The resume seam, one call per <c>PlayerTurn</c> iteration on the game thread. It
		/// spends the opportunity the engine holds open for a keypress once the count is met, or
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
			catch (Exception error) { Fail(error); }
		}

		/// <summary>Records a throw out of an engine-loop seam. Letting one escape would end the
		/// game loop for the rest of the session, so it is answered on the next opportunity.</summary>
		private static void Fail(Exception Error)
		{
			if (Abandoned != null) return;
			Abandoned = CodeStalled;
			Fault = KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message);
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
