using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	using XRL.World.Parts;

	/// <summary>
	/// Why a paid job was still Working when the turns ran out.
	///
	/// <para>"The turn budget expired" is true of every unfinished job and tells a reader nothing:
	/// a job that never had an hour of labour, one whose labour stopped, one whose settlement pass
	/// never ran, and one that simply needed longer all look the same from outside. This reads the
	/// state that separates them, by reference, at the moment the check runs, and names which case
	/// it found. It repairs nothing and waives nothing -- the row is still a refusal.</para>
	///
	/// <para>PLOT vs SCAFFOLD (native run 17, f691ab4). A commissioned job's progress lives on ONE
	/// of two positional parts depending on Job.Projection: Scaffold jobs carry RemainingTicks/
	/// LastWorkedTick on r_KingdomScaffold; PlotWorks jobs (a plotted design like "fire") carry the
	/// same facts as KingdomPlots.PlotWork{Remaining,LastTick,Required}Property string-encoded
	/// longs on the root itself (Growth/KingdomPlot2.26.Labour.cs), guarded by
	/// KingdomPlots.PlotWorkSchemaProperty == PlotWorkSchema. Reading the scaffold part off a
	/// plot-backed root always finds it absent (-1/-1), which used to read as "no-labour-ever"
	/// regardless of whether labour ever actually reached it. This now reads whichever lane the
	/// job's own Projection names, so an honest "no-labour-ever" and a merely-unreadable root no
	/// longer look the same.</para>
	///
	/// <para>Every field is read from production state: the job's own Phase, StartedTick, DueTick,
	/// UpdatedTick and InputReceipt (Growth/KingdomConstructionJob.cs:12-55); the crew properties
	/// on the works root (Growth/KingdomConstructionPresence.cs:23-27); and the settlement's
	/// LastSemanticTick (Core/KingdomSystem.z01.State.Foundation.cs:122).</para>
	/// </summary>
	internal static class KingdomQuickstartLifecycleStall
	{
		/// <summary>The settlement pass never reached this job at all.</summary>
		internal const string PassNeverRan = "pass-never-ran";

		/// <summary>Labour never touched it: the last worked tick is still absent or its start.</summary>
		internal const string NoLabourEver = "no-labour-ever";

		/// <summary>Labour was recorded, but the remaining work never came down.</summary>
		internal const string LabourStalled = "labour-stalled";

		/// <summary>Work is coming down; there simply were not enough turns.</summary>
		internal const string InsufficientTurns = "insufficient-turns";

		/// <summary>Journal verb column for the untruncated structured reading a refusal's own
		/// Bounded(...) 300-char stamped row cannot carry (native run 17). Registered as
		/// bookkeeping in Tools/personas/persona_matrix.py and tolerated by
		/// Tools/check-quickstart-lifecycle.py, so no persona's positional EXPECT has to name it.
		/// </summary>
		internal const string DetailRow = "lifecycle-grown-detail";

		/// <summary>What Describe/DetailMessage both read off the job's own works root, once.</summary>
		private readonly struct Reading
		{
			internal readonly long Remaining;
			internal readonly long LastWorked;
			internal readonly long Required;
			internal readonly string Window;
			internal readonly string Crew;
			internal readonly string Schema;

			internal Reading(long Remaining, long LastWorked, long Required, string Window,
				string Crew, string Schema)
			{
				this.Remaining = Remaining;
				this.LastWorked = LastWorked;
				this.Required = Required;
				this.Window = Window;
				this.Crew = Crew;
				this.Schema = Schema;
			}
		}

		/// <summary>
		/// One ASCII clause naming the classification and every field it rests on. Any field that
		/// cannot be read says so by name rather than being omitted or guessed.
		/// </summary>
		internal static string Describe(XRLGame Game, Zone Zone, KingdomSystem System,
			KingdomConstructionJob Job)
		{
			if (Game == null || Zone == null || System == null || Job == null)
				return "stall=unreadable; reason=no live game, zone, settlement or job";
			long now = Game.TimeTicks;
			Reading reading = Read(Zone, Job);
			long authored = reading.Required > 0L ? reading.Required : Authored(Job.TargetKey);
			string classification = Classify(System.LastSemanticTick, Job.StartedTick,
				reading.LastWorked, reading.Remaining, authored);
			return "stall=" + classification
				+ "; phase=" + Job.Phase + "; physical=" + Job.PhysicalPhase
				+ "; startedTick=" + Job.StartedTick + "; dueTick=" + Job.DueTick
				+ "; updatedTick=" + Job.UpdatedTick + "; nowTick=" + now
				+ "; inputReceipt=" + (string.IsNullOrEmpty(Job.InputReceipt) ? "absent" : "present")
				+ "; remainingTicks=" + reading.Remaining + "; lastWorkedTick=" + reading.LastWorked
				+ "; authoredTicks=" + authored + "; workWindow=" + reading.Window
				+ "; crew=" + reading.Crew
				+ "; lastSemanticTick=" + System.LastSemanticTick;
		}

		/// <summary>
		/// The untruncated reading behind Describe's stamped row, plus the settlement's one-gang
		/// allocation state (Growth/KingdomConstructionPresenceRules.cs) at the moment this ran.
		/// Read-only: nothing here binds a survey the caller did not already need, mutates a
		/// property, or assigns a gang. A field this cannot read honestly names itself
		/// absent/unavailable rather than being guessed or omitted. Meant for
		/// <see cref="DetailRow"/>, never for the stamped, length-bounded refusal row itself.
		/// </summary>
		internal static string DetailMessage(XRLGame Game, Zone Zone, KingdomSystem System,
			KingdomConstructionJob Job)
		{
			if (Game == null || Zone == null || System == null || Job == null)
				return "reason=no live game, zone, settlement or job";
			Reading reading = Read(Zone, Job);
			string roots = "unavailable";
			string candidates = "unavailable";
			string selectedId = "absent";
			string free = "unavailable";
			if (KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope, out string failure))
			{
				using (scope)
				{
					KingdomSurvey survey = KingdomSurvey.ActiveFor(Zone);
					if (survey != null)
					{
						int candidateCount = 0;
						for (int i = 0; i < survey.ConstructionRoots.Count; i++)
						{
							GameObject item = survey.ConstructionRoots[i];
							if (!GameObject.Validate(item)) continue;
							if (item.GetIntProperty(KingdomConstructionPresence.ActiveProperty) == 1)
								candidateCount++;
							if (item.GetIntProperty(KingdomConstructionPresence.SelectedProperty) == 1)
								selectedId = string.IsNullOrEmpty(item.IDIfAssigned)
									? "unassigned" : item.IDIfAssigned;
						}
						roots = survey.ConstructionRoots.Count.ToString(CultureInfo.InvariantCulture);
						candidates = candidateCount.ToString(CultureInfo.InvariantCulture);
						List<GameObject> available = KingdomCrews.AvailableSettlers(System, survey);
						int prefix = KingdomCrews.WorkHandCount(System, available);
						int freeCount = 0;
						for (int i = 0; i < prefix; i++)
							if (GameObject.Validate(available[i])
								&& KingdomStations.PostOf(available[i]) == 0) freeCount++;
						free = freeCount.ToString(CultureInfo.InvariantCulture);
					}
				}
			}
			else if (!string.IsNullOrEmpty(failure)) roots = "unavailable(" + Bounded(failure) + ")";
			return "selectedId=" + selectedId + "; candidates=" + candidates + "; roots=" + roots
				+ "; free=" + free + "; plotRemaining=" + reading.Remaining
				+ "; lastSemanticTick=" + System.LastSemanticTick + "; schema=" + reading.Schema;
		}

		/// <summary>The job's works root, read once for both Describe and DetailMessage.</summary>
		private static Reading Read(Zone Zone, KingdomConstructionJob Job)
		{
			long remaining = -1L;
			long lastWorked = -1L;
			long required = 0L;
			string window = "absent";
			string crew = "root-absent";
			string schema = "absent";
			GameObject root = null;
			if (!string.IsNullOrEmpty(Job.SubjectId)
				&& KingdomConstruction.FindExactId(Zone, Job.SubjectId, out root)
					== KingdomPhysicalLookupState.Exact && GameObject.Validate(root))
			{
				if (Job.Projection == KingdomConstructionProjection.PlotWorks)
				{
					if (root.GetIntProperty(KingdomPlots.PlotWorkSchemaProperty)
						== KingdomPlots.PlotWorkSchema)
					{
						if (TryLong(root, KingdomPlots.PlotWorkRemainingProperty, out long r))
							remaining = r;
						if (TryLong(root, KingdomPlots.PlotWorkLastTickProperty, out long l))
							lastWorked = l;
						if (TryLong(root, KingdomPlots.PlotWorkRequiredProperty, out long q))
							required = q;
					}
					string encoded = root.GetStringProperty(KingdomPlots.PlotWorkWindowProperty);
					if (!string.IsNullOrEmpty(encoded)) window = encoded;
				}
				else
				{
					r_KingdomScaffold scaffold = root.GetPart<r_KingdomScaffold>();
					if (scaffold != null)
					{
						remaining = scaffold.RemainingTicks;
						lastWorked = scaffold.LastWorkedTick;
					}
					string encoded = root.GetStringProperty(r_KingdomScaffold.WorkWindowProperty);
					if (!string.IsNullOrEmpty(encoded)) window = encoded;
				}
				schema = Text(root, KingdomConstructionPresence.SchemaProperty);
				crew = "selected=" + Text(root, KingdomConstructionPresence.SelectedProperty)
					+ " hands=" + Text(root, KingdomConstructionPresence.HandsProperty)
					+ " effectiveness=" + Text(root, KingdomConstructionPresence.EffectivenessProperty)
					+ " schema=" + schema;
			}
			return new Reading(remaining, lastWorked, required, window, crew, schema);
		}

		/// <summary>
		/// The four cases, in the order that makes each answer the previous one's absence: a pass
		/// that never reached this job explains everything downstream of it; then labour that
		/// never happened; then labour that happened without the work coming down; and only then
		/// the ordinary case of a job that needed more turns.
		/// </summary>
		internal static string Classify(long LastSemanticTick, long StartedTick, long LastWorkedTick,
			long RemainingTicks, long AuthoredTicks)
		{
			if (LastSemanticTick < StartedTick) return PassNeverRan;
			if (LastWorkedTick <= 0L || LastWorkedTick == StartedTick) return NoLabourEver;
			if (AuthoredTicks > 0L && RemainingTicks >= AuthoredTicks) return LabourStalled;
			if (RemainingTicks < 0L) return LabourStalled;
			return InsufficientTurns;
		}

		/// <summary>The design's authored build ticks, or zero when the catalogue cannot say.</summary>
		private static long Authored(string TargetKey)
		{
			return KingdomData.TryGetBuilding(TargetKey, out KingdomRules.BuildEntry entry)
				&& entry != null ? entry.BuildTicks : 0L;
		}

		/// <summary>A property as text, with absence named rather than blank.</summary>
		private static string Text(GameObject Root, string Property)
		{
			if (Root.HasStringProperty(Property))
				return Bounded(Root.GetStringProperty(Property));
			if (Root.HasIntProperty(Property))
				return Root.GetIntProperty(Property).ToString();
			return "absent";
		}

		/// <summary>A string-encoded long property, the same shape
		/// Growth/KingdomPlot2.26.Labour.cs's own (private) TryGetPlotWorkLong writes.</summary>
		private static bool TryLong(GameObject Object, string Property, out long Value)
		{
			Value = 0L;
			return Object != null && long.TryParse(Object.GetStringProperty(Property),
				NumberStyles.Integer, CultureInfo.InvariantCulture, out Value);
		}

		/// <summary>Printable ASCII, bounded, with the row's separators kept out.</summary>
		private static string Bounded(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "empty";
			int length = Value.Length > 64 ? 64 : Value.Length;
			System.Text.StringBuilder text = new System.Text.StringBuilder(length);
			for (int i = 0; i < length; i++)
			{
				char value = Value[i];
				text.Append(value >= ' ' && value <= '~' && value != ';' && value != '\t'
					? value : '?');
			}
			return text.ToString();
		}
	}
}
