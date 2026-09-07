#if TAF_TESTS
using System;

using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomConstructionInputObservationRulesTests
	{
		[Test]
		public void AttendedZonePartitionsSurviveCanonicalReloadAndReplacement()
		{
			KingdomConstructionInputZoneObservation first = Zone("settlement-a", "zone-a", 10,
				Material("holder-a", "item-a", 3));
			KingdomConstructionInputZoneObservation second = Zone("settlement-b", "zone-b", 20,
				Water("water-b", 900));
			KingdomConstructionInputObservationBook book = Book(first, second);
			ClassicAssert.IsTrue(KingdomConstructionInputObservationCodec.TryEncode(book,
				out string encoded));
			ClassicAssert.IsTrue(KingdomConstructionInputObservationCodec.TryDecode(encoded,
				out KingdomConstructionInputObservationBook loaded));
			ClassicAssert.AreEqual(2, loaded.ZoneCount);
			ClassicAssert.AreEqual("item-a", Find(loaded, "zone-a").LineAt(0).SourceObjectId);
			ClassicAssert.AreEqual("water-b", Find(loaded, "zone-b").LineAt(0).SourceObjectId);

			KingdomConstructionInputZoneObservation revisited = Zone(
				"settlement-a", "zone-a", 30, Material("holder-a", "item-a-2", 2));
			KingdomConstructionInputObservationBook replaced = Book(revisited,
				Find(loaded, "zone-b"));
			ClassicAssert.IsTrue(KingdomConstructionInputObservationCodec.TryEncode(replaced,
				out string replacement));
			ClassicAssert.IsTrue(KingdomConstructionInputObservationCodec.TryDecode(replacement,
				out KingdomConstructionInputObservationBook reloaded));
			ClassicAssert.AreEqual(30L, Find(reloaded, "zone-a").ObservedTick);
			ClassicAssert.AreEqual("item-a-2", Find(reloaded, "zone-a").LineAt(0).SourceObjectId);
			ClassicAssert.AreEqual("water-b", Find(reloaded, "zone-b").LineAt(0).SourceObjectId);
			ClassicAssert.IsTrue(KingdomConstructionInputObservationCodec.TryEncode(reloaded,
				out string canonical));
			ClassicAssert.AreEqual(replacement, canonical);
		}

		[Test]
		public void CodecRejectsFutureNoncanonicalDuplicateAndMalformedEvidence()
		{
			KingdomConstructionInputZoneObservation zone = Zone("settlement", "zone", 1,
				Material("holder", "item", 1));
			ClassicAssert.IsFalse(KingdomConstructionInputObservationRules.Valid(Book(zone, zone)));
			ClassicAssert.IsTrue(KingdomConstructionInputObservationCodec.TryEncode(Book(zone),
				out string encoded));
			ClassicAssert.IsFalse(KingdomConstructionInputObservationCodec.TryDecode(encoded + "\n",
				out KingdomConstructionInputObservationBook _));
			byte[] future = Convert.FromBase64String(encoded);
			future[4] = 2; future[5] = future[6] = future[7] = 0;
			ClassicAssert.IsFalse(KingdomConstructionInputObservationCodec.TryDecode(
				Convert.ToBase64String(future), out _));
			ClassicAssert.IsFalse(KingdomConstructionInputObservationRules.Valid(Zone(
				"settlement", "bad-grid", 1, new byte[] { 1, 1, 1, 0 },
				new byte[] { 0, 0, 0, 1 }, Material("holder", "item", 1))));
			ClassicAssert.IsFalse(KingdomConstructionInputObservationRules.Valid(Zone(
				"settlement", "bad-kind", 1, new KingdomConstructionInputObservationLine(
					(KingdomConstructionInputKind)99, "unknown", "holder", "item",
					KingdomConstructionInputTopology.ContainerInventory, 0, 0,
					"Item", 1, 0, false, false))));
		}

		[Test]
		public void RemoteRichObservationCannotIncreaseLocalPoorAllowance()
		{
			KingdomConstructionInputObservationBook remote = Book(Zone(
				"remote", "remote-zone", 4, Water("remote-water", 1000)));
			ClassicAssert.IsTrue(KingdomConstructionInputObservationRules.Valid(remote));
			ClassicAssert.IsTrue(KingdomConstructionInputLeaseRules.TryAvailableWater(
				2, 3, true, out int localAvailable));
			ClassicAssert.AreEqual(0, localAvailable);
		}

		private static KingdomConstructionInputObservationBook Book(
			params KingdomConstructionInputZoneObservation[] zones)
		{
			return new KingdomConstructionInputObservationBook(
				KingdomConstructionInputObservationRules.Schema, "realm", 7, zones);
		}

		private static KingdomConstructionInputZoneObservation Zone(string settlement,
			string zone, long tick, params KingdomConstructionInputObservationLine[] lines)
		{
			return Zone(settlement, zone, tick, new byte[] { 1, 1, 1, 1 },
				new byte[] { 0, 0, 0, 0 }, lines);
		}

		private static KingdomConstructionInputZoneObservation Zone(string settlement,
			string zone, long tick, byte[] passable, byte[] paved,
			params KingdomConstructionInputObservationLine[] lines)
		{
			return new KingdomConstructionInputZoneObservation(settlement, zone, tick, 3,
				2, 2, passable, paved, lines);
		}

		private static KingdomConstructionInputObservationLine Material(string holder,
			string item, int count)
		{
			return new KingdomConstructionInputObservationLine(
				KingdomConstructionInputKind.Material, "material", holder, item,
				KingdomConstructionInputTopology.ContainerInventory, 0, 0, "Item",
				count, 0, false, false);
		}

		private static KingdomConstructionInputObservationLine Water(string id, int count)
		{
			return new KingdomConstructionInputObservationLine(
				KingdomConstructionInputKind.Water,
				KingdomConstructionInputRules.WaterClassification, id, id,
				KingdomConstructionInputTopology.LiquidVessel, 1, 1, "Waterskin",
				count, 0, false, false);
		}

		private static KingdomConstructionInputZoneObservation Find(
			KingdomConstructionInputObservationBook book, string zone)
		{
			for (int i = 0; i < book.ZoneCount; i++)
				if (book.ZoneAt(i).ZoneId == zone) return book.ZoneAt(i);
			return null;
		}
	}
}
#endif
