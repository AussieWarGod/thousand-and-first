#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomZoneObservationReceiptTests
	{
		private static KingdomZoneObservationReceipt Receipt(long Tick = 19L,
			string Payload = "payload-v1")
		{
			Assert.That(KingdomZoneObservationRules.TryCreate("purpose-a", "realm-a",
				"settlement-a", "zone-a", "owner-a", "source-v1", Tick, Payload,
				out KingdomZoneObservationReceipt receipt), Is.True);
			return receipt;
		}

		[Test]
		public void CanonicalWireRoundTripsEveryBoundField()
		{
			KingdomZoneObservationReceipt before = Receipt();
			Assert.That(KingdomZoneObservationCodec.TryEncode(before, out string wire), Is.True);
			Assert.That(KingdomZoneObservationCodec.TryDecode(wire,
				out KingdomZoneObservationReceipt after), Is.True);
			Assert.Multiple(() => {
				Assert.That(after.Version, Is.EqualTo(1));
				Assert.That(after.Purpose, Is.EqualTo("purpose-a"));
				Assert.That(after.RealmId, Is.EqualTo("realm-a"));
				Assert.That(after.SettlementId, Is.EqualTo("settlement-a"));
				Assert.That(after.ZoneId, Is.EqualTo("zone-a"));
				Assert.That(after.OwnerId, Is.EqualTo("owner-a"));
				Assert.That(after.SourceRevision, Is.EqualTo("source-v1"));
				Assert.That(after.ObservedTick, Is.EqualTo(19L));
				Assert.That(after.Payload, Is.EqualTo("payload-v1"));
				Assert.That(KingdomZoneObservationRules.LowerHexDigest(after.SourceDigest), Is.True);
			});
			Assert.That(KingdomZoneObservationCodec.TryEncode(after, out string canonical), Is.True);
			Assert.That(canonical, Is.EqualTo(wire));
		}

		[Test]
		public void SourceDigestBindsEveryAuthorityAndPayloadField()
		{
			KingdomZoneObservationReceipt original = Receipt();
			KingdomZoneObservationRules.TryCreate("purpose-b", "realm-a", "settlement-a",
				"zone-a", "owner-a", "source-v1", 19L, "payload-v1", out var purpose);
			KingdomZoneObservationRules.TryCreate("purpose-a", "realm-b", "settlement-a",
				"zone-a", "owner-a", "source-v1", 19L, "payload-v1", out var realm);
			KingdomZoneObservationRules.TryCreate("purpose-a", "realm-a", "settlement-b",
				"zone-a", "owner-a", "source-v1", 19L, "payload-v1", out var settlement);
			KingdomZoneObservationRules.TryCreate("purpose-a", "realm-a", "settlement-a",
				"zone-b", "owner-a", "source-v1", 19L, "payload-v1", out var zone);
			KingdomZoneObservationRules.TryCreate("purpose-a", "realm-a", "settlement-a",
				"zone-a", "owner-b", "source-v1", 19L, "payload-v1", out var owner);
			KingdomZoneObservationRules.TryCreate("purpose-a", "realm-a", "settlement-a",
				"zone-a", "owner-a", "source-v2", 19L, "payload-v1", out var revision);
			KingdomZoneObservationRules.TryCreate("purpose-a", "realm-a", "settlement-a",
				"zone-a", "owner-a", "source-v1", 20L, "payload-v1", out var tick);
			KingdomZoneObservationRules.TryCreate("purpose-a", "realm-a", "settlement-a",
				"zone-a", "owner-a", "source-v1", 19L, "payload-v2", out var payload);
			KingdomZoneObservationReceipt[] receipts =
				{ purpose, realm, settlement, zone, owner, revision, tick, payload };
			for (int i = 0; i < receipts.Length; i++)
				Assert.That(receipts[i].SourceDigest, Is.Not.EqualTo(original.SourceDigest));
		}

		[Test]
		public void ExactRawReadRejectsWrongTypeBindingRevisionAndFutureTick()
		{
			KingdomZoneObservationReceipt receipt = Receipt();
			KingdomZoneObservationCodec.TryEncode(receipt, out string wire);
			Assert.That(KingdomZoneObservationRules.TryReadExact(wire, "purpose-a", "realm-a",
				"settlement-a", "zone-a", "owner-a", "source-v1", 19L, out _), Is.True);
			Assert.That(KingdomZoneObservationRules.TryReadExact(wire, "purpose-a", "realm-a",
				"settlement-a", "zone-a", "owner-a", "source-v1", long.MaxValue, out _),
				Is.True, "receipt age has no expiry");
			object[] wrongRaw = { null, 1, 1L, new object(), new char[] { 'x' } };
			for (int i = 0; i < wrongRaw.Length; i++)
				Assert.That(KingdomZoneObservationRules.TryReadExact(wrongRaw[i], "purpose-a",
					"realm-a", "settlement-a", "zone-a", "owner-a", "source-v1", 19L,
					out _), Is.False);
			string[][] wrong = {
				new[] { "purpose-b", "realm-a", "settlement-a", "zone-a", "owner-a", "source-v1" },
				new[] { "purpose-a", "realm-b", "settlement-a", "zone-a", "owner-a", "source-v1" },
				new[] { "purpose-a", "realm-a", "settlement-b", "zone-a", "owner-a", "source-v1" },
				new[] { "purpose-a", "realm-a", "settlement-a", "zone-b", "owner-a", "source-v1" },
				new[] { "purpose-a", "realm-a", "settlement-a", "zone-a", "owner-b", "source-v1" },
				new[] { "purpose-a", "realm-a", "settlement-a", "zone-a", "owner-a", "source-v2" }
			};
			for (int i = 0; i < wrong.Length; i++)
				Assert.That(KingdomZoneObservationRules.TryReadExact(wire, wrong[i][0], wrong[i][1],
					wrong[i][2], wrong[i][3], wrong[i][4], wrong[i][5], 19L, out _), Is.False);
			Assert.That(KingdomZoneObservationRules.TryReadExact(wire, "purpose-a", "realm-a",
				"settlement-a", "zone-a", "owner-a", "source-v1", 18L, out _), Is.False);
		}

		[Test]
		public void MalformedNoncanonicalAndMutatedWiresFailClosed()
		{
			KingdomZoneObservationCodec.TryEncode(Receipt(), out string wire);
			string changed = wire.Substring(0, 12) + (wire[12] == 'A' ? "B" : "A")
				+ wire.Substring(13);
			foreach (string candidate in new[] { "", "TAFZO1:not-base64", wire + " ", changed })
				Assert.That(KingdomZoneObservationCodec.TryDecode(candidate, out _), Is.False,
					candidate);
			KingdomZoneObservationReceipt mutated = Receipt(); mutated.Payload = "third-state";
			Assert.That(KingdomZoneObservationRules.Valid(mutated), Is.False);
			Assert.That(KingdomZoneObservationCodec.TryEncode(mutated, out _), Is.False);
		}

		[Test]
		public void CreationRejectsEmptyTrimmedOversizedAndNegativeInputs()
		{
			Assert.That(KingdomZoneObservationRules.TryCreate("purpose-a", "realm-a",
				"settlement-a", "zone-a", "owner-a", "source-v1", -1L, "payload", out _),
				Is.False);
			Assert.That(KingdomZoneObservationRules.TryCreate(" purpose-a", "realm-a",
				"settlement-a", "zone-a", "owner-a", "source-v1", 0L, "payload", out _),
				Is.False);
			Assert.That(KingdomZoneObservationRules.TryCreate("purpose-a", "",
				"settlement-a", "zone-a", "owner-a", "source-v1", 0L, "payload", out _),
				Is.False);
			Assert.That(KingdomZoneObservationRules.TryCreate("purpose-a", "realm-a",
				"settlement-a", "zone-a", "owner-a", "source-v1", 0L,
				new string('x', KingdomZoneObservationRules.MaxPayloadChars + 1), out _), Is.False);
		}

		[Test]
		public void ReachPayloadIsFixedCanonicalBoundedAndPurposeSeparated()
		{
			Assert.That(KingdomReachObservationRules.TryAuthorityDigest(
				new List<string>(), out string emptyDigest), Is.True,
				"an attended zone with no designations still needs an authoritative zero receipt");
			Assert.That(KingdomZoneObservationRules.LowerHexDigest(emptyDigest), Is.True);
			Assert.That(KingdomReachObservationRules.TryAuthorityDigest(
				new List<string> { "designation-b", "designation-a" }, out string digest), Is.True);
			Assert.That(KingdomReachObservationRules.TryAuthorityDigest(
				new List<string> { "designation-a", "designation-b" }, out string reordered), Is.True);
			Assert.That(reordered, Is.EqualTo(digest));
			Assert.That(KingdomReachObservationRules.TryAuthorityDigest(
				new List<string> { "designation-a", "designation-a" }, out _), Is.False);
			Assert.That(KingdomReachObservationRules.TryAuthorityDigest(
				new List<string> { new string('x',
					KingdomReachObservationRules.MaxAuthorityRowChars + 1) }, out _), Is.False);
			Assert.That(KingdomReachObservationRules.KindCount, Is.EqualTo(6));
			Assert.That(KingdomReachObservationRules.LegacyKindCount, Is.EqualTo(5));
			int[] city = { 1, 0, int.MaxValue, 3, 4, 9 };
			int[] realm = { 0, 2, 0, 6, 8, 7 };
			Assert.That(KingdomReachObservationRules.TryEncodePayload(city, realm, digest,
				out string payload), Is.True);
			StringAssert.StartsWith("rp2|", payload);
			Assert.That(KingdomReachObservationRules.TryDecodePayload(payload, out int[] readCity,
				out int[] readRealm, out string readDigest, out bool legacy), Is.True);
			Assert.That(legacy, Is.False);
			CollectionAssert.AreEqual(city, readCity); CollectionAssert.AreEqual(realm, readRealm);
			Assert.That(readDigest, Is.EqualTo(digest));
			Assert.That(KingdomReachObservationRules.Amount(payload, "learning", false),
				Is.EqualTo(int.MaxValue));
			Assert.That(KingdomReachObservationRules.Amount(payload, "spirit", true), Is.EqualTo(2));
			Assert.That(KingdomReachObservationRules.Amount(payload, "wealth", true), Is.EqualTo(7));
			Assert.That(KingdomReachObservationRules.Amount(payload, "unknown", true), Is.Zero);
			Assert.That(KingdomReachObservationRules.TryDecodeVersionedPayload(
				KingdomReachObservationRules.SourceRevision, payload, out _, out _, out _), Is.True);
			Assert.That(KingdomReachObservationRules.TryDecodeVersionedPayload(
				KingdomReachObservationRules.LegacySourceRevision, payload,
				out _, out _, out _), Is.False);

			string legacyPayload = "rp1|" + digest + "|1,2,3,4,5|5,4,3,2,1";
			Assert.That(KingdomReachObservationRules.TryDecodePayload(legacyPayload,
				out int[] legacyCity, out int[] legacyRealm, out _, out legacy), Is.True);
			Assert.That(legacy, Is.True);
			CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 0 }, legacyCity);
			CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1, 0 }, legacyRealm);
			Assert.That(KingdomReachObservationRules.Amount(
				legacyPayload, "wealth", false), Is.Zero);
			Assert.That(KingdomReachObservationRules.TryDecodeVersionedPayload(
				KingdomReachObservationRules.LegacySourceRevision, legacyPayload,
				out _, out _, out _), Is.True);
			Assert.That(KingdomReachObservationRules.TryDecodeVersionedPayload(
				KingdomReachObservationRules.SourceRevision, legacyPayload,
				out _, out _, out _), Is.False);
			Assert.That(KingdomReachObservationRules.TryDecodePayload(
				"rp1|" + digest + "|1,2,3,4,5,6|1,2,3,4,5,6",
				out _, out _, out _), Is.False);
			Assert.That(KingdomReachObservationRules.TryDecodePayload(
				payload.Replace("1,0,", "01,0,"), out _, out _, out _), Is.False);
			city[0] = -1;
			Assert.That(KingdomReachObservationRules.TryEncodePayload(city, realm, digest, out _),
				Is.False);
		}
	}
}
#endif
