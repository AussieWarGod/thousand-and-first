#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomSubsidenceRungCodecTests
	{
		private static KingdomSubsidenceRungPlan FullPlan()
		{
			KingdomSubsidenceRungWork second = RungFixture.Work(1, before: 0, hadPart: false);
			second = new KingdomSubsidenceRungWork(second.WorkId, second.ObjectId, second.Blueprint,
				"", null, "Unstaffed \ud83c\udfe0:|", second.X, second.Y, false, 0, second.AfterWear,
				second.WearPhase, second.Roofs);
			return RungFixture.Plan(RungFixture.Work(0, roofs: new[] {
				RungFixture.Roof(1), RungFixture.Roof(2, true) }), second);
		}

		[Test]
		public void CanonicalRoundTripPreservesEveryFrozenFieldAndNullableDesign()
		{
			KingdomSubsidenceRungPlan original = FullPlan(), restored = RungFixture.RoundTrip(original);
			StringAssert.StartsWith("sr2:", RungFixture.Wire(original));
			Assert.AreNotSame(original, restored);
			Assert.AreEqual(original.StepId, restored.StepId);
			Assert.AreEqual(original.RealmId, restored.RealmId);
			Assert.AreEqual(original.SettlementId, restored.SettlementId);
			Assert.AreEqual(original.ZoneId, restored.ZoneId);
			Assert.AreEqual(original.From, restored.From); Assert.AreEqual(original.To, restored.To);
			Assert.AreEqual(original.DueTick, restored.DueTick); Assert.AreEqual(original.PreparedTick, restored.PreparedTick);
			Assert.AreEqual(original.Departed, restored.Departed);
			Assert.AreEqual(original.Works.Count, restored.Works.Count);
			for (int i = 0; i < original.Works.Count; i++)
			{
				KingdomSubsidenceRungWork a = original.Works[i], b = restored.Works[i];
				Assert.AreEqual(a.WorkId, b.WorkId); Assert.AreEqual(a.ObjectId, b.ObjectId);
				Assert.AreEqual(a.Blueprint, b.Blueprint); Assert.AreEqual(a.PlotId, b.PlotId);
				Assert.AreEqual(a.DesignStamp, b.DesignStamp); Assert.AreEqual(a.Name, b.Name);
				Assert.AreEqual(a.X, b.X); Assert.AreEqual(a.Y, b.Y);
				Assert.AreEqual(a.HadWearPart, b.HadWearPart);
				Assert.AreEqual(a.BeforeWear, b.BeforeWear); Assert.AreEqual(a.AfterWear, b.AfterWear);
				Assert.AreEqual(a.WearPhase, b.WearPhase);
				Assert.AreEqual(a.ReleasePhase, b.ReleasePhase);
				Assert.IsTrue(KingdomSubsidenceReleaseRules.Same(a.ReleaseBefore, b.ReleaseBefore));
				Assert.IsTrue(KingdomSubsidenceReleaseRules.Same(a.ReleaseAfter, b.ReleaseAfter));
				Assert.AreEqual(KingdomSubsidenceReleasePhase.Pending, b.ReleasePhase);
				Assert.IsNull(b.ReleaseBefore); Assert.IsNull(b.ReleaseAfter);
				Assert.AreEqual(a.Roofs.Count, b.Roofs.Count);
				for (int j = 0; j < a.Roofs.Count; j++)
				{
					KingdomSubsidenceRungRoof x = a.Roofs[j], y = b.Roofs[j];
					Assert.AreEqual(x.ResidentId, y.ResidentId); Assert.AreEqual(x.BodyObjectId, y.BodyObjectId);
					Assert.AreEqual(x.BeforeStanding, y.BeforeStanding);
					Assert.AreEqual(x.BeforeReached, y.BeforeReached); Assert.AreEqual(x.BeforeWarned, y.BeforeWarned);
					Assert.AreEqual(x.Phase, y.Phase);
				}
			}
			Assert.IsNull(restored.Works[1].DesignStamp);
			Assert.AreEqual("", restored.Works[1].PlotId);
			Assert.IsFalse(restored.Works[1].HadWearPart);
		}

		[Test]
		public void EveryByteTruncationRefusesWithoutPublishingPartialPlan()
		{
			string original = RungFixture.Wire(FullPlan());
			byte[] bytes = Bytes(original);
			for (int length = 0; length < bytes.Length; length++)
			{
				byte[] truncated = new byte[length];
				Array.Copy(bytes, truncated, length);
				Refuses(Wire(truncated), "truncated at " + length);
			}
			Assert.AreEqual(original, Wire(bytes));
			Assert.IsTrue(KingdomSubsidenceRungCodec.TryDecode(original, out _));
		}

		[Test]
		public void TrailingBytesAndWrongMagicCannotBeAcceptedAsTheSamePlan()
		{
			byte[] bytes = Bytes(RungFixture.Wire(FullPlan()));
			byte[] longer = new byte[bytes.Length + 1];
			Array.Copy(bytes, longer, bytes.Length);
			Refuses(Wire(longer));
			bytes[0] ^= 1;
			Refuses(Wire(bytes));
		}

		[TestCase(null)] [TestCase("")] [TestCase("sr1:")] [TestCase("sr1:not base64")]
		[TestCase("sr2:")] [TestCase("sr2:not base64")] [TestCase("sr2:AAAA")] [TestCase("sr3:AAAA")]
		[TestCase("SR1:AAAA")] [TestCase("SR2:AAAA")] [TestCase(" sr1:AAAA")]
		[TestCase("sr1:none")] [TestCase("sr1:pending")]
		public void MissingFutureOrSentinelWireIsNotAPlan(string wire) => Refuses(wire);

		[Test]
		public void WhitespaceAndNonzeroBase64PaddingBitsRefuseDespiteEquivalentDecodedBytes()
		{
			string wire = RungFixture.Wire(FullPlan());
			Refuses(wire.Insert(4, " "));
			Refuses(wire.Insert(12, "\n"));
			Refuses(wire + "\r\n");
			for (int extra = 0; !wire.EndsWith("=", StringComparison.Ordinal) && extra < 3; extra++)
				wire = RungFixture.Wire(RungFixture.Plan(RungFixture.CopyWork(RungFixture.Work(),
					name: "padding" + new string('x', extra), replaceName: true)));
			Assert.IsTrue(wire.EndsWith("=", StringComparison.Ordinal), "fixture must exercise base64 padding");
			const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
			int at = wire.Length - 1;
			while (wire[at] == '=') at--;
			int value = alphabet.IndexOf(wire[at]);
			Assert.AreEqual(0, value & 1, "canonical unused padding bits must be zero");
			string forged = wire.Substring(0, at) + alphabet[value | 1] + wire.Substring(at + 1);
			CollectionAssert.AreEqual(Bytes(wire), Bytes(forged));
			Refuses(forged);
		}

		[TestCase("design", 2)] [TestCase("design", 255)]
		[TestCase("part", 2)] [TestCase("part", 255)]
		[TestCase("standing", 2)] [TestCase("standing", 255)]
		public void NoncanonicalBooleanBytesAreNotCoercedToTrue(string field, int value)
		{
			string wire = RungFixture.Wire(FullPlan());
			byte[] bytes = Bytes(wire);
			bytes[Layout(wire)[field]] = (byte)value;
			Refuses(Wire(bytes));
			Assert.IsTrue(KingdomSubsidenceRungCodec.TryDecode(wire, out _));
		}

		[TestCase("works", -1)] [TestCase("works", 4097)] [TestCase("works", int.MaxValue)]
		[TestCase("roofs", -1)] [TestCase("roofs", int.MaxValue)]
		[TestCase("x", -1)] [TestCase("x", 80)] [TestCase("y", -1)] [TestCase("y", 25)]
		[TestCase("before", -1)] [TestCase("before", 61)] [TestCase("after", -1)] [TestCase("after", 61)]
		[TestCase("resident", 0)] [TestCase("resident", -1)]
		[TestCase("departed", 0)] [TestCase("departed", 6)]
		public void NumericBoundsRefuseWithoutClamping(string field, int value)
		{
			string wire = RungFixture.Wire(FullPlan());
			byte[] bytes = Bytes(wire);
			PutInt(bytes, Layout(wire)[field], value);
			Refuses(Wire(bytes));
		}

		[TestCase("wear-phase")] [TestCase("roof-phase")] [TestCase("release-phase")]
		public void UnknownEffectPhaseRefuses(string field)
		{
			string wire = RungFixture.Wire(FullPlan());
			byte[] bytes = Bytes(wire);
			bytes[Layout(wire)[field]] = 255;
			Refuses(Wire(bytes));
		}

		[Test]
		public void WrongNumericWorkIdentityIsNotRecomputedDuringDecode()
		{
			string wire = RungFixture.Wire(FullPlan());
			byte[] bytes = Bytes(wire);
			PutInt(bytes, Layout(wire)["work-id"], FullPlan().Works[0].WorkId ^ 1);
			Refuses(Wire(bytes));
		}

		[Test]
		public void MalformedUtf8AndOverlongStringLengthEncodingRefuse()
		{
			string wire = RungFixture.Wire(FullPlan());
			byte[] bytes = Bytes(wire);
			// StepId is the first string and has a one-byte UTF-8 byte-length prefix after magic.
			Assert.Less(bytes[4], 128);
			bytes[5] = 0xFF;
			Refuses(Wire(bytes));
			bytes = Bytes(wire);
			byte[] overlong = new byte[bytes.Length + 1];
			Array.Copy(bytes, 0, overlong, 0, 4);
			overlong[4] = (byte)(bytes[4] | 0x80);
			overlong[5] = 0;
			Array.Copy(bytes, 5, overlong, 6, bytes.Length - 5);
			Refuses(Wire(overlong));
		}

		[Test]
		public void PerFieldAndAggregateWireBoundsRefuseInsteadOfTruncatingThePlan()
		{
			KingdomSubsidenceRungWork work = RungFixture.Work();
			KingdomSubsidenceRungPlan longName = RungFixture.Plan(
				RungFixture.CopyWork(work, name: new string('n', 513), replaceName: true));
			Assert.IsFalse(KingdomSubsidenceRungCodec.TryEncode(longName, out string rejected));
			Assert.IsNull(rejected);
			Assert.IsTrue(KingdomSubsidenceRungCodec.TryEncode(RungFixture.Plan(
				RungFixture.CopyWork(work, name: new string('n', 512), replaceName: true)), out _));
			Refuses("sr1:" + new string('A', KingdomSubsidenceRungCodec.MaxWireChars));
			Refuses("sr2:" + new string('A', KingdomSubsidenceRungCodec.MaxWireChars));
			List<KingdomSubsidenceRungWork> large = new List<KingdomSubsidenceRungWork>();
			for (int i = 0; i < 14; i++)
			{
				KingdomSubsidenceRungWork row = RungFixture.Work(i);
				large.Add(new KingdomSubsidenceRungWork(row.WorkId, row.ObjectId, row.Blueprint,
					row.PlotId, new string('d', 32768), row.Name, row.X, row.Y,
					row.HadWearPart, row.BeforeWear, row.AfterWear, row.WearPhase, row.Roofs));
			}
			KingdomSubsidenceRungPlan aggregate = RungFixture.Plan(large.ToArray());
			Assert.IsTrue(KingdomSubsidenceRungRules.Valid(aggregate), "every complete row is individually bounded");
			Assert.IsFalse(KingdomSubsidenceRungCodec.TryEncode(aggregate, out rejected));
			Assert.IsNull(rejected);
		}

		[Test]
		public void TotalRoofBoundIncludesEarlierWorksAndNeverTruncatesRecipients()
		{
			List<KingdomSubsidenceRungRoof> first = new List<KingdomSubsidenceRungRoof>();
			for (int i = 1; i <= KingdomSubsidenceRungRules.MaxRoofs; i++) first.Add(RungFixture.Roof(i));
			KingdomSubsidenceRungWork a = RungFixture.Work(0, roofs: first);
			KingdomSubsidenceRungPlan maximum = RungFixture.Plan(a, RungFixture.Work(1));
			Assert.IsTrue(KingdomSubsidenceRungRules.Valid(maximum));
			Assert.AreEqual(first.Count, RungFixture.RoundTrip(maximum).Works[0].Roofs.Count);
			KingdomSubsidenceRungPlan over = RungFixture.Plan(a, RungFixture.Work(1,
				roofs: new[] { RungFixture.Roof(KingdomSubsidenceRungRules.MaxRoofs + 1) }));
			Assert.IsFalse(KingdomSubsidenceRungRules.Valid(over));
			Assert.IsFalse(KingdomSubsidenceRungCodec.TryEncode(over, out _));
			// Keep the valid header and first work; splice one complete second-work record from a valid wire.
			string oneWire = RungFixture.Wire(RungFixture.Plan(RungFixture.Work(1,
				roofs: new[] { RungFixture.Roof(KingdomSubsidenceRungRules.MaxRoofs + 1) })));
			byte[] maximumBytes = Bytes(RungFixture.Wire(maximum)), oneBytes = Bytes(oneWire);
			int secondAt;
			using (MemoryStream stream = new MemoryStream(maximumBytes, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				stream.Position = Layout(RungFixture.Wire(maximum))["roofs"];
				int count = reader.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					reader.ReadInt32(); reader.ReadString(); reader.ReadByte();
					reader.ReadInt64(); reader.ReadInt64(); reader.ReadByte();
				}
				Assert.AreEqual((byte)KingdomSubsidenceReleasePhase.Pending, reader.ReadByte());
				secondAt = (int)stream.Position;
			}
			int oneAt = Layout(oneWire)["work-id"];
			byte[] forged = new byte[secondAt + oneBytes.Length - oneAt];
			Array.Copy(maximumBytes, forged, secondAt);
			Array.Copy(oneBytes, oneAt, forged, secondAt, oneBytes.Length - oneAt);
			Refuses(Wire(forged));
		}

		private static void Refuses(string wire, string context = null)
		{
			Assert.IsFalse(KingdomSubsidenceRungCodec.TryDecode(wire, out KingdomSubsidenceRungPlan decoded), context);
			Assert.IsNull(decoded, context);
		}
		private static byte[] Bytes(string wire) => Convert.FromBase64String(wire.Substring(4));
		private static string Wire(byte[] bytes) => "sr2:" + Convert.ToBase64String(bytes);
		private static void PutInt(byte[] bytes, int position, int value)
		{
			using (MemoryStream stream = new MemoryStream(bytes, true))
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				stream.Position = position; writer.Write(value);
			}
		}
		// Parse actual production framing to locate corruption cuts; this does not implement validation.
		internal static Dictionary<string, int> Layout(string wire)
		{
			Dictionary<string, int> offsets = new Dictionary<string, int>();
			using (MemoryStream stream = new MemoryStream(Bytes(wire), false))
			using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
			{
				reader.ReadInt32();
				for (int i = 0; i < 4; i++) reader.ReadString();
				reader.ReadByte(); reader.ReadByte(); reader.ReadInt64(); reader.ReadInt64();
				offsets["departed"] = (int)stream.Position; reader.ReadInt32();
				offsets["works"] = (int)stream.Position; reader.ReadInt32();
				offsets["work-id"] = (int)stream.Position; reader.ReadInt32();
				reader.ReadString(); reader.ReadString(); reader.ReadString();
				offsets["design"] = (int)stream.Position;
				if (reader.ReadByte() == 1) reader.ReadString();
				reader.ReadString();
				offsets["x"] = (int)stream.Position; reader.ReadInt32();
				offsets["y"] = (int)stream.Position; reader.ReadInt32();
				offsets["part"] = (int)stream.Position; reader.ReadByte();
				offsets["before"] = (int)stream.Position; reader.ReadInt32();
				offsets["after"] = (int)stream.Position; reader.ReadInt32();
				offsets["wear-phase"] = (int)stream.Position; reader.ReadByte();
				offsets["roofs"] = (int)stream.Position; int count = reader.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					if (i == 0) offsets["resident"] = (int)stream.Position;
					reader.ReadInt32(); reader.ReadString();
					if (i == 0) offsets["standing"] = (int)stream.Position;
					reader.ReadByte(); reader.ReadInt64(); reader.ReadInt64();
					if (i == 0) offsets["roof-phase"] = (int)stream.Position;
					reader.ReadByte();
				}
				if (wire.StartsWith("sr2:", StringComparison.Ordinal)) offsets["release-phase"] = (int)stream.Position;
			}
			return offsets;
		}
	}
}
#endif
