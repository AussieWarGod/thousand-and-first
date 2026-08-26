using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomHappeningLifecycleRules
	{
		internal static bool TrySetPhase(KingdomHappeningLifecycleBook book, string eventId,
			KingdomHappeningLifecyclePhase expected, KingdomHappeningLifecyclePhase phase,
			bool attended, long holdUntilTick, long nowTick,
			out KingdomHappeningLifecycleBook next, out KingdomHappeningLifecycleFault fault)
		{
			next = book;
			if (!Exact(book, eventId, out KingdomHappeningOperation operation))
			{
				fault = KingdomHappeningLifecycleFault.WrongOperation;
				return false;
			}
			if (operation.Phase != expected)
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			if (!PhaseTransition(expected, phase) || nowTick < operation.UpdatedTick)
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			KingdomHappeningOperation changed = operation.WithPhase(phase, attended,
				holdUntilTick, nowTick);
			if (!ValidOperation(changed))
			{
				fault = KingdomHappeningLifecycleFault.Malformed;
				return false;
			}
			next = new KingdomHappeningLifecycleBook(book.Sequence, changed,
				book.SemanticReceipts);
			fault = KingdomHappeningLifecycleFault.None;
			return true;
		}

		internal static bool TrySetSinks(KingdomHappeningLifecycleBook book, string eventId,
			KingdomHappeningSinkState chronicle, KingdomHappeningSinkState told,
			KingdomHappeningSinkState effect, KingdomHappeningSinkState ledger,
			KingdomHappeningSinkState message, long nowTick,
			out KingdomHappeningLifecycleBook next, out KingdomHappeningLifecycleFault fault)
		{
			next = book;
			if (!Exact(book, eventId, out KingdomHappeningOperation operation))
			{
				fault = KingdomHappeningLifecycleFault.WrongOperation;
				return false;
			}
			if ((operation.Phase != KingdomHappeningLifecyclePhase.Ready
				&& operation.Phase != KingdomHappeningLifecyclePhase.Restoring)
				|| nowTick < operation.UpdatedTick
				|| !SinkTransition(operation.ChronicleState, chronicle)
				|| !SinkTransition(operation.ToldState, told)
				|| !SinkTransition(operation.EffectState, effect)
				|| !SinkTransition(operation.LedgerState, ledger)
				|| !SinkTransition(operation.MessageState, message))
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			KingdomHappeningOperation changed = operation.WithSinks(chronicle, told, effect,
				ledger, message, nowTick);
			if (!ValidOperation(changed))
			{
				fault = KingdomHappeningLifecycleFault.Malformed;
				return false;
			}
			next = new KingdomHappeningLifecycleBook(book.Sequence, changed,
				book.SemanticReceipts);
			fault = KingdomHappeningLifecycleFault.None;
			return true;
		}

		internal static bool TryClear(KingdomHappeningLifecycleBook book, string eventId,
			out KingdomHappeningLifecycleBook next, out KingdomHappeningLifecycleFault fault)
		{
			next = book;
			if (!Exact(book, eventId, out KingdomHappeningOperation ignored))
			{
				fault = KingdomHappeningLifecycleFault.WrongOperation;
				return false;
			}
			if (book.Active.Phase != KingdomHappeningLifecyclePhase.Restoring
				|| !RestorationSettled(book.Active))
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			KingdomHappeningSemanticReceipt[] receipts = book.SemanticReceipts;
			if (PermanentSemantic(book.Active.Kind)
				&& !AlreadyCompleted(book, book.Active.Kind, book.Active.SubjectA,
					book.Active.SubjectB))
			{
				if (receipts.Length >= MaxSemanticReceipts)
				{
					fault = KingdomHappeningLifecycleFault.OverBudget;
					return false;
				}
				KingdomHappeningSemanticReceipt[] grown =
					new KingdomHappeningSemanticReceipt[receipts.Length + 1];
				Array.Copy(receipts, grown, receipts.Length);
				grown[receipts.Length] = new KingdomHappeningSemanticReceipt(book.Active.Kind,
					book.Active.SubjectA, book.Active.SubjectB);
				receipts = grown;
			}
			next = new KingdomHappeningLifecycleBook(book.Sequence, null, receipts);
			fault = KingdomHappeningLifecycleFault.None;
			return true;
		}

		internal static bool TryMarkRestored(KingdomHappeningLifecycleBook book,
			string eventId, int participantIndex, bool fixture, long nowTick,
			out KingdomHappeningLifecycleBook next, out KingdomHappeningLifecycleFault fault)
		{
			next = book;
			if (!Exact(book, eventId, out KingdomHappeningOperation operation))
			{
				fault = KingdomHappeningLifecycleFault.WrongOperation;
				return false;
			}
			if (operation.Phase != KingdomHappeningLifecyclePhase.Restoring
				|| nowTick < operation.UpdatedTick)
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			KingdomHappeningParticipant[] people = operation.CopyParticipants();
			bool fixtureRestored = operation.FixtureRestored;
			if (fixture)
				fixtureRestored = true;
			else
			{
				if (participantIndex < 0 || participantIndex >= people.Length)
				{
					fault = KingdomHappeningLifecycleFault.Malformed;
					return false;
				}
				people[participantIndex] = people[participantIndex].WithRestored();
			}
			KingdomHappeningOperation changed = operation.WithRestoration(people,
				fixtureRestored, nowTick);
			if (!ValidOperation(changed))
			{
				fault = KingdomHappeningLifecycleFault.Malformed;
				return false;
			}
			next = new KingdomHappeningLifecycleBook(book.Sequence, changed,
				book.SemanticReceipts);
			fault = KingdomHappeningLifecycleFault.None;
			return true;
		}

	}
}
