#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	// Actual pure codec, not engine serialization. Opaque role/job payloads require separate native authority proof.
	public sealed class KingdomResidentDeathCodecTests
	{
		private static KingdomResidentDeathReceipt Full()
		{
			var r = DeathFixture.Accounted(DeathFixture.Receipt());
			r.OfficeGeneration = 2; r.OfficeBefore = KingdomResidentDeathCodec.Fields("office", "retained :| text", null, "");
			r.CookGeneration = 3; r.CookBefore = KingdomResidentDeathCodec.Fields("cook", "retained \ud83c\udfe0", null, "");
			r.FigureId = "retained-figure"; r.Remembrance = true;
			r.ExpeditionBefore = KingdomResidentDeathCodec.Fields("job-before", "exact opaque fields");
			r.ExpeditionPrepared = KingdomResidentDeathCodec.Fields("job-prepared", "exact opaque fields");
			return r;
		}

		[Test]
		public void UnreadableRoleWitnessRetainsItsFaultWithoutClaimingRolesSettled()
		{
			var r = DeathFixture.Receipt(); r.RoleFault = "Exact role authority awaits native proof";
			var copy = DeathFixture.RoundTrip(DeathFixture.Journal(r)).Entries[0];
			Assert.AreEqual(r.RoleFault, copy.RoleFault); Assert.AreEqual(KingdomResidentDeathPhase.Witnessed, copy.Phase);
		}

		[Test]
		public void CanonicalRoundTripPreservesEveryDeclaredFieldAndFreshArrayReferences()
		{
			var original = Full(); var journal = DeathFixture.Journal(original);
			var loaded = DeathFixture.RoundTrip(journal); var copy = loaded.Entries[0];
			Assert.AreNotSame(journal, loaded); Assert.AreNotSame(journal.Entries, loaded.Entries);
			Assert.AreNotSame(original, copy); Assert.AreEqual(journal.Realm, loaded.Realm); Assert.AreEqual(journal.Settlement, loaded.Settlement);
			foreach (var field in typeof(KingdomResidentDeathReceipt).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				object before = field.GetValue(original), after = field.GetValue(copy);
				if (before is Array) { Assert.AreNotSame(before, after, field.Name); CollectionAssert.AreEqual((IEnumerable)before, (IEnumerable)after, field.Name); }
				else Assert.AreEqual(before, after, field.Name);
			}
			StringAssert.StartsWith("rd1:", DeathFixture.Wire(loaded));
			Assert.AreEqual(KingdomResidentDeathCodec.Row(original.Before), KingdomResidentDeathCodec.Row(copy.Before));
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
		public void EveryLegalPhaseRetainsItsOwnRequiredAccountingAndTellingState(int phase)
		{
			var r = phase >= 4 ? DeathFixture.Accounted(DeathFixture.Receipt()) : DeathFixture.Receipt();
			r.Phase = (KingdomResidentDeathPhase)phase;
			if (phase == 6) { r.Telling = KingdomResidentDeathTelling.Uncertain; r.BeforeAccounts = r.AfterAccounts = new string[0]; }
			var loaded = DeathFixture.RoundTrip(DeathFixture.Journal(r)).Entries[0];
			Assert.AreEqual(r.Phase, loaded.Phase); Assert.AreEqual(r.Telling, loaded.Telling);
			Assert.AreEqual(phase == 4 || phase == 5 ? 6 : 0, loaded.BeforeAccounts.Length);
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
		public void TellingDispositionsArePreservedWithoutClaimingThatOwnedMeansDisplayed(int telling)
		{
			var r = DeathFixture.Accounted(DeathFixture.Receipt(memory: telling != 4));
			r.Telling = (KingdomResidentDeathTelling)telling;
			if (telling >= 2) { r.Phase = KingdomResidentDeathPhase.Settled; r.BeforeAccounts = r.AfterAccounts = new string[0]; }
			var restored = DeathFixture.RoundTrip(DeathFixture.Journal(r)).Entries[0];
			Assert.AreEqual(r.Telling, restored.Telling); Assert.AreEqual(r.Memory, restored.Memory);
		}

		[Test]
		public void EveryByteTruncationRefusesWithoutPublishingAPartialJournal()
		{
			string wire = DeathFixture.Wire(DeathFixture.Journal(DeathFixture.Receipt())); byte[] bytes = Bytes(wire);
			for (int length = 0; length < bytes.Length; length++)
			{
				var cut = new byte[length]; Array.Copy(bytes, cut, length); Refuses(Wire(cut), "length=" + length);
			}
			Assert.IsTrue(KingdomResidentDeathCodec.TryDecode(wire, out _));
		}

		[Test]
		public void WrongMagicTrailingBytesAndOverlongStringFramesRefuse()
		{
			byte[] original = Bytes(DeathFixture.Wire(DeathFixture.Journal(DeathFixture.Receipt())));
			byte[] changed = (byte[])original.Clone(); changed[0] ^= 1; Refuses(Wire(changed));
			changed = new byte[original.Length + 1]; Array.Copy(original, changed, original.Length); Refuses(Wire(changed));
			Assert.Less(original[4], 128, "first canonical realm length uses one byte");
			changed = new byte[original.Length + 1]; Array.Copy(original, changed, 4);
			changed[4] = (byte)(original[4] | 128); changed[5] = 0;
			Array.Copy(original, 5, changed, 6, original.Length - 5); Refuses(Wire(changed));
		}

		[TestCase(null)] [TestCase("")] [TestCase("rd1:")] [TestCase("rd1:none")]
		[TestCase("rd0:AAAA")] [TestCase("rd2:AAAA")] [TestCase("RD1:AAAA")]
		[TestCase(" rd1:AAAA")] [TestCase("rd1:not base64")]
		public void MissingUnknownOrMalformedWireNeverMeansAnEmptyJournal(string wire) => Refuses(wire);

		[Test]
		public void EmptyJournalIsExplicitCanonicalAndDifferentFromAbsence()
		{
			var empty = DeathFixture.Journal(); string wire = DeathFixture.Wire(empty);
			Assert.IsTrue(KingdomResidentDeathCodec.TryDecode(wire, out var loaded)); Assert.AreEqual(0, loaded.Entries.Count);
			Assert.AreEqual(DeathFixture.Realm, loaded.Realm); Assert.AreEqual(DeathFixture.Settlement, loaded.Settlement);
			Refuses(null); Refuses(""); Assert.IsFalse(KingdomResidentDeathCodec.TryEncode(null, out string missing)); Assert.IsNull(missing);
		}

		[Test]
		public void NoncanonicalBase64WhitespaceAndUnusedBitsAreRejected()
		{
			var r = DeathFixture.Receipt(); string wire = DeathFixture.Wire(DeathFixture.Journal(r));
			Refuses(wire.Insert(4, " ")); Refuses(wire + "\r\n");
			for (int i = 0; i < 3 && !wire.EndsWith("=", StringComparison.Ordinal); i++)
			{ r.Body += "x"; wire = DeathFixture.Wire(DeathFixture.Journal(r)); }
			Assert.IsTrue(wire.EndsWith("=", StringComparison.Ordinal));
			const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
			int at = wire.Length - 1; while (wire[at] == '=') at--;
			int value = alphabet.IndexOf(wire[at]); Assert.AreEqual(0, value & 1);
			string forged = wire.Substring(0, at) + alphabet[value | 1] + wire.Substring(at + 1);
			CollectionAssert.AreEqual(Bytes(wire), Bytes(forged)); Refuses(forged);
		}

		[TestCase(0xFF)] [TestCase(0xC0)] [TestCase(0x80)]
		public void InvalidUtf8IsNotReplacedByAValidCharacter(int invalid)
		{
			byte[] bytes = Bytes(DeathFixture.Wire(DeathFixture.Journal(DeathFixture.Receipt())));
			bytes[5] = (byte)invalid; Refuses(Wire(bytes));
		}

		[TestCase(0xD800)] [TestCase(0xDFFF)] [TestCase(0)] [TestCase(10)]
		public void InvalidConstructedTextRefusesWithoutMutatingTheOriginalReceipt(int code)
		{
			var r = DeathFixture.Receipt(); string before = DeathFixture.Wire(DeathFixture.Journal(r)); var bad = r.Copy();
			bad.Body += new string((char)code, 1);
			Assert.IsFalse(KingdomResidentDeathCodec.TryEncode(DeathFixture.Journal(bad), out string wire)); Assert.IsNull(wire);
			Assert.AreEqual(before, DeathFixture.Wire(DeathFixture.Journal(r)));
		}

		[TestCase(-1)] [TestCase(4097)] [TestCase(int.MaxValue)]
		public void ForgedJournalCountRefusesRatherThanTruncating(int count)
		{
			byte[] bytes = Bytes(DeathFixture.Wire(DeathFixture.Journal(DeathFixture.Receipt()))); int at;
			using (var stream = new MemoryStream(bytes, false)) using (var read = new BinaryReader(stream))
			{ read.ReadInt32(); read.ReadString(); read.ReadString(); at = (int)stream.Position; }
			Array.Copy(BitConverter.GetBytes(count), 0, bytes, at, 4); Refuses(Wire(bytes));
		}

		[TestCase("phase")] [TestCase("telling")] [TestCase("memory")]
		public void ForgedUnknownPhaseOrNoncanonicalBooleanBytesRefuse(string field)
		{
			var r = DeathFixture.Accounted(DeathFixture.Receipt()); byte[] bytes = Bytes(DeathFixture.Wire(DeathFixture.Journal(r)));
			int proof = FindOnce(bytes, Encoding.UTF8.GetBytes(r.AccountProof)); Assert.AreEqual(64, bytes[proof - 1]);
			if (field == "phase") bytes[proof - 3] = 255;
			else if (field == "telling") bytes[proof - 2] = 255;
			else
			{
				byte[] row = Convert.FromBase64String(KingdomResidentDeathCodec.Row(r.Before));
				int at = FindOnce(bytes, row) + row.Length; Assert.AreEqual((byte)r.Cause, bytes[at]);
				bytes[at + 1] = 2;
			}
			Refuses(Wire(bytes));
		}

		[TestCase("settled-pending")] [TestCase("settled-attempting")] [TestCase("premature-owned")]
		[TestCase("disabled-memory")] [TestCase("enabled-disabled")]
		[TestCase("prepared-no-arrays")] [TestCase("early-arrays")] [TestCase("changed-proof")]
		[TestCase("changed-after")] [TestCase("unpaired-expedition")]
		[TestCase("both-remembrance")] [TestCase("disabled-remembrance")]
		public void ImpossiblePhaseAndFrozenAccountingShapesRefuse(string kind)
		{
			var r = DeathFixture.Accounted(DeathFixture.Receipt());
			switch (kind)
			{
			case "settled-pending": r.Phase = KingdomResidentDeathPhase.Settled; r.BeforeAccounts = r.AfterAccounts = new string[0]; break;
			case "settled-attempting": r.Phase = KingdomResidentDeathPhase.Settled; r.Telling = KingdomResidentDeathTelling.Attempting; r.BeforeAccounts = r.AfterAccounts = new string[0]; break;
			case "premature-owned": r = DeathFixture.Receipt(); r.Telling = KingdomResidentDeathTelling.Owned; break;
			case "disabled-memory": r.Memory = false; break;
			case "enabled-disabled": r.Telling = KingdomResidentDeathTelling.Disabled; break;
			case "prepared-no-arrays": r.Phase = KingdomResidentDeathPhase.AccountingPrepared; r.BeforeAccounts = r.AfterAccounts = new string[0]; break;
			case "early-arrays": r.Phase = KingdomResidentDeathPhase.RolesSettled; break;
			case "changed-proof": r.AccountProof = new string('f', 64); break;
			case "changed-after": r.AfterAccounts[5] = r.BeforeAccounts[5]; break;
			case "unpaired-expedition": r.ExpeditionBefore = "opaque-before"; break;
			case "both-remembrance": r.Remembrance = r.RemembranceUnavailable = true; break;
			case "disabled-remembrance": r.Memory = false; r.Telling = KingdomResidentDeathTelling.Disabled; r.Remembrance = true; break;
			}
			Assert.IsFalse(KingdomResidentDeathRules.Valid(r)); Assert.IsFalse(KingdomResidentDeathCodec.TryEncode(DeathFixture.Journal(r), out string wire)); Assert.IsNull(wire);
		}

		[Test]
		public void DuplicateRowsAndAggregateOrPerFieldBoundsRefuseWithoutEviction()
		{
			var r = DeathFixture.Receipt(); Assert.IsFalse(KingdomResidentDeathCodec.TryEncode(DeathFixture.Journal(r, r.Copy()), out _));
			r.Body = new string('x', 1025); Assert.IsFalse(KingdomResidentDeathCodec.TryEncode(DeathFixture.Journal(r), out _));
			r.Body = new string('x', 1024); Assert.IsTrue(KingdomResidentDeathCodec.TryEncode(DeathFixture.Journal(r), out _));
			Refuses("rd1:" + new string('A', KingdomResidentDeathCodec.MaxWire));
		}

		[Test]
		public void CanonicalCountMapsSortWithoutMutatingInputAndRejectDuplicateKeys()
		{
			var map = new Dictionary<string, int>(StringComparer.Ordinal) { { "z", 3 }, { "a", 2 } };
			var reverse = new Dictionary<string, int>(StringComparer.Ordinal) { { "a", 2 }, { "z", 3 } };
			Assert.AreEqual(KingdomResidentDeathCodec.Map(map), KingdomResidentDeathCodec.Map(reverse));
			Assert.AreEqual(2, map.Count); Assert.AreEqual(3, map["z"]); Assert.AreEqual(2, map["a"]);
			using (var stream = new MemoryStream()) using (var write = new BinaryWriter(stream))
			{
				write.Write(2); write.Write("a"); write.Write(1); write.Write("a"); write.Write(2); write.Flush();
				Assert.Throws<ArgumentException>(() => KingdomResidentDeathCodec.ReadMap(Convert.ToBase64String(stream.ToArray())));
			}
		}

		private static int FindOnce(byte[] data, byte[] needle)
		{
			int found = -1;
			for (int i = 0; i <= data.Length - needle.Length; i++)
			{
				int j = 0; while (j < needle.Length && data[i + j] == needle[j]) j++;
				if (j != needle.Length) continue; Assert.AreEqual(-1, found, "fixture pattern must be unique"); found = i;
			}
			Assert.GreaterOrEqual(found, 0); return found;
		}
		private static byte[] Bytes(string wire) => Convert.FromBase64String(wire.Substring(4));
		private static string Wire(byte[] bytes) => "rd1:" + Convert.ToBase64String(bytes);
		private static void Refuses(string wire, string reason = null)
		{ Assert.IsFalse(KingdomResidentDeathCodec.TryDecode(wire, out var result), reason); Assert.IsNull(result, reason); }
	}
}
#endif
