#if TAF_TESTS
using System;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	// Executable transport tests only. Opaque field graphs are not native save/load authority.
	[TestFixture]
	public sealed class KingdomUpgradeSnapshotTests
	{
		private const string Id = "01234567-89ab-cdef-0123-456789abcdef";

		[TestCase("inheritance")]
		[TestCase("detached-transition")]
		public void BothCasesRoundTripAllObservedFields(string scenario)
		{
			KingdomUpgradeSnapshot expected = Snapshot(scenario);
			ClassicAssert.IsTrue(KingdomUpgradeSnapshotCodec.TryEncode(expected, out string wire));
			ClassicAssert.AreEqual(12, wire.Split('\n').Length);
			ClassicAssert.IsTrue(KingdomUpgradeSnapshotCodec.TryDecode(wire, out KingdomUpgradeSnapshot actual));
			ClassicAssert.AreNotSame(expected, actual);
			CollectionAssert.AreEqual(Fields(expected), Fields(actual));
			ClassicAssert.IsTrue(KingdomUpgradeSnapshotCodec.TryEncode(actual, out string second));
			ClassicAssert.AreEqual(wire, second);
		}

		[Test]
		public void GraphFramesPreserveNullEmptyUnicodeAndOrder()
		{
			string absent = KingdomUpgradeGraph.Capture(null), empty = KingdomUpgradeGraph.Capture("");
			ClassicAssert.AreNotEqual(absent, empty);
			foreach (object value in new object[] { null, "", "é漢😀\n", 0, long.MaxValue, true,
				new System.Collections.Generic.List<string> { "a", null, "" },
				new System.Collections.Generic.List<int> { 3, 1, 2 }, new byte[] { 0, 255 } })
				ClassicAssert.IsTrue(KingdomUpgradeGraph.Valid(KingdomUpgradeGraph.Capture(value)));
			ClassicAssert.AreNotEqual(KingdomUpgradeGraph.Capture("é"), KingdomUpgradeGraph.Capture("e\u0301"));
			Assert.Throws<System.Text.EncoderFallbackException>(() => KingdomUpgradeGraph.Capture("\ud800"));
		}

		[TestCase(1, "01234567-89ab-cdef-0123-456789ABCDEF")]
		[TestCase(2, "transition")]
		[TestCase(3, "d2097d086f65e81c4d84c1a35d7d20b6f606c489")]
		[TestCase(4, "-1")]
		[TestCase(5, "01")]
		[TestCase(6, "+1")]
		[TestCase(7, "9223372036854775808")]
		[TestCase(8, "not-base64")]
		public void AlteredMetadataOrGraphRefuses(int row, string value)
		{
			ClassicAssert.IsTrue(KingdomUpgradeSnapshotCodec.TryEncode(Snapshot(), out string wire));
			string[] rows = wire.Split('\n'); rows[row] = value;
			ClassicAssert.IsFalse(KingdomUpgradeSnapshotCodec.TryDecode(string.Join("\n", rows), out _));
		}

		[Test]
		public void TransportNeverTrimsNormalizesOrAcceptsTrailingBytes()
		{
			ClassicAssert.IsTrue(KingdomUpgradeSnapshotCodec.TryEncode(Snapshot(), out string wire));
			foreach (string bad in new[] { "\ufeff" + wire, wire.Replace("\n", "\r\n"), wire + "\n",
				wire.Substring(0, wire.Length - 1), wire + "junk", wire.Replace("taf-upgrade-save-v1", "taf-upgrade-save-v2") })
				ClassicAssert.IsFalse(KingdomUpgradeSnapshotCodec.TryDecode(bad, out _));
			ClassicAssert.IsFalse(KingdomUpgradeSnapshotCodec.TryDecode(new string('x', KingdomUpgradeSnapshotCodec.MaxWireChars + 1), out _));
			string graph = KingdomUpgradeGraph.Capture("payload");
			byte[] bytes = Convert.FromBase64String(graph);
			ClassicAssert.IsFalse(KingdomUpgradeGraph.Valid(Convert.ToBase64String(bytes.Concat(new byte[] { 0 }).ToArray())));
			ClassicAssert.IsFalse(KingdomUpgradeGraph.Valid(graph + "\n"));
			ClassicAssert.IsFalse(KingdomUpgradeGraph.Valid(Convert.ToBase64String(bytes.Take(bytes.Length - 1).ToArray())));
		}

		[TestCase("inheritance")]
		[TestCase("detached-transition")]
		public void ArmingRequestRequiresExactCaseAndOldPin(string scenario)
		{
			string wire = "taf-upgrade-save-request-v1\n" + KingdomUpgradeSnapshotCodec.OldPin + "\n" + scenario + "\n";
			ClassicAssert.IsTrue(KingdomUpgradeSnapshotCodec.TryRequest(wire, out string actual));
			ClassicAssert.AreEqual(scenario, actual);
			foreach (string bad in new[] { wire + "\n", wire.Replace("\n", "\r\n"), wire.Replace("v1", "v2"),
				wire.Replace(KingdomUpgradeSnapshotCodec.OldPin, new string('0', 40)), wire.Replace(scenario, "unknown") })
				ClassicAssert.IsFalse(KingdomUpgradeSnapshotCodec.TryRequest(bad, out _));
		}

		[Test]
		public void ReceiptDelegatesLiveCacheBindingExplicitlyToStoppedHost()
		{
			string hash = new string('a', 64);
			string receipt = KingdomUpgradeSnapshotCodec.Receipt(Snapshot(), hash, hash, hash);
			string[] rows = receipt.Split('\n');
			ClassicAssert.AreEqual(9, rows.Length);
			ClassicAssert.AreEqual("cache-bind-after-quit", rows[6]);
			ClassicAssert.AreEqual(hash, rows[7]);
			Assert.Throws<ArgumentException>(() => KingdomUpgradeSnapshotCodec.Receipt(Snapshot(), "", hash, hash));
		}

		private static KingdomUpgradeSnapshot Snapshot(string scenario = "inheritance")
		{
			return new KingdomUpgradeSnapshot(Id, scenario, KingdomUpgradeSnapshotCodec.OldPin, 1, 2, 3, 4,
				KingdomUpgradeGraph.Capture("opaque inherited wire"), KingdomUpgradeGraph.Capture(null), KingdomUpgradeGraph.Capture(null));
		}

		private static object[] Fields(KingdomUpgradeSnapshot value)
		{
			return new object[] { value.GameId, value.Case, value.OldPin, value.Turns, value.TimeTicks,
				value.ActionTicks, value.PlayerActionTicks, value.Inheritance, value.Transition, value.Legacy };
		}
	}
}
#endif
