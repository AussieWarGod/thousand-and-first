#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>Executable witness/codec checks; no native save or subsidence-state validity proof.</summary>
	public sealed class KingdomScenarioSaveSnapshotTests
	{
		private const string GameId = "01234567-89ab-cdef-0123-456789abcdef";
		private const string StepWire = "ss4:opaque-native-owner-must-validate";
		private const string ZoneId = "JoppaWorld.8.22.1.1.10";
		private const string MissingObject = "missing-body";

		[Test]
		public void ExactlyFortyNineRetainedPairsAndEveryScalarRoundTripCanonically()
		{
			KingdomScenarioSaveSnapshot expected = Good();
			string wire = Wire(expected);
			ClassicAssert.IsTrue(wire.StartsWith("ssv1:", StringComparison.Ordinal));
			ClassicAssert.IsTrue(KingdomScenarioSaveSnapshotCodec.TryDecode(wire, out KingdomScenarioSaveSnapshot actual));
			ClassicAssert.AreEqual(expected.GameId, actual.GameId); ClassicAssert.AreEqual(expected.StepWire, actual.StepWire);
			ClassicAssert.AreEqual(expected.ZoneId, actual.ZoneId); ClassicAssert.AreEqual(expected.Now, actual.Now);
			ClassicAssert.AreEqual(expected.LedgerDepartures, actual.LedgerDepartures);
			ClassicAssert.AreEqual(expected.MissingResidentId, actual.MissingResidentId);
			ClassicAssert.AreEqual(expected.MissingObjectId, actual.MissingObjectId);
			CollectionAssert.AreEqual(expected.ResidentIds, actual.ResidentIds);
			CollectionAssert.AreEqual(expected.ObjectIds, actual.ObjectIds);
			ClassicAssert.AreEqual(wire, Wire(actual));
		}

		[Test]
		public void ConstructorCopiesArraysAndExposedCollectionsCannotMutateTheWitness()
		{
			int[] ids = Ids(); string[] objects = Objects();
			KingdomScenarioSaveSnapshot value = Good(ids: ids, objects: objects);
			string before = Wire(value); ids[0] = 500; objects[0] = "changed";
			ClassicAssert.AreEqual(1, value.ResidentIds[0]); ClassicAssert.AreEqual("body-1", value.ObjectIds[0]);
			Assert.Throws<NotSupportedException>(() => ((IList<int>)value.ResidentIds)[0] = 500);
			Assert.Throws<NotSupportedException>(() => ((IList<string>)value.ObjectIds)[0] = "changed");
			ClassicAssert.AreEqual(before, Wire(value));
		}

		[TestCase(null)] [TestCase("")] [TestCase("01234567-89AB-CDEF-0123-456789ABCDEF")]
		[TestCase("0123456789abcdef0123456789abcdef")] [TestCase("{01234567-89ab-cdef-0123-456789abcdef}")]
		[TestCase("../01234567-89ab-cdef-0123-456789abcdef")]
		public void GameIdMustBeExactLowercaseGuidD(string gameId) { Refuses(Good(gameId: gameId)); }

		[TestCase("step", null)] [TestCase("step", "")]
		[TestCase("zone", null)] [TestCase("zone", "")]
		[TestCase("missing", null)] [TestCase("missing", "")]
		[TestCase("object", null)] [TestCase("object", "")]
		public void RequiredTextNeverDefaultsMissingValues(string field, string text) { Refuses(TextField(field, text)); }

		[TestCase("step", 131073)] [TestCase("zone", 129)]
		[TestCase("missing", 513)] [TestCase("object", 513)]
		public void TextBoundsRefuseWithoutTruncation(string field, int length)
		{
			Refuses(TextField(field, new string('x', length)));
		}

		[TestCase(0)] [TestCase(10)] [TestCase(127)] [TestCase(0xD800)] [TestCase(0xDC00)]
		public void EveryTextPositionRejectsControlsAndUnpairedUtf16(int codeUnit)
		{
			string invalid = "before" + new string((char)codeUnit, 1) + "after";
			foreach (string field in new[] { "step", "zone", "missing", "object" }) Refuses(TextField(field, invalid));
		}

		[Test]
		public void PairedUnicodeAndOrdinalDistinctObjectIdentitiesRemainExact()
		{
			string[] objects = Objects(); objects[0] = "body-\U0001F9EA"; objects[1] = "BODY-2";
			KingdomScenarioSaveSnapshot value = Good(step: "opaque-\U0001F9EA", objects: objects);
			ClassicAssert.IsTrue(KingdomScenarioSaveSnapshotCodec.TryDecode(Wire(value), out KingdomScenarioSaveSnapshot read));
			ClassicAssert.AreEqual(value.StepWire, read.StepWire); CollectionAssert.AreEqual(objects, read.ObjectIds);
		}

		[TestCase(-1L, 1, 50)] [TestCase(0L, 0, 50)] [TestCase(0L, -1, 50)]
		[TestCase(0L, 1, 0)] [TestCase(0L, 1, -1)]
		public void NegativeClocksUnchargedLedgerAndInvalidMissingIdsRefuse(long now, int departures, int missingId)
		{
			Refuses(Good(now: now, departures: departures, missingId: missingId));
		}

		[TestCase(0)] [TestCase(-1)] [TestCase(2)] [TestCase(50)]
		public void RetainedResidentIdsMustBePositiveUniqueAndDisjointFromMissing(int first)
		{
			int[] ids = Ids(); ids[0] = first; Refuses(Good(ids: ids));
		}

		[TestCase("body-2")] [TestCase(MissingObject)]
		public void RetainedObjectIdsMustBeUniqueAndDisjointFromMissing(string first)
		{
			string[] objects = Objects(); objects[0] = first; Refuses(Good(objects: objects));
		}

		[TestCase(0)] [TestCase(48)] [TestCase(50)]
		public void PairArraysRequireExactlyFortyNineEntriesEach(int count)
		{
			Refuses(Good(ids: Ids(count))); Refuses(Good(objects: Objects(count)));
		}

		[Test]
		public void NullModelOrNullArraysRefuseRatherThanInventSurvivors()
		{
			Refuses(null);
			Refuses(new KingdomScenarioSaveSnapshot(GameId, StepWire, ZoneId, 1, 1, 50, MissingObject, null, Objects()));
			Refuses(new KingdomScenarioSaveSnapshot(GameId, StepWire, ZoneId, 1, 1, 50, MissingObject, Ids(), null));
		}

		[Test]
		public void MaximumAsciiStepWireFitsButOversizedUtf8PayloadRefuses()
		{
			string maximum = new string('s', KingdomScenarioSaveSnapshotCodec.MaxStepWireChars);
			ClassicAssert.IsTrue(KingdomScenarioSaveSnapshotCodec.TryDecode(Wire(Good(step: maximum)), out _));
			Refuses(Good(step: new string('\u0800', KingdomScenarioSaveSnapshotCodec.MaxStepWireChars)));
		}

		[TestCase(null)] [TestCase("")] [TestCase("ssv1:")] [TestCase("ssv2:AAAA")]
		[TestCase("SSV1:AAAA")] [TestCase("ssv1:!not-base64")]
		public void MissingUnknownOrMalformedEnvelopeRefuses(string wire) { RefusesWire(wire); }

		[Test]
		public void EveryTruncatedByteBoundaryAndTrailingBytesRefuse()
		{
			byte[] bytes = Bytes(Wire(Good()));
			for (int length = 0; length < bytes.Length; length++)
			{
				byte[] truncated = new byte[length]; Array.Copy(bytes, truncated, length);
				RefusesWire(Envelope(truncated));
			}
			byte[] trailing = new byte[bytes.Length + 1]; Array.Copy(bytes, trailing, bytes.Length);
			RefusesWire(Envelope(trailing));
		}

		[TestCase(0)] [TestCase(4)]
		public void WrongMagicOrVersionRefuses(int offset)
		{
			byte[] bytes = Bytes(Wire(Good())); bytes[offset] ^= 1; RefusesWire(Envelope(bytes));
		}

		[Test]
		public void NoncanonicalBase64WhitespaceAndUnusedPaddingBitsRefuse()
		{
			string wire = Wire(Good()); RefusesWire(wire.Insert(8, "\n"));
			for (int extra = 0; extra < 3 && !wire.EndsWith("=", StringComparison.Ordinal); extra++)
				wire = Wire(Good(step: StepWire + new string('x', extra + 1)));
			ClassicAssert.IsTrue(wire.EndsWith("=", StringComparison.Ordinal));
			const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
			int at = wire.Length - (wire.EndsWith("==", StringComparison.Ordinal) ? 3 : 2);
			char[] chars = wire.ToCharArray(); chars[at] = alphabet[alphabet.IndexOf(chars[at]) + 1];
			CollectionAssert.AreEqual(Bytes(wire), Bytes(new string(chars)), "padding mutation must preserve decoded bytes");
			RefusesWire(new string(chars));
		}

		[TestCase(-1)] [TestCase(int.MaxValue)] [TestCase(0)]
		public void ForgedStringLengthsRefuseWithoutUnboundedAllocation(int length)
		{
			byte[] bytes = Bytes(Wire(Good())); PutInt(bytes, 8, length); RefusesWire(Envelope(bytes));
		}

		[TestCase(48)] [TestCase(50)] [TestCase(int.MaxValue)]
		public void ForgedPairCountRefusesBeforeReadingPairs(int count)
		{
			byte[] bytes = Bytes(Wire(Good())); PutInt(bytes, PairCountOffset(bytes), count); RefusesWire(Envelope(bytes));
		}

		[TestCase(0)] [TestCase(2)] [TestCase(50)]
		public void ForgedRetainedIdsCannotBypassModelValidationOnDecode(int first)
		{
			byte[] bytes = Bytes(Wire(Good())); PutInt(bytes, PairCountOffset(bytes) + 4, first);
			RefusesWire(Envelope(bytes));
		}

		[Test]
		public void InvalidUtf8InsideAFramedStringRefuses()
		{
			byte[] bytes = Bytes(Wire(Good())); bytes[12] = 0xFF; RefusesWire(Envelope(bytes));
		}

		[Test]
		public void OversizedEnvelopeRefusesBeforeBase64Decode()
		{
			RefusesWire("ssv1:" + new string('A', KingdomScenarioSaveSnapshotCodec.MaxWireChars));
		}

		private static KingdomScenarioSaveSnapshot Good(string gameId = GameId, string step = StepWire,
			string zone = ZoneId, long now = 6000, int departures = 1, int missingId = 50,
			string missingObject = MissingObject, int[] ids = null, string[] objects = null)
		{
			return new KingdomScenarioSaveSnapshot(gameId, step, zone, now, departures, missingId,
				missingObject, ids ?? Ids(), objects ?? Objects());
		}
		private static KingdomScenarioSaveSnapshot TextField(string field, string text)
		{
			if (field == "step") return Good(step: text);
			if (field == "zone") return Good(zone: text);
			if (field == "missing") return Good(missingObject: text);
			string[] objects = Objects(); objects[0] = text; return Good(objects: objects);
		}
		private static int[] Ids(int count = 49)
		{
			int[] ids = new int[count]; for (int i = 0; i < count; i++) ids[i] = i + 1; return ids;
		}
		private static string[] Objects(int count = 49)
		{
			string[] ids = new string[count]; for (int i = 0; i < count; i++) ids[i] = "body-" + (i + 1); return ids;
		}
		private static string Wire(KingdomScenarioSaveSnapshot value)
		{
			ClassicAssert.IsTrue(KingdomScenarioSaveSnapshotCodec.Valid(value));
			ClassicAssert.IsTrue(KingdomScenarioSaveSnapshotCodec.TryEncode(value, out string wire)); return wire;
		}
		private static void Refuses(KingdomScenarioSaveSnapshot value)
		{
			ClassicAssert.IsFalse(KingdomScenarioSaveSnapshotCodec.Valid(value));
			ClassicAssert.IsFalse(KingdomScenarioSaveSnapshotCodec.TryEncode(value, out string wire)); ClassicAssert.IsNull(wire);
		}
		private static void RefusesWire(string wire)
		{
			ClassicAssert.IsFalse(KingdomScenarioSaveSnapshotCodec.TryDecode(wire, out KingdomScenarioSaveSnapshot value)); ClassicAssert.IsNull(value);
		}
		private static byte[] Bytes(string wire) { return Convert.FromBase64String(wire.Substring(5)); }
		private static string Envelope(byte[] bytes) { return "ssv1:" + Convert.ToBase64String(bytes); }
		private static void PutInt(byte[] bytes, int offset, int value)
		{
			for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (8 * i));
		}
		private static int PairCountOffset(byte[] bytes)
		{
			using (MemoryStream stream = new MemoryStream(bytes))
			using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
			{
				stream.Position = 8;
				for (int i = 0; i < 3; i++) SkipText(reader);
				stream.Position += 16; SkipText(reader);
				return (int)stream.Position;
			}
		}
		private static void SkipText(BinaryReader reader)
		{
			int length = reader.ReadInt32(); reader.BaseStream.Position += length;
		}
	}
}
#endif
