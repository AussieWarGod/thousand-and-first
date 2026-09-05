using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst
{
	public static partial class KingdomRaids
	{
		internal static void NativeOutboxChecks(KingdomRaidOutboxNativeContext Context)
		{
			Context.Case("dispatch-success", () => NativeOutboxSuccess(Context));
			Context.Case("chronicle-refusal-retry", () => NativeOutboxRetry(Context));
			Context.Case("throw-chronicle", () => NativeOutboxThrow(Context, KingdomLifecycleSinkMask.Chronicle));
			Context.Case("throw-ledger", () => NativeOutboxThrow(Context, KingdomLifecycleSinkMask.Ledger));
			Context.Case("throw-message", () => NativeOutboxThrow(Context, KingdomLifecycleSinkMask.Message));
			Context.Case("throw-deed", () => NativeOutboxThrow(Context, KingdomLifecycleSinkMask.Deed));
		}

		private static KingdomSystem NativeOutboxSystem(KingdomRaidOutboxNativeContext C, string Name)
		{
			// Unregistered fixture authority: never borrow the game's KingdomSystem or found a realm.
			KingdomSystem system = new KingdomSystem();
			KingdomLifecycleBook book = system.LifecycleBook;
			C.Check(KingdomLifecycleRules.BindSettlementIdentity(book, "native-raid-outbox-" + Name,
				false, null, new List<string>()), "fixture identity refused");
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Raid, KingdomLifecycleAction.RaidWarning, 10L);
			C.Check(op != null, "fixture operation refused");
			op.ZoneId = C.Zone.ZoneID;
			op.Origin = KingdomLifecycleRules.ChildId(book.SettlementId, "outbox-provocation", 0);
			op.ObjectId = KingdomRaidIncidentRules.GrievanceId(op.Origin);
			op.ObjectMarker = KingdomRaidIncidentRules.IncidentId(op.ObjectId);
			op.ObjectName = "native fixture challenge";
			op.Faction = "Snapjaws";
			op.DisplayFaction = "native fixture salt-road reach";
			op.Creed = "native-provocation";
			op.Detail = "a fixture scout was explicitly challenged";
			op.ArrivalText = "native-source-zone";
			op.Target = 1;
			op.Count = 1;
			op.DepartTick = 110L;
			op.PlunderRequested = 1;
			op.Kind = 24;
			op.Blueprint = "native-profile";
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op, "the native " + Name + " telling was tested",
				"native " + Name + " ledger", "native " + Name + " message", "native " + Name + " deed", null);
			C.Check(!op.Outbox.ChronicleAccomplishment, "fixture must never publish a journal accomplishment");
			C.Check(KingdomLifecycleRules.RaidRuntimeAdapter.PrepareLeases(book, op)
				&& KingdomLifecycleRules.TryPublish(book, op), "fixture publication refused");
			C.Check(KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.DomainIntent, 11L)
				&& KingdomLifecycleRules.RaidRuntimeAdapter.ProveDomain(book, op)
				&& KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.DomainSettled, 12L)
				&& KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.Sinks, 13L),
				"fixture sink authority refused");
			C.Check(!system.Founded && !ReferenceEquals(system, C.Game.GetSystem<KingdomSystem>()),
				"fixture borrowed registered authority");
			return system;
		}

		private static void NativeOutboxSuccess(KingdomRaidOutboxNativeContext C)
		{
			KingdomSystem system = NativeOutboxSystem(C, "success");
			KingdomLifecycleOperation op = system.LifecycleBook.Raid;
			C.ExpectedMessage(op.Outbox.Message);
			C.Check(DispatchOutbox(system, op), "actual outbox dispatch refused");
			C.CaptureRegistry(op.Outbox.ChronicleReceiptId, op.Outbox.Chronicle);
			NativeOutboxEffects(C, system, op);
			byte[] before = NativeOutboxBytes(system.LifecycleBook);
			C.Check(DispatchOutbox(system, op), "settled dispatch refused");
			C.Check(NativeOutboxSame(before, NativeOutboxBytes(system.LifecycleBook)),
				"settled dispatch changed lifecycle bytes");
			ResumeOpen(system, C.Zone);
			C.Check(system.LifecycleBook.Raid == null, "successful operation did not retire");
			NativeOutboxEffects(C, system, op);
		}

		private static void NativeOutboxRetry(KingdomRaidOutboxNativeContext C)
		{
			KingdomSystem system = NativeOutboxSystem(C, "retry");
			KingdomLifecycleOperation op = system.LifecycleBook.Raid;
			for (int i = 0; i <= KingdomChronicle.MaxEntries; i++) system.ChronicleEntries.Add("native bound");
			C.ExpectFault("5:list-bound");
			C.ExpectedMessage("{{r|A kingdom chronicle receipt could not be proved. This telling was settled as lost or refused; no receipt was discarded.}}");
			C.Check(!DispatchOutbox(system, op), "overbound native Chronicle unexpectedly delivered");
			C.Check(op.Phase == KingdomLifecyclePhase.Sinks
				&& op.Outbox.ChronicleState == KingdomLifecycleSinkState.Intent
				&& system.Ledger.Notes.Count == 0 && system.LastDeed == null
				&& !KingdomNativeRegressionContext.HasAnyState(C.Game, KingdomRaidOutboxNativeContext.RegistryKey)
				&& KingdomScenarioDurableState.ProvesExactText(KingdomRaidOutboxNativeContext.FaultKey, "5:list-bound"),
				"native refusal did not retain exact retry evidence");
			system.ChronicleEntries.Clear();
			C.ExpectedMessage(op.Outbox.Message);
			ResumeOpen(system, C.Zone);
			C.CaptureRegistry(op.Outbox.ChronicleReceiptId, op.Outbox.Chronicle);
			C.Check(system.LifecycleBook.Raid == null, "repaired Chronicle did not recover and retire");
			NativeOutboxEffects(C, system, op);
		}

		private static void NativeOutboxEffects(KingdomRaidOutboxNativeContext C,
			KingdomSystem System, KingdomLifecycleOperation Op)
		{
			C.Check(System.ChronicleEntries.Count == 1 && System.OutsiderEntries.Count == 1
				&& System.ChronicleEntries[0].Contains(Op.Outbox.Chronicle)
				&& System.Ledger.Notes.Count == 1 && System.Ledger.Notes[0] == Op.Outbox.Ledger
				&& System.LastDeed == Op.Outbox.Deed && System.LastDeedTick == C.Game.TimeTicks,
				"actual sink effects differ from the frozen telling");
			C.Check(Op.Outbox.ChronicleState == KingdomLifecycleSinkState.Delivered
				&& Op.Outbox.LedgerState == KingdomLifecycleSinkState.Delivered
				&& Op.Outbox.MessageState == KingdomLifecycleSinkState.Delivered
				&& Op.Outbox.DeedState == KingdomLifecycleSinkState.Delivered
				&& Op.Outbox.GuestbookState == KingdomLifecycleSinkState.Skipped,
				"actual dispatcher did not settle all intended sinks");
		}

		private static void NativeOutboxThrow(KingdomRaidOutboxNativeContext C, KingdomLifecycleSinkMask Sink)
		{
			KingdomSystem system = NativeOutboxSystem(C, Sink.ToString());
			KingdomLifecycleBook book = system.LifecycleBook;
			KingdomLifecycleOperation op = book.Raid;
			NativeOutboxInterruption interruption = new NativeOutboxInterruption(op);
			int calls = 0;
			if (Sink == KingdomLifecycleSinkMask.Message) C.ExpectedMessage(op.Outbox.Message);
			C.Check(!Deliver(system, op, Sink, delegate
			{
				calls++;
				switch (Sink)
				{
				case KingdomLifecycleSinkMask.Chronicle:
					C.Check(KingdomChronicle.RecordOnce(system, op.Outbox.ChronicleReceiptId,
						op.Outbox.Chronicle), "native Chronicle effect refused");
					C.CaptureRegistry(op.Outbox.ChronicleReceiptId, op.Outbox.Chronicle);
					break;
				case KingdomLifecycleSinkMask.Ledger: system.Ledger.Note(op.Outbox.Ledger); break;
				case KingdomLifecycleSinkMask.Message: MessageQueue.AddPlayerMessage(op.Outbox.Message); break;
				case KingdomLifecycleSinkMask.Deed: system.RecordDeed(op.Outbox.Deed); break;
				default: throw new InvalidOperationException("unexpected fixture sink");
				}
				// Synthetic post-effect interruption; no claim an engine event raised this exception.
				throw interruption;
			}), "interrupted delivery returned success");
			C.Check(calls == 1 && interruption.Read && interruption.QuarantinedFirst
				&& ReferenceEquals(book.Raid, op) && op.Phase == KingdomLifecyclePhase.Quarantined,
				"interruption did not retain exact authority before exception text");
			C.Check(op.Fault == "raid outbox " + Sink + " callback threw"
				&& (Sink != KingdomLifecycleSinkMask.Chronicle || op.Outbox.ChronicleState == KingdomLifecycleSinkState.Intent)
				&& (Sink != KingdomLifecycleSinkMask.Ledger || op.Outbox.LedgerState == KingdomLifecycleSinkState.Intent)
				&& (Sink != KingdomLifecycleSinkMask.Message || op.Outbox.MessageState == KingdomLifecycleSinkState.Intent)
				&& (Sink != KingdomLifecycleSinkMask.Deed || op.Outbox.DeedState == KingdomLifecycleSinkState.Intent),
				"interrupted sink lost its exact Intent or diagnostic evidence");
			C.Check((Sink != KingdomLifecycleSinkMask.Chronicle || system.ChronicleEntries.Count == 1)
				&& (Sink != KingdomLifecycleSinkMask.Ledger || system.Ledger.Notes.Count == 1)
				&& (Sink != KingdomLifecycleSinkMask.Deed || (system.LastDeed == op.Outbox.Deed
					&& system.LastDeedTick == C.Game.TimeTicks)), "native effect disappeared after interruption");
			byte[] held = NativeOutboxBytes(book);
			C.Check(!KingdomLifecycleRules.RecoverOutbox(book, op) && !DispatchOutbox(system, op)
				&& !Deliver(system, op, Sink, () => { calls++; return true; }), "retained lane replayed a sink");
			ResumeOpen(system, C.Zone);
			C.Check(!KingdomLifecycleRules.AdvancePhase(book, op, KingdomLifecyclePhase.ScheduleIntent, 20L)
				&& !KingdomLifecycleRules.Retire(book, op, 20L)
				&& KingdomLifecycleRules.PrepareOperation(book, KingdomLifecycleLane.Raid,
					KingdomLifecycleAction.RaidWarning, 20L) == null
				&& calls == 1 && NativeOutboxSame(held, NativeOutboxBytes(book)),
				"retained lane allowed replay, retirement, new publication, or byte mutation");
		}

		private static byte[] NativeOutboxBytes(KingdomLifecycleBook Book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
					KingdomLifecycleWireCodec.WriteLifecycle(writer, Book);
				return stream.ToArray();
			}
		}

		private static bool NativeOutboxSame(byte[] First, byte[] Second)
		{
			if (First.Length != Second.Length) return false;
			for (int i = 0; i < First.Length; i++) if (First[i] != Second[i]) return false;
			return true;
		}

		private sealed class NativeOutboxInterruption : Exception
		{
			private readonly KingdomLifecycleOperation Operation;
			internal bool Read, QuarantinedFirst;
			internal NativeOutboxInterruption(KingdomLifecycleOperation Operation) { this.Operation = Operation; }
			public override string Message
			{
				get
				{
					Read = true;
					QuarantinedFirst = Operation.Phase == KingdomLifecyclePhase.Quarantined;
					throw new InvalidOperationException("native fixture exception text unavailable");
				}
			}
		}
	}
}
