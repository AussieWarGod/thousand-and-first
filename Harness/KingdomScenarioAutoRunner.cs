using System;
using System.Collections.Generic;
using System.Reflection;
using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst
{
	/// <summary>
	/// Unattended scenario execution: runs the sealed script once, hands-free, in the built world,
	/// and journals every step.
	/// <para>
	/// SEAM. Registered from the scenario mode in <c>Harness/EmbarkModules.xml</c> as
	/// <c>&lt;gamesystem Class="ThousandAndFirst.KingdomScenarioAutoRunner"/&gt;</c>, the same way
	/// the mode already registers <c>ThousandAndFirst.KingdomSuccession</c>.
	/// <c>QudGamemodeModule.bootGame</c> hands each declared class to <c>XRLGame.AddSystem</c>,
	/// which resolves it through <c>ModManager.CreateInstance</c>, so a mod type registered by name
	/// is reached.
	/// </para>
	/// <para>
	/// TWO SEAMS, ONE RUN. <see cref="OnAdded" /> is the PRIMER and <c>BeginTakeActionEvent</c> is
	/// the RUN. <c>QudGamemodeModule</c> is the second embark module and <c>QudGameBootModule</c> the
	/// last, so the <c>AddSystem</c> that lands this runner - and therefore <c>OnAdded</c> - happens
	/// before a single boot event fires: before world init, before the starting zone is generated,
	/// and long before <c>QudSpecificBootHandlersModule</c>'s blocking "You embark for the caves of
	/// Qud." popup on <c>GameStarting</c>. That earliness is load-bearing and was learned live:
	/// priming on <c>ZoneActivatedEvent</c> instead still let a village arrival announcement through,
	/// because <c>VillageSurface</c> reveals fire off zone build and cell entry, both of which
	/// precede it. That event is kept below only as a fallback. The script itself still waits for the
	/// first player action opportunity, the first moment the world is genuinely playable:
	/// <c>ActionManager.RunSegment</c> fires <c>BeginTakeActionEvent</c> before it ever reaches
	/// <c>XRLCore.PlayerTurn</c>'s input wait, and anything earlier would run scenario verbs against
	/// a half-built world.
	/// </para>
	/// <para>
	/// POPUPS, UNDER A SEALED SCRIPT ONLY. The primer raises the engine's own <c>Popup.Suppress</c>,
	/// which every <c>Popup.Show</c> / <c>ShowBlock</c> / <c>ShowSpace</c> / <c>ShowBlockPrompt</c> /
	/// <c>ShowBlockSpace</c> path already honours by routing the message to
	/// <c>MessageQueue.AddPlayerMessage</c> and returning <c>Keys.Space</c> - the engine's own
	/// auto-acknowledge, and exactly the key an operator was pressing. Nothing is lost: every
	/// suppressed message is in the player's message log. It is raised ONLY when a sealed script is
	/// present, so the attended path keeps every popup, and it is lowered again the moment the run
	/// ends by any route. The later vanilla opening-story popup has its own narrow, exception-safe
	/// sealed-profile bracket in <c>KingdomScenarioOpeningStoryPatch</c>.
	/// </para>
	/// <para>
	/// SUSPENDS AND RESUMES. <c>advance</c> makes a script span turns, so the verb list and a cursor
	/// live across events (see <see cref="KingdomScenarioAdvance" />). The cursor is session state:
	/// the durable one-shot below already forbids a replay after a reload, so a script interrupted
	/// by a save simply does not resume, and the journal's last row says where it stopped.
	/// </para>
	/// <para>
	/// NEVER AUTO-QUITS and never prevents the player's action; it leaves the game exactly where the
	/// operator can look at it. INERT WHEN THE SCRIPT IS ABSENT - a prepared profile with no sealed
	/// script is an ordinary attended profile, and nothing here writes a row, suppresses a popup, or
	/// spends a turn.
	/// </para>
	/// </summary>
	[Serializable]
	public sealed class KingdomScenarioAutoRunner : IPlayerSystem
	{
		private const int SerializationMagic = 1414746964;
		private const int CurrentSerializationVersion = 1;

		/// <summary>Journal verb column naming which seam started the run, and where.</summary>
		internal const string ArmedRow = "RUNNER-ARMED";

		/// <summary>Journal verb column for the row that opens a scripted run.</summary>
		internal const string BeginRow = "SCRIPT-BEGIN";

		/// <summary>Journal verb column for a script that ran every verb without a refusal.</summary>
		internal const string CompleteRow = "SCRIPT-COMPLETE";

		/// <summary>Journal verb column for a script stopped by a refusal or a fault.</summary>
		internal const string StoppedRow = "SCRIPT-STOPPED";

		private int SerializationVersion = CurrentSerializationVersion;

		/// <summary>
		/// Durable one-shot. Serialized, not merely per-session: the sole mutating verb refuses
		/// permanently once its transaction marker stands, so a replay after a reload could only
		/// ever append refusals to the journal and muddy the evidence.
		/// </summary>
		private bool ScriptConsidered;

		/// <summary>The primer's seam description, or null when it has not fired.</summary>
		[NonSerialized]
		private string PrimedSeam;

		/// <summary>Raised only by this runner, and only under a sealed script. Lowered on every exit.</summary>
		[NonSerialized]
		private bool SuppressedPopups;

		[NonSerialized]
		private IList<string> Verbs;

		[NonSerialized]
		private int Cursor;

		public override bool WantFieldReflection => false;

		/// <summary>
		/// Announces the turn pump for <c>advance</c> and raises the popup bracket.
		/// <c>XRLGame.AddSystem</c> calls this exactly once per game, before any boot event, which
		/// is both the earliest point a popup can be suppressed and where any state a previous game
		/// in this process left behind is cleared. It is deliberately NOT done in
		/// <see cref="RegisterPlayer" />: the engine calls that method for unregistration too, so a
		/// side effect there would fire on both.
		/// </summary>
		public override void OnAdded()
		{
			base.OnAdded();
			KingdomScenarioAdvance.ArmDriver();
			Prime("IGameSystem.OnAdded, before the boot sequence");
		}

		/// <summary>Game-scoped registration, the same shape the base game's own systems use.</summary>
		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(ZoneActivatedEvent.ID);
		}

		public override void RegisterPlayer(GameObject Player, IEventRegistrar Registrar)
		{
			Registrar.Register(BeginTakeActionEvent.ID);
		}

		/// <summary>
		/// Fallback primer, for the case where <see cref="OnAdded" /> could not read the sealed
		/// script - a game restored rather than booted, say. It cannot catch a popup that already
		/// fired during zone build, which is exactly why it is not the primary seam.
		/// </summary>
		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			GameObject player = The.Player;
			if (E != null && E.Zone != null && player != null
				&& ReferenceEquals(player.CurrentZone, E.Zone))
				Prime("ZoneActivatedEvent " + E.Zone.ZoneID);
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Raises the popup bracket once, and only under a sealed script. Records the seam so the
		/// armed row can say which one it was rather than asserting a mechanism nobody checked.
		/// </summary>
		private void Prime(string Seam)
		{
			if (PrimedSeam != null || ScriptConsidered) return;
			if (!KingdomScenarioScript.Present()) return;
			PrimedSeam = Seam;
			SuppressedPopups = Popup.Suppress = true;
		}

		public override bool HandleEvent(BeginTakeActionEvent E)
		{
			if (E != null && E.Object != null && E.Object.IsPlayer())
				KingdomSystem.Guard("scenario auto-runner", Step);
			return base.HandleEvent(E);
		}

		/// <summary>
		/// One action opportunity. Starts the script on the first one, resumes it after a wait, and
		/// otherwise does nothing at all.
		/// </summary>
		private void Step()
		{
			if (KingdomScenarioAdvance.Pending)
			{
				bool faulted;
				if (KingdomScenarioAdvance.Pump(out faulted)) return;
				// A wait armed by the attended wish carries no script, and abandoning it is
				// already journalled under its own reason code; only a scripted run stops here.
				if (faulted)
				{
					if (Verbs != null) Finish(StoppedRow, false, "the advance was abandoned");
					return;
				}
			}
			else if (!ScriptConsidered)
			{
				// Set BEFORE running: a verb that throws must not leave the script armed for the
				// next turn, or one fault would repeat every turn for the rest of the session.
				ScriptConsidered = true;
				if (!Begin()) return;
			}
			if (Verbs != null) Continue();
		}

		/// <summary>
		/// Reads the sealed script and opens the run. Returns false when there is nothing to run,
		/// which is the ordinary attended case as well as a refusal.
		/// </summary>
		private bool Begin()
		{
			if (!KingdomScenarioScript.Present()) { Release(); return false; }
			IList<string> verbs;
			string failure;
			if (!KingdomScenarioScript.TryRead(out verbs, out failure))
			{
				Finish(StoppedRow, false, failure);
				return false;
			}
			UnityEngine.Application.runInBackground = true; // in-world only; mid-boot crashed
			XRL.World.ZoneBuilders.KingdomScenarioTestGroundBuilder.Restrip(The.Player?.CurrentZone);
			KingdomScenarioJournal.Append(ArmedRow, true, "armed by BeginTakeActionEvent; popups "
				+ (SuppressedPopups ? "suppressed from " + PrimedSeam : "NOT suppressed - no primer "
					+ "seam fired, so the boot and arrival popups still need a keypress"));
			KingdomScenarioJournal.Append(BeginRow, true,
				verbs.Count + " verb(s) from " + KingdomScenarioScript.Locate());
			Verbs = verbs;
			Cursor = 0;
			return true;
		}

		/// <summary>
		/// Executes verbs through the SAME entry the wish uses, so an unattended run and an attended
		/// one produce identical verbs, identical text, and identical journal rows. Stops on the
		/// first refusal: a scenario's steps are ordered, and running <c>realize</c> after
		/// <c>flatten</c> refused would stage onto ground nobody prepared. Returns to the caller as
		/// soon as a verb arms a wait, which is what lets the game loop actually run the turns.
		/// </summary>
		private void Continue()
		{
			while (Cursor < Verbs.Count)
			{
				string verb = Verbs[Cursor];
				Cursor++;
				bool ok;
				try
				{
					KingdomScenarioVerbs.Invoke(verb, out ok);
				}
				catch (Exception exception)
				{
					// The verb's own row never landed, so this row is the only record of it. A
					// throwing verb is a stop, exactly like a refusal.
					Finish(StoppedRow, false, "verb '" + verb + "' threw: "
						+ KingdomScenarioRules.Bounded(exception.Message));
					return;
				}
				if (!ok)
				{
					Finish(StoppedRow, false, "refused at verb " + Cursor + " of " + Verbs.Count
						+ ": " + verb);
					return;
				}
				if (KingdomScenarioAdvance.Pending) return;
			}
			Finish(CompleteRow, true, Verbs.Count + " verb(s) ran without a refusal");
		}

		/// <summary>Closes the run: one final row, then the popup bracket is released.</summary>
		private void Finish(string Row, bool Ok, string Message)
		{
			Verbs = null;
			Cursor = 0;
			KingdomScenarioJournal.Append(Row, Ok, Message);
			Release();
		}

		/// <summary>
		/// Lowers the suppression this runner raised, and only that. The flag is the engine's own
		/// global, so it is never cleared blindly - an unrelated caller's bracket must survive.
		/// </summary>
		private void Release()
		{
			if (!SuppressedPopups) return;
			SuppressedPopups = false;
			Popup.Suppress = false;
		}

		public override void Write(SerializationWriter Writer)
		{
			SerializationVersion = CurrentSerializationVersion;
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(KingdomScenarioAutoRunner),
				BindingFlags.Instance | BindingFlags.NonPublic);
		}

		public override void Read(SerializationReader Reader)
		{
			int magic = Reader.ReadInt32();
			int version = Reader.ReadInt32();
			if (magic != SerializationMagic || version < 1
				|| version > CurrentSerializationVersion)
				throw new InvalidOperationException(
					"Unsupported ThousandAndFirst scenario auto-runner save block.");
			Reader.ReadNamedFields(this, typeof(KingdomScenarioAutoRunner),
				BindingFlags.Instance | BindingFlags.NonPublic);
			if (SerializationVersion != version)
				throw new InvalidOperationException(
					"Unsupported ThousandAndFirst scenario auto-runner named-field version.");
		}
	}
}
