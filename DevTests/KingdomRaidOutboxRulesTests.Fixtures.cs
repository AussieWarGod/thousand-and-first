#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public sealed partial class KingdomRaidOutboxRulesTests
	{
		private static readonly KingdomLifecycleSinkMask[] Sinks =
		{
			KingdomLifecycleSinkMask.Chronicle, KingdomLifecycleSinkMask.Ledger,
			KingdomLifecycleSinkMask.Message, KingdomLifecycleSinkMask.Deed,
			KingdomLifecycleSinkMask.Guestbook
		};

		private static KingdomLifecycleBook BoundBook()
		{
			KingdomLifecycleBook book = new KingdomLifecycleBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(book, "raid-outbox-city",
				false, null, new List<string>()));
			return book;
		}

		private static KingdomLifecycleBook Warning(bool atSinks = true, bool guestbook = true)
		{
			KingdomLifecycleBook book = BoundBook();
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Raid, KingdomLifecycleAction.RaidWarning, 10L);
			ClassicAssert.NotNull(op);
			op.ZoneId = "zone-a";
			op.Origin = KingdomLifecycleRules.ChildId(book.SettlementId, "outbox-provocation", 0);
			op.ObjectId = KingdomRaidIncidentRules.GrievanceId(op.Origin);
			op.ObjectMarker = KingdomRaidIncidentRules.IncidentId(op.ObjectId);
			op.ObjectName = "test authored act";
			op.Faction = "Snapjaws";
			op.DisplayFaction = "test salt-road reach";
			op.Creed = "test-provocation";
			op.Detail = "a test scout was explicitly challenged";
			op.ArrivalText = "zone-source";
			op.Target = 1;
			op.Count = 1;
			op.DepartTick = 110L;
			op.PlunderRequested = 1;
			op.Kind = 24;
			op.Blueprint = "test-profile";
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, "chronicle", "ledger", "message",
				"deed", guestbook ? "guestbook" : null);
			ClassicAssert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			if (atSinks)
			{
				ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.DomainIntent, 11L));
				ClassicAssert.IsTrue(KingdomLifecycleRules.RaidRuntimeAdapter.ProveDomain(book, op));
				ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.DomainSettled, 12L));
				ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.Sinks, 13L));
			}
			ClassicAssert.AreSame(op, book.Raid);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book));
			return book;
		}

		private static KingdomLifecycleBook GuestAtSinks()
		{
			KingdomLifecycleBook book = BoundBook();
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Passages, 10L);
			ClassicAssert.NotNull(op);
			op.ZoneId = "zone-a";
			op.DueBefore = 0L;
			op.DueAfter = 1L;
			op.ResourceLeases.Add(KingdomLifecycleRules.TrustedAdapter.PreparePhysicalLease(book, op,
				KingdomLifecycleResourceKind.Schedule, book.SettlementId,
				KingdomLifecycleRules.ScheduleSubjectId(book.SettlementId, op.Lane), 0L, 1L));
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, "chronicle", "ledger", "message", "deed", "guestbook");
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublish(book, op));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.Sinks, 11L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(book));
			return book;
		}

		private static KingdomLifecycleSinkState State(KingdomLifecycleOperation op, KingdomLifecycleSinkMask sink)
		{
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: return op.Outbox.ChronicleState;
			case KingdomLifecycleSinkMask.Ledger: return op.Outbox.LedgerState;
			case KingdomLifecycleSinkMask.Message: return op.Outbox.MessageState;
			case KingdomLifecycleSinkMask.Deed: return op.Outbox.DeedState;
			case KingdomLifecycleSinkMask.Guestbook: return op.Outbox.GuestbookState;
			default: throw new ArgumentOutOfRangeException(nameof(sink));
			}
		}

		private static byte[] Bytes(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteLifecycle(writer, book);
				return stream.ToArray();
			}
		}

		private static KingdomLifecycleBook RoundTrip(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream(Bytes(book), false))
			using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
			{
				KingdomLifecycleBook loaded = new KingdomLifecycleBook();
				KingdomLifecycleWireCodec.ReadLifecycle(reader, loaded);
				ClassicAssert.AreEqual(stream.Length, stream.Position, "real lifecycle codec must consume the whole graph");
				return loaded;
			}
		}

		private static void AssertHeld(KingdomLifecycleBook book, KingdomLifecycleOperation op,
			KingdomLifecycleSinkMask sink)
		{
			ClassicAssert.AreSame(op, book.Raid);
			ClassicAssert.AreEqual(KingdomLifecyclePhase.Quarantined, op.Phase);
			ClassicAssert.AreEqual(KingdomLifecycleSinkState.Intent, State(op, sink));
			ClassicAssert.IsNotEmpty(op.Fault);
			byte[] before = Bytes(book);
			ClassicAssert.IsFalse(KingdomLifecycleRules.RecoverOutbox(book, op));
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.ScheduleIntent, 20L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.Terminal, 21L));
			ClassicAssert.IsFalse(KingdomLifecycleRules.Retire(book, op, 22L));
			ClassicAssert.IsNull(KingdomLifecycleRules.PrepareOperation(book, KingdomLifecycleLane.Raid,
				KingdomLifecycleAction.RaidWarning, 23L));
			int calls = 0;
			ClassicAssert.IsFalse(KingdomRaidOutboxRules.Deliver(book, op, sink,
				() => { calls++; return true; }, _ => { calls++; }));
			ClassicAssert.AreEqual(0, calls, "a quarantined lane cannot retry either callback");
			CollectionAssert.AreEqual(before, Bytes(book), "recovery and replay refusals must retain exact durable state");
			ClassicAssert.AreSame(op, book.Raid);
		}

		private sealed class HostileMessageException : Exception
		{
			private readonly Action OnRead;
			internal HostileMessageException(Action onRead) { OnRead = onRead; }
			public override string Message
			{
				get { OnRead(); throw new InvalidOperationException("hostile diagnostic getter"); }
			}
		}
	}
}
#endif
