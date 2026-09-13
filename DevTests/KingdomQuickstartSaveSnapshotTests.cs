#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Snapshot = ThousandAndFirst.Harness.KingdomQuickstartSaveSnapshot;
using Codec = ThousandAndFirst.Harness.KingdomQuickstartSaveSnapshotCodec;

namespace ThousandAndFirst.Tests
{
	// Executable codec tests. Opaque heart/reservation text is not native ownership or save proof.
	[TestFixture]
	public sealed class KingdomQuickstartSaveSnapshotTests
	{
		private const string GameId = "01234567-89ab-cdef-0123-456789abcdef";
		private static readonly int[] TextFields = { 0, 1, 2, 5, 10, 11, 12, 13, 14 };

		[TestCase("marsh", true)] [TestCase("marsh", false)]
		[TestCase("canyon", true)] [TestCase("canyon", false)]
		[TestCase("dunes", true)] [TestCase("dunes", false)]
		public void SixSelectionsRoundTripEveryImmutableField(string profile, bool advisor)
		{
			Snapshot expected = Good(profile, advisor); string wire = Wire(expected);
			ClassicAssert.IsTrue(Codec.TryDecode(wire, out Snapshot actual)); ClassicAssert.AreNotSame(expected, actual);
			CollectionAssert.AreEqual(Values(expected), Values(actual)); ClassicAssert.AreEqual(15, Values(actual).Length);
			ClassicAssert.AreEqual(wire, Wire(actual)); CollectionAssert.AreEqual(Raw(expected), Bytes(wire));
			FieldInfo[] fields = typeof(Snapshot).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
			ClassicAssert.AreEqual(15, fields.Length); foreach (FieldInfo field in fields) ClassicAssert.IsTrue(field.IsInitOnly, field.Name);
		}

		[TestCase(5)] [TestCase(13)]
		public void OptionalNullAndEmptyRemainDistinct(int field)
		{
			Snapshot absent = Change(field, null), empty = Change(field, "");
			ClassicAssert.IsTrue(Codec.TryDecode(Wire(absent), out Snapshot a));
			ClassicAssert.IsTrue(Codec.TryDecode(Wire(empty), out Snapshot b));
			ClassicAssert.IsNull(Values(a)[field]); ClassicAssert.AreEqual("", Values(b)[field]);
			ClassicAssert.AreNotEqual(Wire(absent), Wire(empty));
		}

		[TestCase(5)] [TestCase(11)] [TestCase(13)] [TestCase(14)]
		public void PairedUnicodeAndNormalizationDistinctTextStayExact(int field)
		{
			foreach (string text in new[] { "é-漢-\U0001F9EA", "e\u0301-漢-\U0001F9EA" })
			{
				Snapshot value = Change(field, text); ClassicAssert.IsTrue(Codec.TryDecode(Wire(value), out Snapshot read));
				ClassicAssert.AreEqual(text, Values(read)[field]);
			}
			ClassicAssert.AreNotEqual(Wire(Change(field, "é")), Wire(Change(field, "e\u0301")));
		}

		[TestCase(null)] [TestCase("")] [TestCase("01234567-89AB-CDEF-0123-456789ABCDEF")]
		[TestCase("0123456789abcdef0123456789abcdef")] [TestCase("{01234567-89ab-cdef-0123-456789abcdef}")]
		public void NoncanonicalGameIdRefuses(string value) { Refuses(Change(0, value)); }
		[TestCase(null)] [TestCase("")] [TestCase("#")] [TestCase("##42")]
		[TestCase("#42 x")] [TestCase("#42\n")] [TestCase("SEED")]
		public void InvalidSharedSeedGrammarRefuses(string value) { Refuses(Change(1, value)); }
		[TestCase("#0")] [TestCase("#4242")] [TestCase("named-seed")]
		public void ExistingSeedGrammarIsNotNarrowed(string value) { ClassicAssert.IsNotNull(Wire(Change(1, value))); }
		[TestCase(null)] [TestCase("")] [TestCase("Marsh")] [TestCase("swamp")] [TestCase("marsh ")]
		public void InvalidProfileRefuses(string value) { Refuses(Change(2, value)); }
		[TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)]
		public void InvalidFounderBaseIdRefuses(int value) { Refuses(Change(4, value)); }
		[TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
		public void NegativeClockRefuses(int field) { Refuses(Change(field, -1L)); }

		[Test]
		public void PositiveIdentityAndClockExtremesRemainRepresentable()
		{
			ClassicAssert.IsNotNull(Wire(Change(4, int.MaxValue)));
			for (int i = 6; i <= 9; i++) foreach (long value in new[] { 0L, long.MaxValue }) ClassicAssert.IsNotNull(Wire(Change(i, value)));
		}

		[Test]
		public void ReceiptMustBeCanonicalCompleteAndMatchProfileAndAdvisor()
		{
			Refuses(Change(3, false)); Refuses(Change(2, "dunes"));
			Refuses(Change(10, Receipt("canyon", true))); Refuses(Change(10, Receipt("marsh", false)));
			for (int phase = 0; phase < 6; phase++) Refuses(Change(10, Receipt("marsh", true, phase)));
			Refuses(Change(10, Receipt("marsh", true) + " "));
		}

		[Test]
		public void ReceiptMustBeAFinishedNonFaultedCohortJustAsTheNativeBootDemands()
		{
			// A seeded world snapshots, saves and cold-loads exactly as an omitted one does.
			ClassicAssert.IsNotNull(Wire(Change(10, FoundersReceipt(
				KingdomQuickstartFoundersDisposition.Seeded))));
			// A world that still owes a cohort, is part-way through one, or has faulted one is not
			// a finished world and the harness may not carry it at all.
			foreach (KingdomQuickstartFoundersDisposition unfinished in new[]
			{
				KingdomQuickstartFoundersDisposition.Pending,
				KingdomQuickstartFoundersDisposition.Seeding,
				KingdomQuickstartFoundersDisposition.Faulted
			})
				Refuses(Change(10, FoundersReceipt(unfinished)));
		}

		private static string FoundersReceipt(KingdomQuickstartFoundersDisposition disposition)
		{
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryProfile("marsh", out KingdomQuickstartProfile profile));
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryCreateReceipt("marsh", profile.ZoneId,
				KingdomQuickstartFoundersDisposition.Pending, out KingdomQuickstartReceipt receipt));
			string[] values = { "", "Starapple", "water-id", "larder-id", "materials-id", "advisor-id", "" };
			for (int phase = 1; phase <= 6; phase++)
			{
				var advisor = phase == 5 ? KingdomQuickstartAdvisorDisposition.Included
					: KingdomQuickstartAdvisorDisposition.Unresolved;
				ClassicAssert.IsTrue(KingdomQuickstartRules.TryAdvance(receipt,
					(KingdomQuickstartPhase)phase, values[phase], advisor, out var next));
				receipt = next;
			}
			if (disposition == KingdomQuickstartFoundersDisposition.Pending)
				return KingdomQuickstartRules.Encode(receipt);
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryRestateFounders(receipt,
				KingdomQuickstartFoundersDisposition.Seeding,
				new[] { "f0", "f1", "f2", "f3" }, out KingdomQuickstartReceipt seeding));
			if (disposition == KingdomQuickstartFoundersDisposition.Seeding)
				return KingdomQuickstartRules.Encode(seeding);
			if (disposition == KingdomQuickstartFoundersDisposition.Faulted)
			{
				ClassicAssert.IsTrue(KingdomQuickstartRules.TryRestateFounders(seeding,
					KingdomQuickstartFoundersDisposition.Faulted, null, out KingdomQuickstartReceipt faulted));
				return KingdomQuickstartRules.Encode(faulted);
			}
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryAdvance(seeding,
				KingdomQuickstartPhase.FoundersSeeded, "",
				KingdomQuickstartAdvisorDisposition.Unresolved, out KingdomQuickstartReceipt seeded));
			return KingdomQuickstartRules.Encode(seeded);
		}

		[Test]
		public void RequiredTextCannotDefaultNullOrEmpty()
		{
			foreach (int field in TextFields) if (field != 5 && field != 13)
				foreach (string value in new string[] { null, "" }) Refuses(Change(field, value));
			Refuses(null);
		}

		[TestCase(0)] [TestCase(10)] [TestCase(127)] [TestCase(0xD800)] [TestCase(0xDC00)]
		public void AllStringColumnsRejectControlsAndConstructedUnpairedUtf16(int code)
		{
			foreach (int field in TextFields) Refuses(Change(field, "before" + new string((char)code, 1) + "after"), false);
		}

		[TestCase("hs1-")] [TestCase("hs2-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
		[TestCase("hs1-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
		public void HeartSealShapeIsExact(string seal) { Refuses(Change(12, seal)); }

		[Test]
		public void FiniteFieldBoundsAndTotalUtf8EnvelopeCapAreEnforced()
		{
			int[] limits = { 36, Codec.MaxSeedChars, 6, Codec.MaxFounderIdChars, Codec.MaxReceiptChars,
				Codec.MaxHeartReceiptChars, 68, Codec.MaxHeartTerminalChars, Codec.MaxReservationsChars };
			for (int i = 0; i < TextFields.Length; i++) Refuses(Change(TextFields[i], new string('x', limits[i] + 1)), false);
			foreach (int field in new[] { 5, 11, 13, 14 })
			{
				int at = Array.IndexOf(TextFields, field);
				ClassicAssert.IsTrue(Codec.TryDecode(Wire(Change(field, new string('x', limits[at]))), out _));
			}
			ClassicAssert.IsNotNull(Wire(Change(1, "#" + new string('a', 96))));
			Refuses(Change(11, new string('\u0800', Codec.MaxHeartReceiptChars)), false);
			BadWire(Codec.Prefix + new string('A', Codec.MaxWireChars));
		}

		[TestCase(true)] [TestCase(false)]
		public void EveryByteTruncationAndTrailingByteRefuse(bool advisor)
		{
			byte[] bytes = Bytes(Wire(Good("marsh", advisor)));
			for (int count = 0; count < bytes.Length; count++)
			{ byte[] cut = new byte[count]; Array.Copy(bytes, cut, count); BadWire(Envelope(cut)); }
			foreach (byte extra in new byte[] { 0, 1, 255 })
			{ byte[] tail = new byte[bytes.Length + 1]; Array.Copy(bytes, tail, bytes.Length); tail[bytes.Length] = extra; BadWire(Envelope(tail)); }
		}

		[TestCase(null)] [TestCase("")] [TestCase("taf-quickstart-save-v1:")]
		[TestCase("taf-quickstart-save-v2:AAAA")] [TestCase("TAF-quickstart-save-v1:AAAA")]
		[TestCase("taf-quickstart-save-v1:!bad")]
		public void EnvelopeFailureClearsOutput(string wire) { BadWire(wire); }

		[Test]
		public void WrongMagicVersionBooleanAndLengthsRefuse()
		{
			byte[] original = Bytes(Wire(Good())); int[] offsets = Offsets(original);
			foreach (int offset in new[] { 0, 4 })
			{ byte[] bytes = (byte[])original.Clone(); bytes[offset] ^= 1; BadWire(Envelope(bytes)); }
			foreach (byte value in new byte[] { 2, 127, 255 })
			{ byte[] bytes = (byte[])original.Clone(); bytes[offsets[3]] = value; BadWire(Envelope(bytes)); }
			foreach (int field in TextFields) foreach (int length in new[] { -2, int.MaxValue })
			{ byte[] bytes = (byte[])original.Clone(); PutInt(bytes, offsets[field], length); BadWire(Envelope(bytes)); }
		}

		[Test]
		public void InvalidUtf8AndEncodedSurrogatesInEveryStringColumnRefuse()
		{
			byte[] original = Bytes(Wire(Good())); int[] offsets = Offsets(original);
			foreach (int field in TextFields) foreach (byte[] invalid in new[] { new byte[] { 255 }, new byte[] { 0xED, 0xA0, 0x80 } })
			{ byte[] bytes = (byte[])original.Clone(); Array.Copy(invalid, 0, bytes, offsets[field] + 4, invalid.Length); BadWire(Envelope(bytes)); }
		}

		[Test]
		public void Base64WhitespaceAndUnusedPaddingBitsAreNotCanonical()
		{
			string wire = Wire(Good()); BadWire(wire.Insert(Codec.Prefix.Length + 4, "\n"));
			for (int i = 0; i < 3 && !wire.EndsWith("=", StringComparison.Ordinal); i++) wire = Wire(Change(11, "heart" + new string('x', i)));
			ClassicAssert.IsTrue(wire.EndsWith("=", StringComparison.Ordinal));
			const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
			int at = wire.Length - (wire.EndsWith("==", StringComparison.Ordinal) ? 3 : 2);
			char[] chars = wire.ToCharArray(); chars[at] = alphabet[alphabet.IndexOf(chars[at]) + 1];
			CollectionAssert.AreEqual(Bytes(wire), Bytes(new string(chars))); BadWire(new string(chars));
		}

		private static Snapshot Good(string profile = "marsh", bool advisor = true)
		{
			return new Snapshot(GameId, "#4242", profile, advisor, 123, "founder", 11, 22, 33, 44,
				Receipt(profile, advisor), "opaque-heart", "hs1-" + new string('a', 64), "opaque-terminal", "opaque-reservations");
		}
		private static string Receipt(string key, bool advisor, int target = 6)
		{
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryProfile(key, out KingdomQuickstartProfile profile));
			ClassicAssert.IsTrue(KingdomQuickstartRules.TryCreateReceipt(key, profile.ZoneId,
				KingdomQuickstartFoundersDisposition.Omitted, out KingdomQuickstartReceipt receipt));
			string[] values = { "", "Starapple", "water-id", "larder-id", "materials-id", advisor ? "advisor-id" : "", "" };
			for (int phase = 1; phase <= target; phase++)
			{
				var disposition = phase == 5 ? (advisor ? KingdomQuickstartAdvisorDisposition.Included
					: KingdomQuickstartAdvisorDisposition.Omitted) : KingdomQuickstartAdvisorDisposition.Unresolved;
				ClassicAssert.IsTrue(KingdomQuickstartRules.TryAdvance(receipt, (KingdomQuickstartPhase)phase, values[phase], disposition, out var next));
				receipt = next;
			}
			return KingdomQuickstartRules.Encode(receipt);
		}
		private static object[] Values(Snapshot s)
		{
			return new object[] { s.GameId, s.Seed, s.ProfileKey, s.Advisor, s.FounderBaseId, s.FounderId,
				s.Turns, s.TimeTicks, s.ActionTicks, s.PlayerActionTicks, s.ReceiptWire, s.HeartReceipt,
				s.HeartSeal, s.HeartTerminal, s.ReservationsWire };
		}
		private static Snapshot Change(int field, object value)
		{
			object[] v = Values(Good()); v[field] = value;
			return new Snapshot((string)v[0], (string)v[1], (string)v[2], (bool)v[3], (int)v[4], (string)v[5],
				(long)v[6], (long)v[7], (long)v[8], (long)v[9], (string)v[10], (string)v[11], (string)v[12], (string)v[13], (string)v[14]);
		}
		private static string Wire(Snapshot value)
		{ ClassicAssert.IsTrue(Codec.Valid(value)); ClassicAssert.IsTrue(Codec.TryEncode(value, out string wire)); return wire; }
		private static void Refuses(Snapshot value, bool decode = true)
		{
			object[] before = value == null ? null : Values(value); ClassicAssert.IsFalse(Codec.Valid(value));
			string wire = "stale"; ClassicAssert.IsFalse(Codec.TryEncode(value, out wire)); ClassicAssert.IsNull(wire);
			if (value != null) { CollectionAssert.AreEqual(before, Values(value)); if (decode) BadWire(Envelope(Raw(value))); }
		}
		private static void BadWire(string wire)
		{ Snapshot value = Good(); ClassicAssert.IsFalse(Codec.TryDecode(wire, out value), wire == null ? "null" : "length " + wire.Length); ClassicAssert.IsNull(value); }
		private static string Envelope(byte[] bytes) { return Codec.Prefix + Convert.ToBase64String(bytes); }
		private static byte[] Bytes(string wire) { return Convert.FromBase64String(wire.Substring(Codec.Prefix.Length)); }
		private static byte[] Raw(Snapshot value)
		{
			using (MemoryStream stream = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(stream, new UTF8Encoding(false, true)))
			{
				w.Write(Codec.Magic); w.Write(Codec.Version);
				foreach (object v in Values(value))
				{
					if (v is bool b) w.Write((byte)(b ? 1 : 0)); else if (v is int i) w.Write(i); else if (v is long l) w.Write(l);
					else if (v == null) w.Write(-1); else { byte[] bytes = new UTF8Encoding(false, true).GetBytes((string)v); w.Write(bytes.Length); w.Write(bytes); }
				}
				w.Flush(); return stream.ToArray();
			}
		}
		private static int[] Offsets(byte[] bytes)
		{
			int[] result = new int[15]; int at = 8;
			for (int i = 0; i < result.Length; i++)
			{ result[i] = at; at += i == 3 ? 1 : i == 4 ? 4 : i >= 6 && i <= 9 ? 8 : 4 + Math.Max(0, BitConverter.ToInt32(bytes, at)); }
			return result;
		}
		private static void PutInt(byte[] bytes, int at, int value)
		{ for (int i = 0; i < 4; i++) bytes[at + i] = (byte)(value >> (8 * i)); }
	}
}
#endif
