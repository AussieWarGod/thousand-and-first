#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryFingerprint(id, "a deed", true,
				null, out fingerprint));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official", official,
				out officialBefore));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("official", official,
				"new official", out officialAfter));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("outsider", outsider,
				out outsiderBefore));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("outsider", outsider,
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
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { "a", "bc", null }, out fixture));
			ClassicAssert.AreEqual(
				"f72b719bd06c8f3663b948f75846594a08cc577cb02797b005f11dbb04fa1453",
				fixture);
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { "ab", "c" }, out splitA));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { "a", "bc" }, out splitB));
			ClassicAssert.AreNotEqual(splitA, splitB, "field boundaries must not alias");
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { null }, out nullValue));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryCanonicalHash("fixture",
				new string[] { "" }, out emptyValue));
			ClassicAssert.AreNotEqual(nullValue, emptyValue);
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official",
				new List<string> { "same" }, out official));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("outsider",
				new List<string> { "same" }, out outsider));
			ClassicAssert.AreNotEqual(official, outsider);
			ClassicAssert.AreEqual(64, fixture.Length);
		}

		[Test]
		public void BoundedEvictionPreservesConstitutionalRootAndMatchesHash()
		{
			List<string> values = new List<string>();
			for (int i = 0; i < KingdomChronicleReceiptRules.MaxEntries; i++)
				values.Add("entry-" + i);
			string predicted, actual;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("official", values,
				"tail", out predicted));
			KingdomChronicleReceiptRules.AppendBounded(values, "tail");
			ClassicAssert.AreEqual(KingdomChronicleReceiptRules.MaxEntries, values.Count);
			ClassicAssert.AreEqual("entry-0", values[0],
				"the founding/root milestone is not ordinary FIFO news");
			ClassicAssert.AreEqual("entry-2", values[1]);
			ClassicAssert.AreEqual("tail", values[values.Count - 1]);
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official", values,
				out actual));
			ClassicAssert.AreEqual(predicted, actual);
		}

		[Test]
		public void BoundedListFixedPointCannotClaimDeliveryOrRetry()
		{
			List<string> values = new List<string>();
			for (int i = 0; i < KingdomChronicleReceiptRules.MaxEntries; i++)
				values.Add("same");
			string before, after;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official", values,
				out before));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("official", values,
				"same", out after));
			ClassicAssert.AreEqual(before, after);
			ClassicAssert.AreEqual(KingdomChronicleListAction.MarkLost,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Pending, before, before, after));
			ClassicAssert.AreEqual(KingdomChronicleListAction.MarkLost,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Attempting, after, before, after));
		}

		[Test]
		public void RecoveryClassifiesExactBeforeAfterAndInterleaving()
		{
			List<string> beforeList = new List<string> { "before" };
			string before, after, unrelated;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official", beforeList,
				out before));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashAfter("official", beforeList,
				"event", out after));
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryHashList("official",
				new List<string> { "other" }, out unrelated));
			ClassicAssert.AreEqual(KingdomChronicleListAction.Append,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Pending, before, before, after));
			ClassicAssert.AreEqual(KingdomChronicleListAction.Append,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Attempting, before, before, after));
			ClassicAssert.AreEqual(KingdomChronicleListAction.ConfirmDelivered,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Attempting, after, before, after));
			ClassicAssert.AreEqual(KingdomChronicleListAction.MarkLost,
				KingdomChronicleReceiptRules.ListAction(
					KingdomChronicleSinkDisposition.Attempting, unrelated, before, after));
			ClassicAssert.AreEqual(KingdomChronicleSinkDisposition.Lost,
				KingdomChronicleReceiptRules.RecoverUninspectable(
					KingdomChronicleSinkDisposition.Attempting));
		}

		[Test]
		public void ActiveAndTerminalRowsRoundTripWithExplicitDispositions()
		{
			KingdomChronicleReceipt active = Active("event:active");
			string text;
			KingdomChronicleRegistryFault fault;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(
				new List<KingdomChronicleReceipt> { active }, out text, out fault), fault.ToString());
			StringAssert.Contains("\na|", text);
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(text, out parsed,
				out migrated, out fault), fault.ToString());
			ClassicAssert.IsFalse(migrated);
			ClassicAssert.AreEqual(KingdomChronicleSinkDisposition.Pending,
				parsed[0].OfficialState);
			ClassicAssert.AreEqual(KingdomChronicleSinkDisposition.Delivered,
				parsed[0].OutsiderState);
			ClassicAssert.AreEqual(KingdomChronicleSinkDisposition.Pending,
				parsed[0].JournalState);
			ClassicAssert.AreEqual(active.OfficialAfter, parsed[0].OfficialAfter);

			active.OfficialState = KingdomChronicleSinkDisposition.Delivered;
			active.JournalState = KingdomChronicleSinkDisposition.Skipped;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.ReceiptValid(active),
				"terminal active row must remain valid across a save cut");
			KingdomChronicleReceipt compact = KingdomChronicleReceiptRules.Compact(active);
			ClassicAssert.IsNotNull(compact);
			ClassicAssert.IsTrue(compact.Compact);
			ClassicAssert.IsNull(compact.Official);
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(
				new List<KingdomChronicleReceipt> { compact }, out text, out fault));
			StringAssert.Contains("\ntg|", text);

			KingdomChronicleReceipt illegal = Terminal("event:illegal");
			illegal.OfficialState = KingdomChronicleSinkDisposition.Skipped;
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.ReceiptValid(illegal),
				"inspectable list sinks cannot silently use journal-only Skipped");
			illegal = Active("event:none");
			illegal.JournalState = KingdomChronicleSinkDisposition.None;
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.ReceiptValid(illegal));
		}

		[Test]
		public void SixtyFifthReceiptLivesAndNoReceiptIsEvicted()
		{
			List<KingdomChronicleReceipt> rows = new List<KingdomChronicleReceipt>();
			for (int i = 0; i < 65; i++) rows.Add(Terminal("event:" + i));
			string text;
			KingdomChronicleRegistryFault fault;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out text,
				out fault), fault.ToString());
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(text, out parsed,
				out migrated, out fault), fault.ToString());
			ClassicAssert.AreEqual(65, parsed.Count);
			ClassicAssert.AreEqual("event:0", parsed[0].EventId);
			ClassicAssert.AreEqual("event:64", parsed[64].EventId);
		}

		[Test]
		public void CapacityFailsClosedWithoutDiscardingExactRows()
		{
			List<KingdomChronicleReceipt> rows = new List<KingdomChronicleReceipt>();
			for (int i = 0; i < KingdomChronicleReceiptRules.MaxReceipts; i++)
				rows.Add(Terminal("event:" + i));
			string text;
			KingdomChronicleRegistryFault fault;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out text,
				out fault), fault.ToString());
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(text, out parsed,
				out migrated, out fault), fault.ToString());
			ClassicAssert.AreEqual(KingdomChronicleReceiptRules.MaxReceipts, parsed.Count);
			rows.Add(Terminal("event:overflow"));
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out text,
				out fault));
			ClassicAssert.AreEqual(KingdomChronicleRegistryFault.TooManyRows, fault);
		}

		[Test]
		public void ConstructionTerminalUsesExactJobAndCoordinate()
		{
			const string job = "0123456789abcdef0123456789abcdef";
			string id = "construction:" + job + ":raised:chronicle";
			KingdomChronicleReceipt receipt = Terminal(id);
			string text;
			KingdomChronicleRegistryFault fault;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(
				new List<KingdomChronicleReceipt> { receipt }, out text, out fault));
			StringAssert.Contains("\ntc|" + job + "|", text);
			ClassicAssert.IsFalse(text.Contains("construction:"),
				"compact row stores exact job and coordinate, not repeated prefix");
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(text, out parsed,
				out migrated, out fault), fault.ToString());
			ClassicAssert.AreEqual(id, parsed[0].EventId);
			ClassicAssert.AreEqual(KingdomChronicleSinkDisposition.Lost,
				parsed[0].OutsiderState);
			string parsedJob, coordinate;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryConstructionIdentity(id,
				out parsedJob, out coordinate));
			ClassicAssert.AreEqual(job, parsedJob);
			ClassicAssert.AreEqual("raised:chronicle", coordinate);
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.TryConstructionIdentity(
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
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(legacy, out parsed,
				out migrated, out fault), fault.ToString());
			ClassicAssert.IsTrue(migrated);
			ClassicAssert.AreEqual(2, parsed.Count);
			ClassicAssert.AreEqual(generic, parsed[0].EventId);
			ClassicAssert.IsTrue(parsed[0].LegacyBlocked);
			ClassicAssert.IsNull(parsed[0].Fingerprint,
				"legacy FNV fingerprint must never authorize v3 delivery");
			ClassicAssert.AreEqual(KingdomChronicleSinkDisposition.Lost,
				parsed[0].OfficialState);
			ClassicAssert.AreEqual(KingdomChronicleSinkDisposition.Lost,
				parsed[1].JournalState,
				"legacy accomplishment intent is uncertainty, not delivery");
			string v3;
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(parsed, out v3,
				out fault), fault.ToString());
			StringAssert.StartsWith(KingdomChronicleReceiptRules.Header, v3);
			StringAssert.Contains("\ntg|", v3);
			StringAssert.Contains("\ntc|" + job + "|", v3);
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(v3, out parsed,
				out migrated, out fault), fault.ToString());
			ClassicAssert.IsFalse(migrated);
			ClassicAssert.AreEqual(construction, parsed[1].EventId);
		}

		[Test]
		public void UnknownMalformedAndOversizeRegistryNeverThrows()
		{
			List<KingdomChronicleReceipt> parsed;
			bool migrated;
			KingdomChronicleRegistryFault fault;
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				"taf-chronicle|4", out parsed, out migrated, out fault));
			ClassicAssert.AreEqual(KingdomChronicleRegistryFault.UnknownVersion, fault);
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				"not-a-registry", out parsed, out migrated, out fault));
			ClassicAssert.AreEqual(KingdomChronicleRegistryFault.MalformedHeader, fault);
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				KingdomChronicleReceiptRules.Header + new string('\n',
					KingdomChronicleReceiptRules.MaxReceipts + 1), out parsed,
					out migrated, out fault));
			ClassicAssert.AreEqual(KingdomChronicleRegistryFault.TooManyRows, fault);
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				new string('x', KingdomChronicleReceiptRules.MaxRegistryChars + 1),
				out parsed, out migrated, out fault));
			ClassicAssert.AreEqual(KingdomChronicleRegistryFault.RawTooLong, fault);
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				KingdomChronicleReceiptRules.Header + "\ntg|!!!!|" + ZeroHash
					+ "|3|3|4|1|0", out parsed, out migrated, out fault));
			ClassicAssert.AreEqual(KingdomChronicleRegistryFault.MalformedRow, fault);
			ClassicAssert.IsFalse(KingdomChronicleReceiptRules.TryParseRegistry(
				"v1\n" + LegacyRow("legacy", 2), out parsed, out migrated, out fault));
			ClassicAssert.AreEqual(KingdomChronicleRegistryFault.MalformedRow, fault);
			ClassicAssert.IsFalse(migrated, "invalid legacy data must never be labeled migrated");
		}
	}
}
#endif
