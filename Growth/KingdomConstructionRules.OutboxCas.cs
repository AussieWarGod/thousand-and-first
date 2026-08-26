using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		public static bool ValidOutbox(KingdomConstructionOutbox Outbox)
		{
			if (Outbox == null) return true;
			if (!TextLength(Outbox.EventId, 1, 256) || Outbox.Mode < 1 || Outbox.Mode > 4
				|| !TextLength(Outbox.Chronicle, 0, MaxOutboxTextChars)
				|| !TextLength(Outbox.Ledger, 0, MaxOutboxTextChars)
				|| !TextLength(Outbox.Message, 0, MaxOutboxTextChars)
				|| !TextLength(Outbox.Deed, 0, MaxOutboxTextChars)
				|| !ValidSink(Outbox.Chronicle, Outbox.ChronicleState)
				|| !ValidSink(Outbox.Ledger, Outbox.LedgerState)
				|| !ValidSink(Outbox.Message, Outbox.MessageState)
				|| !ValidSink(Outbox.Deed, Outbox.DeedState)) return false;
			bool receiptEmpty = Outbox.LedgerBeforeCount == -1
				&& string.IsNullOrEmpty(Outbox.LedgerBeforeHash)
				&& Outbox.LedgerAfterCount == -1
				&& string.IsNullOrEmpty(Outbox.LedgerAfterHash);
			bool receiptComplete = Outbox.LedgerBeforeCount >= 0
				&& Outbox.LedgerBeforeCount < MaxLedgerNotes
				&& Outbox.LedgerAfterCount == Outbox.LedgerBeforeCount + 1
				&& IsSha256(Outbox.LedgerBeforeHash) && IsSha256(Outbox.LedgerAfterHash);
			if (!receiptEmpty && !receiptComplete) return false;
			if (Outbox.LedgerState == KingdomConstructionSinkDisposition.Attempting
				&& !receiptComplete) return false;
			return true;
		}

		public static bool TryCounterAfter(int Before, int Delta, out int After)
		{
			After = 0;
			if (Delta <= 0 || Before < 0 || Before > int.MaxValue - Delta) return false;
			After = Before + Delta;
			return true;
		}

		public static KingdomConstructionCasAction CounterCasAction(int Current,
			int Before, int After)
		{
			if (Before < 0 || After <= Before) return KingdomConstructionCasAction.Quarantine;
			if (Current == Before) return KingdomConstructionCasAction.Apply;
			return Current == After ? KingdomConstructionCasAction.Confirm
				: KingdomConstructionCasAction.Quarantine;
		}

		/// <summary>Strong, length-framed hash for an inspectable ledger snapshot.</summary>
		public static string HashLedger(IList<string> Notes)
		{
			if (Notes == null || Notes.Count > MaxLedgerNotes) return null;
			StringBuilder framed = new StringBuilder();
			for (int i = 0; i < Notes.Count; i++)
			{
				string note = Notes[i] ?? "";
				if (note.Length > MaxOutboxTextChars) return null;
				framed.Append(note.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
					.Append(note);
			}
			return Sha256(framed.ToString());
		}

		public static bool TryFreezeLedger(IList<string> Notes, string Entry,
			out int BeforeCount, out string BeforeHash, out int AfterCount,
			out string AfterHash)
		{
			BeforeCount = AfterCount = -1;
			BeforeHash = AfterHash = null;
			if (Notes == null || Notes.Count >= MaxLedgerNotes
				|| !TextLength(Entry, 1, MaxOutboxTextChars)) return false;
			BeforeCount = Notes.Count;
			BeforeHash = HashLedger(Notes);
			List<string> after = new List<string>(Notes);
			after.Add(Entry);
			AfterCount = after.Count;
			AfterHash = HashLedger(after);
			return IsSha256(BeforeHash) && IsSha256(AfterHash);
		}

		public static KingdomConstructionCasAction LedgerCasAction(IList<string> Notes,
			int BeforeCount, string BeforeHash, int AfterCount, string AfterHash)
		{
			if (Notes == null || BeforeCount < 0 || AfterCount != BeforeCount + 1
				|| !IsSha256(BeforeHash) || !IsSha256(AfterHash))
				return KingdomConstructionCasAction.Quarantine;
			string hash = HashLedger(Notes);
			if (Notes.Count == BeforeCount && hash == BeforeHash)
				return KingdomConstructionCasAction.Apply;
			return Notes.Count == AfterCount && hash == AfterHash
				? KingdomConstructionCasAction.Confirm : KingdomConstructionCasAction.Quarantine;
		}

		public static string InterruptedFundingDiagnostic(KingdomConstructionPhase Phase)
		{
			if (Phase == KingdomConstructionPhase.WaterPending)
				return "A save interrupted the aggregate water debit; exact vessel bindings were not persisted. Inspect stores; automatic recharge is disabled.";
			if (Phase == KingdomConstructionPhase.MaterialPending)
				return "A save interrupted the aggregate material debit; exact source bindings were not persisted. Inspect stores; automatic recharge is disabled.";
			return null;
		}

		public static bool CanSupersedeTerminal(KingdomConstructionJob Job,
			string OwnerKey, string ZoneId, string ReceiptId, string ObjectId)
		{
			if (Job == null || Job.Id != ReceiptId || Job.OwnerKey != OwnerKey
				|| Job.ZoneId != ZoneId || !IsTerminal(Job.Phase)
				|| (!Job.Compacted && !TerminalClosureSettled(Job))
				|| string.IsNullOrEmpty(ObjectId)) return false;
			return Job.OutputId == ObjectId || (string.IsNullOrEmpty(Job.OutputId)
				&& Job.SourceId == ObjectId && Job.SubjectId == ObjectId);
		}

		/// <summary>Last row/active slot is reserved for one durable saturation diagnostic.</summary>
		public static bool CapacityInspectionRequired(int TotalRows, int ActiveRows)
		{
			return TotalRows >= MaxRows - 1 || ActiveRows >= MaxActiveRows - 1;
		}

		private static bool ValidSink(string Text, KingdomConstructionSinkDisposition State)
		{
			if (State <= KingdomConstructionSinkDisposition.None
				|| State > KingdomConstructionSinkDisposition.Lost) return false;
			return State == KingdomConstructionSinkDisposition.Skipped
				? string.IsNullOrEmpty(Text) : !string.IsNullOrEmpty(Text);
		}

		public static KingdomConstructionResumeAction ResumeAction(KingdomConstructionJob Job)
		{
			if (Job == null || Job.Claims == null || IsTerminal(Job.Phase))
			{
				return KingdomConstructionResumeAction.None;
			}
			if (!Job.Claims.Exact || IsMutationPending(Job.Phase)
				|| Job.Phase == KingdomConstructionPhase.InspectionRequired)
			{
				return KingdomConstructionResumeAction.Inspect;
			}
			if (Job.Phase == KingdomConstructionPhase.Published
				|| Job.Phase == KingdomConstructionPhase.WaterSettled
				|| Job.Claims.WaterOutstanding > 0 || !MaterialOutstanding(Job.Claims).IsEmpty)
			{
				return KingdomConstructionResumeAction.ResumeFunding;
			}
			if (Job.Phase == KingdomConstructionPhase.Funded
				|| Job.Phase == KingdomConstructionPhase.Outstanding)
			{
				return KingdomConstructionResumeAction.RetryProjection;
			}
			if (Job.Phase == KingdomConstructionPhase.Projected || Job.Phase == KingdomConstructionPhase.Working)
			{
				return KingdomConstructionResumeAction.AdvanceWork;
			}
			return KingdomConstructionResumeAction.Inspect;
		}

	}
}
