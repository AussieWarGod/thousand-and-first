#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Executable pure-wire fixtures; supplied exactAuthority does not prove native carrier ownership.
	public sealed class KingdomSubsidenceReleaseCodecTests
	{
		[TestCase(false, null, null)] [TestCase(true, null, null)]
		[TestCase(false, "", "")] [TestCase(true, "", "")]
		[TestCase(false, "prior:|", "Frozen \ud83c\udfe0:|")] [TestCase(true, "prior:|", "Frozen \ud83c\udfe0:|")]
		public void Sr2RoundTripRetainsEveryIntentOrReleasedTupleField(bool released, string last, string line)
		{
			KingdomSubsidenceRungPlan physical = Fixture(true);
			string untouched = RungFixture.Wire(physical);
			KingdomSubsidenceRungPlan plan = Arm(physical, last, line);
			if (released) ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryProveRelease(plan, 0, true,
				plan.Works[0].ReleaseAfter, out plan));
			KingdomSubsidenceRungPlan decoded = RungFixture.RoundTrip(plan);
			StringAssert.StartsWith("sr2:", RungFixture.Wire(decoded));
			ClassicAssert.AreEqual(released ? KingdomSubsidenceReleasePhase.Released
				: KingdomSubsidenceReleasePhase.Intent, decoded.Works[0].ReleasePhase);
			EqualReceipt(plan.Works[0].ReleaseBefore, decoded.Works[0].ReleaseBefore);
			EqualReceipt(plan.Works[0].ReleaseAfter, decoded.Works[0].ReleaseAfter);
			ClassicAssert.AreEqual(last, decoded.Works[0].ReleaseBefore.LastCompletedId);
			ClassicAssert.AreEqual(line, decoded.Works[0].ReleaseBefore.Line);
			ClassicAssert.IsNull(decoded.Works[0].ReleaseAfter.Id); ClassicAssert.IsNull(decoded.Works[0].ReleaseAfter.Line);
			ClassicAssert.AreEqual(untouched, RungFixture.Wire(physical));
			ClassicAssert.AreEqual(KingdomSubsidenceReleasePhase.Pending, decoded.Works[1].ReleasePhase);
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.ReleasedComplete(decoded));
		}

		[TestCase(false)] [TestCase(true)]
		public void WorkWithAndPlanReplaceKeepExactReleaseProofAndPhysicalFields(bool released)
		{
			KingdomSubsidenceRungPlan plan = Arm(Fixture(true), "prior", "line");
			if (released) ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryProveRelease(plan, 0, true,
				plan.Works[0].ReleaseAfter, out plan));
			KingdomSubsidenceRungWork row = plan.Works[0], copy = row.With(row.WearPhase, row.Roofs);
			ClassicAssert.AreSame(row.ReleaseBefore, copy.ReleaseBefore); ClassicAssert.AreSame(row.ReleaseAfter, copy.ReleaseAfter);
			ClassicAssert.AreSame(row.Roofs[0], copy.Roofs[0]); ClassicAssert.AreEqual(row.ReleasePhase, copy.ReleasePhase);
			ClassicAssert.AreEqual(RungFixture.Wire(plan), RungFixture.Wire(plan.Replace(0, copy)));
			ClassicAssert.AreSame(plan.Works[1], plan.Replace(0, copy).Works[1]);
			ClassicAssert.IsFalse(TryVersion(typeof(KingdomSubsidenceRungCodec), plan, 1, out string refused));
			ClassicAssert.IsNull(refused, "the old writer cannot erase an armed or released receipt");
		}

		[TestCase(false)] [TestCase(true)]
		public void RealCanonicalSr1ReadsPendingWithoutInventingReleaseEvenForMultipleProvedWorks(bool complete)
		{
			KingdomSubsidenceRungPlan original = Fixture(complete);
			ClassicAssert.IsTrue(TryVersion(typeof(KingdomSubsidenceRungCodec), original, 1, out string legacy));
			StringAssert.StartsWith("sr1:", legacy); ClassicAssert.AreEqual(0x31525354, BitConverter.ToInt32(Bytes(legacy), 0));
			ClassicAssert.IsTrue(KingdomSubsidenceRungCodec.TryDecode(legacy, out KingdomSubsidenceRungPlan decoded));
			ClassicAssert.AreEqual(2, decoded.Works.Count); ClassicAssert.AreEqual(complete, KingdomSubsidenceRungRules.PhysicalComplete(decoded));
			foreach (KingdomSubsidenceRungWork row in decoded.Works)
			{
				ClassicAssert.AreEqual(KingdomSubsidenceReleasePhase.Pending, row.ReleasePhase);
				ClassicAssert.IsNull(row.ReleaseBefore); ClassicAssert.IsNull(row.ReleaseAfter);
			}
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.ReleasedComplete(decoded));
			ClassicAssert.IsTrue(TryVersion(typeof(KingdomSubsidenceRungCodec), decoded, 1, out string canonical));
			ClassicAssert.AreEqual(legacy, canonical);
			ClassicAssert.AreEqual(RungFixture.Wire(original), RungFixture.Wire(decoded));
			ClassicAssert.AreEqual(Bytes(legacy).Length + decoded.Works.Count, Bytes(RungFixture.Wire(decoded)).Length);
		}

		[TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)] [TestCase(4, false)]
		[TestCase(3, true)] [TestCase(4, true)]
		public void EveryOuterVersionAndCopyPreservesRawSr1AndAnyAlreadyPublishedReport(int version, bool published)
		{
			KingdomSubsidenceStepBook book = LegacyBook(published);
			string rung = book.Active.RungModel, report = book.Active.RungReportModel;
			ClassicAssert.IsTrue(TryVersion(typeof(KingdomSubsidenceStepCodec), book, version, out string wire));
			StringAssert.StartsWith("ss" + version + ":", wire);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook decoded));
			ClassicAssert.IsTrue(TryVersion(typeof(KingdomSubsidenceStepCodec), decoded, version, out string canonical));
			ClassicAssert.AreEqual(wire, canonical);
			decoded = decoded.With(decoded.Active.Copy(), decoded.Sequence).WithOption(decoded.OptionModel)
				.WithBatch(decoded.BatchModel).WithFailures(decoded.FailureModel);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(decoded, out string current));
			StringAssert.StartsWith("ss5:", current);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(current, out decoded));
			ClassicAssert.AreEqual(rung, decoded.Active.RungModel); ClassicAssert.AreEqual(report, decoded.Active.RungReportModel);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(decoded, out KingdomSubsidenceRungPlan plan));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.PhysicalComplete(plan));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.ReleasedComplete(plan));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(decoded, decoded.Active.AnchorTick, out _));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryRetire(decoded, decoded.Active.DueTick, out _));
			if (published)
			{
				ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(report, out KingdomSubsidenceReportPlan telling));
				ClassicAssert.IsTrue(KingdomSubsidenceReportRules.Complete(telling));
			}
		}

		[TestCase(1)] [TestCase(2)]
		public void OuterVersionsWithoutReportSlotRefuseDroppingPublishedEvidence(int version)
		{
			ClassicAssert.IsFalse(TryVersion(typeof(KingdomSubsidenceStepCodec), LegacyBook(true), version, out string refused));
			ClassicAssert.IsNull(refused);
		}

		[TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)] [TestCase(4, false)]
		[TestCase(1, true)] [TestCase(2, true)] [TestCase(3, true)] [TestCase(4, true)]
		public void EveryOuterLayoutRetainsSr2ProofAcrossOperationAndBookCopies(int version, bool released)
		{
			KingdomSubsidenceStepBook book = LegacyBook(false);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan plan));
			plan = Arm(plan, "", "line");
			if (released) ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryProveRelease(plan, 0, true,
				plan.Works[0].ReleaseAfter, out plan));
			string rung = RungFixture.Wire(plan);
			book = book.With(book.Active.Copy(rungModel: rung), book.Sequence);
			ClassicAssert.IsTrue(TryVersion(typeof(KingdomSubsidenceStepCodec), book, version, out string wire));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out book));
			book = book.With(book.Active.Copy(), book.Sequence).WithOption(book.OptionModel)
				.WithBatch(book.BatchModel).WithFailures(book.FailureModel);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out wire));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out book));
			ClassicAssert.AreEqual(rung, book.Active.RungModel);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan decoded));
			EqualReceipt(plan.Works[0].ReleaseBefore, decoded.Works[0].ReleaseBefore);
			EqualReceipt(plan.Works[0].ReleaseAfter, decoded.Works[0].ReleaseAfter);
		}

		[TestCase("design", 2)] [TestCase("design", 255)] [TestCase("part", 2)] [TestCase("part", 255)]
		[TestCase("standing", 2)] [TestCase("standing", 255)] [TestCase("wear-phase", 255)] [TestCase("roof-phase", 255)]
		public void Sr1CanonicalValidationRejectsOldFlagAndPhaseCorruptionBeforeMigration(string field, int value)
		{
			ClassicAssert.IsTrue(TryVersion(typeof(KingdomSubsidenceRungCodec), Fixture(true), 1, out string wire));
			byte[] bytes = Bytes(wire); bytes[KingdomSubsidenceRungCodecTests.Layout(wire)[field]] = (byte)value;
			Refuses("sr1:" + Convert.ToBase64String(bytes));
			ClassicAssert.IsTrue(KingdomSubsidenceRungCodec.TryDecode(wire, out _));
		}

		[Test]
		public void Sr1TrailingWhitespaceOverlongStringAndWrongVersionCannotBeNormalizedIntoSr2()
		{
			ClassicAssert.IsTrue(TryVersion(typeof(KingdomSubsidenceRungCodec), Fixture(true), 1, out string wire));
			Refuses(wire.Insert(4, " ")); Refuses(wire + "\n"); Refuses("sr2:" + wire.Substring(4));
			byte[] bytes = Bytes(wire), longer = new byte[bytes.Length + 1];
			Array.Copy(bytes, longer, bytes.Length); Refuses("sr1:" + Convert.ToBase64String(longer));
			ClassicAssert.Less(bytes[4], 128); Array.Copy(bytes, 0, longer, 0, 4);
			longer[4] = (byte)(bytes[4] | 0x80); longer[5] = 0;
			Array.Copy(bytes, 5, longer, 6, bytes.Length - 5); Refuses("sr1:" + Convert.ToBase64String(longer));
			bytes[5] = 255; Refuses("sr1:" + Convert.ToBase64String(bytes));
			Refuses("sr1:" + RungFixture.Wire(Fixture(true)).Substring(4));
		}

		[Test]
		public void Sr1EquivalentNonzeroPaddingBitsStillFailOriginalVersionCanonicalCheck()
		{
			string wire = null;
			for (int count = 1; count <= 3; count++)
			{
				ClassicAssert.IsTrue(TryVersion(typeof(KingdomSubsidenceRungCodec), RungFixture.Plan(
					RungFixture.CopyWork(RungFixture.Work(), name: new string('x', count), replaceName: true)), 1, out wire));
				if (wire.EndsWith("=", StringComparison.Ordinal)) break;
			}
			ClassicAssert.IsTrue(wire.EndsWith("=", StringComparison.Ordinal));
			const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
			int at = wire.Length - 1; while (wire[at] == '=') at--;
			string forged = wire.Substring(0, at) + alphabet[alphabet.IndexOf(wire[at]) | 1] + wire.Substring(at + 1);
			CollectionAssert.AreEqual(Bytes(wire), Bytes(forged)); Refuses(forged);
		}

		[TestCase("before-id")] [TestCase("before-last")] [TestCase("before-line")]
		[TestCase("after-id")] [TestCase("after-last")] [TestCase("after-line")]
		public void Sr2EachNullableReceiptFlagIsStrictZeroOrOne(string field)
		{
			string wire = RungFixture.Wire(Arm(Fixture(true), "", "line"));
			foreach (byte value in new byte[] { 2, 255 })
			{
				byte[] bytes = Bytes(wire); bytes[ReceiptFlags(wire)[field]] = value;
				Refuses("sr2:" + Convert.ToBase64String(bytes));
			}
		}

		[TestCase("before-id", -4)] [TestCase("after-id", -4)]
		[TestCase("before-last", -4)] [TestCase("after-last", -4)] [TestCase("after-line", 1)]
		public void Sr2ChangedReceiptPhaseOrFrozenFieldCannotBecomeAValidReleaseProof(string flag, int delta)
		{
			string wire = RungFixture.Wire(Arm(Fixture(true), "prior", "line"));
			byte[] bytes = Bytes(wire); bytes[ReceiptFlags(wire)[flag] + delta] ^= 1;
			Refuses("sr2:" + Convert.ToBase64String(bytes));
		}

		[TestCase(false)] [TestCase(true)]
		public void EveryReceiptTruncationAndTrailingByteRefusesWithoutPublishingPartialPlan(bool released)
		{
			KingdomSubsidenceRungPlan plan = Arm(Fixture(true), "prior", "line");
			if (released) ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryProveRelease(plan, 0, true,
				plan.Works[0].ReleaseAfter, out plan));
			string wire = RungFixture.Wire(plan); byte[] bytes = Bytes(wire);
			for (int count = 0; count < bytes.Length; count++)
			{
				byte[] cut = new byte[count]; Array.Copy(bytes, cut, count);
				Refuses("sr2:" + Convert.ToBase64String(cut));
			}
			byte[] longer = new byte[bytes.Length + 1]; Array.Copy(bytes, longer, bytes.Length);
			Refuses("sr2:" + Convert.ToBase64String(longer));
			ClassicAssert.AreEqual(wire, RungFixture.Wire(RungFixture.RoundTrip(plan)));
		}

		private static KingdomSubsidenceRungPlan Fixture(bool complete)
		{
			// Historical physical state only: no release receipts or blanket acknowledgement stamps.
			KingdomSubsidenceEffectPhase phase = complete ? KingdomSubsidenceEffectPhase.Proved : KingdomSubsidenceEffectPhase.Prepared;
			return RungFixture.Plan(RungFixture.Work(0, roofs: new[] { RungFixture.Roof(1).With(phase) }, phase: phase),
				RungFixture.Work(1, roofs: new[] { RungFixture.Roof(2, true).With(phase) }, phase: phase));
		}
		private static KingdomSubsidenceRungPlan Arm(KingdomSubsidenceRungPlan plan, string last, string line)
		{
			KingdomSubsidenceRungWork row = plan.Works[0];
			var observed = new KingdomSubsidenceWearReceipt((int)KingdomWearIncidentPhase.Mutated, plan.StepId,
				(int)KingdomWearRules.WearCause.Subsidence, row.BeforeWear, row.AfterWear, row.AfterWear,
				(int)KingdomWearRules.WearCause.Raid, last, line, (int)KingdomWearSinkDisposition.Lost);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryArmRelease(plan, 0, true, observed, out KingdomSubsidenceRungPlan next));
			ClassicAssert.AreSame(observed, next.Works[0].ReleaseBefore);
			return next;
		}
		private static KingdomSubsidenceStepBook LegacyBook(bool published)
		{
			KingdomSubsidenceStepBook book = RungFixture.Settling(false);
			KingdomSubsidenceRungPlan p = Fixture(true);
			p = new KingdomSubsidenceRungPlan(book.Active.Id, p.RealmId, p.SettlementId, p.ZoneId,
				p.From, p.To, p.DueTick, p.PreparedTick, p.Departed, p.Works);
			ClassicAssert.IsTrue(TryVersion(typeof(KingdomSubsidenceRungCodec), p, 1, out string legacy));
			book = book.With(book.Active.Copy(rungModel: legacy), book.Sequence);
			if (!published) return book;
			var report = new KingdomSubsidenceReportPlan(book.Active.Id, book.RealmId, book.SettlementId,
				new[] { new KingdomSubsidenceReportEntry("The city became a town.", "The city fell.", p.DueTick) });
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(report, 0, new string[0], out report));
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryProveLedger(report, 0, new[] { "The city fell." }, out report));
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryProveChronicle(report, 0, out report));
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string wire));
			return book.With(book.Active.Copy(rungReportModel: wire), book.Sequence);
		}
		private static bool TryVersion(Type codec, object model, int version, out string wire)
		{
			// Invoke the actual retained old-layout writer, never relabel sr2 bytes as an sr1 fixture.
			MethodInfo writer = codec.GetMethod("TryEncodeVersion", BindingFlags.Static | BindingFlags.NonPublic,
				null, new[] { model.GetType(), typeof(int), typeof(string).MakeByRefType() }, null);
			ClassicAssert.IsNotNull(writer); object[] arguments = { model, version, null };
			bool result = (bool)writer.Invoke(null, arguments); wire = (string)arguments[2]; return result;
		}
		private static void EqualReceipt(KingdomSubsidenceWearReceipt a, KingdomSubsidenceWearReceipt b)
		{
			ClassicAssert.IsNotNull(a); ClassicAssert.IsNotNull(b); ClassicAssert.AreNotSame(a, b);
			ClassicAssert.AreEqual(a.Phase, b.Phase); ClassicAssert.AreEqual(a.Id, b.Id); ClassicAssert.AreEqual(a.Cause, b.Cause);
			ClassicAssert.AreEqual(a.BeforeWear, b.BeforeWear); ClassicAssert.AreEqual(a.AfterWear, b.AfterWear);
			ClassicAssert.AreEqual(a.Wear, b.Wear); ClassicAssert.AreEqual(a.LastCause, b.LastCause);
			ClassicAssert.AreEqual(a.LastCompletedId, b.LastCompletedId); ClassicAssert.AreEqual(a.Line, b.Line);
			ClassicAssert.AreEqual(a.MessageState, b.MessageState);
		}
		private static Dictionary<string, int> ReceiptFlags(string wire)
		{
			var offsets = new Dictionary<string, int>();
			using (var stream = new MemoryStream(Bytes(wire), false))
			using (var reader = new BinaryReader(stream))
			{
				stream.Position = KingdomSubsidenceRungCodecTests.Layout(wire)["release-phase"];
				ClassicAssert.AreEqual((byte)KingdomSubsidenceReleasePhase.Intent, reader.ReadByte());
				foreach (string prefix in new[] { "before-", "after-" })
				{
					reader.ReadInt32(); NullableFlag(reader, offsets, prefix + "id");
					for (int i = 0; i < 5; i++) reader.ReadInt32();
					NullableFlag(reader, offsets, prefix + "last"); NullableFlag(reader, offsets, prefix + "line");
					reader.ReadInt32();
				}
			}
			return offsets;
		}
		private static void NullableFlag(BinaryReader reader, Dictionary<string, int> offsets, string field)
		{
			offsets[field] = (int)reader.BaseStream.Position;
			if (reader.ReadByte() == 1) reader.ReadString();
		}
		private static byte[] Bytes(string wire) => Convert.FromBase64String(wire.Substring(4));
		private static void Refuses(string wire)
		{
			ClassicAssert.IsFalse(KingdomSubsidenceRungCodec.TryDecode(wire, out KingdomSubsidenceRungPlan value));
			ClassicAssert.IsNull(value);
		}
	}
}
#endif
