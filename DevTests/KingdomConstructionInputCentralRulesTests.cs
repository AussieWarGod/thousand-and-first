#if TAF_TESTS
using NUnit.Framework;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	internal class KingdomConstructionInputCentralRulesTests
	{
		private const string SourceZone = "JoppaWorld.11.22.0.1.10";
		private const string TargetZone = "JoppaWorld.11.22.1.1.10";
		private const string Owner = "construction-input:parent:1";
		private const string Digest = "0123456789abcdef";

		[Test]
		public void NeutralBatchMustAdoptOneParentManifestAtomically()
		{
			KingdomJobRow first = Row(101, 0, 2, KingdomDeliveryPhase.ReservationPrepared,
				0, null, 0L);
			KingdomJobRow second = Row(102, 2, 1, KingdomDeliveryPhase.ReservationPrepared,
				0, null, 0L);
			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobTable.TryCreate(new[] { first, second }, out table,
				out fault), fault.ToString());

			KingdomJobRow adoptedFirst = first.WithManifestAuthority(1, Digest, 1L,
				KingdomDeliveryPhase.SourceDebitPrepared);
			KingdomJobTable next;
			Assert.IsFalse(table.TryRewrite(new[] { adoptedFirst }, 1, out next, out fault),
				"one parent cannot leave a neutral sibling behind");
			KingdomJobRow adoptedSecond = second.WithManifestAuthority(1, Digest, 1L,
				KingdomDeliveryPhase.SourceDebitPrepared);
			Assert.IsTrue(table.TryRewrite(new[] { adoptedFirst, adoptedSecond }, 2,
				out next, out fault), fault.ToString());
			table = next;

			Assert.IsTrue(table.TryRewrite(new[]
			{
				adoptedFirst.WithManifestRevision(2L, KingdomDeliveryPhase.InFlight)
			}, 1, out next, out fault), "children advance independently after atomic adoption");
			table = next;
			KingdomJobRow inFlight;
			Assert.IsTrue(table.TryGet(101, out inFlight));
			Assert.IsTrue(table.TryRewrite(new[]
			{
				inFlight.WithManifestRevision(3L,
					KingdomDeliveryPhase.LandedAwaitingOwner)
			}, 1, out next, out fault), fault.ToString());
			table = next;
			KingdomJobRow[] closed;
			Assert.IsTrue(table.TryCloseTrip(101, out next, out closed, out fault));
			Assert.AreEqual(1, closed.Length);
			Assert.IsTrue(next.Holds(102));
		}

		[Test]
		public void LegalStatesKeepOpaqueObjectCustodyAndCoordinateEndpoints()
		{
			AssertLegal(Row(111, 0, 1, KingdomDeliveryPhase.ReservationPrepared,
				0, null, 0L));
			AssertLegal(Row(112, 0, 1, KingdomDeliveryPhase.SourceDebitPrepared,
				1, Digest, 1L));
			AssertLegal(Row(113, 0, 1, KingdomDeliveryPhase.InFlight,
				1, Digest, 2L));
			AssertLegal(Row(114, 0, 1, KingdomDeliveryPhase.LandedAwaitingOwner,
				1, Digest, 3L));
			AssertLegal(Row(115, 0, 1, KingdomDeliveryPhase.Quarantined,
				0, null, 0L));
			AssertLegal(Row(116, 0, 1, KingdomDeliveryPhase.Quarantined,
				1, Digest, 2L));
			Assert.AreEqual(3L, KingdomJobRules.DeliveryCapacityLoad(
				KingdomDeliveryCargoAuthority.ConstructionInput,
				KingdomStockKind.OpaqueManifest, 99, 3));
		}

		[Test]
		public void InvalidCargoRangesAndParentCollisionsAreRefused()
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomJobTable.TryCreate(new[]
			{
				Row(121, 0, 1, KingdomDeliveryPhase.InFlight, 1, Digest, 1L,
					KingdomStockKind.Water)
			}, out table, out fault), "water vessels remain opaque object cargo centrally");
			Assert.IsFalse(KingdomJobTable.TryCreate(new[]
			{
				Row(122, 0, 1, KingdomDeliveryPhase.Planned, 1, Digest, 1L)
			}, out table, out fault));
			Assert.IsFalse(KingdomJobTable.TryCreate(new[]
			{
				Row(123, 0, 2, KingdomDeliveryPhase.InFlight, 1, Digest, 1L),
				Row(124, 1, 2, KingdomDeliveryPhase.InFlight, 1, Digest, 1L)
			}, out table, out fault), "one parent ordinal cannot enter two trips");
			Assert.IsFalse(KingdomJobTable.TryCreate(new[]
			{
				Row(125, 0, KingdomLogisticsRules.CarrierCapacity + 1,
					KingdomDeliveryPhase.InFlight, 1, Digest, 1L)
			}, out table, out fault));
			Assert.IsTrue(KingdomJobTable.TryCreate(new[]
			{
				Row(126, 0, 1, KingdomDeliveryPhase.InFlight, 1, Digest, 1L),
				Row(127, 0, 1, KingdomDeliveryPhase.InFlight, 1, Digest, 1L,
					KingdomStockKind.OpaqueManifest, "construction-input:parent:2")
			}, out table, out fault), "global ordinals are scoped by parent owner id");
		}

		private static void AssertLegal(KingdomJobRow row)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobTable.TryCreate(new[] { row }, out table, out fault),
				row.DeliveryPhase + ": " + fault);
		}

		private static KingdomJobRow Row(int jobId, int sourceStart, int sourceCount,
			KingdomDeliveryPhase phase, int version, string digest, long revision,
			KingdomStockKind cargo = KingdomStockKind.OpaqueManifest, string owner = Owner)
		{
			KingdomLeg[] legs = new KingdomLeg[]
			{
				new KingdomLeg(SourceZone, 40, 12, 79, 12, 39, 100L, 139L),
				new KingdomLeg(TargetZone, 0, 12, 40, 12, 40, 139L, 179L)
			};
			return new KingdomJobRow(jobId, KingdomJobKind.Delivery, cargo, sourceCount,
				SourceZone, TargetZone, 100L, 1, KingdomJobStatus.Open, 0, 1, legs, 2,
				deliverySourceEndpointId: 101, deliverySourceObjectId: "",
				deliverySourceX: 40, deliverySourceY: 12,
				deliveryTargetEndpointId: 202, deliveryTargetObjectId: "",
				deliveryTargetX: 40, deliveryTargetY: 12,
				deliveryTripId: jobId, deliveryStopOrdinal: 1, deliveryPhase: phase,
				deliveryCargoAuthority: KingdomDeliveryCargoAuthority.ConstructionInput,
				deliveryOwnerOperationId: owner,
				deliveryOwnerManifestVersion: version,
				deliveryOwnerManifestDigest: digest,
				deliveryOwnerManifestRevision: revision,
				deliveryManifestSourceStart: sourceStart,
				deliveryManifestSourceCount: sourceCount);
		}
	}
}
#endif
