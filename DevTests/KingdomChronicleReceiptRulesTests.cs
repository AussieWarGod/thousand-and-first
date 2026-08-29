#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomChronicleReceiptRulesTests
	{
		private const string ZeroHash =
			"0000000000000000000000000000000000000000000000000000000000000000";

		private static KingdomChronicleReceipt Active(string id)
		{
			List<string> official = new List<string> { "old official" };
			List<string> outsider = new List<string> { "old outsider" };
			string fingerprint, officialBefore, officialAfter, outsiderBefore, outsiderAfter;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryFingerprint(id, "a deed", true,
				null, out fingerprint));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official", official,
				out officialBefore));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("official", official,
				"new official", out officialAfter));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashList("outsider", outsider,
				out outsiderBefore));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("outsider", outsider,
				"new outsider", out outsiderAfter));
			return new KingdomChronicleReceipt
			{
				EventId = id,
				Fingerprint = fingerprint,
				Official = "new official",
				Outsider = "new outsider",
				OfficialBefore = officialBefore,
				OfficialAfter = officialAfter,
				OutsiderBefore = outsiderBefore,
				OutsiderAfter = outsiderAfter,
				OfficialState = KingdomChronicleSinkDisposition.Pending,
				OutsiderState = KingdomChronicleSinkDisposition.Delivered,
				JournalState = KingdomChronicleSinkDisposition.Pending,
				Updated = 17L
			};
		}

		private static KingdomChronicleReceipt Terminal(string id)
		{
			return new KingdomChronicleReceipt
			{
				EventId = id,
				Fingerprint = ZeroHash,
				OfficialState = KingdomChronicleSinkDisposition.Delivered,
				OutsiderState = KingdomChronicleSinkDisposition.Lost,
				JournalState = KingdomChronicleSinkDisposition.Skipped,
				Updated = 19L,
				Compact = true
			};
		}

		private static string B64(string value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
		}

		private static string LegacyRow(string id, int phase)
		{
			const string hash = "0123456789abcdef";
			return B64(id) + "|" + B64(hash) + "|" + B64("old official") + "|"
				+ B64("old outsider") + "|" + B64(hash) + "|" + B64(hash) + "|"
				+ B64(hash) + "|" + B64(hash) + "|" + phase + "|23";
		}

		[Test]
		public void CanonicalSha256UsesLengthPrefixesNullAndDomainSeparation()
		{
			string fixture, splitA, splitB, nullValue, emptyValue, official, outsider;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { "a", "bc", null }, out fixture));
			Assert.AreEqual(
				"f72b719bd06c8f3663b948f75846594a08cc577cb02797b005f11dbb04fa1453",
				fixture);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { "ab", "c" }, out splitA));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { "a", "bc" }, out splitB));
			Assert.AreNotEqual(splitA, splitB, "field boundaries must not alias");
			Assert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { null }, out nullValue));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { "" }, out emptyValue));
			Assert.AreNotEqual(nullValue, emptyValue);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official",
				new List<string> { "same" }, out official));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashList("outsider",
				new List<string> { "same" }, out outsider));
			Assert.AreNotEqual(official, outsider);
			Assert.AreEqual(64, fixture.Length);
		}

		[Test]
		public void BoundedEvictionPreservesConstitutionalRootAndMatchesHash()
		{
			List<string> values = new List<string>();
			for (int i = 0; i < KingdomChronicleReceiptRules.MaxEntries; i++)
				values.Add("entry-" + i);
			string predicted, actual;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("official", values,
				"tail", out predicted));
			KingdomChronicleReceiptRules.AppendBounded(values, "tail");
			Assert.AreEqual(KingdomChronicleReceiptRules.MaxEntries, values.Count);
			Assert.AreEqual("entry-0", values[0],
				"the founding/root milestone is not ordinary FIFO news");
			Assert.AreEqual("entry-2", values[1]);
			Assert.AreEqual("tail", values[values.Count - 1]);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official", values,
				out actual));
			Assert.AreEqual(predicted, actual);
		}

		[Test]
		public void BoundedListFixedPointCannotClaimDeliveryOrRetry()
		{
			List<string> values = new List<string>();
			for (int i = 0; i < KingdomChronicleReceiptRules.MaxEntries; i++)
				values.Add("same");
			string before, after;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official", values,
				out before));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("official", values,
				"same", out after));
			Assert.AreEqual(before, after);
			Assert.AreEqual(KingdomChronicleListAction.MarkLost,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Pending, before, before, after));
			Assert.AreEqual(KingdomChronicleListAction.MarkLost,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Attempting, after, before, after));
		}

		[Test]
		public void RecoveryClassifiesExactBeforeAfterAndInterleaving()
		{
			List<string> beforeList = new List<string> { "before" };
			string before, after, unrelated;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official", beforeList,
				out before));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("official", beforeList,
				"event", out after));
			Assert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official",
				new List<string> { "other" }, out unrelated));
			Assert.AreEqual(KingdomChronicleListAction.Append,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Pending, before, before, after));
			Assert.AreEqual(KingdomChronicleListAction.Append,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Attempting, before, before, after));
			Assert.AreEqual(KingdomChronicleListAction.ConfirmDelivered,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Attempting, after, before, after));
			Assert.AreEqual(KingdomChronicleListAction.MarkLost,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Attempting, unrelated, before, after));
			Assert.AreEqual(KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleReceiptRules.RecoverUninspectable(
					KingdomChronicleSinkDisposition.Attempting));
		}

		[Test]
		public void ActiveAndTerminalRowsRoundTripWithExplicitDispositions()
		{
			KingdomChronicleReceipt active = Active("event:active");
			string text;
			KingdomChronicleRegistryFault fault;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(
				new List<KingdomChronicleReceipt> { active }, out text, out fault), fault.ToString());
			StringAssert.Contains("\na|", text);
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(text, out parsed,
				out migrated, out fault), fault.ToString());
			Assert.IsFalse(migrated);
			Assert.AreEqual(KingdomChronicleSinkDisposition.Pending,
				parsed[0].OfficialState);
			Assert.AreEqual(KingdomChronicleSinkDisposition.Delivered,
				parsed[0].OutsiderState);
			Assert.AreEqual(KingdomChronicleSinkDisposition.Pending,
				parsed[0].JournalState);
			Assert.AreEqual(active.OfficialAfter, parsed[0].OfficialAfter);

			active.OfficialState = KingdomChronicleSinkDisposition.Delivered;
			active.JournalState = KingdomChronicleSinkDisposition.Skipped;
			Assert.IsTrue(KingdomChronicleReceiptRules.ReceiptValid(active),
				"terminal active row must remain valid across a save cut");
			KingdomChronicleReceipt compact = KingdomChronicleReceiptRules.Compact(active);
			Assert.IsNotNull(compact);
			Assert.IsTrue(compact.Compact);
			Assert.IsNull(compact.Official);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(
				new List<KingdomChronicleReceipt> { compact }, out text, out fault));
			StringAssert.Contains("\ntg|", text);

			KingdomChronicleReceipt illegal = Terminal("event:illegal");
			illegal.OfficialState = KingdomChronicleSinkDisposition.Skipped;
			Assert.IsFalse(KingdomChronicleReceiptRules.ReceiptValid(illegal),
				"inspectable list sinks cannot silently use journal-only Skipped");
			illegal = Active("event:none");
			illegal.JournalState = KingdomChronicleSinkDisposition.None;
			Assert.IsFalse(KingdomChronicleReceiptRules.ReceiptValid(illegal));
		}

		[Test]
		public void SixtyFifthReceiptLivesAndNoReceiptIsEvicted()
		{
			List<KingdomChronicleReceipt> rows = new List<KingdomChronicleReceipt>();
			for (int i = 0; i < 65; i++) rows.Add(Terminal("event:" + i));
			string text;
			KingdomChronicleRegistryFault fault;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out text,
				out fault), fault.ToString());
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(text, out parsed,
				out migrated, out fault), fault.ToString());
			Assert.AreEqual(65, parsed.Count);
			Assert.AreEqual("event:0", parsed[0].EventId);
			Assert.AreEqual("event:64", parsed[64].EventId);
		}

		[Test]
		public void CapacityFailsClosedWithoutDiscardingExactRows()
		{
			List<KingdomChronicleReceipt> rows = new List<KingdomChronicleReceipt>();
			for (int i = 0; i < KingdomChronicleReceiptRules.MaxReceipts; i++)
				rows.Add(Terminal("event:" + i));
			string text;
			KingdomChronicleRegistryFault fault;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out text,
				out fault), fault.ToString());
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(text, out parsed,
				out migrated, out fault), fault.ToString());
			Assert.AreEqual(KingdomChronicleReceiptRules.MaxReceipts, parsed.Count);
			rows.Add(Terminal("event:overflow"));
			Assert.IsFalse(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out text,
				out fault));
			Assert.AreEqual(KingdomChronicleRegistryFault.TooManyRows, fault);
		}

		[Test]
		public void ConstructionTerminalUsesExactJobAndCoordinate()
		{
			const string job = "0123456789abcdef0123456789abcdef";
			string id = "construction:" + job + ":raised:chronicle";
			KingdomChronicleReceipt receipt = Terminal(id);
			string text;
			KingdomChronicleRegistryFault fault;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(
				new List<KingdomChronicleReceipt> { receipt }, out text, out fault));
			StringAssert.Contains("\ntc|" + job + "|", text);
			Assert.IsFalse(text.Contains("construction:"),
				"compact row stores exact job and coordinate, not repeated prefix");
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(text, out parsed,
				out migrated, out fault), fault.ToString());
			Assert.AreEqual(id, parsed[0].EventId);
			Assert.AreEqual(KingdomChronicleSinkDisposition.Lost,
				parsed[0].OutsiderState);
			string parsedJob, coordinate;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryConstructionIdentity(id,
				out parsedJob, out coordinate));
			Assert.AreEqual(job, parsedJob);
			Assert.AreEqual("raised:chronicle", coordinate);
			Assert.IsFalse(KingdomChronicleReceiptRules.TryConstructionIdentity(
				"construction:" + job.ToUpperInvariant() + ":raised", out parsedJob,
				out coordinate));
		}

		[Test]
		public void LegacyV1MigratesOneForOneToBlockedLostTombstones()
		{
			const string job = "0123456789abcdef0123456789abcdef";
			string generic = "legacy:event";
			string construction = "construction:" + job + ":closed:chronicle";
			string legacy = "v1\n" + LegacyRow(generic, 31) + "\n"
				+ LegacyRow(construction, 16);
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			KingdomChronicleRegistryFault fault;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(legacy, out parsed,
				out migrated, out fault), fault.ToString());
			Assert.IsTrue(migrated);
			Assert.AreEqual(2, parsed.Count);
			Assert.AreEqual(generic, parsed[0].EventId);
			Assert.IsTrue(parsed[0].LegacyBlocked);
			Assert.IsNull(parsed[0].Fingerprint,
				"legacy FNV fingerprint must never authorize v3 delivery");
			Assert.AreEqual(KingdomChronicleSinkDisposition.Lost,
				parsed[0].OfficialState);
			Assert.AreEqual(KingdomChronicleSinkDisposition.Lost,
				parsed[1].JournalState,
				"legacy accomplishment intent is uncertainty, not delivery");
			string v3;
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(parsed, out v3,
				out fault), fault.ToString());
			StringAssert.StartsWith(KingdomChronicleReceiptRules.Header, v3);
			StringAssert.Contains("\ntg|", v3);
			StringAssert.Contains("\ntc|" + job + "|", v3);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(v3, out parsed,
				out migrated, out fault), fault.ToString());
			Assert.IsFalse(migrated);
			Assert.AreEqual(construction, parsed[1].EventId);
		}

		[Test]
		public void UnknownMalformedAndOversizeRegistryNeverThrows()
		{
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			KingdomChronicleRegistryFault fault;
			Assert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				"taf-chronicle|4", out parsed, out migrated, out fault));
			Assert.AreEqual(KingdomChronicleRegistryFault.UnknownVersion, fault);
			Assert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				"not-a-registry", out parsed, out migrated, out fault));
			Assert.AreEqual(KingdomChronicleRegistryFault.MalformedHeader, fault);
			Assert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				KingdomChronicleReceiptRules.Header + new string('\n',
					KingdomChronicleReceiptRules.MaxReceipts + 1), out parsed,
					out migrated, out fault));
			Assert.AreEqual(KingdomChronicleRegistryFault.TooManyRows, fault);
			Assert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				new string('x', KingdomChronicleReceiptRules.MaxRegistryChars + 1),
				out parsed, out migrated, out fault));
			Assert.AreEqual(KingdomChronicleRegistryFault.RawTooLong, fault);
			Assert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				KingdomChronicleReceiptRules.Header + "\ntg|!!!!|" + ZeroHash
					+ "|3|3|4|1|0", out parsed, out migrated, out fault));
			Assert.AreEqual(KingdomChronicleRegistryFault.MalformedRow, fault);
			Assert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				"v1\n" + LegacyRow("legacy", 2), out parsed, out migrated, out fault));
			Assert.AreEqual(KingdomChronicleRegistryFault.MalformedRow, fault);
			Assert.IsFalse(migrated, "invalid legacy data must never be labeled migrated");
		}
	}
}
#endif
