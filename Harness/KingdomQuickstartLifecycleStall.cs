using System;
using XRL;
using XRL.World;

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
	/// <para>Every field is read from production state: the job's own Phase, StartedTick, DueTick,
	/// UpdatedTick and InputReceipt (Growth/KingdomConstructionJob.cs:12-55); the scaffold's
	/// RemainingTicks and LastWorkedTick (Growth/KingdomScaffold.cs:60,64); the crew properties on
	/// the works root (Growth/KingdomConstructionPresence.cs:23-27) and the scaffold's own work
	/// window (Growth/KingdomScaffold.LabourWindow.cs:8); and the settlement's LastSemanticTick
	/// (Core/KingdomSystem.z01.State.Foundation.cs:122).</para>
	/// </summary>
	internal static class KingdomQuickstartLifecycleStall
	{
		/// <summary>The settlement pass never reached this job at all.</summary>
		internal const string PassNeverRan = "pass-never-ran";

		/// <summary>Labour never touched it: the scaffold's last worked tick is still its start.</summary>
		internal const string NoLabourEver = "no-labour-ever";

		/// <summary>Labour was recorded, but the remaining work never came down.</summary>
		internal const string LabourStalled = "labour-stalled";

		/// <summary>Work is coming down; there simply were not enough turns.</summary>
		internal const string InsufficientTurns = "insufficient-turns";

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
			long remaining = -1L;
			long lastWorked = -1L;
			string window = "absent";
			string crew = "root-absent";
			GameObject root = null;
			if (!string.IsNullOrEmpty(Job.SubjectId)
				&& KingdomConstruction.FindExactId(Zone, Job.SubjectId, out root)
					== KingdomPhysicalLookupState.Exact && GameObject.Validate(root))
			{
				r_KingdomScaffold scaffold = root.GetPart<r_KingdomScaffold>();
				if (scaffold != null)
				{
					remaining = scaffold.RemainingTicks;
					lastWorked = scaffold.LastWorkedTick;
				}
				string encoded = root.GetStringProperty(r_KingdomScaffold.WorkWindowProperty);
				if (!string.IsNullOrEmpty(encoded)) window = encoded;
				crew = "selected=" + Text(root, KingdomConstructionPresence.SelectedProperty)
					+ " hands=" + Text(root, KingdomConstructionPresence.HandsProperty)
					+ " effectiveness=" + Text(root, KingdomConstructionPresence.EffectivenessProperty)
					+ " schema=" + Text(root, KingdomConstructionPresence.SchemaProperty);
			}
			long authored = Authored(Job.TargetKey);
			string classification = Classify(System.LastSemanticTick, Job.StartedTick,
				lastWorked, remaining, authored);
			return "stall=" + classification
				+ "; phase=" + Job.Phase + "; physical=" + Job.PhysicalPhase
				+ "; startedTick=" + Job.StartedTick + "; dueTick=" + Job.DueTick
				+ "; updatedTick=" + Job.UpdatedTick + "; nowTick=" + now
				+ "; inputReceipt=" + (string.IsNullOrEmpty(Job.InputReceipt) ? "absent" : "present")
				+ "; remainingTicks=" + remaining + "; lastWorkedTick=" + lastWorked
				+ "; authoredTicks=" + authored + "; workWindow=" + window
				+ "; crew=" + crew
				+ "; lastSemanticTick=" + System.LastSemanticTick;
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
