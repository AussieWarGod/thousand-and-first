#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Real lifecycle authority and codec; only the external sink callbacks are injected.</summary>
	[TestFixture]
	public sealed partial class KingdomRaidOutboxRulesTests
	{
		[TestCase(KingdomLifecycleSinkMask.Chronicle)]
		[TestCase(KingdomLifecycleSinkMask.Ledger)]
		[TestCase(KingdomLifecycleSinkMask.Message)]
		[TestCase(KingdomLifecycleSinkMask.Deed)]
		[TestCase(KingdomLifecycleSinkMask.Guestbook)]
		public void SuccessfulSinkBeginsIntentCommitsAndDoesNotDeliverTwice(KingdomLifecycleSinkMask sink)
		{
			KingdomLifecycleBook book = Warning();
			KingdomLifecycleOperation op = book.Raid;
			int calls = 0, diagnostics = 0;
			bool sawIntent = false;
			ClassicAssert.IsTrue(KingdomRaidOutboxRules.Deliver(book, op, sink, () =>
			{
				calls++;
				sawIntent = ReferenceEquals(book.Raid, op)
					&& State(op, sink) == KingdomLifecycleSinkState.Intent;
				return true;
			}, _ => { diagnostics++; }));
			ClassicAssert.IsTrue(sawIntent);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Delivered, State(op, sink));
			byte[] settled = Bytes(book);
			ClassicAssert.IsTrue(KingdomRaidOutboxRules.Deliver(book, op, sink,
				() => { calls++; return false; }, _ => { diagnostics++; }));
			ClassicAssert.AreEqual(1, calls);
			ClassicAssert.AreEqual(0, diagnostics);
			CollectionAssert.AreEqual(settled, Bytes(book));
			foreach (KingdomLifecycleSinkMask other in Sinks)
				if (other != sink) ClassicAssert.AreEqual(KingdomLifecycleSinkState.Pending, State(op, other));
		}

		[TestCase(KingdomLifecycleSinkMask.Chronicle)]
		[TestCase(KingdomLifecycleSinkMask.Ledger)]
		[TestCase(KingdomLifecycleSinkMask.Message)]
		[TestCase(KingdomLifecycleSinkMask.Deed)]
		[TestCase(KingdomLifecycleSinkMask.Guestbook)]
		public void ExplicitFalseKeepsIntentThenUsesExistingRecoveryPolicy(KingdomLifecycleSinkMask sink)
		{
			KingdomLifecycleBook book = Warning();
			KingdomLifecycleOperation op = book.Raid;
			int calls = 0, diagnostics = 0;
			ClassicAssert.IsFalse(KingdomRaidOutboxRules.Deliver(book, op, sink,
				() => { calls++; return false; }, _ => { diagnostics++; }));
			ClassicAssert.AreEqual(KingdomLifecyclePhase.Sinks, op.Phase);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Intent, State(op, sink));
			byte[] intent = Bytes(book);
			ClassicAssert.IsFalse(KingdomRaidOutboxRules.Deliver(book, op, sink,
				() => { calls++; return true; }, _ => { diagnostics++; }));
			CollectionAssert.AreEqual(intent, Bytes(book), "unrecovered Intent must not be delivered again");
			ClassicAssert.AreEqual(1, calls);
			ClassicAssert.IsTrue(KingdomLifecycleRules.RecoverOutbox(book, op));
			bool chronicle = sink == KingdomLifecycleSinkMask.Chronicle;
			ClassicAssert.AreEqual(chronicle ? KingdomLifecycleSinkState.Pending : KingdomLifecycleSinkState.Lost,
				State(op, sink));
			ClassicAssert.IsTrue(KingdomRaidOutboxRules.Deliver(book, op, sink,
				() => { calls++; return true; }, _ => { diagnostics++; }));
			ClassicAssert.AreEqual(chronicle ? 2 : 1, calls);
			ClassicAssert.AreEqual(0, diagnostics);
			ClassicAssert.AreEqual(chronicle ? KingdomLifecycleSinkState.Delivered : KingdomLifecycleSinkState.Lost,
				State(op, sink));
			ClassicAssert.AreSame(op, book.Raid);
		}

		[TestCase(KingdomLifecycleSinkMask.Chronicle)]
		[TestCase(KingdomLifecycleSinkMask.Ledger)]
		[TestCase(KingdomLifecycleSinkMask.Message)]
		[TestCase(KingdomLifecycleSinkMask.Deed)]
		[TestCase(KingdomLifecycleSinkMask.Guestbook)]
		public void ThrownSinkQuarantinesBeforeDiagnosticsAndRetainsLaneAcrossWire(KingdomLifecycleSinkMask sink)
		{
			KingdomLifecycleBook book = Warning();
			KingdomLifecycleOperation op = book.Raid;
			int callbacks = 0, diagnostics = 0;
			bool quarantinedFirst = false, recoveryRefused = false;
			ClassicAssert.IsFalse(KingdomRaidOutboxRules.Deliver(book, op, sink, () =>
			{
				callbacks++;
				throw new InvalidOperationException("raid sink interruption");
			}, _ =>
			{
				diagnostics++;
				quarantinedFirst = op.Phase == KingdomLifecyclePhase.Quarantined
					&& State(op, sink) == KingdomLifecycleSinkState.Intent && ReferenceEquals(book.Raid, op);
				recoveryRefused = !KingdomLifecycleRules.RecoverOutbox(book, op);
			}));
			ClassicAssert.AreEqual(1, callbacks);
			ClassicAssert.AreEqual(1, diagnostics);
			ClassicAssert.IsTrue(quarantinedFirst);
			ClassicAssert.IsTrue(recoveryRefused);
			AssertHeld(book, op, sink);
			KingdomLifecycleBook loaded = RoundTrip(book);
			ClassicAssert.AreNotSame(op, loaded.Raid);
			ClassicAssert.AreEqual(op.Id, loaded.Raid.Id);
			ClassicAssert.AreEqual(op.PlanHash, loaded.Raid.PlanHash);
			ClassicAssert.AreEqual(op.Fault, loaded.Raid.Fault);
			CollectionAssert.AreEqual(Bytes(book), Bytes(loaded));
			AssertHeld(loaded, loaded.Raid, sink);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void LoggerOrExceptionMessageFailureCannotPreventQuarantine(bool hostileMessage)
		{
			KingdomLifecycleBook book = Warning();
			KingdomLifecycleOperation op = book.Raid;
			bool messageRead = false, quarantinedWhenMessageRead = false, quarantinedWhenLogged = false;
			int diagnostics = 0;
			Exception interruption = hostileMessage ? (Exception)new HostileMessageException(() =>
			{
				messageRead = true;
				quarantinedWhenMessageRead = op.Phase == KingdomLifecyclePhase.Quarantined;
			}) : new InvalidOperationException("ordinary callback failure");
			bool delivered = true;
			Assert.DoesNotThrow(() => delivered = KingdomRaidOutboxRules.Deliver(book, op,
				KingdomLifecycleSinkMask.Message, () => { throw interruption; }, _ =>
				{
					diagnostics++;
					quarantinedWhenLogged = op.Phase == KingdomLifecyclePhase.Quarantined;
					throw new InvalidOperationException("diagnostics failed too");
				}));
			ClassicAssert.IsFalse(delivered);
			if (hostileMessage)
			{
				ClassicAssert.IsTrue(messageRead);
				ClassicAssert.IsTrue(quarantinedWhenMessageRead);
			}
			else
			{
				ClassicAssert.AreEqual(1, diagnostics);
				ClassicAssert.IsTrue(quarantinedWhenLogged);
			}
			AssertHeld(book, op, KingdomLifecycleSinkMask.Message);
		}

		[Test]
		public void AuthoredSkippedSinkDoesNotInvokeCallback()
		{
			KingdomLifecycleBook book = Warning(guestbook: false);
			byte[] before = Bytes(book);
			int calls = 0;
			ClassicAssert.IsTrue(KingdomRaidOutboxRules.Deliver(book, book.Raid, KingdomLifecycleSinkMask.Guestbook,
				() => { calls++; return true; }, _ => { calls++; }));
			ClassicAssert.AreEqual(0, calls);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Skipped, book.Raid.Outbox.GuestbookState);
			CollectionAssert.AreEqual(before, Bytes(book));
		}

		[Test]
		public void AllSuccessfulSinksPermitRealScheduleProofAndRetirement()
		{
			KingdomLifecycleBook book = Warning();
			KingdomLifecycleOperation op = book.Raid;
			int calls = 0;
			foreach (KingdomLifecycleSinkMask sink in Sinks)
				ClassicAssert.IsTrue(KingdomRaidOutboxRules.Deliver(book, op, sink, () => { calls++; return true; }, null));
			ClassicAssert.AreEqual(5, calls);
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.ScheduleIntent, 14L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.ProveSchedule(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.Terminal, 15L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.Retire(book, op, 16L));
			ClassicAssert.IsNull(book.Raid);
			ClassicAssert.AreEqual(op.Sequence, book.RaidRetiredThrough);
			ClassicAssert.NotNull(KingdomLifecycleRules.PrepareOperation(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidCancel, 17L));
		}

		[TestCase("null-book")]
		[TestCase("null-operation")]
		[TestCase("foreign-book")]
		[TestCase("foreign-operation")]
		[TestCase("wrong-lane")]
		[TestCase("wrong-phase")]
		[TestCase("null-callback")]
		[TestCase("null-callback-settled")]
		[TestCase("operation-quarantined")]
		[TestCase("book-quarantined")]
		[TestCase("changed-plan")]
		public void InvalidAuthorityRefusesWithoutCallbackDiagnosticsOrMutation(string kind)
		{
			KingdomLifecycleBook book = kind == "wrong-lane" ? GuestAtSinks() : Warning(kind != "wrong-phase");
			KingdomLifecycleOperation op = book.Raid ?? book.PlainGuest;
			KingdomLifecycleBook other = RoundTrip(book);
			if (kind == "operation-quarantined") ClassicAssert.IsTrue(KingdomLifecycleRules.Quarantine(op, "prior fault"));
			if (kind == "book-quarantined") book.Quarantined = true;
			if (kind == "changed-plan") op.PlanHash = new string('0', 64);
			if (kind == "null-callback-settled")
				ClassicAssert.IsTrue(KingdomRaidOutboxRules.Deliver(book, op, KingdomLifecycleSinkMask.Message, () => true, null));
			byte[] before = Bytes(book), otherBefore = Bytes(other);
			int calls = 0;
			Func<bool> callback = kind.StartsWith("null-callback", StringComparison.Ordinal)
				? (Func<bool>)null : () => { calls++; return true; };
			ClassicAssert.IsFalse(KingdomRaidOutboxRules.Deliver(kind == "null-book" ? null : kind == "foreign-book" ? other : book,
				kind == "null-operation" ? null : kind == "foreign-operation" ? other.Raid : op,
				KingdomLifecycleSinkMask.Message, callback, _ => { calls++; }));
			ClassicAssert.AreEqual(0, calls);
			CollectionAssert.AreEqual(before, Bytes(book));
			CollectionAssert.AreEqual(otherBefore, Bytes(other));
			ClassicAssert.AreSame(op, book.Raid ?? book.PlainGuest);
		}

		[TestCase(0)]
		[TestCase(3)]
		[TestCase(32)]
		[TestCase(255)]
		public void UnknownOrCombinedSinkRefusesBeforeCallback(int mask)
		{
			KingdomLifecycleBook book = Warning();
			byte[] before = Bytes(book);
			int calls = 0;
			ClassicAssert.IsFalse(KingdomRaidOutboxRules.Deliver(book, book.Raid, (KingdomLifecycleSinkMask)mask,
				() => { calls++; return true; }, _ => { calls++; }));
			ClassicAssert.AreEqual(0, calls);
			CollectionAssert.AreEqual(before, Bytes(book));
		}
	}
}
#endif
