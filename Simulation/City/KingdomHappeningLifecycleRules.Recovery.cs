using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomHappeningLifecycleRules
	{
		internal static KingdomHappeningLifecycleBook RecoverInterruptedSinks(
			KingdomHappeningLifecycleBook book, long nowTick)
		{
			KingdomHappeningOperation operation = book == null ? null : book.Active;
			if (operation == null || nowTick < operation.UpdatedTick) return book;
			KingdomHappeningSinkState effect = RecoverUninspectable(operation.EffectState);
			KingdomHappeningSinkState ledger = RecoverUninspectable(operation.LedgerState);
			KingdomHappeningSinkState message = RecoverUninspectable(operation.MessageState);
			if (effect == operation.EffectState && ledger == operation.LedgerState
				&& message == operation.MessageState) return book;
			return new KingdomHappeningLifecycleBook(book.Sequence, operation.WithSinks(
				operation.ChronicleState, operation.ToldState, effect, ledger, message, nowTick),
				book.SemanticReceipts);
		}

		internal static KingdomHappeningResumeAction ResumeAction(
			KingdomHappeningOperation operation, long nowTick, bool founderHere,
			bool fixtureExact, bool participantsExact, bool allArrived, bool useReceiptExact)
		{
			if (!ValidOperation(operation) || nowTick <= 0L)
				return KingdomHappeningResumeAction.Refuse;
			if (operation.Phase == KingdomHappeningLifecyclePhase.Restoring)
				return KingdomHappeningResumeAction.Restore;
			// Ready is durable proof that attendance already completed. Later zone departure,
			// fixture loss, or reload cannot retroactively turn a witnessed rite into a report.
			if (operation.Phase == KingdomHappeningLifecyclePhase.Ready)
			{
				if (operation.ExternalSemantic
					&& nowTick - operation.UpdatedTick >= ExternalReadyTimeoutTicks)
					return KingdomHappeningResumeAction.Restore;
				return operation.ExternalSemantic ? KingdomHappeningResumeAction.WaitExternal
					: KingdomHappeningResumeAction.Publish;
			}
			if (!operation.Physical) return KingdomHappeningResumeAction.Refuse;
			if (!fixtureExact || !participantsExact || !founderHere
				|| (operation.Phase == KingdomHappeningLifecyclePhase.Walking
					&& nowTick - operation.StartedTick >= WalkTimeoutTicks))
				return KingdomHappeningResumeAction.Restore;
			switch (operation.Phase)
			{
			case KingdomHappeningLifecyclePhase.Prepared:
				return KingdomHappeningResumeAction.PreparePosts;
			case KingdomHappeningLifecyclePhase.Walking:
				return allArrived ? KingdomHappeningResumeAction.BeginHold
					: KingdomHappeningResumeAction.WaitForArrival;
			case KingdomHappeningLifecyclePhase.Holding:
				if (!allArrived || !useReceiptExact)
					return KingdomHappeningResumeAction.Restore;
				return nowTick < operation.HoldUntilTick
					? KingdomHappeningResumeAction.WaitHold
					: KingdomHappeningResumeAction.Publish;
			default:
				return KingdomHappeningResumeAction.Refuse;
			}
		}

		internal static bool SinksSettled(KingdomHappeningOperation operation)
		{
			return operation != null && Terminal(operation.ChronicleState)
				&& Terminal(operation.ToldState) && Terminal(operation.EffectState)
				&& Terminal(operation.LedgerState) && Terminal(operation.MessageState);
		}

		internal static bool RestorationSettled(KingdomHappeningOperation operation)
		{
			if (operation == null || !operation.FixtureRestored) return false;
			for (int i = 0; i < operation.Participants.Length; i++)
				if (!operation.Participants[i].Restored) return false;
			return true;
		}

		internal static bool AlreadyCompleted(KingdomHappeningLifecycleBook book,
			KingdomPhysicalHappeningKind kind, int subjectA, int subjectB)
		{
			if (book == null || !PermanentSemantic(kind)) return false;
			KingdomHappeningSemanticReceipt expected = new KingdomHappeningSemanticReceipt(kind,
				subjectA, subjectB);
			for (int i = 0; i < book.SemanticReceipts.Length; i++)
			{
				KingdomHappeningSemanticReceipt row = book.SemanticReceipts[i];
				if (row.Kind == expected.Kind && row.SubjectA == expected.SubjectA
					&& row.SubjectB == expected.SubjectB) return true;
			}
			return false;
		}

		internal static bool Matches(KingdomHappeningOperation operation,
			KingdomPhysicalHappeningKind kind, long eventTick, int subjectA, int subjectB,
			int outcome)
		{
			if (operation == null || operation.Kind != kind || operation.SubjectA != subjectA
				|| operation.SubjectB != subjectB || operation.Outcome != outcome) return false;
			return kind == KingdomPhysicalHappeningKind.Wedding
				|| operation.EventTick == eventTick;
		}

	}
}
