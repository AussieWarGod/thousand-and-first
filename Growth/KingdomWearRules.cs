using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for WHETHER a work wears, what that damage costs it, and what a repair
	/// job is waiting on (BUILDING-CATALOGUE-BRIEF.md Addendum 7: "maintenance/wear translation",
	/// Addendum 10(b): "damage degrades function &mdash; for every work, in its own kind").
	/// <para>
	/// <b>Time is labour, never decay.</b> Nothing here ever DECIDES that a work wore from a
	/// clock: every function that answers "did this take damage" is a pure function of an EVENT
	/// already in hand &mdash; a raid tick, a streak of consecutive full-stretch attended passes,
	/// a tick a certified machine was running &mdash; never of how long the save has existed.
	/// What a work that is ALREADY damaged goes on losing is a different question, and that one
	/// does run on world days (<see cref="Leaked"/>): a hole in a cistern empties it whether
	/// anybody is watching. The damage is still an event; only its consequence is a clock, and
	/// mending ends the consequence outright.
	/// </para>
	/// <para>
	/// What a work's wear COSTS to mend, how it runs while worn, and the one line a founder reads
	/// when a work takes damage all belong to the chain the wear was built from, and already live
	/// on <c>KingdomMaterialRules</c> (<c>MaxWearPercent</c>, <c>AddWear</c>,
	/// <c>ConditionPercent</c>/<c>ConditionWord</c>, <c>RepairCost</c>/<c>RepairBits</c>/
	/// <c>RepairEffort</c>, <c>DamageLine</c>) &mdash; this file calls those rather than keeping a
	/// second, divergent copy of them. What is uniquely this file's own: whether an event wears a
	/// work at all (the three causes and their kernel draws), what a damaged work of any kind is
	/// worth to the settlement afterwards (<see cref="WorkEffectiveness"/>), what a damaged STORE
	/// goes on losing while it stands unmended (<see cref="Leaked"/>), and whether a repair job
	/// already under way is READY, or waiting on hands, materials, or the founder's own standing
	/// wish.
	/// </para>
	/// <para>
	/// The three causes draw on <see cref="ThousandAndFirst.Simulation.Kernel.CounterRandom"/> the
	/// same way <see cref="KingdomConversionRules"/> does: a pure function of the settlement, the
	/// work, the cause, and the event's own ordinal (a raid tick, a hard-run milestone, an engine
	/// tick a machine was running), so a reload asks every open question exactly once more and
	/// never rerolls a question already answered. The engine-coupled half that walks a real
	/// <c>Survey.Works</c>, attaches <c>XRL.World.Parts.r_KingdomWear</c>, spends real stockpiled
	/// material and bits, and writes the founder-facing lines this file only composes, is
	/// <c>KingdomWear</c>, beside it.
	/// </para>
	/// </summary>
	public static partial class KingdomWearRules
	{
		public const int MaxSavedTextChars = 4096;
		public const int MaxRows = 256;
		public const int MaxRowsChars = 8192;
		public const int MaxObjectIdChars = 256;
		public const int MaxIntegerChars = 10;
		public const int MaxRepairPayloadChars = 64;

		public static bool SinkSettled(KingdomWearSinkDisposition State)
		{
			return State == KingdomWearSinkDisposition.Delivered
				|| State == KingdomWearSinkDisposition.Skipped
				|| State == KingdomWearSinkDisposition.Lost;
		}

		public static KingdomWearSinkDisposition RecoverUninspectable(
			KingdomWearSinkDisposition State)
		{
			return State == KingdomWearSinkDisposition.Attempting
				? KingdomWearSinkDisposition.Lost : State;
		}

		public static bool TryCanonicalIntRows(string Text, out int[] Values)
		{
			Values = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxRowsChars) return false;
			int separators = 0;
			for (int i = 0; i < Text.Length; i++)
			{
				if (Text[i] == '|') separators++;
				if (separators >= MaxRows) return false;
			}
			string[] rows = Text.Split('|');
			if (rows.Length == 0 || rows.Length > MaxRows) return false;
			Values = new int[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].Length == 0 || rows[i].Length > MaxIntegerChars
					|| !int.TryParse(rows[i], global::System.Globalization.NumberStyles.None,
						global::System.Globalization.CultureInfo.InvariantCulture, out Values[i])
					|| Values[i] < 0 || Values[i].ToString(
						global::System.Globalization.CultureInfo.InvariantCulture) != rows[i]) return false;
			}
			return true;
		}

		public static bool TryObjectIdRows(string Text, out string[] Values)
		{
			Values = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxRowsChars) return false;
			int separators = 0;
			for (int i = 0; i < Text.Length; i++)
			{
				if (Text[i] == '|') separators++;
				if (separators >= MaxRows) return false;
			}
			string[] rows = Text.Split('|');
			if (rows.Length == 0 || rows.Length > MaxRows) return false;
			for (int i = 0; i < rows.Length; i++)
			{
				if (string.IsNullOrEmpty(rows[i]) || rows[i].Length > MaxObjectIdChars) return false;
				for (int j = 0; j < i; j++)
					if (string.Equals(rows[j], rows[i], global::System.StringComparison.Ordinal)) return false;
			}
			Values = rows;
			return true;
		}

		public static bool TryRepairPayload(string Payload, out int Wear, out bool Finishing)
		{
			Wear = 0;
			Finishing = false;
			if (string.IsNullOrEmpty(Payload) || Payload.Length > MaxRepairPayloadChars) return false;
			int separators = 0;
			for (int i = 0; i < Payload.Length; i++) if (Payload[i] == '|') separators++;
			if (separators != 2) return false;
			string[] fields = Payload.Split('|');
			return fields.Length == 3 && fields[0] == "v1"
				&& fields[1].Length > 0 && fields[1].Length <= MaxIntegerChars
				&& int.TryParse(fields[1], global::System.Globalization.NumberStyles.None,
					global::System.Globalization.CultureInfo.InvariantCulture, out Wear)
				&& Wear > 0 && Wear.ToString(global::System.Globalization.CultureInfo.InvariantCulture) == fields[1]
				&& (fields[2] == "0" || fields[2] == "1")
				&& ((Finishing = fields[2] == "1") || fields[2] == "0");
		}

		/// <summary>Pure clock/phase law for an attended semantic pass.</summary>
		public static KingdomWearPassAction PassAction(long LastCompletedTick,
			long ActiveTick, KingdomWearPassPhase Phase, long NowTick)
		{
			if (LastCompletedTick < 0L || ActiveTick < 0L || NowTick < 0L
				|| Phase < KingdomWearPassPhase.None || Phase > KingdomWearPassPhase.Quarantined)
			{
				return KingdomWearPassAction.Quarantine;
			}
			if (Phase == KingdomWearPassPhase.Quarantined) return KingdomWearPassAction.Quarantine;
			if (NowTick < LastCompletedTick) return KingdomWearPassAction.Quarantine;
			if (NowTick == LastCompletedTick && LastCompletedTick > 0L)
			{
				return KingdomWearPassAction.AlreadyApplied;
			}
			if (Phase == KingdomWearPassPhase.None)
			{
				return ActiveTick == 0L ? KingdomWearPassAction.Start : KingdomWearPassAction.Quarantine;
			}
			return ActiveTick == NowTick
				? KingdomWearPassAction.Resume : KingdomWearPassAction.Quarantine;
		}

		/// <summary>Pure absolute-clock law for one work's storage loss.</summary>
		public static KingdomWearClockAction LeakClockAction(bool Initialized,
			long LastTick, long NowTick, int ElapsedDays)
		{
			if (LastTick < 0L || NowTick < 0L || ElapsedDays < 0 || NowTick < LastTick)
			{
				return KingdomWearClockAction.Quarantine;
			}
			if (!Initialized) return KingdomWearClockAction.Plant;
			return ElapsedDays == 0 ? KingdomWearClockAction.Wait : KingdomWearClockAction.Advance;
		}

		/// <summary>
		/// Exact-state recovery for damage, water, charge, or one bound food row. Only original
		/// state authorizes mutation; only exact intended state proves completion.
		/// </summary>
		public static KingdomWearMutationAction MutationAction(int Phase, int BoundPhase,
			int IntentPhase, int TerminalPhase, int Before, int Current, int After)
		{
			if (Before < 0 || Current < 0 || After < 0
				|| Phase < BoundPhase || Phase > TerminalPhase)
			{
				return KingdomWearMutationAction.Quarantine;
			}
			if (Phase >= TerminalPhase) return KingdomWearMutationAction.Wait;
			if (Current == After) return KingdomWearMutationAction.Confirm;
			if (Current == Before && Phase <= IntentPhase) return KingdomWearMutationAction.Apply;
			return KingdomWearMutationAction.Quarantine;
		}

		public static KingdomWearMutationAction DamageMutationAction(
			KingdomWearIncidentPhase Phase, int Before, int Current, int After)
		{
			return MutationAction((int)Phase, (int)KingdomWearIncidentPhase.Bound,
				(int)KingdomWearIncidentPhase.MutationIntent,
				(int)KingdomWearIncidentPhase.Quarantined, Before, Current, After);
		}

		public static KingdomWearMutationAction LeakMutationAction(
			KingdomWearLeakPhase Phase, int Before, int Current, int After)
		{
			if (Before < 0 || Current < 0 || After < 0 || After > Before)
				return KingdomWearMutationAction.Quarantine;
			if (Phase >= KingdomWearLeakPhase.Mutated
				&& Phase < KingdomWearLeakPhase.Quarantined) return KingdomWearMutationAction.Wait;
			if (Phase == KingdomWearLeakPhase.Bound && Current == Before)
				return KingdomWearMutationAction.Apply;
			// Persisted intent and a Bound receipt already shaped like After are both ambiguous:
			// only a same-stack callback frame may publish the physical loss.
			return KingdomWearMutationAction.Quarantine;
		}
	}
}
