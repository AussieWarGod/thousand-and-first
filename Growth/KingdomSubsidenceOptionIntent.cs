using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Frozen option-transition evidence one subsidence step may retire against after a
	/// crash cut. Every field is captured at observation; nothing is re-read from the live option,
	/// the live toggle, or the current clock.</summary>
	internal sealed class KingdomSubsidenceOptionIntent
	{
		internal readonly bool PriorPresent;
		internal readonly string PriorWire;
		internal readonly string NextWire;
		internal readonly long BeforeTick;
		internal readonly long Sequence;
		internal readonly long RetiredTick;
		internal readonly string StepId;
		internal readonly long StepDueTick;

		internal KingdomSubsidenceOptionIntent(bool priorPresent, string priorWire, string nextWire,
			long beforeTick, long sequence, long retiredTick, string stepId, long stepDueTick)
		{
			PriorPresent = priorPresent; PriorWire = priorWire; NextWire = nextWire;
			BeforeTick = beforeTick; Sequence = sequence; RetiredTick = retiredTick;
			StepId = stepId; StepDueTick = stepDueTick;
		}
	}

	/// <summary>Pure law over that frozen evidence. An intent proves what the step book looked like
	/// before an anchor transition was published; it never invents a later clock.</summary>
	internal static class KingdomSubsidenceOptionIntentRules
	{
		internal const int MaxWireChars = 2048;
		private const string Prefix = "so1:";
		private const int Magic = 0x314F5354;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool TryPrepare(KingdomSubsidenceStepBook book, long beforeTick,
			KingdomSubsidenceOptionRules.Snapshot snapshot, out KingdomSubsidenceOptionIntent intent)
		{
			intent = null;
			if (book == null || !KingdomSubsidenceStepRules.Valid(book)
				|| book.Admission != KingdomSubsidenceAdmission.Admitted
				|| snapshot == null || !snapshot.Decision.Valid
				|| snapshot.Decision.Action != KingdomElapsedOptionAction.AnchorEnabled
					&& snapshot.Decision.Action != KingdomElapsedOptionAction.AnchorDisabled
				|| !Frozen(snapshot.Present, snapshot.PriorWire,
					out KingdomElapsedOptionRecord prior)) return false;
			// The snapshot is trusted only where the shared elapsed law reproduces it exactly from
			// the frozen prior and the next record's own state, token and tick.
			KingdomElapsedOptionRecord next = snapshot.Decision.Record;
			KingdomElapsedOptionDecision replay = KingdomElapsedOptionRules.Observe(prior,
				next.State == KingdomElapsedOptionState.Enabled, next.MasterResumeToken,
				next.ObservedTick);
			if (!replay.Valid || replay.Action != snapshot.Decision.Action
				|| replay.Transition != snapshot.Decision.Transition
				|| !string.Equals(KingdomElapsedOptionRules.Encode(replay.Record), snapshot.NextWire,
					StringComparison.Ordinal)
				|| beforeTick < 0 || beforeTick > next.ObservedTick) return false;
			KingdomSubsidenceStepOperation op = book.Active;
			string stepId = "";
			long due = 0;
			if (op != null)
			{
				if (op.Phase == KingdomSubsidenceStepPhase.Quarantined
					|| beforeTick != op.AnchorTick && beforeTick != op.DueTick
					|| next.ObservedTick < op.LastActivityTick) return false;
				stepId = op.Id; due = op.DueTick;
			}
			KingdomSubsidenceOptionIntent value = new KingdomSubsidenceOptionIntent(snapshot.Present,
				snapshot.PriorWire, snapshot.NextWire, beforeTick, book.Sequence,
				book.LastRetiredTick, stepId, due);
			if (!Valid(value)) return false;
			intent = value; return true;
		}

		internal static bool Valid(KingdomSubsidenceOptionIntent intent)
		{
			if (intent == null || intent.BeforeTick < 0 || intent.Sequence < 0
				|| intent.RetiredTick < 0 || intent.StepDueTick < 0
				|| intent.Sequence == 0 && intent.RetiredTick != 0
				|| intent.RetiredTick > intent.BeforeTick
				|| !Frozen(true, intent.NextWire, out KingdomElapsedOptionRecord next)
				|| !Frozen(intent.PriorPresent, intent.PriorWire, out KingdomElapsedOptionRecord prior)
				|| next.ObservedTick < prior.ObservedTick
				|| next.MasterResumeToken < prior.MasterResumeToken
				|| intent.BeforeTick > next.ObservedTick
				|| Transition(prior, next) == KingdomElapsedOptionTransition.None
				|| intent.StepId == null) return false;
			if (intent.StepId.Length == 0)
				return intent.StepDueTick == 0 && (intent.Sequence == 0 || intent.RetiredTick > 0);
			// The frozen step is one step length long: the intent froze either at its anchor or at
			// the due tick itself, and that due tick cannot precede the receipt it was begun after
			// nor follow the observation this transition was taken at.
			return intent.StepDueTick > 0 && intent.Sequence > 0
				&& (intent.StepDueTick - KingdomSubsidenceStepRules.StepTicks == intent.BeforeTick
					|| intent.BeforeTick == intent.StepDueTick)
				&& intent.StepDueTick > intent.RetiredTick
				&& intent.StepDueTick <= next.ObservedTick
				&& KingdomSubsidenceStepRules.IsStepId(intent.StepId);
		}

		internal static bool TrySnapshot(KingdomSubsidenceOptionIntent intent,
			out KingdomSubsidenceOptionRules.Snapshot snapshot)
		{
			snapshot = null;
			if (!Valid(intent) || !Frozen(true, intent.NextWire, out KingdomElapsedOptionRecord next)
				|| !Frozen(intent.PriorPresent, intent.PriorWire,
					out KingdomElapsedOptionRecord prior)) return false;
			snapshot = new KingdomSubsidenceOptionRules.Snapshot(intent.PriorPresent, intent.PriorWire,
				new KingdomElapsedOptionDecision(true, next, Transition(prior, next),
					next.State == KingdomElapsedOptionState.Enabled
						? KingdomElapsedOptionAction.AnchorEnabled
						: KingdomElapsedOptionAction.AnchorDisabled));
			return true;
		}

		/// <summary>Binding alone: no book validator is consulted, so the book's own validator may
		/// call this while it is still deciding whether that book is valid.</summary>
		internal static bool MatchesShape(KingdomSubsidenceOptionIntent intent,
			KingdomSubsidenceStepBook book)
		{
			if (!Valid(intent) || book == null || book.Sequence != intent.Sequence
				|| !Frozen(true, intent.NextWire, out KingdomElapsedOptionRecord next)) return false;
			KingdomSubsidenceStepOperation op = book.Active;
			if (op != null)
				// The freeze sits on one of this step's own edges and never earlier than the step's
				// last activity, and a cancelled step was cancelled by exactly this transition.
				return intent.StepId.Length != 0
					&& string.Equals(op.Id, intent.StepId, StringComparison.Ordinal)
					&& op.DueTick == intent.StepDueTick && book.LastRetiredTick == intent.RetiredTick
					&& (intent.BeforeTick == op.AnchorTick || intent.BeforeTick == op.DueTick)
					&& next.ObservedTick >= op.LastActivityTick
					&& (!op.CancelRequested || op.CancelTick == next.ObservedTick
						&& op.CancelToken == next.MasterResumeToken);
			if (intent.StepId.Length == 0) return book.LastRetiredTick == intent.RetiredTick;
			// The frozen step retired either for its full quota at its own due tick, or as a
			// partial cancellation spent at the observation that carried this transition.
			return book.LastRetiredTick == intent.StepDueTick
				|| book.LastRetiredTick == next.ObservedTick;
		}

		internal static bool Matches(KingdomSubsidenceOptionIntent intent,
			KingdomSubsidenceStepBook book)
		{
			return book != null && KingdomSubsidenceStepRules.Valid(book)
				&& book.Admission == KingdomSubsidenceAdmission.Admitted && MatchesShape(intent, book);
		}

		internal static bool TryCheckpoint(KingdomSubsidenceOptionIntent intent,
			KingdomSubsidenceStepBook book, long observed, out long target)
		{
			target = 0;
			if (!Matches(intent, book) || book.Active != null) return false;
			long frozen = FrozenTick(intent);
			// A retired receipt proves only this intent's own step retiring; an intent that held no
			// step must never repair a rollback onto some other step's retired tick.
			if (frozen < 0 || observed != intent.BeforeTick && observed != frozen
				&& (intent.StepId.Length == 0 || observed != book.LastRetiredTick)) return false;
			target = frozen; return true;
		}

		internal static bool TryEncode(KingdomSubsidenceOptionIntent intent, out string wire)
		{
			wire = null;
			if (!Valid(intent)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic);
					writer.Write((byte)(intent.PriorPresent ? 1 : 0));
					if (intent.PriorPresent) writer.Write(intent.PriorWire);
					writer.Write(intent.NextWire); writer.Write(intent.BeforeTick);
					writer.Write(intent.Sequence); writer.Write(intent.RetiredTick);
					writer.Write(intent.StepId); writer.Write(intent.StepDueTick);
					writer.Flush();
					if (stream.Length > MaxWireChars / 4 * 3 - 3) return false;
					wire = Prefix + Convert.ToBase64String(stream.ToArray());
					return wire.Length <= MaxWireChars;
				}
			}
			catch { wire = null; return false; }
		}

		internal static bool TryDecode(string wire, out KingdomSubsidenceOptionIntent intent)
		{
			intent = null;
			if (string.IsNullOrEmpty(wire) || wire.Length > MaxWireChars
				|| !wire.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(wire.Substring(Prefix.Length));
				using (MemoryStream stream = new MemoryStream(bytes, false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic) return false;
					byte present = reader.ReadByte();
					if (present > 1) return false;
					string priorWire = present == 1 ? reader.ReadString() : null;
					string nextWire = reader.ReadString();
					long beforeTick = reader.ReadInt64(), sequence = reader.ReadInt64();
					long retiredTick = reader.ReadInt64();
					string stepId = reader.ReadString();
					long stepDueTick = reader.ReadInt64();
					if (stream.Position != stream.Length) return false;
					KingdomSubsidenceOptionIntent value = new KingdomSubsidenceOptionIntent(
						present == 1, priorWire, nextWire, beforeTick, sequence, retiredTick,
						stepId, stepDueTick);
					if (!TryEncode(value, out string canonical) || canonical != wire) return false;
					intent = value; return true;
				}
			}
			catch { return false; }
		}

		/// <summary>Exactly the branches the shared law answers with an anchor action: a first
		/// observation, a changed module state, or a master relatch under an unchanged state.</summary>
		private static KingdomElapsedOptionTransition Transition(KingdomElapsedOptionRecord prior,
			KingdomElapsedOptionRecord next)
		{
			bool enabled = next.State == KingdomElapsedOptionState.Enabled;
			if (prior.State == KingdomElapsedOptionState.Unobserved)
				return enabled ? KingdomElapsedOptionTransition.InitializedEnabled
					: KingdomElapsedOptionTransition.InitializedDisabled;
			if (prior.State != next.State)
				return enabled ? KingdomElapsedOptionTransition.Enabled
					: KingdomElapsedOptionTransition.Disabled;
			if (prior.MasterResumeToken != next.MasterResumeToken)
				return enabled ? KingdomElapsedOptionTransition.MasterRelatchedEnabled
					: KingdomElapsedOptionTransition.MasterRelatchedDisabled;
			return KingdomElapsedOptionTransition.None;
		}

		/// <summary>Demonstrated absence carries a null wire; presence carries exact canonical
		/// bounded option bytes. A stored empty string is neither.</summary>
		private static bool Frozen(bool present, string wire, out KingdomElapsedOptionRecord record)
		{
			record = KingdomElapsedOptionRecord.Unobserved;
			if (!present) return wire == null;
			return wire != null && wire.Length != 0
				&& wire.Length <= KingdomElapsedOptionRules.MaxEncodedChars
				&& KingdomElapsedOptionRules.TryDecode(wire, out record)
				&& record.State != KingdomElapsedOptionState.Unobserved
				&& string.Equals(KingdomElapsedOptionRules.Encode(record), wire,
					StringComparison.Ordinal);
		}

		private static long FrozenTick(KingdomSubsidenceOptionIntent intent)
		{
			return Frozen(true, intent.NextWire, out KingdomElapsedOptionRecord next)
				? next.ObservedTick : -1L;
		}
	}
}
