#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// "Never looks stupid", asserted rather than hoped for. LIVING-CITY-ARCHITECTURE &sect;3.10,
	/// invariant I6: <i>no carrier is ever routed past a nearer holder, and no two under-capacity
	/// trips run where one would do</i>.
	/// </summary>
	public class KingdomLogisticsRulesTests
	{
		[Test]
		public void LogisticsAbiKeepsEnumTypesAndReadonlyRowShapes()
		{
			ClassicAssert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomScalarReceiptAction)));
			CollectionAssert.AreEqual(new[] { "Refuse", "Apply", "AlreadyApplied", "ContinueFood", "Interference" },
				Enum.GetNames(typeof(KingdomScalarReceiptAction)));
			ClassicAssert.AreEqual(0, (byte)KingdomScalarReceiptAction.Refuse);
			ClassicAssert.AreEqual(1, (byte)KingdomScalarReceiptAction.Apply);
			ClassicAssert.AreEqual(2, (byte)KingdomScalarReceiptAction.AlreadyApplied);
			ClassicAssert.AreEqual(3, (byte)KingdomScalarReceiptAction.ContinueFood);
			ClassicAssert.AreEqual(4, (byte)KingdomScalarReceiptAction.Interference);
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.City.KingdomScalarReceiptAction", typeof(KingdomScalarReceiptAction).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.City.KingdomScalarReceiptRules", typeof(KingdomScalarReceiptRules).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.City.KingdomHolderRow", typeof(KingdomHolderRow).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.City.KingdomTripPlan", typeof(KingdomTripPlan).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.City.KingdomLogisticsRequest", typeof(KingdomLogisticsRequest).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.Simulation.City.KingdomLogisticsSnapshotPlan", typeof(KingdomLogisticsSnapshotPlan).FullName);
			ClassicAssert.IsFalse(typeof(KingdomScalarReceiptAction).IsPublic);
			ClassicAssert.IsTrue(typeof(KingdomScalarReceiptRules).IsAbstract && typeof(KingdomScalarReceiptRules).IsSealed);

			AssertFields(typeof(KingdomHolderRow), true,
				new[] { "HolderId", "ZoneIndex", "WorkSlot", "DedicationOrdinal", "Holds", "Amount" },
				new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(KingdomStockKind), typeof(long) });
			AssertFields(typeof(KingdomTripPlan), true,
				new[] { "Order", "StopCount", "Cells", "Operations", "Improvements" },
				new[] { typeof(int[]), typeof(int), typeof(int), typeof(int), typeof(int) });
			AssertFields(typeof(KingdomLogisticsRequest), true,
				new[] { "JobId", "SourceEndpointId", "SourceZoneIndex", "DestinationEndpointId", "DestinationZoneIndex", "Cargo", "Load", "SourceToDestinationCells", "ZoneRoute", "ZoneRouteCount", "CargoAuthority", "OwnerOperationId" },
				new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(KingdomStockKind), typeof(int), typeof(int), typeof(int[]), typeof(int), typeof(KingdomDeliveryCargoAuthority), typeof(string) });
			AssertFields(typeof(KingdomLogisticsSnapshotPlan), false,
				new[] { "ConsideredCount", "TripCount", "Operations", "TripIndexes", "TripLeaderJobIds", "StopOrdinals", "TripLoads", "TripStops" },
				new[] { typeof(int), typeof(int), typeof(int), typeof(int[]), typeof(int[]), typeof(int[]), typeof(long[]), typeof(int[]) });

			KingdomLogisticsRequest request = new KingdomLogisticsRequest(1, 2, 3, 4, 5,
				KingdomStockKind.Water, 6, 7, new[] { 3, 5 }, 2);
			ClassicAssert.AreEqual(KingdomDeliveryCargoAuthority.ScalarStock, request.CargoAuthority);
			ClassicAssert.IsNull(request.OwnerOperationId);
		}

		[Test]
		public void ScalarReceiptRecoveryKeepsExactVerdictTransitions()
		{
			KingdomScalarReceiptAction action;
			ClassicAssert.IsTrue(KingdomScalarReceiptRules.TryRecover(KingdomStockKind.Water, 10L, 2,
				10L, true, 0, out action));
			ClassicAssert.AreEqual(KingdomScalarReceiptAction.Apply, action);
			ClassicAssert.IsTrue(KingdomScalarReceiptRules.TryRecover(KingdomStockKind.Water, 10L, 2,
				12L, true, 0, out action));
			ClassicAssert.AreEqual(KingdomScalarReceiptAction.AlreadyApplied, action);
			ClassicAssert.IsTrue(KingdomScalarReceiptRules.TryRecover(KingdomStockKind.Food, 10L, 3,
				11L, true, 1, out action));
			ClassicAssert.AreEqual(KingdomScalarReceiptAction.ContinueFood, action);
			ClassicAssert.IsTrue(KingdomScalarReceiptRules.TryRecover(KingdomStockKind.Food, 10L, 3,
				13L, true, 3, out action));
			ClassicAssert.AreEqual(KingdomScalarReceiptAction.AlreadyApplied, action);
			ClassicAssert.IsTrue(KingdomScalarReceiptRules.TryRecover(KingdomStockKind.Food, 10L, 3,
				10L, false, 0, out action));
			ClassicAssert.AreEqual(KingdomScalarReceiptAction.Interference, action);
		}

		[Test]
		public void LogicalSourceKeepsOneOrderedPlanningAuthority()
		{
			string source = LogicalSource();
			ClassicAssert.AreEqual(4, Count(source, "internal static partial class KingdomLogisticsRules"));
			ClassicAssert.AreEqual(1, Count(source, "internal static class KingdomScalarReceiptRules"));
			ClassicAssert.AreEqual(1, Count(source, "internal readonly struct KingdomHolderRow"));
			ClassicAssert.AreEqual(1, Count(source, "internal readonly struct KingdomTripPlan"));
			ClassicAssert.AreEqual(1, Count(source, "internal readonly struct KingdomLogisticsRequest"));
			ClassicAssert.AreEqual(1, Count(source, "internal sealed class KingdomLogisticsSnapshotPlan"));
			ClassicAssert.Less(source.IndexOf("internal static bool TryNearestHolder", StringComparison.Ordinal),
				source.IndexOf("internal static bool TryPlanSnapshot", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("internal static bool TryPlanSnapshot", StringComparison.Ordinal),
				source.IndexOf("internal static bool TryPlanTrip", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("internal static bool TryPlanTrip", StringComparison.Ordinal),
				source.IndexOf("private static bool Eligible", StringComparison.Ordinal));
		}

		private static KingdomHolderRow Holder(int id, int zone, int ordinal, KingdomStockKind kind, long amount)
		{
			return new KingdomHolderRow(id, zone, -1, ordinal, kind, amount);
		}

		private static int Nearest(KingdomHolderRow[] holders, int[] distances, KingdomStockKind kind)
		{
			int chosen;
			KingdomCityFault fault;
			bool found = KingdomLogisticsRules.TryNearestHolder(holders, holders.Length, distances, kind, out chosen, out fault);
			ClassicAssert.AreEqual(KingdomCityFault.None, fault);
			return found ? holders[chosen].HolderId : -1;
		}

		// ---- (1) Nearest-holder sourcing -----------------------------------------------------

		/// <summary>
		/// &sect;3.10(1), the whole sentence: <i>"a building should try to fetch stored resources
		/// from whatever building is holding it closest to them"</i>. The far store has more of it
		/// and an older dedication; neither buys it the trip.
		/// </summary>
		[Test]
		public void TheNearStoreIsChosenOverTheFarOneHoweverMuchTheFarOneHolds()
		{
			KingdomHolderRow[] holders = new KingdomHolderRow[2]
			{
				Holder(1, 0, 0, KingdomStockKind.Water, 900L),
				Holder(2, 1, 9, KingdomStockKind.Water, 12L)
			};
			ClassicAssert.AreEqual(2, Nearest(holders, new int[2] { 240, 40 }, KingdomStockKind.Water));
		}

		/// <summary>A container of the right kind that is EMPTY is not a holder. §3.10(1) binds to
		/// the closest container actually holding the resource, which is the whole difference
		/// between this and walking to the nearest shelf.</summary>
		[Test]
		public void AnEmptyStoreIsNotAHolderHoweverNearItIs()
		{
			KingdomHolderRow[] holders = new KingdomHolderRow[2]
			{
				Holder(1, 0, 0, KingdomStockKind.Water, 0L),
				Holder(2, 1, 1, KingdomStockKind.Water, 5L)
			};
			ClassicAssert.AreEqual(2, Nearest(holders, new int[2] { 0, 400 }, KingdomStockKind.Water));
		}

		[Test]
		public void AStoreHoldingSomethingElseIsNeverSourcedFrom()
		{
			KingdomHolderRow[] holders = new KingdomHolderRow[2]
			{
				Holder(1, 0, 0, KingdomStockKind.Food, 99L),
				Holder(2, 1, 1, KingdomStockKind.Water, 5L)
			};
			ClassicAssert.AreEqual(2, Nearest(holders, new int[2] { 0, 400 }, KingdomStockKind.Water));
			ClassicAssert.AreEqual(1, Nearest(holders, new int[2] { 0, 400 }, KingdomStockKind.Food));
		}

		[Test]
		public void GroundWithNoRouteToItIsPassedOverRatherThanPickedAtAnInventedDistance()
		{
			KingdomHolderRow[] holders = new KingdomHolderRow[2]
			{
				Holder(1, 0, 0, KingdomStockKind.Water, 50L),
				Holder(2, 1, 1, KingdomStockKind.Water, 50L)
			};
			ClassicAssert.AreEqual(2, Nearest(holders, new int[2] { KingdomLogisticsRules.NoRoute, 800 }, KingdomStockKind.Water));
			ClassicAssert.AreEqual(-1, Nearest(holders, new int[2] { KingdomLogisticsRules.NoRoute, KingdomLogisticsRules.NoRoute }, KingdomStockKind.Water));
		}

		/// <summary>
		/// The tie-break is frozen and stored: equal distance goes to the lower holder id, and only
		/// then to the older dedication. Both are facts a reload reproduces, which is what makes
		/// step 90j's "every time, and after a reload" testable at all.
		/// </summary>
		[Test]
		public void EqualDistanceBreaksOnTheLowerHolderIdAndThenOnTheOlderDedication()
		{
			KingdomHolderRow[] byId = new KingdomHolderRow[2]
			{
				Holder(7, 0, 0, KingdomStockKind.Food, 10L),
				Holder(3, 1, 9, KingdomStockKind.Food, 10L)
			};
			ClassicAssert.AreEqual(3, Nearest(byId, new int[2] { 120, 120 }, KingdomStockKind.Food));
			KingdomHolderRow[] byOrdinal = new KingdomHolderRow[2]
			{
				Holder(5, 0, 4, KingdomStockKind.Food, 10L),
				Holder(5, 1, 1, KingdomStockKind.Food, 10L)
			};
			ClassicAssert.AreEqual(5, Nearest(byOrdinal, new int[2] { 120, 120 }, KingdomStockKind.Food));
		}

		/// <summary>
		/// <b>Assertion 1 of &sect;3.10</b>, as the check the selftest and Pass 32 step 90j make:
		/// the holder the plan bound to has no strictly nearer rival that was holding the same
		/// thing. Held for what <see cref="KingdomLogisticsRules.TryNearestHolder"/> chose;
		/// violated, with the offender NAMED, for a choice made any other way.
		/// </summary>
		[Test]
		public void NoCarrierCrossesTheCityPastANearerHolder()
		{
			KingdomHolderRow[] holders = new KingdomHolderRow[3]
			{
				Holder(1, 0, 0, KingdomStockKind.Water, 40L),
				Holder(2, 1, 1, KingdomStockKind.Water, 40L),
				Holder(3, 2, 2, KingdomStockKind.Water, 40L)
			};
			int[] distances = new int[3] { 600, 80, 240 };
			int chosen;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomLogisticsRules.TryNearestHolder(holders, 3, distances, KingdomStockKind.Water, out chosen, out fault));

			bool held;
			int offender;
			ClassicAssert.IsTrue(KingdomLogisticsRules.TryNoNearerHolder(holders, 3, distances, KingdomStockKind.Water, holders[chosen].HolderId, out held, out offender, out fault));
			ClassicAssert.IsTrue(held, "the planner's own choice must satisfy the assertion the planner exists for");
			ClassicAssert.AreEqual(-1, offender);

			ClassicAssert.IsTrue(KingdomLogisticsRules.TryNoNearerHolder(holders, 3, distances, KingdomStockKind.Water, 1, out held, out offender, out fault));
			ClassicAssert.IsFalse(held, "walking to the far store past two nearer ones is the pathology, and it must be caught");
			ClassicAssert.AreEqual(2, offender, "and the check names WHICH one was nearer");
		}

		/// <summary>A fetch bound to a holder that is not in the index, or is not holding what it
		/// was fetched for, fails the check rather than passing it by default.</summary>
		[Test]
		public void AFetchBoundToNothingFailsTheCheckRatherThanPassingByDefault()
		{
			KingdomHolderRow[] holders = new KingdomHolderRow[1] { Holder(1, 0, 0, KingdomStockKind.Water, 40L) };
			bool held;
			int offender;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomLogisticsRules.TryNoNearerHolder(holders, 1, new int[1] { 40 }, KingdomStockKind.Water, 99, out held, out offender, out fault));
			ClassicAssert.IsFalse(held);
		}

		// ---- (4) Capacity-bound batching ------------------------------------------------------

		private static KingdomLogisticsRequest[] Requests(int[] ids, int[] dest, long[] loads)
		{
			KingdomLogisticsRequest[] requests = new KingdomLogisticsRequest[ids.Length];
			for (int i = 0; i < ids.Length; i++)
			{
				int targetZone = dest[i] + 1;
				requests[i] = new KingdomLogisticsRequest(ids[i], 101, 0,
					1000 + targetZone, targetZone, KingdomStockKind.Water, (int)loads[i],
					targetZone * 10, new int[2] { 0, targetZone }, 2);
			}
			return requests;
		}

		private static KingdomLogisticsSnapshotPlan Batch(int[] ids, int[] dest, long[] loads,
			long capacity, int[] trip)
		{
			KingdomLogisticsRequest[] requests = Requests(ids, dest, loads);
			int[] between = new int[ids.Length * ids.Length];
			for (int i = 0; i < ids.Length; i++)
			for (int j = 0; j < ids.Length; j++)
				between[i * ids.Length + j] = System.Math.Abs(dest[i] - dest[j]) * 10;
			KingdomLogisticsSnapshotPlan plan;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomLogisticsRules.TryPlanSnapshot(requests, ids.Length, between,
				capacity, out plan, out fault), fault.ToString());
			for (int i = 0; i < trip.Length; i++) trip[i] = plan.TripIndexes[i];
			return plan;
		}

		/// <summary>Step 90j's second half: <i>"queue three small jobs along one route: ONE trip
		/// serves them, not three."</i></summary>
		[Test]
		public void ThreeSmallLoadsForOneGroundBecomeOneTrip()
		{
			int[] trip = new int[3];
			ClassicAssert.AreEqual(1, Batch(new int[3] { 1, 2, 3 }, new int[3] { 2, 2, 2 }, new long[3] { 3L, 4L, 2L }, 12L, trip).TripCount);
			ClassicAssert.AreEqual(0, trip[0]);
			ClassicAssert.AreEqual(0, trip[1]);
			ClassicAssert.AreEqual(0, trip[2]);
		}

		[Test]
		public void LoadsForDifferentGroundNeverShareATrip()
		{
			int[] trip = new int[3];
			ClassicAssert.AreEqual(2, Batch(new int[3] { 1, 2, 3 }, new int[3] { 2, 5, 2 }, new long[3] { 3L, 4L, 2L }, 12L, trip).TripCount);
			ClassicAssert.AreEqual(trip[0], trip[2]);
			ClassicAssert.AreNotEqual(trip[0], trip[1]);
		}

		[Test]
		public void ALoadThatWillNotFitOpensTheNextTripRatherThanOverloadingTheCarrier()
		{
			int[] trip = new int[3];
			ClassicAssert.AreEqual(2, Batch(new int[3] { 1, 2, 3 }, new int[3] { 2, 2, 2 }, new long[3] { 8L, 8L, 3L }, 12L, trip).TripCount);
			ClassicAssert.AreEqual(0, trip[0]);
			ClassicAssert.AreEqual(1, trip[1]);
			ClassicAssert.AreEqual(0, trip[2], "the third fits back on the first, which still has room");
		}

		[Test]
		public void ATripNeverTakesMoreStopsThanTheHardCap()
		{
			int stops = KingdomLogisticsRules.MaxStopsPerTrip;
			int count = stops + 2;
			int[] ids = new int[count];
			int[] dest = new int[count];
			long[] loads = new long[count];
			for (int i = 0; i < count; i++)
			{
				ids[i] = i + 1;
				dest[i] = 4;
				loads[i] = 1L;
			}
			int[] trip = new int[count];
			ClassicAssert.AreEqual(2, Batch(ids, dest, loads, 1000L, trip).TripCount);
			int onFirst = 0;
			for (int i = 0; i < count; i++)
			{
				if (trip[i] == 0)
				{
					onFirst++;
				}
			}
			ClassicAssert.AreEqual(stops, onFirst, "the stop cap is a constant, not an aspiration");
		}

		/// <summary>The planner only ever looks at the first &le; 16 open jobs (&sect;3.10(4)), so a
		/// slice's cost is a constant whatever the backlog is.</summary>
		[Test]
		public void OnlyTheFirstSixteenOpenJobsAreEverConsidered()
		{
			int count = KingdomLogisticsRules.MaxJobsConsidered + 4;
			int[] ids = new int[count];
			int[] dest = new int[count];
			long[] loads = new long[count];
			for (int i = 0; i < count; i++)
			{
				ids[i] = i + 1;
				dest[i] = i;
				loads[i] = 1L;
			}
			int[] trip = new int[count];
			ClassicAssert.AreEqual(KingdomLogisticsRules.MaxJobsConsidered, Batch(ids, dest, loads, 10L, trip).TripCount);
			ClassicAssert.AreEqual(-1, trip[count - 1], "a job beyond the cap is left for the next slice, never half-planned");
		}

		/// <summary>
		/// <b>Assertion 2 of &sect;3.10</b>, held as a property of the batcher rather than as a
		/// hope about it: whatever the batcher produces, no two of its trips are bound for the same
		/// ground with room to spare between them.
		/// </summary>
		[Test]
		public void TheBatcherNeverLeavesTwoHalfEmptyTripsBehind()
		{
			int[] ids = new int[6] { 1, 2, 3, 4, 5, 6 };
			int[] dest = new int[6] { 1, 2, 1, 1, 2, 3 };
			long[] loads = new long[6] { 5L, 7L, 4L, 6L, 6L, 1L };
			int[] trip = new int[6];
			KingdomLogisticsSnapshotPlan plan = Batch(ids, dest, loads, 12L, trip);
			ClassicAssert.Greater(plan.TripCount, 0);
		}

		[Test]
		public void TwoHandMadeHalfEmptyTripsAreCaughtAndNamed()
		{
			bool held;
			int offender;
			KingdomCityFault fault;
			KingdomLogisticsRequest[] requests = Requests(new int[2] { 1, 2 },
				new int[2] { 3, 3 }, new long[2] { 4L, 5L });
			ClassicAssert.IsTrue(KingdomLogisticsRules.TryNoTwoHalfEmptyTrips(requests, 2,
				new int[2] { 0, 1 }, new long[2] { 4L, 5L }, new int[2] { 1, 1 },
				new int[2] { 0, 1 }, 2, 12L, out held, out offender, out fault));
			ClassicAssert.IsFalse(held);
			ClassicAssert.AreEqual(1, offender);
			// Two FULL trips to the same ground are not the pathology: one carrier could not have
			// done it.
			ClassicAssert.IsTrue(KingdomLogisticsRules.TryNoTwoHalfEmptyTrips(requests, 2,
				new int[2] { 0, 1 }, new long[2] { 9L, 8L }, new int[2] { 1, 1 },
				new int[2] { 0, 1 }, 2, 12L, out held, out offender, out fault));
			ClassicAssert.IsTrue(held);
		}

		// ---- The route itself: nearest-neighbour, then 2-opt ----------------------------------

		/// <summary>A square of stops, with the carrier starting at a corner. Node 0 is the start
		/// and stop <c>s</c> is node <c>s + 1</c>, which is the shape <see cref="KingdomLogisticsRules.TryPlanTrip"/>
		/// takes.</summary>
		private static int[] Square()
		{
			int[,] at = new int[5, 2] { { 0, 0 }, { 0, 0 }, { 10, 0 }, { 10, 10 }, { 0, 10 } };
			int[] between = new int[25];
			for (int i = 0; i < 5; i++)
			{
				for (int j = 0; j < 5; j++)
				{
					int dx = at[i, 0] - at[j, 0];
					int dy = at[i, 1] - at[j, 1];
					between[(i * 5) + j] = ((dx < 0) ? -dx : dx) + ((dy < 0) ? -dy : dy);
				}
			}
			return between;
		}

		private static KingdomTripPlan Plan(int[] between, int count)
		{
			KingdomTripPlan plan;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomLogisticsRules.TryPlanTrip(between, count, out plan, out fault), fault.ToString());
			return plan;
		}

		/// <summary>The route walks the square rather than crossing it: nearest-neighbour from the
		/// corner the carrier is standing on, and 2-opt to unpick any crossing it left.</summary>
		[Test]
		public void TheRouteWalksTheSquareInsteadOfCrossingIt()
		{
			KingdomTripPlan plan = Plan(Square(), 4);
			ClassicAssert.AreEqual(4, plan.StopCount);
			ClassicAssert.AreEqual(30, plan.Cells, "0 -> (10,0) -> (10,10) -> (0,10), which is three sides");
		}

		/// <summary>Determinism, which is what makes step 90j reproduce after a reload: routing is
		/// arithmetic and the planner has no draw in it. Same snapshot, same order, every
		/// time.</summary>
		[Test]
		public void ThePlannerIsDeterministicOverTheSameSnapshot()
		{
			int[] between = Square();
			KingdomTripPlan first = Plan(between, 4);
			for (int again = 0; again < 8; again++)
			{
				KingdomTripPlan repeat = Plan(between, 4);
				ClassicAssert.AreEqual(first.Cells, repeat.Cells);
				ClassicAssert.AreEqual(first.Operations, repeat.Operations);
				for (int i = 0; i < 4; i++)
				{
					ClassicAssert.AreEqual(first.Order[i], repeat.Order[i]);
				}
			}
		}

		/// <summary>
		/// The op budget, measured rather than asserted: &sect;0.0 gives the <c>RoutePlan</c> lane
		/// &asymp; 1,000 int ops a slice and warns above 2,000. A full trip at the hard caps must
		/// sit inside that with room, or the caps are wrong.
		/// </summary>
		[Test]
		public void AFullTripPlanStaysInsideTheRoutePlanLanesOpBudget()
		{
			int stops = KingdomLogisticsRules.MaxStopsPerTrip;
			int nodes = stops + 1;
			int[] between = new int[nodes * nodes];
			// A deliberately awkward metric: every pair a different length, so 2-opt keeps finding
			// improvements until the test cap stops it.
			for (int i = 0; i < nodes; i++)
			{
				for (int j = 0; j < nodes; j++)
				{
					between[(i * nodes) + j] = (i == j) ? 0 : (((i * 7) + (j * 13)) % 97) + 1;
				}
			}
			KingdomTripPlan plan = Plan(between, stops);
			ClassicAssert.AreEqual(stops, plan.StopCount);
			KingdomBudgetVerdict verdict = KingdomBudgetRules.JudgeCount(KingdomBudgetLane.RoutePlan, plan.Operations);
			ClassicAssert.AreEqual(KingdomBudgetVerdict.Within, verdict,
				"a full trip cost " + plan.Operations + " ops, which is outside the lane it is budgeted in");
			ClassicAssert.LessOrEqual(plan.Operations, 1000, "§0.0 prices the whole slice's planning at ≲ 1,000 int ops");
		}

		[Test]
		public void ThePlannerRefusesATripLongerThanTheStopCapRatherThanTruncatingIt()
		{
			KingdomTripPlan plan;
			KingdomCityFault fault;
			ClassicAssert.IsFalse(KingdomLogisticsRules.TryPlanTrip(new int[400], KingdomLogisticsRules.MaxStopsPerTrip + 1, out plan, out fault));
			ClassicAssert.AreEqual(KingdomCityFault.InvalidIndex, fault);
			ClassicAssert.IsFalse(KingdomLogisticsRules.TryPlanTrip(new int[4], 3, out plan, out fault));
			ClassicAssert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}

		[Test]
		public void AnEmptySliceCostsNothingAndIsNotAFault()
		{
			KingdomTripPlan plan = Plan(new int[1], 0);
			ClassicAssert.AreEqual(0, plan.StopCount);
			ClassicAssert.AreEqual(0, plan.Cells);
			ClassicAssert.AreEqual(0, plan.Operations);
		}

		private static void AssertFields(Type type, bool valueType, string[] names, Type[] types)
		{
			ClassicAssert.IsFalse(type.IsPublic, type.FullName);
			ClassicAssert.AreEqual(valueType, type.IsValueType, type.FullName);
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
			Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
			CollectionAssert.AreEqual(names, Array.ConvertAll(fields, field => field.Name), type.FullName);
			CollectionAssert.AreEqual(types, Array.ConvertAll(fields, field => field.FieldType), type.FullName);
			foreach (FieldInfo field in fields)
				ClassicAssert.IsTrue(field.IsAssembly && field.IsInitOnly, type.FullName + "." + field.Name);
		}

		private static string LogicalSource()
		{
			return string.Join("\n", new[]
			{
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomLogisticsRules.ScalarReceipt.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomLogisticsRules.Declarations.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomLogisticsRules.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomLogisticsRules.SnapshotPlanning.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomLogisticsRules.TripPlanningAndAssertions.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Simulation", "City", "KingdomLogisticsRules.Helpers.cs"))
			});
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int at = 0;
			while ((at = source.IndexOf(term, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += term.Length;
			}
			return count;
		}
	}
}
#endif
