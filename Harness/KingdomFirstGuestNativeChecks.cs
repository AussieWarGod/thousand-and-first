using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Machine witness for the first guest's correspondence opening
	/// (<c>Growth/KingdomGrowth.FirstGuestStart.cs</c>,
	/// <c>Core/KingdomReportsPeople.cs</c>), standing in for the attended message-and-Charter read.
	/// <para>
	/// OBSERVATION ONLY. The fixture founds a camp and dedicates one stocked reservoir; every
	/// arrival step after that is the production cadence running on REAL turns the persona's
	/// <c>advance</c> spends. No candidate, opportunity, clock, receipt or choice is assigned, and
	/// no popup is shown.
	/// </para>
	/// <para>
	/// EXACTLY ONCE, WITHOUT TRUSTING THE SCROLLBACK. <c>MessageQueue.BeginPlayerTurn</c> trims
	/// the retained log at two thousand lines (<c>D/XRL/Messages/MessageQueue.cs</c>), so a count
	/// taken only from <c>Messages</c> could under-report. The count that decides is taken at
	/// <c>MessageQueue.Add</c> as the line is written; the retained log is asserted as well, and
	/// both must say one.
	/// </para>
	/// </summary>
	internal static class KingdomFirstGuestNativeChecks
	{
		private static Frame Retained;

		internal static bool Vacant { get { return Retained == null; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, out bool Complete)
		{
			Complete = false;
			if (Verb == KingdomFirstGuestNativeProvider.SetupVerb)
			{
				Require(Retained == null, "a first-guest attempt is already retained");
				Retained = new Frame(Game, Zone);
				Retained.Start();
			}
			else
			{
				Require(Retained != null, "first-guest setup is absent");
				Retained.Check();
			}
			Complete = Retained.Done;
			return (Complete ? "native-first-guest cases=1 passed=1 failed=0"
				: "native-first-guest phase=" + Retained.Phase)
				+ "; synthetic-camp=true; ordinary-acceptance=false; save-load=untested"
				+ Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			if (Retained != null) Retained.Armed = false;
			return "native-first-guest cases=1 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ " observer-fault=" + Retained?.Fault + Retained?.Evidence;
		}

		/// <summary>Void observation at the real write. Counts, never rewrites, never suppresses.</summary>
		internal static void Observe(string Written)
		{
			Frame frame = Retained;
			if (frame == null || !frame.Armed) return;
			try { frame.Written(Written); }
			catch (Exception error)
			{
				if (frame.Fault == null) frame.Fault = KingdomScenarioRules.Bounded(
					error.GetType().Name + ": " + error.Message);
			}
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomFirstGuestNativeProvider.Require(Value, Failure);
		}

		private sealed class Frame
		{
			private const string GuestNeed = "A first guest is waiting for your answer. "
				+ "(Charter: read the first guest's correspondence)";
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly List<GameObject> Owned = new List<GameObject>();
			private KingdomSystem System;
			private string Raw, Stored;
			private int Emissions, Notes, PassOne;
			internal bool Armed, Done;
			internal int Phase;
			internal string Fault;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			/// <summary>Real founding, one dedicated store, and the exact line to watch for.</summary>
			internal void Start()
			{
				System = KingdomNativeCampFounding.Found(Game, Zone, Require);
				Require(System.Population == 0 && System.ClaimedZones.Contains(Zone.ZoneID),
					"the real founding is not an empty claimed camp");
				KingdomNativeCampFounding.Dedicate(Game, Zone, System,
					16 * KingdomRules.DramsPerArrival, Owned.Add, Require);
				Require(KingdomGrowth.CountStoredWater(Zone) >= KingdomRules.DramsPerArrival,
					"the dedicated store does not cover one arrival");
				Require(!KingdomGrowth.TryCountBeds(Zone, out int beds, out string roofFailure)
					|| beds == 0, "the fixture camp already has a roof, so it has no ordinary need");
				Raw = ConsoleLib.Console.ColorUtility.CapitalizeExceptFormatting("{{W|A traveller writes to "
					+ KingdomPresentation.Rich(System.KingdomDisplayName) + ".}} "
					+ "{{K|(Charter: read the first guest's correspondence)}}");
				Stored = ConsoleLib.Console.Markup.Transform(Raw);
				Notes = System.Ledger.Notes.Count;
				Require(Emissions == 0 && !KingdomFirstGuestRuntime.IsAwaitingAnswer(System),
					"a first guest was already awaiting an answer before any turn passed");
				Armed = true;
				Phase = 1;
				Evidence.Append("\nfounded tick=").Append(Game.TimeTicks)
					.Append("; next-arrival tick=").Append(System.NextArrivalTick)
					.Append("; dedicated drams=").Append(KingdomGrowth.CountStoredWater(Zone))
					.Append("; ledger notes before=").Append(Notes)
					.Append("; guest notes before=").Append(GuestNotes())
					.Append("; roofFailure=").Append(roofFailure == null ? "-" : "present");
			}

			/// <summary>
			/// After the due pass: one emission, a standing unanswered candidate, the guest line
			/// ahead of the still-present ordinary want, and no ledger note. The second call
			/// repeats it after a further due pass and requires the SAME single emission.
			/// </summary>
			internal void Check()
			{
				Require(Phase >= 1 && !Done, "first-guest setup did not run");
				Require(Fault == null, "the message observer faulted: " + Fault);
				Require(!KingdomScenarioAdvance.Pending, "turns are still owed");
				Require(Game.TimeTicks >= System.NextArrivalTick
					|| KingdomFirstGuestRuntime.IsAwaitingAnswer(System),
					"the clock never reached the first arrival tick");
				Require(KingdomFirstGuestRuntime.IsAwaitingAnswer(System),
					"no first guest is awaiting an answer after a real due pass");
				Require(Emissions == 1, "the opening message was written " + Emissions
					+ " time(s), not exactly once");
				Require(Logged() == 1, "the retained message log holds " + Logged()
					+ " copies of the opening line, not exactly one");
				// Ordinary notes cap at twelve, so a raw count would break on any unrelated
				// entry the settlement lawfully writes across six thousand real turns. What the
				// corrected acceptance forbids is a GUEST-SPECIFIC line, so that is what is read.
				Require(GuestNotes() == 0, "opening the correspondence wrote a first-guest "
					+ "ledger note, which the corrected acceptance forbids");
				string need = KingdomReports.NextNeed(System, Zone);
				Require(need.StartsWith(GuestNeed, StringComparison.Ordinal),
					"the next need does not open with the first-guest line");
				Require(need.Length > GuestNeed.Length + 1,
					"the deferred guest silenced the settlement's ordinary want");
				Require(need.Substring(GuestNeed.Length + 1).Contains("No roof stands."),
					"the ordinary want behind the guest line is not the camp's real roofless need");
				if (Phase == 1)
				{
					PassOne = Emissions;
					Phase = 2;
					Evidence.Append("\nfirst due pass tick=").Append(Game.TimeTicks)
						.Append("; emissions=").Append(Emissions)
						.Append("; next-need=guest-then-ordinary");
					return;
				}
				Require(Emissions == PassOne,
					"a second due pass re-opened the correspondence and wrote the message again");
				Armed = false;
				Done = true;
				Phase = 3;
				Evidence.Append("\nsecond due pass tick=").Append(Game.TimeTicks)
					.Append("; emissions still=").Append(Emissions)
					.Append("; awaiting=true; guest ledger notes=").Append(GuestNotes())
					.Append("; ledger notes=").Append(System.Ledger.Notes.Count);
			}

			/// <summary>Counts the exact opening line as the engine writes it.</summary>
			internal void Written(string Line)
			{
				if (Raw != null && string.Equals(Line, Raw, StringComparison.Ordinal)) Emissions++;
			}

			/// <summary>Ledger lines that name the guest. The corrected acceptance allows none.</summary>
			private int GuestNotes()
			{
				List<string> notes = System.Ledger.Notes;
				int found = 0;
				if (notes != null)
					for (int i = 0; i < notes.Count; i++)
						if (notes[i] != null && (notes[i].Contains("first guest")
							|| notes[i].Contains("A traveller writes"))) found++;
				return found;
			}

			private int Logged()
			{
				List<string> log = Game.Player?.Messages?.Messages;
				int found = 0;
				if (log != null)
					for (int i = 0; i < log.Count; i++)
						if (string.Equals(log[i], Stored, StringComparison.Ordinal)) found++;
				return found;
			}
		}
	}
}
