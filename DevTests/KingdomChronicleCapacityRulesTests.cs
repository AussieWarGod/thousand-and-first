#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	internal static class ChronicleCapacityFixture
	{
		internal static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		internal static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);
		internal static readonly string Full = Registry(KingdomChronicleReceiptRules.MaxReceipts);
		internal static string Registry(int count, bool alternate = false)
		{
			var rows = new List<KingdomChronicleReceipt>();
			for (int i = 0; i < count; i++) rows.Add(new KingdomChronicleReceipt
			{
				EventId = "capacity-fixture:" + i, Fingerprint = new string(alternate ? 'c' : 'd', 64),
				OfficialState = KingdomChronicleSinkDisposition.Delivered,
				OutsiderState = KingdomChronicleSinkDisposition.Delivered,
				JournalState = KingdomChronicleSinkDisposition.Skipped, Compact = true, Updated = 1
			});
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out string wire, out var fault), fault.ToString());
			return wire;
		}
		internal static string Fingerprint(string id = "new:event", string text = "A departure", long tick = 10)
		{
			Assert.IsTrue(KingdomChronicleCapacityRules.TryFingerprint(Realm, Settlement, id, text, tick, out string value));
			return value;
		}
		internal static KingdomDurableKeyObservation Shape(string raw = null)
		{ return new KingdomDurableKeyObservation { HasString = true, String = raw ?? Full }; }
	}

	[TestFixture]
	public sealed class KingdomChronicleCapacityRulesTests
	{
		[Test]
		public void FullCanonicalRegistryProducesExactBoundedWitnessWithoutChangingAnyRow()
		{
			var shape = ChronicleCapacityFixture.Shape(); string before = shape.String;
			string fingerprint = ChronicleCapacityFixture.Fingerprint();
			Assert.IsTrue(KingdomChronicleCapacityRules.TryObserve(shape, "new:event", fingerprint, out var witness));
			Assert.AreEqual(4096, witness.RegistryCount);
			Assert.IsTrue(KingdomChronicleReceiptRules.IsSha256(witness.RegistryHash));
			Assert.AreEqual("new:event", witness.EventId); Assert.AreEqual(fingerprint, witness.Fingerprint);
			Assert.AreSame(before, shape.String);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(shape.String, out var rows, out bool migrated, out _));
			Assert.IsFalse(migrated); Assert.AreEqual(4096, rows.Count);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryWriteRegistry(rows, out string after, out _));
			Assert.AreEqual(before, after);
			Assert.IsTrue(KingdomChronicleCapacityRules.TryObserve(shape, "new:event", fingerprint, out var repeat));
			Assert.AreEqual(witness.RegistryHash, repeat.RegistryHash);
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
		[TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
		[TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)] [TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)]
		[TestCase(24)] [TestCase(25)] [TestCase(26)] [TestCase(27)] [TestCase(28)] [TestCase(29)] [TestCase(30)] [TestCase(31)]
		public void OnlyStringPresenceAcrossAllFiveTablesCanAuthorizeCapacity(int mask)
		{
			var shape = new KingdomDurableKeyObservation
			{
				HasString = (mask & 1) != 0, String = ChronicleCapacityFixture.Full,
				HasInt = (mask & 2) != 0, HasInt64 = (mask & 4) != 0,
				HasObject = (mask & 8) != 0, HasBoolean = (mask & 16) != 0
			};
			Assert.AreEqual(mask == 1, KingdomChronicleCapacityRules.TryObserve(shape, "new:event",
				ChronicleCapacityFixture.Fingerprint(), out var witness));
			Assert.AreEqual(mask == 1, witness != null);
			Assert.AreSame(ChronicleCapacityFixture.Full, shape.String);
			Assert.AreEqual((mask & 2) != 0, shape.HasInt); Assert.AreEqual((mask & 8) != 0, shape.HasObject);
		}

		[TestCase(null)] [TestCase("")] [TestCase(" ")] [TestCase("taf-chronicle|1")]
		[TestCase("taf-chronicle|4")] [TestCase("unreadable")]
		public void MissingMalformedAndLegacyTextNeverBecomesCapacity(string raw)
		{
			var shape = new KingdomDurableKeyObservation { HasString = true, String = raw };
			Assert.IsFalse(KingdomChronicleCapacityRules.TryObserve(shape, "new:event", ChronicleCapacityFixture.Fingerprint(), out var witness));
			Assert.IsNull(witness); Assert.AreEqual(raw, shape.String);
		}

		[TestCase(0)] [TestCase(1)] [TestCase(4095)]
		public void ValidAvailableCapacityIsNotTerminalRefusal(int count)
		{
			Assert.IsFalse(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(
				ChronicleCapacityFixture.Registry(count)), "new:event", ChronicleCapacityFixture.Fingerprint(), out var witness));
			Assert.IsNull(witness);
		}

		[Test]
		public void ExistingEventEvenWithForeignFingerprintIsNeverCapacityRefused()
		{
			Assert.IsFalse(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(), "capacity-fixture:0",
				ChronicleCapacityFixture.Fingerprint("capacity-fixture:0"), out var witness));
			Assert.IsNull(witness);
		}

		[Test]
		public void NoncanonicalAndOversizedRegistryRemainRefusalsWithoutWitness()
		{
			foreach (string raw in new[] { ChronicleCapacityFixture.Full + "\n", ChronicleCapacityFixture.Full.Replace("\n", "\r\n"),
				new string('x', KingdomChronicleReceiptRules.MaxRegistryChars + 1) })
				Assert.IsFalse(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(raw),
					"new:event", ChronicleCapacityFixture.Fingerprint(), out _));
		}

		[Test]
		public void RegistryHashChangesWhenRetainedReceiptChanges()
		{
			Assert.IsTrue(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(), "new:event",
				ChronicleCapacityFixture.Fingerprint(), out var first));
			Assert.IsTrue(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(
				ChronicleCapacityFixture.Registry(4096, true)), "new:event", ChronicleCapacityFixture.Fingerprint(), out var second));
			Assert.AreNotEqual(first.RegistryHash, second.RegistryHash);
		}

		[Test]
		public void NullObservationAndInvalidEventIdentityNeverAuthorizeCapacity()
		{
			Assert.IsFalse(KingdomChronicleCapacityRules.TryObserve(null, "new:event", ChronicleCapacityFixture.Fingerprint(), out _));
			foreach (string id in new[] { null, "", "bad\nidentity", new string((char)0xD800, 1), new string('x', 257) })
				Assert.IsFalse(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(), id,
					ChronicleCapacityFixture.Fingerprint(), out _));
			foreach (string hash in new[] { null, "", new string('A', 64), new string('a', 63) })
				Assert.IsFalse(KingdomChronicleCapacityRules.TryObserve(ChronicleCapacityFixture.Shape(), "new:event", hash, out _));
		}

		[Test]
		public void DatedFingerprintRejectsInvalidTextIdentityAndClock()
		{
			foreach (string text in new[] { null, "bad\ntext", new string((char)0xD800, 1), new string('x', 4097) })
				Assert.IsFalse(KingdomChronicleCapacityRules.TryFingerprint(ChronicleCapacityFixture.Realm,
					ChronicleCapacityFixture.Settlement, "new:event", text, 10, out _));
			Assert.IsFalse(KingdomChronicleCapacityRules.TryFingerprint("foreign", ChronicleCapacityFixture.Settlement, "event", "text", 10, out _));
			Assert.IsFalse(KingdomChronicleCapacityRules.TryFingerprint(ChronicleCapacityFixture.Realm, "foreign", "event", "text", 10, out _));
			Assert.IsFalse(KingdomChronicleCapacityRules.TryFingerprint(ChronicleCapacityFixture.Realm, ChronicleCapacityFixture.Settlement, "", "text", 10, out _));
			Assert.IsFalse(KingdomChronicleCapacityRules.TryFingerprint(ChronicleCapacityFixture.Realm, ChronicleCapacityFixture.Settlement, "event", "text", -1, out _));
			Assert.IsTrue(KingdomChronicleCapacityRules.TryFingerprint(ChronicleCapacityFixture.Realm, ChronicleCapacityFixture.Settlement,
				"event", "paired \U0001F600", 10, out _));
		}
	}
}
#endif
