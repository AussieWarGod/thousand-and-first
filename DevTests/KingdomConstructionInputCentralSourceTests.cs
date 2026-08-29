#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomConstructionInputCentralSourceTests
	{
		[Test]
		public void AuthorityTwoMovesOnlyOpaqueExactObjects()
		{
			string source = KingdomConstructionInputCentralLogicalSource.Read();
			StringAssert.Contains("KingdomStockKind.OpaqueManifest", source);
			StringAssert.Contains("KingdomDeliveryCargoAuthority.ConstructionInput", source);
			StringAssert.Contains("deliveryManifestSourceStart: sourceStart", source);
			StringAssert.Contains("deliveryManifestSourceCount: sourceCount", source);
			StringAssert.DoesNotContain("KingdomStockKind.Water", source);
			StringAssert.DoesNotContain("KingdomDeliveryCargoAuthority.ScalarStock", source);
			StringAssert.DoesNotContain("StoreIn(", source);
			StringAssert.DoesNotContain("MaterialStock.Put", source);
			StringAssert.DoesNotContain("KingdomMaterials.", source);
			StringAssert.DoesNotContain("TryDebitScalar", source);
			StringAssert.DoesNotContain("GameObject.Create", source);
			StringAssert.Contains("target.AddObject(body", source);
			StringAssert.DoesNotContain(".RemoveObject(", source);
		}

		[Test]
		public void ReservationIsNeutralAndOwnerAdoptionIsOneExactRewrite()
		{
			string source = KingdomConstructionInputCentralLogicalSource.Read();
			string prepare = Between(source,
				"internal static bool TryPrepareConstructionInputReservation(",
				"private static List<KingdomJobRow> ConstructionInputRows(");
			StringAssert.Contains("deliveryPhase: KingdomDeliveryPhase.ReservationPrepared", prepare);
			StringAssert.Contains("new KingdomManifestReservation(new[] { jobId }", prepare);
			StringAssert.DoesNotContain("deliveryOwnerManifestVersion:", prepare);
			StringAssert.DoesNotContain("deliveryOwnerManifestDigest:", prepare);
			StringAssert.DoesNotContain("deliveryOwnerManifestRevision:", prepare);

			string activate = Between(source,
				"internal static bool TryActivateConstructionInputReservations(",
				"internal static bool TryCancelConstructionInputReservations(");
			StringAssert.Contains("ownerRows.Count == jobIds.Length", activate);
			StringAssert.Contains("jobIds[j] == jobIds[i] || tripIds[j] == tripIds[i]", activate);
			StringAssert.Contains("last.ArriveTick != arrivalTicks[i]", activate);
			StringAssert.Contains("row.WithManifestAuthority(manifestVersion, manifestDigest", activate);
			StringAssert.Contains("KingdomDeliveryPhase.SourceDebitPrepared", activate);
			StringAssert.Contains("table.TryRewrite(activated, activated.Length", activate);
		}

		[Test]
		public void GlobalRangesAreIdempotentAndCancellationCannotSweepAnOwner()
		{
			string source = KingdomConstructionInputCentralLogicalSource.Read();
			StringAssert.Contains("ConstructionInputRangesOverlap", source);
			StringAssert.Contains("SameConstructionInputReservation", source);
			StringAssert.Contains("end = (long)row.DeliveryManifestSourceStart", source);
			StringAssert.Contains("sourceOrdinal < row.DeliveryManifestSourceStart", source);
			string cancel = Between(source,
				"internal static bool TryCancelConstructionInputReservations(",
				"internal static bool TryConstructionInputTrip(");
			StringAssert.Contains("int[] reservationJobIds", cancel);
			StringAssert.Contains("row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared", cancel);
			StringAssert.Contains("table.TryClose(found[i]", cancel);
			StringAssert.DoesNotContain("ConstructionInputRows(table", cancel);
			StringAssert.DoesNotContain("TryCloseTrip", cancel);
		}

		[Test]
		public void NeutralCrashRowsAreSweptUnlessAParentReceiptProtectsThem()
		{
			string source = KingdomConstructionInputCentralLogicalSource.Read();
			string sweep = source.Substring(source.IndexOf(
				"internal static bool TrySweepUnadoptedConstructionInputOwner(",
				StringComparison.Ordinal));
			StringAssert.Contains("TrySweepOrphanedConstructionInputReservations", sweep);
			StringAssert.Contains("protectedOwners.Contains", sweep);
			StringAssert.Contains("KingdomDeliveryPhase.ReservationPrepared", sweep);
			StringAssert.Contains("row.DeliveryOwnerManifestVersion != 0", sweep);
			StringAssert.Contains("bindings.Holds(row.DeliveryTripId", sweep);
			StringAssert.Contains("ConstructionInputTransitRootExists", sweep);
			StringAssert.Contains("LookupRetirement", sweep);
			StringAssert.Contains("table.TryClose(close[i]", sweep);
			StringAssert.DoesNotContain("RetireCentralCarrier", sweep);
			StringAssert.DoesNotContain("Obliterate", sweep);
		}

		[Test]
		public void CustodyTransitionsDoNotLandIntoAnEconomicStore()
		{
			string source = KingdomConstructionInputCentralLogicalSource.Read();
			Ordered(source,
				"TryAcknowledgeConstructionInputPickup(",
				"KingdomDeliveryPhase.InFlight",
				"TryMaterializeConstructionInputArrival(",
				"target.AddObject(body",
				"TryAcknowledgeConstructionInputLanded(",
				"KingdomDeliveryPhase.LandedAwaitingOwner",
				"TryCloseConstructionInputTrip(");
			StringAssert.Contains("TryConstructionInputCarrierAtTarget", source);
			StringAssert.Contains("ReferenceEquals(body.CurrentCell, target)", source);
			StringAssert.Contains("TryExactTransitRoot", source);
			StringAssert.Contains("ReleaseTransitRoot", source);
			StringAssert.DoesNotContain("The.ZoneManager.GetZone(binding.ZoneId)", source);
			StringAssert.Contains("ownerReceiptProvesCargoReleased", source);
			StringAssert.Contains("provedManifestVersion", source);
			StringAssert.Contains("provedManifestDigest", source);
			StringAssert.Contains("provedManifestRevision", source);
		}

		[Test]
		public void RouteProofCanonicalizesEveryFrozenLegAndEndpoint()
		{
			string source = KingdomConstructionInputCentralLogicalSource.Read();
			string proof = source.Substring(source.IndexOf(
				"internal readonly struct KingdomConstructionInputRouteProof",
				StringComparison.Ordinal));
			foreach (string field in new[] { "JobId", "TripId", "CargoStart", "CargoCount",
				"SourceEndpointId", "SourceObjectId", "SourceZoneId", "SourceX", "SourceY",
				"TargetEndpointId", "TargetObjectId", "TargetZoneId", "TargetX", "TargetY",
				"ArrivalTick", "RouteDigest" }) StringAssert.Contains(field, proof);
			StringAssert.Contains("TAF-CONSTRUCTION-ROUTE-1", proof);
			StringAssert.Contains("writer.Write(row.DeliveryManifestSourceStart)", proof);
			StringAssert.Contains("for (int i = 0; i < row.LegCount; i++)", proof);
			StringAssert.Contains("leg.PathLength", proof);
			StringAssert.Contains("leg.DepartTick", proof);
			StringAssert.Contains("leg.ArriveTick", proof);
			StringAssert.Contains("KernelDigest.TryComputeSha256", proof);
			StringAssert.Contains("KernelDigest.ToLowercaseHex", proof);
		}

		[Test]
		public void QuarantinePreservesBodyCargoAndBinding()
		{
			string source = KingdomConstructionInputCentralLogicalSource.Read();
			// The quarantine pair closes its own shard; bound the region at that shard's class
			// end so the law is read from the quarantine code and not from every later file.
			string quarantine = Between(source,
				"internal static bool TryQuarantineConstructionInputOwner(", "\n\t}\n}");
			StringAssert.Contains("QuarantineConstructionInputOwner(system, table,", quarantine);
			StringAssert.Contains("WithDeliveryPhase(KingdomDeliveryPhase.Quarantined)", quarantine);
			StringAssert.Contains("system.Jobs.TryPublish", quarantine);
			StringAssert.DoesNotContain("RetireCentralCarrier", quarantine);
			StringAssert.DoesNotContain("Unbind", quarantine);
			StringAssert.DoesNotContain("Inventory", quarantine);
			StringAssert.DoesNotContain("RemoveObject", quarantine);
			StringAssert.DoesNotContain("Obliterate", quarantine);
		}

		[Test]
		public void ActivationMismatchNeverQuarantinesOrRewritesOwnerRows()
		{
			string source = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomCentralLogistics.10.ConstructionInputActivationAndTripView.cs");
			string activation = Between(source,
				"internal static bool TryActivateConstructionInputReservations(",
				"internal static bool TryCancelConstructionInputReservations(");
			StringAssert.DoesNotContain("QuarantineConstructionInputOwner", activation);
			Ordered(activation, "if (!exact)", "KingdomCityFault.DuplicateBinding", "return false;");
		}

		[Test]
		public void UndebitedOwnerReleaseIsExactAtomicAndProofGated()
		{
			string source = KingdomConstructionInputCentralLogicalSource.Read();
			string release = Between(source,
				"internal static bool TryReleaseUndebitedConstructionInputOwner(",
				"internal static bool TryCloseCancelledConstructionInputOwner(");
			StringAssert.Contains("ownerReceiptProvesAllCargoReleasedAndSourcesRestored", release);
			StringAssert.Contains("non-attended legacy entry point", release);
			StringAssert.Contains("KingdomCityFault.OutsideItinerary", release);
			StringAssert.DoesNotContain("RetireCentralCarrier", release);
			StringAssert.DoesNotContain("TryClose(", release);
		}

		[Test]
		public void CancelCloseWaitsForAttendedCarrierRetirement()
		{
			string source = KingdomConstructionInputCentralLogicalSource.Read();
			string close = Between(source,
				"internal static bool TryCloseCancelledConstructionInputOwner(",
				"internal static bool TryQuarantineConstructionInputOwner(");
			Ordered(close, "system.Bindings.TryRead", "bindings.Holds(trips[i]",
				"ConstructionInputTransitRootExists", "table.TryClose(rows[i].JobId",
				"system.Jobs.TryPublish(table");
			StringAssert.DoesNotContain("TryRetireConstructionInputTransitCarrier", close);
			StringAssert.DoesNotContain("RetireCentralCarrier", close);
		}

		[Test]
		public void ConstructionInputShardsStayBounded()
		{
			for (int i = 0; i < KingdomConstructionInputCentralLogicalSource.Paths.Length; i++)
			{
				string source = TestMain.ReadRepositoryText(
					KingdomConstructionInputCentralLogicalSource.Paths[i]);
				Assert.LessOrEqual(source.Split('\n').Length, 300,
					KingdomConstructionInputCentralLogicalSource.Paths[i]);
			}
		}

		private static string Between(string source, string start, string end)
		{
			int from = source.IndexOf(start, StringComparison.Ordinal);
			int to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
			Assert.GreaterOrEqual(from, 0, start);
			Assert.Greater(to, from, end);
			return source.Substring(from, to - from);
		}

		private static void Ordered(string source, params string[] markers)
		{
			int position = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], position + 1, StringComparison.Ordinal);
				Assert.Greater(next, position, markers[i]);
				position = next;
			}
		}
	}
}
#endif
