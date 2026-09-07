#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Departure credit calls supply the pure model's exact-removal premise; these are not native body proofs.
	public sealed class KingdomSubsidenceBatchRulesTests
	{
		private const long Anchor = 500, Step = KingdomSubsidenceStepRules.StepTicks;
		private const long Due = Anchor + Step, Through = Anchor + 2 * Step;
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);

		[TestCase("water", "The fixture")] [TestCase("roof", "Roof \ud83c\udfe0")]
		public void BeginFreezesCanonicalOwnerNameBindingAndClockIdentity(string binding, string name)
		{
			KingdomSubsidenceStepBook book = Admitted();
			string before = Wire(book);
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryBegin(book, Anchor, Through, 10, name, binding,
				out KingdomSubsidenceBatch batch));
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryBegin(book, Anchor, Through, 10, name, binding,
				out KingdomSubsidenceBatch repeated));
			ClassicAssert.AreEqual(BatchWire(batch), BatchWire(repeated));
			ClassicAssert.IsTrue(batch.Id.StartsWith(KingdomSubsidenceBatchRules.Prefix, StringComparison.Ordinal));
			ClassicAssert.AreEqual(Realm, batch.RealmId); ClassicAssert.AreEqual(Settlement, batch.SettlementId);
			ClassicAssert.AreEqual(name, batch.Name); ClassicAssert.AreEqual(binding, batch.Binding);
			ClassicAssert.AreEqual(1, batch.FirstSequence); ClassicAssert.AreEqual(Anchor, batch.AnchorTick);
			ClassicAssert.AreEqual(Through, batch.ThroughTick); ClassicAssert.AreEqual(10, batch.Wanted);
			ClassicAssert.AreEqual(0, batch.Departed); ClassicAssert.IsFalse(batch.Closing); ClassicAssert.AreEqual(0, batch.ClosedTick);
			ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.PendingReport, batch.ReportModel);
			ClassicAssert.AreEqual(before, Wire(book));
			ClassicAssert.AreEqual(name, Batch(RoundTrip(book.WithBatch(BatchWire(batch)))).Name);
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryBegin(book, Anchor, Through, 10, name + "!", binding,
				out KingdomSubsidenceBatch renamed));
			ClassicAssert.AreNotEqual(batch.Id, renamed.Id);
		}

		[Test]
		public void InclusiveNameCountSpanAndLastRepresentableDueBoundsRemainUsable()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryBegin(Admitted(), 0,
				KingdomSubsidenceRules.MaxSteps * Step, KingdomRules.MaxPopulation, new string('n', 512), "water",
				out KingdomSubsidenceBatch largest));
			ClassicAssert.IsTrue(KingdomSubsidenceBatchCodec.TryDecode(BatchWire(largest), out _));
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryBegin(Admitted(), long.MaxValue - Step,
				long.MaxValue, 1, "Last due tick", "roof", out KingdomSubsidenceBatch latest));
			ClassicAssert.AreEqual(long.MaxValue, latest.ThroughTick);
			ClassicAssert.IsTrue(KingdomSubsidenceBatchCodec.TryDecode(BatchWire(latest), out _));
		}

		[Test]
		public void NewBatchMayStartAfterAnOlderRetiredReceiptWithoutRewritingThatReceipt()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), Anchor, Due, GrowthStage.City,
				5, out KingdomSubsidenceStepBook book, 0, "water"));
			for (int id = 1; id <= 5; id++) Credit(ref book, id);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, Due, out book));
			string before = Wire(book); long anchor = Due + 200;
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryBegin(book, anchor, anchor + Step, 5,
				"Later batch", "water", out KingdomSubsidenceBatch batch));
			ClassicAssert.AreEqual(2, batch.FirstSequence);
			KingdomSubsidenceStepBook attached = RoundTrip(book.WithBatch(BatchWire(batch)));
			ClassicAssert.AreEqual(Due, attached.LastRetiredTick); ClassicAssert.AreEqual(before, Wire(book));
			ClassicAssert.IsFalse(KingdomSubsidenceBatchRules.TryBegin(book, Due - 1, Due - 1 + Step, 5,
				"Before receipt", "water", out KingdomSubsidenceBatch refused));
			ClassicAssert.IsNull(refused);
		}

		[TestCase("null-name")] [TestCase("empty-name")] [TestCase("long-name")]
		[TestCase("control-name")] [TestCase("surrogate-name")] [TestCase("binding")]
		[TestCase("negative-anchor")] [TestCase("zero-span")] [TestCase("ragged-span")]
		[TestCase("long-span")] [TestCase("negative-wanted")] [TestCase("zero-wanted")] [TestCase("excess-wanted")]
		public void BeginRefusesInvalidNamesBindingsCountsAndClockBoundsWithoutChangingBook(string corruption)
		{
			string name = "Fixture", binding = "water";
			long anchor = Anchor, through = Through; int wanted = 10;
			switch (corruption)
			{
				case "null-name": name = null; break;
				case "empty-name": name = ""; break;
				case "long-name": name = new string('x', 513); break;
				case "control-name": name = "bad\nname"; break;
				case "surrogate-name": name = new string((char)0xD800, 1); break;
				case "binding": binding = "Water"; break;
				case "negative-anchor": anchor = -1; break;
				case "zero-span": through = anchor; break;
				case "ragged-span": through++; break;
				case "long-span": through = anchor + (KingdomSubsidenceRules.MaxSteps + 1L) * Step; break;
				case "negative-wanted": wanted = -1; break;
				case "zero-wanted": wanted = 0; break;
				default: wanted = KingdomRules.MaxPopulation + 1; break;
			}
			KingdomSubsidenceStepBook book = Admitted(); string before = Wire(book);
			ClassicAssert.IsFalse(KingdomSubsidenceBatchRules.TryBegin(book, anchor, through, wanted, name, binding,
				out KingdomSubsidenceBatch refused));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(before, Wire(book));
		}

		[Test]
		public void OnePlusFourCreditsStayStepLocalUntilOneAtomicRetirementAddsFive()
		{
			KingdomSubsidenceStepBook book = BeginStep(Attached());
			string batchBefore = book.BatchModel;
			Credit(ref book, 1);
			ClassicAssert.AreEqual(1, book.Active.Completed); ClassicAssert.AreEqual(0, Batch(book).Departed);
			ClassicAssert.AreEqual(batchBefore, RoundTrip(book).BatchModel);
			for (int id = 2; id <= 5; id++)
			{
				Credit(ref book, id);
				ClassicAssert.AreEqual(batchBefore, book.BatchModel);
			}
			KingdomSubsidenceStepBook completed = book; string before = Wire(completed);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(completed, Due, out book));
			ClassicAssert.AreEqual(5, Batch(book).Departed); ClassicAssert.IsNull(book.Active);
			ClassicAssert.AreEqual(1, book.Sequence); ClassicAssert.AreEqual(Due, book.LastRetiredTick);
			ClassicAssert.AreEqual(before, Wire(completed)); ClassicAssert.AreEqual(0, Batch(completed).Departed);
			book = RoundTrip(book);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, Due, out KingdomSubsidenceStepBook refused));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(5, Batch(book).Departed);
		}

		[Test]
		public void TwoStepsShareFirstTwoAndFinalNamedSampleForTheWholeSlide()
		{
			KingdomSubsidenceStepBook book = Attached();
			List<int> named = new List<int>();
			for (int step = 0; step < 2; step++)
			{
				book = BeginStep(book);
				for (int id = step * 5 + 1; id <= step * 5 + 5; id++)
				{
					KingdomSubsidenceBatch batch = Batch(book);
					if (KingdomSubsidenceRules.TellsDeparture(batch.Departed + book.Active.Completed, batch.Wanted)) named.Add(id);
					Credit(ref book, id);
				}
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, Anchor + (step + 1) * Step, out book));
				book = RoundTrip(book);
				ClassicAssert.AreEqual(step == 0 ? 2 : 3, KingdomSubsidenceBatchRules.Named(Batch(book)));
			}
			CollectionAssert.AreEqual(new[] { 1, 2, 10 }, named);
			ClassicAssert.AreEqual(10, Batch(book).Departed); ClassicAssert.IsTrue(Batch(book).Closing);
			ClassicAssert.AreEqual(Through, Batch(book).ClosedTick);
			ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.PendingReport, Batch(book).ReportModel);
		}

		[Test]
		public void PartialCancellationClosesAtFrozenOptionTickWithoutAddingUnretiredCreditsEarly()
		{
			KingdomSubsidenceStepBook book = BeginStep(Attached()); Credit(ref book, 1); Credit(ref book, 2);
			long cancelledAt = Due + 200;
			ClassicAssert.IsTrue(KingdomSubsidenceOptionRules.Observe(new KingdomDurableKeyObservation
				{ HasString = true, String = "v1|E|100|2" }, false, 2, cancelledAt,
				out KingdomSubsidenceOptionRules.Snapshot snapshot).Valid);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(book, Anchor, snapshot,
				out KingdomSubsidenceOptionIntent intent));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeOption(book, intent, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, cancelledAt, 2, out book));
			book = RoundTrip(book); string before = Wire(book), option = book.OptionModel;
			ClassicAssert.AreEqual(0, Batch(book).Departed);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(book, cancelledAt + 1, out KingdomSubsidenceStepBook refused));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(before, Wire(book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, cancelledAt, out book));
			book = RoundTrip(book);
			ClassicAssert.AreEqual(2, Batch(book).Departed); ClassicAssert.AreEqual(2, KingdomSubsidenceBatchRules.Named(Batch(book)));
			ClassicAssert.IsTrue(Batch(book).Closing); ClassicAssert.AreEqual(cancelledAt, Batch(book).ClosedTick);
			ClassicAssert.AreEqual(cancelledAt, book.LastRetiredTick); ClassicAssert.AreEqual(option, book.OptionModel);
		}

		[TestCase(false)] [TestCase(true)]
		public void ClosingWithoutAnActiveStepRequiresTheExactInitialOrRetiredCheckpoint(bool retired)
		{
			KingdomSubsidenceStepBook book = retired ? RetiredOne() : Attached();
			KingdomSubsidenceBatch batch = Batch(book); long checkpoint = retired ? Due : Anchor;
			string before = Wire(book);
			foreach (long wrong in new[] { checkpoint - 1, checkpoint + 1 })
			{
				ClassicAssert.IsFalse(KingdomSubsidenceBatchRules.TryClose(batch, book, wrong, out KingdomSubsidenceBatch refused));
				ClassicAssert.IsNull(refused);
			}
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryClose(batch, book, checkpoint, out KingdomSubsidenceBatch closed));
			ClassicAssert.IsTrue(closed.Closing); ClassicAssert.AreEqual(retired ? 5 : 0, closed.Departed);
			ClassicAssert.AreEqual(checkpoint, closed.ClosedTick); ClassicAssert.AreEqual(before, Wire(book));
			book = RoundTrip(book.WithBatch(BatchWire(closed)));
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryClose(closed, book, checkpoint, out KingdomSubsidenceBatch repeated));
			ClassicAssert.AreSame(closed, repeated);
		}

		[TestCase("offset")] [TestCase("binding")] [TestCase("beyond-through")] [TestCase("closed")]
		public void StepBeginCannotPublishAnImpossibleBatchCursorOrReopenAClosingBatch(string change)
		{
			KingdomSubsidenceStepBook book = Attached(); long anchor = Anchor; string binding = "water";
			if (change == "offset") anchor++;
			if (change == "binding") binding = "roof";
			if (change == "beyond-through") anchor = Through;
			if (change == "closed")
			{
				ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryClose(Batch(book), book, Anchor, out KingdomSubsidenceBatch closed));
				book = book.WithBatch(BatchWire(closed));
			}
			string before = Wire(book);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, anchor, anchor + Step, GrowthStage.City,
				5, out KingdomSubsidenceStepBook refused, 0, binding));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(before, Wire(book));
		}

		[TestCase("exact", true)] [TestCase("empty", false)] [TestCase("wrong-date", false)]
		[TestCase("foreign-owner", false)] [TestCase("two-lines", false)]
		public void ClosingUnnamedDeparturesRequireOneReportBoundToOwnerAndClosedTick(string change, bool expected)
		{
			KingdomSubsidenceStepBook book = RetiredOne();
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryClose(Batch(book), book, Due, out KingdomSubsidenceBatch closed));
			ClassicAssert.AreEqual(5, closed.Departed); ClassicAssert.AreEqual(2, KingdomSubsidenceBatchRules.Named(closed));
			string before = BatchWire(closed);
			string owner = change == "foreign-owner" ? KingdomSubsidenceBatchRules.Prefix + new string('c', 64) : closed.Id;
			KingdomSubsidenceReportEntry entry = new KingdomSubsidenceReportEntry("Three more departed", "",
				change == "wrong-date" ? closed.ClosedTick + 1 : closed.ClosedTick);
			KingdomSubsidenceReportEntry[] entries = change == "empty" ? new KingdomSubsidenceReportEntry[0]
				: change == "two-lines" ? new[] { entry, entry } : new[] { entry };
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(new KingdomSubsidenceReportPlan(owner, Realm,
				Settlement, entries), out string reportWire));
			KingdomSubsidenceBatch changed = closed.Copy(reportModel: reportWire);
			ClassicAssert.AreEqual(expected, KingdomSubsidenceBatchRules.Valid(changed));
			ClassicAssert.AreEqual(expected, KingdomSubsidenceBatchCodec.TryEncode(changed, out string wire));
			if (expected)
			{
				KingdomSubsidenceStepBook restored = RoundTrip(book.WithBatch(wire));
				ClassicAssert.AreEqual(reportWire, Batch(restored).ReportModel);
				ClassicAssert.IsNull(restored.Active); ClassicAssert.AreEqual(Due, restored.LastRetiredTick);
			}
			else ClassicAssert.IsNull(wire);
			ClassicAssert.AreEqual(before, BatchWire(closed));
		}

		[TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
		[TestCase(3, false)] [TestCase(5, false)]
		public void ClosingBatchMayOmitSummaryOnlyWhenEveryActualDepartureWasNamed(int departed, bool expected)
		{
			KingdomSubsidenceStepBook book = BeginStep(Attached());
			for (int id = 1; id <= departed; id++) Credit(ref book, id);
			if (departed < 5) ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Due, 2, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, Due, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryClose(Batch(book), book, Due, out KingdomSubsidenceBatch closed));
			ClassicAssert.AreEqual(departed, closed.Departed);
			ClassicAssert.AreEqual(Math.Min(2, departed), KingdomSubsidenceBatchRules.Named(closed));
			KingdomSubsidenceBatch omitted = closed.Copy(reportModel: KingdomSubsidenceBatchRules.NoReport);
			ClassicAssert.AreEqual(expected, KingdomSubsidenceBatchRules.Valid(omitted));
			ClassicAssert.AreEqual(expected, KingdomSubsidenceBatchCodec.TryEncode(omitted, out string wire));
			if (expected) ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.NoReport, Batch(RoundTrip(book.WithBatch(wire))).ReportModel);
			else ClassicAssert.IsNull(wire);
			ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.PendingReport, closed.ReportModel);
		}

		[TestCase("realm")] [TestCase("settlement")] [TestCase("retired-clock")]
		[TestCase("sequence")] [TestCase("over-credit")] [TestCase("zero-credit")] [TestCase("closed-clock")]
		public void ReadableBatchCannotLaunderForeignOwnershipOrImpossibleRetiredBookShape(string change)
		{
			KingdomSubsidenceStepBook original = RetiredOne(); KingdomSubsidenceBatch batch = Batch(original);
			string realm = Realm, settlement = Settlement; long sequence = 1, retired = Due;
			if (change == "realm") realm = KingdomIdentityRules.RealmPrefix + new string('c', 64);
			if (change == "settlement") settlement = KingdomIdentityRules.SettlementPrefix + new string('d', 64);
			if (change == "retired-clock") retired = Anchor - 1;
			if (change == "sequence") sequence = 2;
			if (change == "over-credit") batch = batch.Copy(departed: 6);
			if (change == "zero-credit") batch = batch.Copy(departed: 0);
			if (change == "closed-clock") batch = batch.Copy(closing: true, closedTick: Due - 1);
			KingdomSubsidenceStepBook forged = new KingdomSubsidenceStepBook(KingdomSubsidenceAdmission.Admitted,
				realm, settlement, sequence, null, retired, batchModel: BatchWire(batch));
			ClassicAssert.IsFalse(KingdomSubsidenceBatchRules.MatchesShape(batch, forged));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.Valid(forged));
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(forged, out string refused)); ClassicAssert.IsNull(refused);
			ClassicAssert.AreEqual(5, Batch(original).Departed);
		}

		[TestCase("negative")] [TestCase("over-wanted")] [TestCase("premature-close-tick")]
		[TestCase("report")] [TestCase("identity")]
		public void InvalidBatchPayloadsRefuseWithoutCanonicalRepair(string change)
		{
			KingdomSubsidenceBatch batch = Batch(Attached());
			if (change == "negative") batch = batch.Copy(departed: -1);
			if (change == "over-wanted") batch = batch.Copy(departed: 11);
			if (change == "premature-close-tick") batch = batch.Copy(closedTick: 1);
			if (change == "report") batch = batch.Copy(reportModel: "broken");
			if (change == "identity") batch = new KingdomSubsidenceBatch(batch.Id + "x", batch.RealmId,
				batch.SettlementId, batch.Name, batch.Binding, batch.FirstSequence, batch.AnchorTick,
				batch.ThroughTick, batch.Wanted, batch.Departed, batch.Closing, batch.ClosedTick, batch.ReportModel);
			ClassicAssert.IsFalse(KingdomSubsidenceBatchRules.Valid(batch));
			ClassicAssert.IsFalse(KingdomSubsidenceBatchCodec.TryEncode(batch, out string wire)); ClassicAssert.IsNull(wire);
		}

		[Test]
		public void BatchCodecRefusesTruncationTrailingBytesBadBooleanAndOversize()
		{
			string wire = BatchWire(Batch(Attached()));
			ClassicAssert.IsTrue(wire.StartsWith("sb1:", StringComparison.Ordinal));
			byte[] bytes = Convert.FromBase64String(wire.Substring(4));
			for (int length = 0; length < bytes.Length; length++) Refuses("sb1:" + Convert.ToBase64String(bytes, 0, length));
			byte[] trailing = new byte[bytes.Length + 1]; Array.Copy(bytes, trailing, bytes.Length);
			Refuses("sb1:" + Convert.ToBase64String(trailing)); Refuses(wire.Insert(10, "\n"));
			Refuses("sb2:" + wire.Substring(4)); Refuses(null); Refuses(""); Refuses("sb1:!");
			Refuses("sb1:" + new string('A', 262144));
			using (MemoryStream stream = new MemoryStream(bytes))
			using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
			{
				reader.ReadInt32(); for (int field = 0; field < 5; field++) reader.ReadString();
				for (int field = 0; field < 3; field++) reader.ReadInt64();
				reader.ReadInt32(); reader.ReadInt32(); bytes[(int)stream.Position] = 2;
			}
			Refuses("sb1:" + Convert.ToBase64String(bytes));
		}

		private static KingdomSubsidenceStepBook Admitted()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(book, Realm, Settlement, out book)); return book;
		}
		private static KingdomSubsidenceStepBook Attached()
		{
			KingdomSubsidenceStepBook book = Admitted();
			ClassicAssert.IsTrue(KingdomSubsidenceBatchRules.TryBegin(book, Anchor, Through, 10, "Fixture", "water",
				out KingdomSubsidenceBatch batch)); return RoundTrip(book.WithBatch(BatchWire(batch)));
		}
		private static KingdomSubsidenceStepBook BeginStep(KingdomSubsidenceStepBook book)
		{
			long anchor = book.Sequence < Batch(book).FirstSequence ? Anchor : book.LastRetiredTick;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(book, anchor, anchor + Step, GrowthStage.City,
				5, out book, 0, "water")); return book;
		}
		private static KingdomSubsidenceStepBook RetiredOne()
		{
			KingdomSubsidenceStepBook book = BeginStep(Attached());
			for (int id = 1; id <= 5; id++) Credit(ref book, id);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, Due, out book)); return RoundTrip(book);
		}
		private static void Credit(ref KingdomSubsidenceStepBook book, int id)
		{
			long tick = book.Active.DueTick; string body = "batch-body-" + id;
			KingdomResidentDepartureOperation op = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion, Revision = 1,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, RealmId = Realm, SettlementId = Settlement,
				ResidentId = id, BodyObjectId = body, ZoneId = "JoppaWorld.12.24.1.1.10",
				ResidentName = "Batch fixture", PreparedTick = tick,
				OperationId = KingdomResidentDepartureRules.Id(Realm, Settlement, id, body, tick)
			};
			ClassicAssert.IsTrue(KingdomResidentDepartureRules.Valid(op));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, op, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, op.OperationId, GrowthStage.City, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, op.OperationId, GrowthStage.City,
				out KingdomSubsidenceStepBook duplicate)); ClassicAssert.AreSame(book, duplicate);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, op.OperationId, out book));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCredit(book, op.OperationId, GrowthStage.City, out duplicate));
			ClassicAssert.IsNull(duplicate); book = RoundTrip(book);
		}
		private static KingdomSubsidenceBatch Batch(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch)); return batch;
		}
		private static string BatchWire(KingdomSubsidenceBatch batch)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceBatchCodec.TryEncode(batch, out string wire)); return wire;
		}
		private static string Wire(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire)); return wire;
		}
		private static KingdomSubsidenceStepBook RoundTrip(KingdomSubsidenceStepBook book)
		{
			string wire = Wire(book); ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out book));
			ClassicAssert.AreEqual(wire, Wire(book)); return book;
		}
		private static void Refuses(string wire)
		{
			ClassicAssert.IsFalse(KingdomSubsidenceBatchCodec.TryDecode(wire, out KingdomSubsidenceBatch batch)); ClassicAssert.IsNull(batch);
		}
	}
}
#endif
