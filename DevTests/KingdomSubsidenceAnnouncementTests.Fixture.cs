#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Book = ThousandAndFirst.KingdomSubsidenceStepBook;
using Announcements = ThousandAndFirst.KingdomSubsidenceAnnouncementRules;
using Reports = ThousandAndFirst.KingdomSubsidenceReportRules;

namespace ThousandAndFirst.Tests
{
	public sealed partial class KingdomSubsidenceAnnouncementTests
	{
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);
		private static Book Empty()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out var book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(book, Realm, Settlement, out book)); return book;
		}
		private static Book Prepare(Book prior = null, bool before = false, long tick = 100)
		{
			ClassicAssert.IsTrue(Announcements.TryPrepare(prior ?? Empty(), before, !before, tick,
				before ? "{{G|The fall stopped.}}" : "{{r|The fall began.}}", before ? "The fall stopped" : "The fall began", out var next));
			return next;
		}
		private static KingdomSubsidenceAnnouncement Announcement(Book book)
		{ ClassicAssert.IsTrue(KingdomSubsidenceAnnouncementCodec.TryDecode(book.AnnouncementModel, out var value)); return value; }
		private static string Wire(Book book)
		{ ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire)); return wire; }
		private static Book Reload(Book book)
		{ ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(Wire(book), out var next)); return next; }

		/// <summary>Injected sinks, not an engine stand-in: the production coordinator runs against
		/// actual report laws, ledger and Chronicle receipt/list-CAS laws. Wire reload is model-only.</summary>
		private sealed class Port : IKingdomSubsidenceAnnouncementPort
		{
			public Book Book { get; private set; }
			public bool Announced { get; private set; }
			internal bool Owned = true, SkipReport;
			internal string Cut;
			internal int NoWriteAt, Publishes, FlagWrites;
			internal Action OnMessage;
			internal readonly List<string> Calls = new List<string>(), Messages = new List<string>();
			internal readonly List<string> Official = new List<string>(), Outsider = new List<string>();
			internal readonly KingdomLedger Ledger = new KingdomLedger();
			internal string Registry = "";
			public bool Exact { get { var op = Announcement(Book).Active; return Owned && (op == null || !op.FlagProved || Announced == op.After); } }
			internal Port(Book book, bool announced = false) { Book = book; Announced = announced; }
			internal void Load() { Book = Reload(Book); }
			internal void Install(Book book) { Book = book; }
			private void Fire(string point)
			{ if (Cut != point) return; Cut = null; throw new InvalidOperationException(point); }
			public bool Publish(Book expected, Book next)
			{
				ClassicAssert.AreSame(Book, expected); Publishes++; Calls.Add("save:" + Publishes);
				Fire("save:" + Publishes + ":before");
				if (NoWriteAt == Publishes) return true;
				Book = next; Wire(Book); Fire("save:" + Publishes + ":after"); return true;
			}
			public bool WriteFlag(bool before, bool after)
			{
				ClassicAssert.IsNotNull(Announcement(Book).Active, "intent must precede flag");
				ClassicAssert.AreEqual(before, Announcement(Book).Active.Before); Calls.Add("flag"); Fire("flag:before");
				if (Announced != after) { Announced = after; FlagWrites++; }
				Fire("flag:after"); return true;
			}
			public void Message(string text)
			{
				ClassicAssert.AreEqual(KingdomSubsidenceNoticePhase.Intent, Announcement(Book).Active.Notice);
				Calls.Add("message"); Fire("message:before"); Messages.Add(text); OnMessage?.Invoke(); Fire("message:after");
			}
			public bool Report(KingdomSubsidenceReportPlan report, Func<KingdomSubsidenceReportPlan, bool> save, out string refusal)
			{
				refusal = "Injected report did not settle."; Calls.Add("report"); if (SkipReport) return true;
				if (report.Entries[0].LedgerPhase == ReportLedgerPhase.Prepared)
				{
					if (!Reports.TryArmLedger(report, 0, Ledger.Notes, out var armed) || !save(armed)) return false; report = armed;
				}
				if (report.Entries[0].LedgerPhase == ReportLedgerPhase.Intent)
				{
					if (Reports.LedgerAction(report, 0, Ledger.Notes) == KingdomSubsidenceEffectAction.Apply)
					{ Fire("ledger:before"); Ledger.Note(report.Entries[0].LedgerText); Fire("ledger:after"); }
					if (!Reports.TryProveLedger(report, 0, Ledger.Notes, out var proved) || !save(proved)) return false; report = proved;
				}
				string id = Reports.EventId(report, 0);
				var entry = report.Entries[0];
				ClassicAssert.IsTrue(KingdomChronicleCapacityRules.TryFingerprint(report.RealmId, report.SettlementId,
					id, entry.Text, entry.AtTick, out string fingerprint));
				if (KingdomChronicleCapacityRules.TryObserve(new KingdomDurableKeyObservation { HasString = true, String = Registry },
					id, fingerprint, out var witness))
				{
					string held = Registry;
					bool refused = Reports.TryPublishCapacity(report, 0, witness, () => Exact && Registry == held, save, out _);
					if (refused) refusal = null;
					return refused;
				}
				KingdomChronicleReceipt receipt = Receipt(report, id);
				Sink(receipt, true); Sink(receipt, false);
				bool told = receipt.OfficialState == KingdomChronicleSinkDisposition.Delivered && receipt.OutsiderState == KingdomChronicleSinkDisposition.Delivered;
				KingdomSubsidenceReportPlan complete;
				if (!(told ? Reports.TryProveChronicle(report, 0, out complete) : Reports.TryLoseChronicle(report, 0, out complete)) || !save(complete)) return false;
				refusal = null; return true;
			}
			private KingdomChronicleReceipt Receipt(KingdomSubsidenceReportPlan report, string id)
			{
				ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(Registry, out var rows, out _, out _));
				var prior = rows.Find(row => row.EventId == id); if (prior != null) return prior;
				var entry = report.Entries[0];
				ClassicAssert.IsTrue(KingdomChronicleCapacityRules.TryFingerprint(report.RealmId, report.SettlementId, id, entry.Text, entry.AtTick, out string fingerprint));
				var receipt = new KingdomChronicleReceipt { EventId = id, Fingerprint = fingerprint,
					Official = "official at " + entry.AtTick + ": " + entry.Text, Outsider = "outsider at " + entry.AtTick + ": " + entry.Text,
					OfficialState = KingdomChronicleSinkDisposition.Pending, OutsiderState = KingdomChronicleSinkDisposition.Pending,
					JournalState = KingdomChronicleSinkDisposition.Skipped, Updated = entry.AtTick };
				ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official", Official, out receipt.OfficialBefore));
				ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("official", Official, receipt.Official, out receipt.OfficialAfter));
				ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("outsider", Outsider, out receipt.OutsiderBefore));
				ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("outsider", Outsider, receipt.Outsider, out receipt.OutsiderAfter));
				Persist(receipt); return receipt;
			}
			private void Sink(KingdomChronicleReceipt receipt, bool official)
			{
				var state = official ? receipt.OfficialState : receipt.OutsiderState;
				if (KingdomChronicleReceiptRules.IsSettled(state)) return;
				string name = official ? "official" : "outsider", before = official ? receipt.OfficialBefore : receipt.OutsiderBefore,
					after = official ? receipt.OfficialAfter : receipt.OutsiderAfter;
				List<string> rows = official ? Official : Outsider;
				ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList(name, rows, out string hash));
				var action = KingdomChronicleReceiptRules.ListAction(state, hash, before, after);
				if (action == KingdomChronicleListAction.Append)
				{
					Set(receipt, official, KingdomChronicleSinkDisposition.Attempting); Persist(receipt);
					Fire(name + ":before"); rows.Add(official ? receipt.Official : receipt.Outsider);
					if (rows.Count > KingdomChronicleReceiptRules.MaxEntries) rows.RemoveAt(0); Fire(name + ":after");
				}
				Set(receipt, official, action == KingdomChronicleListAction.MarkLost ? KingdomChronicleSinkDisposition.Lost : KingdomChronicleSinkDisposition.Delivered);
				Persist(receipt);
			}
			private static void Set(KingdomChronicleReceipt row, bool official, KingdomChronicleSinkDisposition state)
			{ if (official) row.OfficialState = state; else row.OutsiderState = state; }
			private void Persist(KingdomChronicleReceipt row)
			{
				ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(Registry, out var rows, out _, out _));
				int index = rows.FindIndex(existing => existing.EventId == row.EventId);
				if (index < 0) rows.Add(row); else rows[index] = row;
				ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out string wire, out _)); Registry = wire;
			}
		}
	}
}
#endif
