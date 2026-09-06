#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Codec = ThousandAndFirst.KingdomSubsidenceReportCodec;
using Entry = ThousandAndFirst.KingdomSubsidenceReportEntry;
using Plan = ThousandAndFirst.KingdomSubsidenceReportPlan;
using Rules = ThousandAndFirst.KingdomSubsidenceReportRules;

namespace ThousandAndFirst.Tests
{
	public class KingdomSubsidenceReportRulesTests
	{
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement
			= KingdomIdentityRules.SettlementPrefix + new string('b', 64);
		private static readonly string Step = "taf:subsidence-step:v1:" + new string('c', 64);
		private static readonly string Batch = Rules.BatchPrefix + new string('d', 64);

		private static Plan Make(string owner, params Entry[] entries)
		{
			return new Plan(owner, Realm, Settlement, entries);
		}

		private static Plan Make(params Entry[] entries) { return Make(Step, entries); }

		private static Entry Line(string ledger) { return new Entry("told", ledger, 10L); }

		/// <summary>The real engine-free KingdomLedger. Its own Note enforces the twelve bound.</summary>
		private static List<string> Notes(params string[] lines)
		{
			KingdomLedger ledger = new KingdomLedger();
			foreach (string line in lines) ledger.Note(line);
			return ledger.Notes;
		}

		private sealed class Row
		{
			internal string Text = "told", Ledger = "", BeforeHash = "", AfterHash = "";
			internal long Tick = 10L;
			internal byte Phase, Chronicle, Loss, ChronicleLost;
			internal int Before, After;
		}

		private static Row Of(Entry entry)
		{
			return new Row
			{
				Text = entry.Text, Ledger = entry.LedgerText, Tick = entry.AtTick,
				Phase = (byte)entry.LedgerPhase, Chronicle = (byte)(entry.ChronicleProved ? 1 : 0),
				Before = entry.BeforeCount, BeforeHash = entry.BeforeHash,
				After = entry.AfterCount, AfterHash = entry.AfterHash,
				Loss = (byte)entry.LedgerLoss, ChronicleLost = (byte)(entry.ChronicleLost ? 1 : 0)
			};
		}

		private static byte[] Bytes(string wire) { return Convert.FromBase64String(wire.Substring(4)); }

		private static string Wire(byte[] bytes) { return "st3:" + Convert.ToBase64String(bytes); }

		// Historical and current framings are written out of the layout itself - a literal magic, then length-framed
		// strict UTF-8 fields with explicit phase and flag bytes - and never by borrowing bytes
		// from the production encoder. The first wire has no loss bytes at all; the second appends
		// exactly two to every entry. Each is proved byte-identical to production before it forges.
		private static string Forge(int magic, string prefix, bool loss, string owner, string realm,
			string settlement, params Row[] rows)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
			{
				writer.Write(magic);
				writer.Write(owner); writer.Write(realm); writer.Write(settlement);
				writer.Write(rows.Length);
				foreach (Row row in rows)
				{
					writer.Write(row.Text); writer.Write(row.Ledger); writer.Write(row.Tick);
					writer.Write(row.Phase); writer.Write(row.Chronicle); writer.Write(row.Before);
					writer.Write(row.BeforeHash); writer.Write(row.After); writer.Write(row.AfterHash);
					if (loss) { writer.Write(row.Loss); writer.Write(row.ChronicleLost); }
					if (magic == 0x33545253)
					{
						writer.Write((byte)0); writer.Write(0); writer.Write(""); writer.Write("");
					}
				}
				writer.Flush();
				return prefix + Convert.ToBase64String(stream.ToArray());
			}
		}

		/// <summary>The original wire, framed here and nowhere else: the literal magic 'S' 'R' 'T'
		/// '1' and no loss bytes anywhere. The one fixture that proves an old save still reads.
		/// </summary>
		private static string ForgeV1(string owner, string realm, string settlement, params Row[] rows)
		{
			return Forge(0x31545253, "st1:", false, owner, realm, settlement, rows);
		}

		private static string ForgeV2(string owner, string realm, string settlement, params Row[] rows)
		{
			return Forge(0x32545253, "st2:", true, owner, realm, settlement, rows);
		}

		private static string ForgeV3(string owner, string realm, string settlement, params Row[] rows)
		{
			return Forge(0x33545253, "st3:", true, owner, realm, settlement, rows);
		}

		private static void Refuses(string wire)
		{
			Assert.IsFalse(Codec.TryDecode(wire, out Plan decoded), wire ?? "<null>");
			Assert.IsNull(decoded);
		}

		private static string Hash(IList<string> notes)
		{
			Assert.IsTrue(Rules.TryHash(notes, out string hash));
			return hash;
		}

		/// <summary>One owed line armed over a single-note ledger.</summary>
		private static Plan Armed()
		{
			Assert.IsTrue(Rules.TryArmLedger(Make(Line("L")), 0, Notes("a"), out Plan armed));
			return armed;
		}

		/// <summary>One owed line armed and then lost to a stranger's note list.</summary>
		private static Plan Lost()
		{
			Assert.IsTrue(Rules.TryLoseLedger(Armed(), 0, Notes("b"), false, out Plan lost));
			return lost;
		}

		/// <summary>Both loss kinds and both chronicle dispositions, in the one order the frontier
		/// admits: a wholly lost line, then a line the homecoming barrier lost but still told.
		/// </summary>
		private static Plan Losses()
		{
			Plan plan = Make(Line("L1"), Line("L2"));
			Assert.IsTrue(Rules.TryArmLedger(plan, 0, Notes("a"), out plan));
			Assert.IsTrue(Rules.TryLoseLedger(plan, 0, Notes("b"), false, out plan));
			Assert.IsTrue(Rules.TryLoseChronicle(plan, 0, out plan));
			Assert.IsTrue(Rules.TryArmLedger(plan, 1, Notes("b"), out plan));
			Assert.IsTrue(Rules.TryLoseLedger(plan, 1, Notes("b"), true, out plan));
			Assert.IsTrue(Rules.TryProveChronicle(plan, 1, out plan));
			Assert.AreEqual(LedgerLossKind.ThirdState, plan.Entries[0].LedgerLoss);
			Assert.AreEqual(LedgerLossKind.HomecomingReset, plan.Entries[1].LedgerLoss);
			return plan;
		}

		/// <summary>Skipped-told, proved-told, pending, untouched: every phase at once, in the one
		/// order the frontier admits.</summary>
		private static Plan Mixed()
		{
			Plan plan = Make(Line(""), Line("L1"), Line("L2"), Line("L3"));
			Assert.IsTrue(Rules.TryArmLedger(plan, 0, Notes(), out plan));
			Assert.IsTrue(Rules.TryProveChronicle(plan, 0, out plan));
			Assert.IsTrue(Rules.TryArmLedger(plan, 1, Notes(), out plan));
			Assert.IsTrue(Rules.TryProveLedger(plan, 1, Notes("L1"), out plan));
			Assert.IsTrue(Rules.TryProveChronicle(plan, 1, out plan));
			Assert.IsTrue(Rules.TryArmLedger(plan, 2, Notes("L1"), out plan));
			Assert.AreEqual(ReportLedgerPhase.Skipped, plan.Entries[0].LedgerPhase);
			Assert.AreEqual(ReportLedgerPhase.Proved, plan.Entries[1].LedgerPhase);
			Assert.AreEqual(ReportLedgerPhase.Intent, plan.Entries[2].LedgerPhase);
			Assert.AreEqual(ReportLedgerPhase.Prepared, plan.Entries[3].LedgerPhase);
			return plan;
		}

		[Test]
		public void OwnerAcceptsOnlyAnExactStepOrBatchHash()
		{
			Assert.IsTrue(KingdomSubsidenceStepRules.IsStepId(Step));
			Assert.IsTrue(Rules.Valid(Make(Step)));
			Assert.IsTrue(Rules.Valid(Make(Batch)));
			Assert.IsFalse(Rules.Valid(Make((string)null)));
			Assert.IsFalse(Rules.Valid(Make("")));
			Assert.IsFalse(Rules.Valid(Make(Realm)));
			Assert.IsFalse(Rules.Valid(Make(Rules.BatchPrefix + new string('d', 63))));
			Assert.IsFalse(Rules.Valid(Make(Rules.BatchPrefix + new string('D', 64))));
			Assert.IsFalse(Rules.Valid(Make("taf:subsidence-batch:v2:" + new string('d', 64))));
			Assert.IsFalse(Rules.Valid(new Plan(Step, Realm, Settlement, null)));
			Assert.IsFalse(Rules.Valid(new Plan(Step, Settlement, Settlement, new Entry[0])));
			Assert.IsFalse(Rules.Valid(new Plan(Step, Realm, Realm, new Entry[0])));
			Assert.IsFalse(Rules.Valid(null));
		}

		[Test]
		public void ValidRefusesMalformedEntriesAndOverlongPlans()
		{
			Assert.IsTrue(Rules.Valid(Make(Line(""), Line("l"), Line("l"), Line("l"))));
			Assert.IsFalse(Rules.Valid(Make(Line(""), Line(""), Line(""), Line(""), Line(""))));
			Assert.IsFalse(Rules.Valid(Make((Entry)null)));
			Assert.IsFalse(Rules.Valid(Make(new Entry("", "", 10L))));
			Assert.IsFalse(Rules.Valid(Make(new Entry(null, "", 10L))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("told", null, 10L))));
			Assert.IsFalse(Rules.Valid(Make(new Entry(
				new string('t', KingdomChronicleReceiptRules.MaxEventTextChars + 1), "", 10L))));
			Assert.IsTrue(Rules.Valid(Make(new Entry(
				new string('t', KingdomChronicleReceiptRules.MaxEventTextChars), "", 10L))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("told",
				new string('l', KingdomChronicleReceiptRules.MaxEntryChars + 1), 10L))));
			Assert.IsTrue(Rules.Valid(Make(new Entry("told",
				new string('l', KingdomChronicleReceiptRules.MaxEntryChars), 10L))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("bad\u0007line", "", 10L))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("told", "bad\u0007line", 10L))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("bad\uD800line", "", 10L))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("trailing\uD83D", "", 10L))));
			Assert.IsTrue(Rules.Valid(Make(new Entry("paired\uD83D\uDE00", "", 10L))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("told", "", -1L))));
			Assert.IsTrue(Rules.Valid(Make(new Entry("told", "", 0L))));
		}

		[Test]
		public void ValidRefusesPhaseCountAndHashInconsistency()
		{
			string hash = Hash(Notes("a"));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L,
				ReportLedgerPhase.Prepared, false, 1, hash, 0, ""))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L,
				ReportLedgerPhase.Intent, false, 1, "", 0, ""))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L,
				ReportLedgerPhase.Intent, false, 1, hash, 2, hash))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "", 1L,
				ReportLedgerPhase.Intent, false, 1, hash, 0, ""))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L,
				ReportLedgerPhase.Intent, false, Rules.MaxNotes, hash, 0, ""))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L,
				ReportLedgerPhase.Intent, true, 1, hash, 0, ""))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L,
				ReportLedgerPhase.Proved, false, 1, hash, 1, hash))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L,
				ReportLedgerPhase.Proved, false, 1, hash, 2, "nothash"))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "", 1L,
				ReportLedgerPhase.Skipped, false, 1, hash, 2, hash))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L,
				ReportLedgerPhase.Skipped, false, 1, hash, 1, hash))));
			Assert.IsTrue(Rules.Valid(Make(new Entry("t", "", 1L,
				ReportLedgerPhase.Skipped, true, 1, hash, 1, hash))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L,
				(ReportLedgerPhase)9, false, 0, "", 0, ""))));
		}

		[Test]
		public void ValidRefusesAStartedLineBehindAnUnfinishedOne()
		{
			string hash = Hash(Notes());
			Entry done = new Entry("t", "", 1L, ReportLedgerPhase.Skipped, true, 0, hash, 0, hash);
			Entry started = new Entry("t", "", 1L, ReportLedgerPhase.Skipped, false, 0, hash, 0, hash);
			Assert.IsTrue(Rules.Valid(Make(done, started)));
			Assert.IsFalse(Rules.Valid(Make(started, started)));
			Assert.IsFalse(Rules.Valid(Make(Line(""), started)));
			Assert.IsFalse(Rules.Valid(Make(Line(""), done)));
			Assert.IsTrue(Rules.Valid(Make(Line(""), Line(""))));
		}

		[Test]
		public void CompleteCoversTheEmptyPlanAndOnlyWhollyToldLines()
		{
			Assert.IsTrue(Rules.Complete(Make()));
			Assert.IsFalse(Rules.Complete(null));
			Assert.IsFalse(Rules.Complete(Make(Line(""))));
			Plan plan = Make(Line(""));
			Assert.IsTrue(Rules.TryArmLedger(plan, 0, Notes(), out plan));
			Assert.IsFalse(Rules.Complete(plan));
			Assert.IsTrue(Rules.TryProveChronicle(plan, 0, out plan));
			Assert.IsTrue(Rules.Complete(plan));
		}

		[Test]
		public void EventIdDerivesFromTheOwnerAndOrdinal()
		{
			Plan plan = Make(Batch, Line(""), Line(""));
			Assert.AreEqual(Batch + ":report:0", Rules.EventId(plan, 0));
			Assert.AreEqual(Batch + ":report:1", Rules.EventId(plan, 1));
			Assert.IsNull(Rules.EventId(plan, 2));
			Assert.IsNull(Rules.EventId(plan, -1));
			Assert.IsNull(Rules.EventId(Make(Realm, Line("")), 0));
		}

		[Test]
		public void ArmFreezesTheExactNoteCountAndHash()
		{
			Plan plan = Make(Line("ledger line"));
			List<string> notes = Notes("first", "second");
			Assert.IsTrue(Rules.TryArmLedger(plan, 0, notes, out Plan armed));
			Assert.AreEqual(ReportLedgerPhase.Intent, armed.Entries[0].LedgerPhase);
			Assert.AreEqual(2, armed.Entries[0].BeforeCount);
			Assert.AreEqual(Hash(notes), armed.Entries[0].BeforeHash);
			Assert.AreEqual(0, armed.Entries[0].AfterCount);
			Assert.AreEqual("", armed.Entries[0].AfterHash);
			Assert.IsFalse(armed.Entries[0].ChronicleProved);
			Assert.AreEqual(ReportLedgerPhase.Prepared, plan.Entries[0].LedgerPhase);
			Assert.IsFalse(Rules.TryArmLedger(armed, 0, notes, out Plan again));
			Assert.IsNull(again);
			Assert.IsFalse(Rules.TryArmLedger(plan, 0, null, out again));
			Assert.IsFalse(Rules.TryArmLedger(plan, 1, notes, out again));
			Assert.IsFalse(Rules.TryArmLedger(plan, 0, new string[1] { null }, out again));
			Assert.IsFalse(Rules.TryArmLedger(Make(Realm, Line("L")), 0, notes, out again));
			Assert.IsNull(again);
		}

		[Test]
		public void AnEmptyLedgerLineAndAFullNoteListBothSkipWithoutStalling()
		{
			Assert.IsTrue(Rules.TryArmLedger(Make(Line("")), 0, Notes("a"), out Plan skipped));
			Assert.AreEqual(ReportLedgerPhase.Skipped, skipped.Entries[0].LedgerPhase);
			Assert.AreEqual(1, skipped.Entries[0].BeforeCount);
			Assert.AreEqual(skipped.Entries[0].BeforeCount, skipped.Entries[0].AfterCount);
			Assert.AreEqual(skipped.Entries[0].BeforeHash, skipped.Entries[0].AfterHash);
			Assert.AreEqual(Hash(Notes("a")), skipped.Entries[0].AfterHash);
			string[] twelve = new string[Rules.MaxNotes];
			for (int i = 0; i < twelve.Length; i++) twelve[i] = "n" + i;
			List<string> full = Notes(new List<string>(twelve) { "overflow" }.ToArray());
			Assert.AreEqual(Rules.MaxNotes, full.Count);
			Assert.IsTrue(Rules.TryArmLedger(Make(Line("ledger line")), 0, full, out Plan stalled));
			Assert.AreEqual(ReportLedgerPhase.Skipped, stalled.Entries[0].LedgerPhase);
			Assert.AreEqual(Rules.MaxNotes, stalled.Entries[0].AfterCount);
			Assert.AreEqual(Hash(full), stalled.Entries[0].AfterHash);
			Assert.IsTrue(Rules.TryProveChronicle(stalled, 0, out Plan told));
			Assert.IsTrue(Rules.Complete(told));
		}

		[Test]
		public void LedgerActionAnswersApplyThenConfirmAndRefusesAnyThirdState()
		{
			Plan armed = Armed();
			Assert.AreEqual(KingdomSubsidenceEffectAction.Apply,
				Rules.LedgerAction(armed, 0, Notes("a")));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Confirm,
				Rules.LedgerAction(armed, 0, Notes("a", "L")));
			foreach (List<string> third in new[] { Notes(), Notes("b"), Notes("a", "X"),
				Notes("b", "L"), Notes("a", "L", "z") })
				Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
					Rules.LedgerAction(armed, 0, third));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse, Rules.LedgerAction(armed, 0, null));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				Rules.LedgerAction(Make(Line("L")), 0, Notes("a")));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				Rules.LedgerAction(armed, 1, Notes("a")));
		}

		[Test]
		public void ADifferentHashDomainNeverAuthorizesTheWrite()
		{
			Assert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("taf-subsidence-ledger-v2",
				Notes("a"), out string stranger));
			Assert.AreNotEqual(stranger, Hash(Notes("a")));
			Plan forged = Make(new Entry("told", "L", 10L, ReportLedgerPhase.Intent, false, 1,
				stranger, 0, ""));
			Assert.IsTrue(Rules.Valid(forged));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				Rules.LedgerAction(forged, 0, Notes("a")));
			Assert.IsFalse(Rules.TryProveLedger(forged, 0, Notes("a", "L"), out Plan next));
			Assert.IsNull(next);
		}

		[Test]
		public void ProveLedgerAcceptsTheExactAppendAndNothingElse()
		{
			Plan armed = Armed();
			foreach (List<string> wrong in new[] { Notes("a"), Notes("a", "X"), Notes("b", "L"),
				Notes("a", "L", "z") })
			{
				Assert.IsFalse(Rules.TryProveLedger(armed, 0, wrong, out Plan refused));
				Assert.IsNull(refused);
			}
			Assert.AreEqual(ReportLedgerPhase.Intent, armed.Entries[0].LedgerPhase);
			List<string> after = Notes("a", "L");
			Assert.IsTrue(Rules.TryProveLedger(armed, 0, after, out Plan proved));
			Assert.AreEqual(ReportLedgerPhase.Proved, proved.Entries[0].LedgerPhase);
			Assert.AreEqual(1, proved.Entries[0].BeforeCount);
			Assert.AreEqual(2, proved.Entries[0].AfterCount);
			Assert.AreEqual(Hash(after), proved.Entries[0].AfterHash);
			Assert.AreEqual(ReportLedgerPhase.Intent, armed.Entries[0].LedgerPhase);
			// A settled append is never re-proved: only a skip answers its own repeat.
			Assert.IsFalse(Rules.TryProveLedger(proved, 0, after, out Plan repeat));
			Assert.IsNull(repeat);
		}

		[Test]
		public void ASkippedLineAnswersItsOwnExactRepeatAndNeverChanges()
		{
			Assert.IsTrue(Rules.TryArmLedger(Make(Line("")), 0, Notes("a"), out Plan skipped));
			Assert.IsTrue(Rules.TryProveLedger(skipped, 0, Notes("a"), out Plan same));
			Assert.AreSame(skipped, same);
			Assert.IsFalse(Rules.TryProveLedger(skipped, 0, Notes("a", "b"), out Plan moved));
			Assert.IsNull(moved);
			Assert.IsFalse(Rules.TryProveLedger(skipped, 0, Notes("z"), out moved));
			Assert.IsNull(moved);
		}

		[Test]
		public void TheChronicleIsToldOnlyOnceAndOnlyAfterTheLedgerSettles()
		{
			Assert.IsFalse(Rules.TryProveChronicle(Make(Line("L")), 0, out Plan next));
			Assert.IsNull(next);
			Plan armed = Armed();
			Assert.IsFalse(Rules.TryProveChronicle(armed, 0, out next));
			Assert.IsNull(next);
			Assert.IsTrue(Rules.TryProveLedger(armed, 0, Notes("a", "L"), out Plan proved));
			Assert.IsTrue(Rules.TryProveChronicle(proved, 0, out Plan told));
			Assert.IsTrue(told.Entries[0].ChronicleProved);
			Assert.IsFalse(proved.Entries[0].ChronicleProved);
			Assert.IsTrue(Rules.TryProveChronicle(told, 0, out Plan again));
			Assert.AreSame(told, again);
			Assert.IsTrue(Rules.Complete(told));
		}

		[Test]
		public void TheSecondLineWaitsUntilTheFirstIsWhollyTold()
		{
			Plan plan = Make(Line("L"), Line("M"));
			Assert.IsFalse(Rules.TryArmLedger(plan, 1, Notes("a"), out Plan blocked));
			Assert.IsNull(blocked);
			Assert.IsTrue(Rules.TryArmLedger(plan, 0, Notes("a"), out plan));
			Assert.IsTrue(Rules.TryProveLedger(plan, 0, Notes("a", "L"), out plan));
			Assert.IsFalse(Rules.TryArmLedger(plan, 1, Notes("a", "L"), out blocked));
			Assert.IsNull(blocked);
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				Rules.LedgerAction(plan, 1, Notes("a", "L")));
			Assert.IsTrue(Rules.TryProveChronicle(plan, 0, out plan));
			Assert.IsTrue(Rules.TryArmLedger(plan, 1, Notes("a", "L"), out plan));
			Assert.AreEqual(ReportLedgerPhase.Intent, plan.Entries[1].LedgerPhase);
			Assert.AreEqual(2, plan.Entries[1].BeforeCount);
		}

		[Test]
		public void CanonicalRoundTripKeepsAnEmptyAndAMixedPhasePlan()
		{
			foreach (Plan original in new[] { Make(), Make(Batch), Mixed(), Losses() })
			{
				Assert.IsTrue(Codec.TryEncode(original, out string wire));
				Assert.IsTrue(wire.StartsWith("st3:", StringComparison.Ordinal));
				Assert.IsTrue(wire.Length <= Codec.MaxWireChars);
				Assert.IsTrue(Codec.TryDecode(wire, out Plan restored));
				Assert.AreNotSame(original, restored);
				Assert.AreEqual(original.OwnerId, restored.OwnerId);
				Assert.AreEqual(original.RealmId, restored.RealmId);
				Assert.AreEqual(original.SettlementId, restored.SettlementId);
				Assert.AreEqual(original.Entries.Count, restored.Entries.Count);
				for (int i = 0; i < original.Entries.Count; i++)
				{
					Entry a = original.Entries[i], b = restored.Entries[i];
					Assert.AreEqual(a.Text, b.Text);
					Assert.AreEqual(a.LedgerText, b.LedgerText);
					Assert.AreEqual(a.AtTick, b.AtTick);
					Assert.AreEqual(a.LedgerPhase, b.LedgerPhase);
					Assert.AreEqual(a.ChronicleProved, b.ChronicleProved);
					Assert.AreEqual(a.BeforeCount, b.BeforeCount);
					Assert.AreEqual(a.BeforeHash, b.BeforeHash);
					Assert.AreEqual(a.AfterCount, b.AfterCount);
					Assert.AreEqual(a.AfterHash, b.AfterHash);
					Assert.AreEqual(a.LedgerLoss, b.LedgerLoss);
					Assert.AreEqual(a.ChronicleLost, b.ChronicleLost);
				}
				Assert.IsTrue(Codec.TryEncode(restored, out string repeated));
				Assert.AreEqual(wire, repeated);
			}
			Assert.IsFalse(Codec.TryEncode(null, out string none));
			Assert.IsNull(none);
			Assert.IsFalse(Codec.TryEncode(Make(Realm, Line("")), out none));
			Assert.IsNull(none);
		}

		[Test]
		public void CodecRefusesEveryNoncanonicalTruncatedOrForgedWire()
		{
			Plan plan = Mixed();
			Assert.IsTrue(Codec.TryEncode(plan, out string wire));
			Assert.AreEqual(wire, ForgeV3(Step, Realm, Settlement, Of(plan.Entries[0]),
				Of(plan.Entries[1]), Of(plan.Entries[2]), Of(plan.Entries[3])));
			Refuses(null);
			Refuses("");
			Refuses("st1:");
			// The step book's own st1 sentinels are not plans and must never decode as one.
			Refuses(KingdomSubsidenceBatchRules.NoReport);
			Refuses(KingdomSubsidenceBatchRules.PendingReport);
			Refuses("st1:" + wire.Substring(4));
			Refuses("st2:" + wire.Substring(4));
			Refuses("st4:" + wire.Substring(4));
			Refuses(" " + wire);
			Refuses(wire.Insert(12, "\n"));
			Refuses(wire + "\n");
			Refuses(wire + "==");
			Refuses(wire.Substring(0, wire.Length - 4));
			Refuses(new string('x', Codec.MaxWireChars + 1));
			byte[] trailing = Bytes(wire);
			Array.Resize(ref trailing, trailing.Length + 1);
			Refuses(Wire(trailing));
			Row phase = Of(plan.Entries[0]);
			phase.Phase = 9;
			Refuses(ForgeV2(Step, Realm, Settlement, phase));
			Row flag = Of(plan.Entries[0]);
			flag.Chronicle = 2;
			Refuses(ForgeV2(Step, Realm, Settlement, flag));
			// Frontier-invalid payload: a wholly told line standing behind an untouched one.
			Refuses(ForgeV2(Step, Realm, Settlement, Of(Line("L")), Of(plan.Entries[1])));
			Refuses(ForgeV2(Realm, Realm, Settlement, Of(plan.Entries[0])));
			Refuses(ForgeV2(Step, Settlement, Settlement, Of(plan.Entries[0])));
			Refuses(ForgeV2(Step, Realm, Realm, Of(plan.Entries[0])));
			Refuses(ForgeV2(Step, Realm, Settlement, Of(Line("")), Of(Line("")),
				Of(Line("")), Of(Line("")), Of(Line(""))));
		}

		[Test]
		public void LedgerHashDomainIsPinnedToVersionOne()
		{
			Assert.AreEqual("taf-subsidence-ledger-v1", Rules.LedgerHashDomain);
		}

		[Test]
		public void TheConstructorCopiesTheEntriesArrayRatherThanAliasingIt()
		{
			Entry original = new Entry("told", "", 10L);
			Entry[] source = new Entry[] { original };
			Plan plan = new Plan(Step, Realm, Settlement, source);
			source[0] = new Entry("changed", "", 10L);
			Assert.AreEqual(1, plan.Entries.Count);
			Assert.AreEqual("told", plan.Entries[0].Text);
		}

		[Test]
		public void EncodeRefusesAValidPlanWhoseWireExceedsTheByteGuard()
		{
			// Each entry's own two text fields hold nothing but 3-byte UTF-8 characters
			// (U+20AC, the euro sign), at each field's own char bound, so a compliant plan
			// still drives the raw byte count past the codec's own guard before base64 ever runs.
			string text = new string('\u20AC', KingdomChronicleReceiptRules.MaxEventTextChars);
			string ledger = new string('\u20AC', KingdomChronicleReceiptRules.MaxEntryChars);
			Entry[] entries = new Entry[Rules.MaxEntries];
			for (int i = 0; i < entries.Length; i++) entries[i] = new Entry(text, ledger, 10L);
			Plan plan = Make(entries);
			Assert.IsTrue(Rules.Valid(plan));
			Assert.IsFalse(Codec.TryEncode(plan, out string wire));
			Assert.IsNull(wire);
		}

		[Test]
		public void ALostLineSettlesTheReportWithoutEverCompletingIt()
		{
			Plan lost = Lost();
			Assert.AreEqual(ReportLedgerPhase.Lost, lost.Entries[0].LedgerPhase);
			Assert.IsFalse(Rules.Settled(lost));
			Assert.IsFalse(Rules.Complete(lost));
			Assert.IsTrue(Rules.HasLoss(lost));
			Assert.IsTrue(Rules.TryLoseChronicle(lost, 0, out Plan wholly));
			Assert.IsTrue(Rules.Settled(wholly));
			Assert.IsFalse(Rules.Complete(wholly));
			Assert.IsTrue(Rules.TryProveChronicle(Lost(), 0, out Plan told));
			Assert.IsTrue(Rules.Settled(told));
			Assert.IsFalse(Rules.Complete(told));
			// A delivered plan is both, and carries no loss anywhere.
			Plan clean = Make(Line(""));
			Assert.IsTrue(Rules.TryArmLedger(clean, 0, Notes(), out clean));
			Assert.IsTrue(Rules.TryProveChronicle(clean, 0, out clean));
			Assert.IsTrue(Rules.Settled(clean));
			Assert.IsTrue(Rules.Complete(clean));
			Assert.IsFalse(Rules.HasLoss(clean));
			Assert.IsTrue(Rules.Settled(Make()));
			Assert.IsFalse(Rules.Settled(null));
			Assert.IsFalse(Rules.HasLoss(null));
			Assert.IsFalse(Rules.HasLoss(new Plan(Step, Realm, Settlement, null)));
		}

		[Test]
		public void AThirdStateLossLatchesOnlyAStrangersList()
		{
			Plan armed = Armed();
			// The frozen before still authorizes the write; the exact append is already delivered.
			Assert.IsFalse(Rules.TryLoseLedger(armed, 0, Notes("a"), false, out Plan refused));
			Assert.IsNull(refused);
			Assert.IsFalse(Rules.TryLoseLedger(armed, 0, Notes("a", "L"), false, out refused));
			Assert.IsNull(refused);
			foreach (List<string> third in new[] { Notes(), Notes("b"), Notes("a", "X"),
				Notes("b", "L"), Notes("a", "L", "z") })
			{
				Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
					Rules.LedgerAction(armed, 0, third));
				Assert.IsTrue(Rules.TryLoseLedger(armed, 0, third, false, out Plan lost));
				Assert.AreEqual(ReportLedgerPhase.Lost, lost.Entries[0].LedgerPhase);
				Assert.AreEqual(LedgerLossKind.ThirdState, lost.Entries[0].LedgerLoss);
				Assert.AreEqual(third.Count, lost.Entries[0].AfterCount);
				Assert.AreEqual(Hash(third), lost.Entries[0].AfterHash);
				Assert.IsTrue(Rules.HasLoss(lost));
			}
			// An unreadable observation is no observation, and nothing outside the frontier moves.
			Assert.IsFalse(Rules.TryLoseLedger(armed, 0, null, false, out refused));
			Assert.IsNull(refused);
			Assert.IsFalse(Rules.TryLoseLedger(armed, 0, new string[1] { null }, false, out refused));
			Assert.IsNull(refused);
			Assert.IsFalse(Rules.TryLoseLedger(armed, 1, Notes("b"), false, out refused));
			Assert.IsNull(refused);
			Assert.IsFalse(Rules.TryLoseLedger(Make(Line("L")), 0, Notes("b"), false, out refused));
			Assert.IsNull(refused);
			Assert.AreEqual(ReportLedgerPhase.Intent, armed.Entries[0].LedgerPhase);
		}

		[Test]
		public void TheHomecomingBarrierLosesTheFrozenBeforeButNeverTheExactAppend()
		{
			Plan armed = Armed();
			Assert.AreEqual(KingdomSubsidenceEffectAction.Apply,
				Rules.LedgerAction(armed, 0, Notes("a")));
			Assert.IsTrue(Rules.TryLoseLedger(armed, 0, Notes("a"), true, out Plan reset));
			Assert.AreEqual(ReportLedgerPhase.Lost, reset.Entries[0].LedgerPhase);
			Assert.AreEqual(LedgerLossKind.HomecomingReset, reset.Entries[0].LedgerLoss);
			Assert.AreEqual(reset.Entries[0].BeforeCount, reset.Entries[0].AfterCount);
			Assert.AreEqual(reset.Entries[0].BeforeHash, reset.Entries[0].AfterHash);
			// A third state is lost by the barrier too, under the barrier's own reason.
			Assert.IsTrue(Rules.TryLoseLedger(armed, 0, Notes("b"), true, out Plan third));
			Assert.AreEqual(LedgerLossKind.HomecomingReset, third.Entries[0].LedgerLoss);
			Assert.AreEqual(Hash(Notes("b")), third.Entries[0].AfterHash);
			// Delivered is delivered: the exact append is proved, never lost, by either reason.
			Assert.IsFalse(Rules.TryLoseLedger(armed, 0, Notes("a", "L"), true, out Plan refused));
			Assert.IsNull(refused);
			Assert.IsTrue(Rules.TryProveLedger(armed, 0, Notes("a", "L"), out Plan proved));
			Assert.IsFalse(Rules.TryLoseLedger(proved, 0, Notes("a", "L"), true, out refused));
			Assert.IsNull(refused);
			Assert.IsFalse(Rules.TryLoseLedger(proved, 0, Notes("a", "L"), false, out refused));
			Assert.IsNull(refused);
		}

		[Test]
		public void ALostLineKeepsItsFrozenBeforeAndRecordsOnlyWhatWasSeen()
		{
			Plan armed = Armed();
			Entry witness = armed.Entries[0];
			List<string> seen = Notes("b", "c", "d");
			Assert.IsTrue(Rules.TryLoseLedger(armed, 0, seen, false, out Plan lost));
			Entry entry = lost.Entries[0];
			Assert.AreEqual(witness.Text, entry.Text);
			Assert.AreEqual(witness.LedgerText, entry.LedgerText);
			Assert.AreEqual(witness.AtTick, entry.AtTick);
			Assert.AreEqual(witness.BeforeCount, entry.BeforeCount);
			Assert.AreEqual(witness.BeforeHash, entry.BeforeHash);
			Assert.AreEqual(3, entry.AfterCount);
			Assert.AreEqual(Hash(seen), entry.AfterHash);
			// Nothing is guessed: the after is what the world held, not the before plus the line.
			Assert.AreNotEqual(witness.BeforeCount + 1, entry.AfterCount);
			Assert.AreNotEqual(Hash(Notes("a", "L")), entry.AfterHash);
			Assert.IsFalse(entry.ChronicleProved);
			Assert.IsFalse(entry.ChronicleLost);
			Assert.AreEqual(ReportLedgerPhase.Intent, armed.Entries[0].LedgerPhase);
		}

		[Test]
		public void ARepeatedLossIsIdempotentAndAnyOtherObservationRefuses()
		{
			Plan lost = Lost();
			Assert.IsTrue(Rules.TryLoseLedger(lost, 0, Notes("b"), false, out Plan again));
			Assert.AreSame(lost, again);
			Assert.IsFalse(Rules.TryLoseLedger(lost, 0, Notes("c"), false, out Plan moved));
			Assert.IsNull(moved);
			// A different reason for the same list is a different fact, not a repeat.
			Assert.IsFalse(Rules.TryLoseLedger(lost, 0, Notes("b"), true, out moved));
			Assert.IsNull(moved);
			Assert.IsFalse(Rules.TryArmLedger(lost, 0, Notes("b"), out moved));
			Assert.IsNull(moved);
			Assert.IsFalse(Rules.TryProveLedger(lost, 0, Notes("b", "L"), out moved));
			Assert.IsNull(moved);
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				Rules.LedgerAction(lost, 0, Notes("b")));
		}

		[Test]
		public void AClearedNoteListNeverReopensALostLine()
		{
			KingdomLedger ledger = new KingdomLedger();
			List<string> notes = ledger.Notes;
			Assert.IsTrue(Rules.TryArmLedger(Make(Line("L")), 0, notes, out Plan armed));
			Assert.AreEqual(0, armed.Entries[0].BeforeCount);
			ledger.Note("x");
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				Rules.LedgerAction(armed, 0, notes));
			Assert.IsTrue(Rules.TryLoseLedger(armed, 0, notes, false, out Plan lost));
			ledger.Reset();
			Assert.AreEqual(0, notes.Count);
			// The very same list, cleared, would authorize the append that was already lost - the
			// fact is not monotone, which is exactly why it is latched rather than re-derived.
			Assert.AreEqual(KingdomSubsidenceEffectAction.Apply,
				Rules.LedgerAction(armed, 0, notes));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				Rules.LedgerAction(lost, 0, notes));
			Assert.IsFalse(Rules.TryArmLedger(lost, 0, notes, out Plan reopened));
			Assert.IsNull(reopened);
			Assert.IsFalse(Rules.TryProveLedger(lost, 0, notes, out reopened));
			Assert.IsNull(reopened);
			Assert.IsFalse(Rules.TryLoseLedger(lost, 0, notes, false, out reopened));
			Assert.IsNull(reopened);
			Assert.IsFalse(Rules.TryLoseLedger(lost, 0, notes, true, out reopened));
			Assert.IsNull(reopened);
			Assert.AreEqual(ReportLedgerPhase.Lost, lost.Entries[0].LedgerPhase);
			Assert.AreEqual(1, lost.Entries[0].AfterCount);
			Assert.AreEqual(Hash(Notes("x")), lost.Entries[0].AfterHash);
		}

		[Test]
		public void TheChronicleIsLostOnItsOwnAndOnlyAfterTheLedgerSettles()
		{
			Assert.IsFalse(Rules.TryLoseChronicle(Make(Line("L")), 0, out Plan early));
			Assert.IsNull(early);
			Plan armed = Armed();
			Assert.IsFalse(Rules.TryLoseChronicle(armed, 0, out early));
			Assert.IsNull(early);
			Assert.IsTrue(Rules.TryProveLedger(armed, 0, Notes("a", "L"), out Plan proved));
			Assert.IsTrue(Rules.TryLoseChronicle(proved, 0, out Plan chronicleLost));
			Assert.IsTrue(chronicleLost.Entries[0].ChronicleLost);
			Assert.IsFalse(chronicleLost.Entries[0].ChronicleProved);
			Assert.AreEqual(ReportLedgerPhase.Proved, chronicleLost.Entries[0].LedgerPhase);
			Assert.AreEqual(LedgerLossKind.None, chronicleLost.Entries[0].LedgerLoss);
			Assert.IsFalse(proved.Entries[0].ChronicleLost);
			Assert.IsTrue(Rules.Settled(chronicleLost));
			Assert.IsFalse(Rules.Complete(chronicleLost));
			Assert.IsTrue(Rules.HasLoss(chronicleLost));
			Assert.IsTrue(Rules.TryLoseChronicle(chronicleLost, 0, out Plan again));
			Assert.AreSame(chronicleLost, again);
			// Lost never becomes delivered, and delivered never becomes lost.
			Assert.IsFalse(Rules.TryProveChronicle(chronicleLost, 0, out Plan turned));
			Assert.IsNull(turned);
			Assert.IsTrue(Rules.TryProveChronicle(proved, 0, out Plan told));
			Assert.IsFalse(Rules.TryLoseChronicle(told, 0, out turned));
			Assert.IsNull(turned);
			// A lost ledger settles its half too, so the chronicle may still be told, or lost.
			Assert.IsTrue(Rules.TryProveChronicle(Lost(), 0, out Plan lostThenTold));
			Assert.IsTrue(lostThenTold.Entries[0].ChronicleProved);
			Assert.IsFalse(Rules.Complete(lostThenTold));
			Assert.IsTrue(Rules.TryLoseChronicle(Lost(), 0, out Plan lostBoth));
			Assert.IsTrue(lostBoth.Entries[0].ChronicleLost);
			Assert.IsTrue(Rules.Settled(lostBoth));
		}

		[Test]
		public void TheFrontierPassesALostLineOnlyOnceItsChronicleSettles()
		{
			Plan plan = Make(Line("L"), Line("M"));
			Assert.IsTrue(Rules.TryArmLedger(plan, 0, Notes("a"), out plan));
			Assert.IsTrue(Rules.TryLoseLedger(plan, 0, Notes("b"), false, out plan));
			// Lost but untold: the line is not settled, so the next may not begin.
			Assert.IsFalse(Rules.TryArmLedger(plan, 1, Notes("b"), out Plan blocked));
			Assert.IsNull(blocked);
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				Rules.LedgerAction(plan, 1, Notes("b")));
			Assert.IsFalse(Rules.TryLoseChronicle(plan, 1, out blocked));
			Assert.IsNull(blocked);
			Assert.IsTrue(Rules.TryLoseChronicle(plan, 0, out plan));
			Assert.IsTrue(Rules.TryArmLedger(plan, 1, Notes("b"), out plan));
			Assert.AreEqual(1, plan.Entries[1].BeforeCount);
			Assert.IsTrue(Rules.TryProveLedger(plan, 1, Notes("b", "M"), out plan));
			Assert.IsTrue(Rules.TryProveChronicle(plan, 1, out plan));
			Assert.IsTrue(Rules.Settled(plan));
			Assert.IsFalse(Rules.Complete(plan));
			Assert.IsTrue(Rules.HasLoss(plan));
		}

		[Test]
		public void ValidRefusesEveryImpossibleLossCombination()
		{
			string one = Hash(Notes("a")), other = Hash(Notes("b")), zero = Hash(Notes());
			Entry done = new Entry("t", "", 1L, ReportLedgerPhase.Skipped, true, 0, zero, 0, zero);
			Assert.IsTrue(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				1, one, 1, other, LedgerLossKind.ThirdState, false))));
			// Lost without a reason, and a reason on a line that lost nothing.
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				1, one, 1, other, LedgerLossKind.None, false))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Intent, false,
				1, one, 0, "", LedgerLossKind.ThirdState, false))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Proved, false,
				1, one, 2, other, LedgerLossKind.HomecomingReset, false))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				1, one, 1, other, (LedgerLossKind)9, false))));
			// The witness must stand on both sides: a frozen before and an observed after.
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				0, "", 1, other, LedgerLossKind.ThirdState, false))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				1, one, 0, "", LedgerLossKind.ThirdState, false))));
			// An unowed line and a line at the note bound are never armed, so never lost.
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "", 1L, ReportLedgerPhase.Lost, false,
				1, one, 1, other, LedgerLossKind.ThirdState, false))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				Rules.MaxNotes, one, 1, other, LedgerLossKind.ThirdState, false))));
			// A third state is by definition not the frozen before; the barrier's loss may be.
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				1, one, 1, one, LedgerLossKind.ThirdState, false))));
			Assert.IsTrue(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				1, one, 1, one, LedgerLossKind.HomecomingReset, false))));
			// The two chronicle halves are exclusive, and neither stands over an open ledger.
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, true,
				1, one, 1, other, LedgerLossKind.ThirdState, true))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Prepared,
				false, 0, "", 0, "", LedgerLossKind.None, true))));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Intent, false,
				1, one, 0, "", LedgerLossKind.None, true))));
			Assert.IsTrue(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, true,
				1, one, 1, other, LedgerLossKind.ThirdState, false))));
			Assert.IsTrue(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Proved, false,
				1, one, 2, other, LedgerLossKind.None, true))));
			// A chronicle-lost line is settled, so the next may stand behind it; an untold lost
			// line is not, and nothing may follow it.
			Assert.IsTrue(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				1, one, 1, other, LedgerLossKind.ThirdState, true), done)));
			Assert.IsFalse(Rules.Valid(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				1, one, 1, other, LedgerLossKind.ThirdState, false), done)));
			// A malformed plan still answers for its loss, so a caller may name it before refusing.
			Assert.IsTrue(Rules.HasLoss(Make(new Entry("t", "l", 1L, ReportLedgerPhase.Lost, false,
				1, one, 1, one, LedgerLossKind.ThirdState, false))));
		}

		[Test]
		public void TheSecondWireCarriesBothLossKindsAndBothChronicleDispositions()
		{
			Plan plan = Losses();
			string wire = ForgeV2(Step, Realm, Settlement, Of(plan.Entries[0]), Of(plan.Entries[1]));
			Assert.IsTrue(wire.StartsWith("st2:", StringComparison.Ordinal));
			Assert.IsTrue(Codec.TryDecode(wire, out Plan restored));
			Assert.AreEqual(LedgerLossKind.ThirdState, restored.Entries[0].LedgerLoss);
			Assert.IsTrue(restored.Entries[0].ChronicleLost);
			Assert.IsFalse(restored.Entries[0].ChronicleProved);
			Assert.AreEqual(LedgerLossKind.HomecomingReset, restored.Entries[1].LedgerLoss);
			Assert.IsFalse(restored.Entries[1].ChronicleLost);
			Assert.IsTrue(restored.Entries[1].ChronicleProved);
			Assert.AreEqual(plan.Entries[0].BeforeHash, restored.Entries[0].BeforeHash);
			Assert.AreEqual(plan.Entries[0].AfterHash, restored.Entries[0].AfterHash);
			Assert.IsTrue(Rules.Settled(restored));
			Assert.IsFalse(Rules.Complete(restored));
			Assert.IsTrue(Codec.TryEncode(restored, out string repeated));
			Assert.AreEqual(ForgeV3(Step, Realm, Settlement, Of(plan.Entries[0]), Of(plan.Entries[1])), repeated);
			Assert.IsTrue(Codec.TryDecode(repeated, out Plan current));
			Assert.IsTrue(Codec.TryEncode(current, out string stable));
			Assert.AreEqual(repeated, stable);
		}

		[Test]
		public void AnIndependentlyFramedFirstWireStillReadsAndIsRewrittenAsCurrent()
		{
			Plan plan = Mixed();
			string first = ForgeV1(Step, Realm, Settlement, Of(plan.Entries[0]),
				Of(plan.Entries[1]), Of(plan.Entries[2]), Of(plan.Entries[3]));
			Assert.IsTrue(first.StartsWith("st1:", StringComparison.Ordinal));
			Assert.IsTrue(Codec.TryDecode(first, out Plan restored));
			Assert.AreEqual(plan.Entries.Count, restored.Entries.Count);
			foreach (Entry entry in restored.Entries)
			{
				Assert.AreEqual(LedgerLossKind.None, entry.LedgerLoss);
				Assert.IsFalse(entry.ChronicleLost);
			}
			Assert.AreEqual(ReportLedgerPhase.Skipped, restored.Entries[0].LedgerPhase);
			Assert.AreEqual(ReportLedgerPhase.Proved, restored.Entries[1].LedgerPhase);
			Assert.IsFalse(Rules.HasLoss(restored));
			// Read at its own version, rewritten at the current one, and stable from there on.
			Assert.IsTrue(Codec.TryEncode(restored, out string second));
			Assert.IsTrue(second.StartsWith("st3:", StringComparison.Ordinal));
			Assert.AreNotEqual(first, second);
			Assert.IsTrue(Codec.TryDecode(second, out Plan again));
			Assert.IsTrue(Codec.TryEncode(again, out string stable));
			Assert.AreEqual(second, stable);
			// Neither body may wear the other's label, and an old wire may carry no loss bytes.
			Refuses("st2:" + first.Substring(4));
			Refuses("st1:" + second.Substring(4));
			byte[] padded = Convert.FromBase64String(first.Substring(4));
			Array.Resize(ref padded, padded.Length + 2);
			Refuses("st1:" + Convert.ToBase64String(padded));
		}

		[Test]
		public void CodecRefusesCorruptLossBytesAndImpossibleFlagCombinations()
		{
			Plan plan = Losses();
			Row lost = Of(plan.Entries[0]);
			Assert.AreEqual((byte)ReportLedgerPhase.Lost, lost.Phase);
			Assert.AreEqual((byte)LedgerLossKind.ThirdState, lost.Loss);
			Assert.AreEqual((byte)1, lost.ChronicleLost);
			Row unknown = Of(plan.Entries[0]);
			unknown.Loss = 3;
			Refuses(ForgeV2(Step, Realm, Settlement, unknown));
			Row flag = Of(plan.Entries[0]);
			flag.ChronicleLost = 2;
			Refuses(ForgeV2(Step, Realm, Settlement, flag));
			Row phase = Of(plan.Entries[0]);
			phase.Phase = 5;
			Refuses(ForgeV2(Step, Realm, Settlement, phase));
			// A lost phase with no reason, and a reason on a phase that lost nothing.
			Row reasonless = Of(plan.Entries[0]);
			reasonless.Loss = 0;
			Refuses(ForgeV2(Step, Realm, Settlement, reasonless));
			Row told = Of(Mixed().Entries[1]);
			told.Loss = 1;
			Refuses(ForgeV2(Step, Realm, Settlement, told));
			// Told and lost at once.
			Row both = Of(Mixed().Entries[1]);
			Assert.AreEqual((byte)1, both.Chronicle);
			both.ChronicleLost = 1;
			Refuses(ForgeV2(Step, Realm, Settlement, both));
			// A lost line whose chronicle never settled cannot stand behind a started one.
			Refuses(ForgeV2(Step, Realm, Settlement, Of(Lost().Entries[0]), Of(Mixed().Entries[1])));
		}
	}
}
#endif
