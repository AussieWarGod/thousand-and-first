#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Entry = ThousandAndFirst.KingdomSubsidenceReportEntry;
using Plan = ThousandAndFirst.KingdomSubsidenceReportPlan;
using Rules = ThousandAndFirst.KingdomSubsidenceReportRules;
using Codec = ThousandAndFirst.KingdomSubsidenceReportCodec;

namespace ThousandAndFirst.Tests
{
	/// <summary>Executable pure protocol and parent-publication cuts; no engine or game-save proof.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceReportCapacityTests
	{
		private static Plan Fresh(int id = 1, int count = 1, string ledger = "")
		{
			var rows = new List<Entry>();
			for (int i = 0; i < count; i++) rows.Add(new Entry("A departure " + i, ledger, 10));
			return new Plan("taf:subsidence-step:v1:" + id.ToString("x64"),
				ChronicleCapacityFixture.Realm, ChronicleCapacityFixture.Settlement, rows);
		}
		private static Plan Ready(int id = 1, int count = 1)
		{ Assert.IsTrue(Rules.TryArmLedger(Fresh(id, count), 0, new string[0], out var ready)); return ready; }
		private static string Wire(Plan plan)
		{ Assert.IsTrue(Codec.TryEncode(plan, out string wire)); return wire; }
		private static KingdomChronicleCapacityWitness Witness(Plan plan, int index = 0)
		{
			string id = Rules.EventId(plan, index); Entry entry = plan.Entries[index];
			Assert.IsTrue(KingdomChronicleCapacityRules.TryFingerprint(plan.RealmId, plan.SettlementId, id,
				entry.Text, entry.AtTick, out string fingerprint));
			Assert.IsTrue(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(), id, fingerprint, out var witness));
			return witness;
		}
		private static Plan Refused(int id = 1)
		{ Plan ready = Ready(id); Assert.IsTrue(Rules.TryRefuseCapacity(ready, 0, Witness(ready), out var refused)); return refused; }
		private static KingdomSubsidenceStepBook Book()
		{ return new KingdomSubsidenceStepBook(KingdomSubsidenceAdmission.Admitted, ChronicleCapacityFixture.Realm,
			ChronicleCapacityFixture.Settlement, 1, null, 100000); }

		[Test]
		public void CapacityRefusalSettlesWithoutClaimingEitherDeliveredOrLostSink()
		{
			Plan before = Ready(); string original = Wire(before); var witness = Witness(before);
			Assert.IsTrue(Rules.TryRefuseCapacity(before, 0, witness, out var after));
			Entry entry = after.Entries[0];
			Assert.IsTrue(entry.CapacityRefused); Assert.IsFalse(entry.ChronicleLost); Assert.IsFalse(entry.ChronicleProved);
			Assert.AreEqual(4096, entry.CapacityCount); Assert.AreEqual(witness.RegistryHash, entry.CapacityHash);
			Assert.AreEqual(witness.Fingerprint, entry.CapacityFingerprint);
			Assert.IsTrue(Rules.Settled(after)); Assert.IsTrue(Rules.HasLoss(after)); Assert.IsFalse(Rules.Complete(after));
			Assert.AreEqual(original, Wire(before)); Assert.AreEqual(before.Entries[0].BeforeHash, entry.BeforeHash);
			Assert.IsTrue(Rules.TryRefuseCapacity(after, 0, witness, out var repeat)); Assert.AreSame(after, repeat);
			Assert.IsFalse(Rules.TryProveChronicle(after, 0, out _)); Assert.IsFalse(Rules.TryLoseChronicle(after, 0, out _));
		}

		[Test]
		public void LedgerMustActuallySettleBeforeCapacityAndRemainsUnchanged()
		{
			Plan plan = Fresh(1, 1, "departed"); var witness = Witness(plan);
			Assert.IsFalse(Rules.TryRefuseCapacity(plan, 0, witness, out _));
			KingdomLedger ledger = new KingdomLedger();
			Assert.IsTrue(Rules.TryArmLedger(plan, 0, ledger.Notes, out plan));
			Assert.IsFalse(Rules.TryRefuseCapacity(plan, 0, witness, out _));
			ledger.Note("departed"); Assert.IsTrue(Rules.TryProveLedger(plan, 0, ledger.Notes, out plan));
			Entry held = plan.Entries[0];
			Assert.IsTrue(Rules.TryRefuseCapacity(plan, 0, witness, out plan));
			Assert.AreEqual(ReportLedgerPhase.Proved, plan.Entries[0].LedgerPhase);
			Assert.AreEqual(held.BeforeHash, plan.Entries[0].BeforeHash); Assert.AreEqual(held.AfterHash, plan.Entries[0].AfterHash);
			CollectionAssert.AreEqual(new[] { "departed" }, ledger.Notes);
		}

		[Test]
		public void SequentialFrontierRequiresEarlierCapacityRefusalToBePersistedFirst()
		{
			Plan plan = Ready(1, 2); string before = Wire(plan);
			Assert.IsFalse(Rules.TryRefuseCapacity(plan, 1, Witness(plan, 1), out _));
			Assert.IsTrue(Rules.TryRefuseCapacity(plan, 0, Witness(plan), out plan));
			Assert.IsTrue(Rules.TryArmLedger(plan, 1, new string[0], out plan));
			Assert.IsTrue(Rules.TryRefuseCapacity(plan, 1, Witness(plan, 1), out plan));
			Assert.IsTrue(Rules.Settled(plan)); Assert.AreNotEqual(before, Wire(plan));
		}

		[TestCase("owner")] [TestCase("realm")] [TestCase("settlement")] [TestCase("index")]
		[TestCase("text")] [TestCase("tick")] [TestCase("count")] [TestCase("hash")]
		[TestCase("fingerprint")] [TestCase("reason")] [TestCase("proved")] [TestCase("lost")]
		public void CopiedCapacityEvidenceCannotAuthorizeChangedEventOrMalformedWitness(string field)
		{
			Plan plan = Refused(); string before = Wire(plan); Entry e = plan.Entries[0];
			var changed = new Entry(field == "text" ? "Different text" : e.Text, e.LedgerText,
				field == "tick" ? e.AtTick + 1 : e.AtTick, e.LedgerPhase, field == "proved", e.BeforeCount,
				e.BeforeHash, e.AfterCount, e.AfterHash, e.LedgerLoss, field == "lost",
				field == "reason" ? (ReportChronicleRefusal)2 : e.ChronicleRefusal,
				field == "count" ? 4095 : e.CapacityCount, field == "hash" ? new string('A', 64) : e.CapacityHash,
				field == "fingerprint" ? new string('f', 64) : e.CapacityFingerprint);
			var rows = new List<Entry> { changed };
			if (field == "index") rows.Insert(0, new Entry("first", "", 10, ReportLedgerPhase.Skipped,
				true, 0, e.BeforeHash, 0, e.BeforeHash));
			var foreign = new Plan(field == "owner" ? Fresh(2).OwnerId : plan.OwnerId,
				field == "realm" ? KingdomIdentityRules.RealmPrefix + new string('e', 64) : plan.RealmId,
				field == "settlement" ? KingdomIdentityRules.SettlementPrefix + new string('e', 64) : plan.SettlementId, rows);
			Assert.IsFalse(Rules.Valid(foreign)); Assert.IsFalse(Codec.TryEncode(foreign, out _));
			Assert.AreEqual(before, Wire(plan));
		}

		[Test]
		public void MissingUnknownAndContradictoryRefusalFieldsNeverDefaultAway()
		{
			Entry e = Ready().Entries[0];
			foreach (string text in new string[] { null, "bad", new string('A', 64) })
			{
				var row = new Entry(e.Text, e.LedgerText, e.AtTick, e.LedgerPhase, false,
					e.BeforeCount, e.BeforeHash, e.AfterCount, e.AfterHash, capacityHash: text);
				Assert.IsFalse(Rules.Valid(new Plan(Fresh().OwnerId, ChronicleCapacityFixture.Realm,
					ChronicleCapacityFixture.Settlement, new[] { row })));
			}
			Assert.IsFalse(Rules.TryRefuseCapacity(Ready(), 0, null, out _));
			Plan prior = Ready(); Assert.IsTrue(Rules.TryProveChronicle(prior, 0, out var delivered));
			Assert.IsFalse(Rules.TryRefuseCapacity(delivered, 0, Witness(prior), out _));
			Assert.IsTrue(Rules.TryLoseChronicle(prior, 0, out var lost));
			Assert.IsFalse(Rules.TryRefuseCapacity(lost, 0, Witness(prior), out _));
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
		[TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
		public void ActualPublicationBoundaryRetainsEveryBeforeAndAfterParentWriteCut(int cut)
		{
			Plan plan = Ready(); string original = Wire(plan), persisted = original;
			var witness = Witness(plan); var calls = new List<string>(); int proofs = 0;
			bool result = Rules.TryPublishCapacity(plan, 0, witness, () =>
			{
				calls.Add("proof"); proofs++;
				if (proofs == 1 && cut == 1 || proofs == 2 && cut == 7) throw new InvalidOperationException();
				return !(proofs == 1 && cut == 0 || proofs == 2 && cut == 6);
			}, next =>
			{
				calls.Add("save");
				if (cut == 2) return false; if (cut == 3) throw new InvalidOperationException();
				persisted = Wire(next);
				if (cut == 4) return false; if (cut == 5) throw new InvalidOperationException();
				return true;
			}, out var returned);
			Assert.AreEqual(cut == 8, result); Assert.AreEqual(cut == 8, returned != null);
			Assert.AreEqual(original, Wire(plan)); Assert.AreEqual("proof", calls[0]);
			Assert.AreEqual(cut < 2 ? 1 : cut < 6 ? 2 : 3, calls.Count);
			Assert.IsTrue(Codec.TryDecode(persisted, out var recovered));
			Assert.AreEqual(cut >= 4, recovered.Entries[0].CapacityRefused);
			if (cut < 4)
				Assert.IsTrue(Rules.TryPublishCapacity(recovered, 0, witness, () => true,
					next => { persisted = Wire(next); return true; }, out recovered));
			Assert.IsTrue(Codec.TryDecode(persisted, out recovered));
			Assert.IsTrue(Rules.Settled(recovered)); Assert.IsFalse(Rules.Complete(recovered));
			Assert.IsTrue(Rules.TryRefuseCapacity(recovered, 0, witness, out var repeat)); Assert.AreSame(recovered, repeat);
		}

		[Test]
		public void InvalidPublicationCallbacksOrUnsettledLedgerHaveNoEffects()
		{
			Plan ready = Ready(); int calls = 0;
			Assert.IsFalse(Rules.TryPublishCapacity(ready, 0, Witness(ready), null, p => { calls++; return true; }, out _));
			Assert.IsFalse(Rules.TryPublishCapacity(ready, 0, Witness(ready), () => { calls++; return true; }, null, out _));
			Assert.IsFalse(Rules.TryPublishCapacity(Fresh(), 0, Witness(ready), () => { calls++; return true; }, p => true, out _));
			Assert.AreEqual(0, calls);
		}

		[Test]
		public void ChangedCanonicalRegistryCannotReuseAnEarlierCapacityWitness()
		{
			Plan plan = Ready(); var first = Witness(plan); string before = Wire(plan); int saves = 0;
			Assert.IsTrue(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(
				ChronicleCapacityFixture.Registry(4096, true)), first.EventId, first.Fingerprint, out var changed));
			Assert.AreNotEqual(first.RegistryHash, changed.RegistryHash);
			Assert.IsFalse(Rules.TryPublishCapacity(plan, 0, first, () => first.RegistryHash == changed.RegistryHash,
				p => { saves++; return true; }, out _));
			Assert.AreEqual(0, saves); Assert.AreEqual(before, Wire(plan));
			Assert.IsTrue(Rules.TryRefuseCapacity(plan, 0, first, out var refused));
			Assert.IsFalse(Rules.TryRefuseCapacity(refused, 0, changed, out _));
		}

		[Test]
		public void RefusedReportSurvivesArchiveBookRoundtripAndVisibleAcknowledgement()
		{
			Plan report = Refused(); string reportWire = Wire(report); var book = Book();
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book, reportWire, out var retained));
			Assert.AreEqual(KingdomSubsidenceReportArchive.None, book.FailureModel);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(retained, out string wire));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out var restored));
			Assert.AreEqual(retained.FailureModel, restored.FailureModel);
			string digest = KingdomSubsidenceReportArchive.Digest(restored);
			StringAssert.Contains("Chronicle not published: registry full", digest);
			StringAssert.Contains("4096 replay receipts retained", digest);
			StringAssert.Contains(report.Entries[0].CapacityHash, digest);
			StringAssert.Contains("delivery is not claimed", digest);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(restored, reportWire, out var repeat));
			Assert.AreSame(restored, repeat);
			var acknowledged = restored.WithFailures(KingdomSubsidenceReportArchive.None);
			Assert.IsTrue(KingdomSubsidenceStepRules.Valid(acknowledged));
			Assert.AreEqual(restored.LastRetiredTick, acknowledged.LastRetiredTick);
			Assert.AreEqual(restored.Sequence, acknowledged.Sequence);
		}

		[Test]
		public void FullArchiveKeepsPendingRefusalUntilExistingWarningsAreAcknowledged()
		{
			var book = Book();
			for (int i = 1; i <= KingdomSubsidenceReportArchive.MaxReports; i++)
				Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book, Wire(Refused(i)), out book));
			string held = book.FailureModel, pending = Wire(Refused(9));
			Assert.IsFalse(KingdomSubsidenceReportArchive.TryRetain(book, pending, out var refused));
			Assert.IsNull(refused); Assert.AreEqual(held, book.FailureModel);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRead(held, out var rows)); Assert.AreEqual(8, rows.Count);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book.WithFailures(KingdomSubsidenceReportArchive.None), pending, out _));
		}

		[Test]
		public void RealFiveDepartureCreditsRetireOnceAndCapacitySummaryPreservesTheirAccounting()
		{
			const long anchor = 500; long due = anchor + KingdomSubsidenceStepRules.StepTicks;
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out var book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(book, ChronicleCapacityFixture.Realm, ChronicleCapacityFixture.Settlement, out book));
			Assert.IsTrue(KingdomSubsidenceBatchRules.TryBegin(book, anchor, due, 5, "Fixture", "water", out var batch));
			Assert.IsTrue(KingdomSubsidenceBatchCodec.TryEncode(batch, out string batchWire)); book = book.WithBatch(batchWire);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(book, anchor, due, GrowthStage.City, 5, out book, 0, "water"));
			for (int id = 1; id <= 5; id++)
			{
				string body = "capacity-departure-" + id;
				var departure = new KingdomResidentDepartureOperation
				{
					Version = KingdomResidentDepartureOperation.CurrentVersion, Revision = 1,
					Phase = (int)KingdomResidentDeparturePhase.Prepared, RealmId = book.RealmId, SettlementId = book.SettlementId,
					ResidentId = id, BodyObjectId = body, ResidentName = "Fixture", ZoneId = "JoppaWorld.12.24.1.1.10", PreparedTick = due,
					OperationId = KingdomResidentDepartureRules.Id(book.RealmId, book.SettlementId, id, body, due)
				};
				Assert.IsTrue(KingdomResidentDepartureRules.Valid(departure));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.City, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.City, out var repeat));
				Assert.AreSame(book, repeat); Assert.AreEqual(id, book.Active.Completed);
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			}
			Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, due, out book));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, due, out _));
			Assert.IsTrue(KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out batch));
			Assert.AreEqual(5, batch.Departed); Assert.IsTrue(batch.Closing);
			Plan report = new Plan(batch.Id, book.RealmId, book.SettlementId, new[] { new Entry("Two more departed", "", batch.ClosedTick) });
			Assert.IsTrue(Rules.TryArmLedger(report, 0, new string[0], out report));
			Assert.IsTrue(Rules.TryPublishCapacity(report, 0, Witness(report), () => true, next =>
			{
				Assert.IsTrue(KingdomSubsidenceBatchCodec.TryEncode(batch.Copy(reportModel: Wire(next)), out string saved));
				book = book.WithBatch(saved); return KingdomSubsidenceStepRules.Valid(book);
			}, out report));
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book, Wire(report), out var retained));
			var retired = retained.WithBatch(KingdomSubsidenceBatchRules.None);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(retired, out string wire));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out var restored));
			Assert.IsNull(restored.Active); Assert.AreEqual(1, restored.Sequence); Assert.AreEqual(due, restored.LastRetiredTick);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(restored, anchor, due, GrowthStage.City, 5, out _, 0, "water"));
			Assert.AreEqual(Wire(report), ReadOnlyFailure(restored)); Assert.IsFalse(Rules.Complete(report));
		}
		private static string ReadOnlyFailure(KingdomSubsidenceStepBook book)
		{ Assert.IsTrue(KingdomSubsidenceReportArchive.TryRead(book.FailureModel, out var rows)); Assert.AreEqual(1, rows.Count); return rows[0]; }

		[Test]
		public void CurrentWireRoundtripsAndEveryTruncationTrailingByteOrNoncanonicalBase64Refuses()
		{
			Plan plan = Refused(); string wire = Wire(plan); StringAssert.StartsWith("st3:", wire);
			Assert.IsTrue(Codec.TryDecode(wire, out var restored)); Assert.AreEqual(wire, Wire(restored));
			byte[] bytes = Convert.FromBase64String(wire.Substring(4));
			for (int i = 0; i < bytes.Length; i++) Assert.IsFalse(Codec.TryDecode("st3:" + Convert.ToBase64String(bytes, 0, i), out _), "cut " + i);
			Array.Resize(ref bytes, bytes.Length + 1);
			Assert.IsFalse(Codec.TryDecode("st3:" + Convert.ToBase64String(bytes), out _));
			Assert.IsFalse(Codec.TryDecode(wire + "\n", out _));
			Assert.IsFalse(Codec.TryDecode("st2:" + wire.Substring(4), out _));
			Assert.IsFalse(Codec.TryDecode("st4:" + wire.Substring(4), out _));
		}

		[TestCase("unknown-reason")] [TestCase("missing-reason")] [TestCase("zero-count")] [TestCase("negative-count")]
		[TestCase("too-many")] [TestCase("proved-flag")] [TestCase("lost-flag")] [TestCase("empty-hash")]
		public void ForgedCurrentWireCannotLaunderCapacityWitnessOrBooleans(string field)
		{
			string original = Wire(Refused()); byte[] bytes = Convert.FromBase64String(original.Substring(4));
			using (var stream = new MemoryStream(bytes, true))
			using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true), true))
			{
				reader.ReadInt32(); reader.ReadString(); reader.ReadString(); reader.ReadString(); reader.ReadInt32();
				reader.ReadString(); reader.ReadString(); reader.ReadInt64(); reader.ReadByte();
				int proved = (int)stream.Position; reader.ReadByte();
				reader.ReadInt32(); reader.ReadString(); reader.ReadInt32(); reader.ReadString(); reader.ReadByte();
				int lost = (int)stream.Position; reader.ReadByte(); int refusal = (int)stream.Position; reader.ReadByte();
				int count = (int)stream.Position; reader.ReadInt32(); int hash = (int)stream.Position;
				if (field == "proved-flag") bytes[proved] = 2;
				else if (field == "lost-flag") bytes[lost] = 2;
				else if (field == "unknown-reason" || field == "missing-reason") bytes[refusal] = (byte)(field == "unknown-reason" ? 2 : 0);
				else if (field == "empty-hash") bytes[hash] = 0;
				else
				{
					stream.Position = count;
					using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
						writer.Write(field == "zero-count" ? 0 : field == "negative-count" ? -1 : 4097);
				}
			}
			Assert.IsFalse(Codec.TryDecode("st3:" + Convert.ToBase64String(bytes), out var bad)); Assert.IsNull(bad);
			Assert.IsTrue(Codec.TryDecode(original, out var retained)); Assert.AreEqual(original, Wire(retained));
		}

		[TestCase(1)] [TestCase(2)]
		public void HistoricalLayoutsKeepTheirExactBytesAndUpgradeOnlyOnCurrentEncode(int version)
		{
			Plan plan = Ready();
			if (version == 2) Assert.IsTrue(Rules.TryLoseChronicle(plan, 0, out plan));
			string old = Historical(plan, version);
			Assert.IsTrue(Codec.TryDecode(old, out var read)); Assert.IsFalse(read.Entries[0].CapacityRefused);
			Assert.AreEqual(version == 2, read.Entries[0].ChronicleLost);
			var method = typeof(Codec).GetMethod("EncodeV" + version, BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(method); object[] args = { read, null };
			Assert.IsTrue((bool)method.Invoke(null, args)); Assert.AreEqual(old, args[1]);
			StringAssert.StartsWith("st3:", Wire(read));
			args = new object[] { Refused(), null }; Assert.IsFalse((bool)method.Invoke(null, args)); Assert.IsNull(args[1]);
		}

		private static string Historical(Plan plan, int version)
		{
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
			{
				writer.Write(version == 1 ? 0x31545253 : 0x32545253);
				writer.Write(plan.OwnerId); writer.Write(plan.RealmId); writer.Write(plan.SettlementId); writer.Write(plan.Entries.Count);
				foreach (Entry e in plan.Entries)
				{
					writer.Write(e.Text); writer.Write(e.LedgerText); writer.Write(e.AtTick); writer.Write((byte)e.LedgerPhase);
					writer.Write((byte)(e.ChronicleProved ? 1 : 0)); writer.Write(e.BeforeCount); writer.Write(e.BeforeHash);
					writer.Write(e.AfterCount); writer.Write(e.AfterHash);
					if (version == 2) { writer.Write((byte)e.LedgerLoss); writer.Write((byte)(e.ChronicleLost ? 1 : 0)); }
				}
				writer.Flush(); return "st" + version + ":" + Convert.ToBase64String(stream.ToArray());
			}
		}
	}
}
#endif
