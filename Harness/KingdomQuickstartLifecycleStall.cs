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
	internal static partial class KingdomQuickstartLifecycleStall
	{
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
			internal readonly int StageApplied;
			internal readonly int StageTarget;
			internal readonly List<string> Occupants;

			internal Reading(long Remaining, long LastWorked, long Required, string Window,
				string Crew, string Schema, int StageApplied, int StageTarget, List<string> Occupants)
			{
				this.Remaining = Remaining;
				this.LastWorked = LastWorked;
				this.Required = Required;
				this.Window = Window;
				this.Crew = Crew;
				this.Schema = Schema;
				this.StageApplied = StageApplied;
				this.StageTarget = StageTarget;
				this.Occupants = Occupants;
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
				reading.LastWorked, reading.Remaining, authored, reading.StageApplied,
				reading.StageTarget, reading.Occupants.Count);
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
