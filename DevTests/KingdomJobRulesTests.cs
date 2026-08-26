#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The porter and the itinerary behind them. LIVING-CITY-ARCHITECTURE §3.7: a job is a timed
	/// itinerary computed once at creation, one pure function answers where the carrier is and what
	/// is on them at any tick, and <b>every zone renders that same answer</b> — which is I5, and
	/// which is why the body never has to literally traverse anything.
	/// <para>
	/// The two acceptance tests §3.7 names are here as
	/// <see cref="PorterDeliveredIntoMyZoneLandsOnceAndLeavesByTheEdgeTheyCameIn"/> (Pass 32 step
	/// 90d) and <see cref="FollowThePorterComesOutJustInsideTheEntryEdge"/> (step 90d2), <i>"the
	/// harder of the two: a handoff that pops is a visible failure of I5 even when every number is
	/// right."</i>
	/// </para>
	/// </summary>
	internal class KingdomJobRulesTests
	{
		private const string Here = "JoppaWorld.11.22.1.1.10";

		private const string Westward = "JoppaWorld.11.22.0.1.10";

		private const long Start = 1000L;

		private const short EntryX = 0;

		private const short EntryY = 12;

		private const short StoreX = 40;

		private const short StoreY = 12;

		/// <summary>The itinerary a delivery into the attended zone gets: in by the west edge to the
		/// store, back out by the same edge, and on into the ground the load came from.</summary>
		private static KingdomJobRow Delivery(int cargo)
		{
			short mirrorX;
			short mirrorY;
			KingdomJobRules.Mirror(EntryX, EntryY, KingdomZoneStep.West, KingdomJobRules.ZoneWidth, KingdomJobRules.ZoneHeight, out mirrorX, out mirrorY);
			KingdomLegPlan[] plans = new KingdomLegPlan[3]
			{
				new KingdomLegPlan(Here, EntryX, EntryY, StoreX, StoreY, KingdomItineraryRules.SinuosityBuiltPercent, KingdomItineraryRules.NoRoadDiscountPercent),
				new KingdomLegPlan(Here, StoreX, StoreY, EntryX, EntryY, KingdomItineraryRules.SinuosityBuiltPercent, KingdomItineraryRules.NoRoadDiscountPercent),
				new KingdomLegPlan(Westward, mirrorX, mirrorY, 40, 12, KingdomItineraryRules.SinuosityOpenPercent, KingdomItineraryRules.NoRoadDiscountPercent)
			};
			KingdomLeg[] legs;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobRules.TryBuildLegs(plans, 3, Start, KingdomItineraryRules.WalkTicksPerCellDefault, out legs, out fault));
			return new KingdomJobRow(7, KingdomJobKind.Delivery, KingdomStockKind.Food, cargo, Westward, Here,
				Start, KingdomItineraryRules.WalkTicksPerCellDefault, KingdomJobStatus.Open, 1, 0, legs, 3);
		}

		private static KingdomItineraryFix At(KingdomJobRow job, long tick)
		{
			KingdomItineraryFix fix;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryAt(job.Legs(), job.LegCount, tick, out fix, out fault), "tick " + tick);
			return fix;
		}

		// ==================================================================================
		// The two acceptance tests §3.7 names
		// ==================================================================================

		/// <summary>
		/// <b>Pass 32 step 90d — the porter.</b> Stand in your house in zone A while a farm in zone
		/// B finishes harvesting: a porter walks in at the edge, crosses to the larder beside you,
		/// puts the real crop items in it, and leaves.
		/// <para>
		/// The model's half of that, asserted: the carrier enters at the edge cell holding the whole
		/// load, is carrying it right up to the tick the deposit leg ends, is carrying nothing from
		/// that tick on, and the load leaves the debt <b>exactly once</b> — which is I2, the
		/// one-event-two-renderings invariant, in arithmetic.
		/// </para>
		/// </summary>
		[Test]
		public void PorterDeliveredIntoMyZoneLandsOnceAndLeavesByTheEdgeTheyCameIn()
		{
			KingdomJobRow job = Delivery(9);
			KingdomLeg inbound;
			KingdomLeg outbound;
			Assert.IsTrue(job.TryLeg(0, out inbound));
			Assert.IsTrue(job.TryLeg(1, out outbound));

			KingdomItineraryFix arriving = At(job, Start);
			Assert.AreEqual(Here, arriving.ZoneId, "the carrier is minted in the ground the founder is standing in");
			Assert.AreEqual(EntryX, arriving.X);
			Assert.AreEqual(EntryY, arriving.Y);
			Assert.AreEqual(KingdomItineraryPhase.EnRoute, arriving.Phase);
			Assert.AreEqual(9, KingdomJobRules.CargoAt(job, arriving), "they walk in holding the whole load");

			KingdomItineraryFix justBefore = At(job, inbound.ArriveTick - 1L);
			Assert.AreEqual(9, KingdomJobRules.CargoAt(job, justBefore));
			Assert.IsFalse(KingdomJobRules.Deposited(job, inbound.ArriveTick - 1L));

			Assert.IsTrue(KingdomJobRules.Deposited(job, inbound.ArriveTick), "the load lands at the end of the deposit leg");
			KingdomItineraryFix leaving = At(job, inbound.ArriveTick);
			Assert.AreEqual(0, KingdomJobRules.CargoAt(job, leaving), "and never twice");
			Assert.AreEqual(0, KingdomJobRules.CargoAt(job, At(job, outbound.ArriveTick - 1L)));
			Assert.AreEqual(0, KingdomJobRules.CargoAt(job, At(job, outbound.ArriveTick)));

			// They leave by the edge they came in by. The road home is the road they walked, so the
			// exit cell is not a second choice and needs no second draw.
			Assert.AreEqual(inbound.EnterX, outbound.ExitX);
			Assert.AreEqual(inbound.EnterY, outbound.ExitY);
			Assert.AreEqual(inbound.ExitX, outbound.EnterX);
			Assert.AreEqual(inbound.ExitY, outbound.EnterY);
		}

		/// <summary>
		/// <b>Pass 32 step 90d2 — follow the porter.</b> Walk out of the zone behind them: they exit
		/// by the correct edge cell, you come out beside them, and they are <i>just inside the entry
		/// edge, a cell or two along — not at the far wall and not standing on the boundary</i>.
		/// Walk faster and you catch them at the edge; dawdle and they are further on. No pop, no
		/// teleport.
		/// <para>
		/// The handoff needs no draw and cannot disagree with where the founder comes out, because
		/// leg k+1 begins at the cell the engine's own zone connection maps leg k's exit to
		/// (§3.7). The itinerary is contiguous, so there is no tick at which the carrier is nowhere.
		/// </para>
		/// </summary>
		[Test]
		public void FollowThePorterComesOutJustInsideTheEntryEdge()
		{
			KingdomJobRow job = Delivery(9);
			KingdomLeg home;
			KingdomLeg onward;
			Assert.IsTrue(job.TryLeg(1, out home));
			Assert.IsTrue(job.TryLeg(2, out onward));

			// The engine's own connection: leaving by the west wall arrives on the east wall of the
			// zone west of here, on the same row.
			Assert.AreEqual(KingdomJobRules.ZoneWidth - 1, onward.EnterX);
			Assert.AreEqual(home.ExitY, onward.EnterY);
			Assert.AreEqual(Westward, onward.ZoneId);
			Assert.AreEqual(home.ArriveTick, onward.DepartTick, "contiguous: there is no tick at which the carrier is nowhere");

			// Cross fast and they are right at the edge.
			KingdomItineraryFix quick = At(job, onward.DepartTick);
			Assert.AreEqual(Westward, quick.ZoneId);
			Assert.AreEqual(onward.EnterX, quick.X);
			Assert.AreEqual(0, quick.StepsTaken);

			// Cross a few turns later and they are a cell or two along — never at the far wall.
			KingdomItineraryFix beside = At(job, onward.DepartTick + 3L);
			Assert.AreEqual(Westward, beside.ZoneId);
			Assert.AreEqual(KingdomItineraryPhase.EnRoute, beside.Phase);
			Assert.IsTrue(beside.X < onward.EnterX, "they have moved off the boundary");
			Assert.IsTrue(onward.EnterX - beside.X <= 3, "a cell or two along, not a teleport");
			Assert.IsTrue(beside.X > onward.ExitX, "and nowhere near the far wall");

			// Dawdle and they are further on. Monotone, so following one never rubber-bands.
			KingdomItineraryFix dawdled = At(job, onward.DepartTick + 20L);
			Assert.IsTrue(dawdled.X < beside.X);
			Assert.IsTrue(dawdled.StepsTaken > beside.StepsTaken);

			// And every tick of the journey has exactly one answer, in exactly one zone.
			for (long tick = Start; tick <= onward.ArriveTick; tick++)
			{
				KingdomItineraryFix fix = At(job, tick);
				Assert.IsTrue(fix.ZoneId == Here || fix.ZoneId == Westward);
				Assert.AreNotEqual(KingdomItineraryPhase.Pending, fix.Phase);
			}
		}

		// ==================================================================================
		// Planning
		// ==================================================================================

		/// <summary>
		/// A leg's length at creation is Chebyshev × sinuosity × the road discount, in integer
		/// percent, with ZERO zone access. That is §3.7's absolute cost bound and the reason the
		/// estimate is a prior that reality corrects rather than a pathfind.
		/// </summary>
		[Test]
		public void ALegsLengthIsChebyshevTimesSinuosityTimesTheRoad()
		{
			KingdomLegPlan[] plans = new KingdomLegPlan[1]
			{
				new KingdomLegPlan(Here, 0, 0, 40, 10, KingdomItineraryRules.SinuosityBuiltPercent, KingdomItineraryRules.NoRoadDiscountPercent)
			};
			KingdomLeg[] legs;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobRules.TryBuildLegs(plans, 1, Start, 1, out legs, out fault));
			Assert.AreEqual(40 * KingdomItineraryRules.SinuosityBuiltPercent / 100, legs[0].PathLength);
			Assert.AreEqual(Start, legs[0].DepartTick);
			Assert.AreEqual(Start + legs[0].PathLength, legs[0].ArriveTick);
		}

		/// <summary>
		/// §3.10(3): the discount is applied identically to the estimate and to any measured length,
		/// so a road cannot make the two disagree. Here it is the estimate's half: the same leg
		/// along a road is shorter, and arrives sooner.
		/// </summary>
		[Test]
		public void LayingARoadShortensTheItineraryAndTheArrival()
		{
			KingdomLegPlan plain = new KingdomLegPlan(Here, 0, 0, 40, 10, KingdomItineraryRules.SinuosityBuiltPercent, KingdomItineraryRules.NoRoadDiscountPercent);
			KingdomLegPlan paved = new KingdomLegPlan(Here, 0, 0, 40, 10, KingdomItineraryRules.SinuosityBuiltPercent, KingdomItineraryRules.RoadDiscountPercent);
			KingdomLeg[] slow;
			KingdomLeg[] fast;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobRules.TryBuildLegs(new KingdomLegPlan[1] { plain }, 1, Start, 1, out slow, out fault));
			Assert.IsTrue(KingdomJobRules.TryBuildLegs(new KingdomLegPlan[1] { paved }, 1, Start, 1, out fast, out fault));
			Assert.IsTrue(fast[0].PathLength < slow[0].PathLength);
			Assert.IsTrue(fast[0].ArriveTick < slow[0].ArriveTick);
		}

		/// <summary>A carrier that arrives on the tick it departs has not walked. Zero cells still
		/// costs one tick, so no leg is ever instantaneous.</summary>
		[Test]
		public void NoLegIsInstantaneous()
		{
			KingdomLegPlan[] plans = new KingdomLegPlan[1] { new KingdomLegPlan(Here, 5, 5, 5, 5, 125, 100) };
			KingdomLeg[] legs;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobRules.TryBuildLegs(plans, 1, Start, 1, out legs, out fault));
			Assert.AreEqual(0, legs[0].PathLength);
			Assert.AreEqual(Start + 1L, legs[0].ArriveTick);
		}

		/// <summary>
		/// §3.7: a job that wants more than six legs is <b>refused at planning and told</b>. Never
		/// truncated — a truncated route is a carrier arriving somewhere else.
		/// </summary>
		[Test]
		public void AJourneyTooLongToCarryIsRefusedAndNeverTruncated()
		{
			KingdomLegPlan[] plans = new KingdomLegPlan[KingdomItineraryRules.MaxLegs + 1];
			for (int i = 0; i < plans.Length; i++)
			{
				plans[i] = new KingdomLegPlan("z" + i, 0, 0, 10, 10, 125, 100);
			}
			KingdomLeg[] legs;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomJobRules.TryBuildLegs(plans, plans.Length, Start, 1, out legs, out fault));
			Assert.AreEqual(KingdomCityFault.RowCapExceeded, fault);
			Assert.IsNull(legs, "a refusal publishes nothing");
		}

		/// <summary>The level-1 path is frozen whole: both endpoints and every ground crossed between
		/// them. A four-zone route therefore becomes the destination inbound leg plus four return
		/// legs; no intermediate zone may disappear behind a direct destination-to-source estimate.</summary>
		[Test]
		public void APorterPathKeepsEveryIntermediateZone()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[4]
			{
				new KingdomZoneNode("destination", 0, 0, 10),
				new KingdomZoneNode("middle-a", 1, 0, 10),
				new KingdomZoneNode("middle-b", 2, 0, 10),
				new KingdomZoneNode("source", 3, 0, 10)
			};
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, nodes.Length,
				KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int[] path;
			int count;
			Assert.IsTrue(KingdomJobRules.TryPorterPath(graph, "destination", "source",
				out path, out count, out fault));
			Assert.AreEqual(4, count);
			Assert.AreEqual(5, count + 1, "one inbound destination leg plus one leg for every path node");
			for (int i = 0; i < count; i++)
			{
				KingdomZoneNode node;
				Assert.IsTrue(graph.TryNode(path[i], out node));
				Assert.AreEqual(nodes[i].ZoneId, node.ZoneId, "path node " + i);
			}
		}

		/// <summary>A true six-node path needs seven legs once the destination inbound leg is added.
		/// The durable row holds six, so the planner refuses the whole journey instead of omitting a
		/// piece of ground.</summary>
		[Test]
		public void APorterPathThatCannotFitItsDurableRowIsRefusedWhole()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[6];
			for (int i = 0; i < nodes.Length; i++)
			{
				nodes[i] = new KingdomZoneNode("z" + i, i, 0, 10);
			}
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, nodes.Length,
				KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int[] path;
			int count;
			Assert.IsFalse(KingdomJobRules.TryPorterPath(graph, "z0", "z5",
				out path, out count, out fault));
			Assert.AreEqual(KingdomCityFault.RowCapExceeded, fault);
			Assert.IsNull(path, "a refusal publishes no shortened path");
			Assert.AreEqual(0, count);
		}

		/// <summary>Every mirrored cell lands on the opposite wall of the same row or column, which
		/// is what makes the handoff a fact rather than a choice.</summary>
		[TestCase(KingdomZoneStep.West, (short)0, (short)7, (short)79, (short)7)]
		[TestCase(KingdomZoneStep.East, (short)79, (short)7, (short)0, (short)7)]
		[TestCase(KingdomZoneStep.North, (short)33, (short)0, (short)33, (short)24)]
		[TestCase(KingdomZoneStep.South, (short)33, (short)24, (short)33, (short)0)]
		public void AnEdgeCellMapsToTheOppositeWall(KingdomZoneStep edge, short x, short y, short expectedX, short expectedY)
		{
			short mx;
			short my;
			KingdomJobRules.Mirror(x, y, edge, KingdomJobRules.ZoneWidth, KingdomJobRules.ZoneHeight, out mx, out my);
			Assert.AreEqual(expectedX, mx);
			Assert.AreEqual(expectedY, my);
		}

		/// <summary>
		/// The edge a carrier enters by is a FACT from two adjacent horizontal zone ids and never a
		/// draw, so it cannot disagree with where the founder comes out (§3.7). A non-neighbour,
		/// malformed id, or vertical shaft refuses: inventing a wall would make a false journey.
		/// </summary>
		[Test]
		public void TheEntryEdgeIsAFactAndNeverADraw()
		{
			Assert.AreEqual(KingdomZoneStep.West, KingdomJobRules.EdgeToward("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.0.1.10"));
			Assert.AreEqual(KingdomZoneStep.East, KingdomJobRules.EdgeToward("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.2.1.10"));
			Assert.AreEqual(KingdomZoneStep.North, KingdomJobRules.EdgeToward("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.0.10"));
			Assert.AreEqual(KingdomZoneStep.South, KingdomJobRules.EdgeToward("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.2.10"));
			Assert.AreEqual(KingdomZoneStep.None, KingdomJobRules.EdgeToward("JoppaWorld.11.22.1.1.10", "JoppaWorld.9.22.1.1.10"));
			Assert.AreEqual(KingdomZoneStep.None, KingdomJobRules.EdgeToward("JoppaWorld.11.22.1.1.10", null));
			Assert.AreEqual(KingdomZoneStep.None, KingdomJobRules.EdgeToward("nonsense", "JoppaWorld.11.22.0.1.10"));
			Assert.AreEqual(KingdomZoneStep.None, KingdomJobRules.EdgeToward("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.11"));
			short mirrorX;
			short mirrorY;
			Assert.IsFalse(KingdomJobRules.TryMirror(12, 7, KingdomZoneStep.Up, 80, 25,
				out mirrorX, out mirrorY), "a shaft uses its authored cell, never a mirrored wall");
		}

		// ==================================================================================
		// Determinism
		// ==================================================================================

		/// <summary>
		/// §3.7: the delivery lane, ordinal = the delivery's occurrence index. <b>The same delivery
		/// yields the same carrier</b> — which edge cell they walk in by, and where they say they
		/// are from — whether the founder watches it or reads about it afterwards. Same seed, same
		/// journey, every time.
		/// </summary>
		[Test]
		public void TheSameDeliveryDrawsTheSameCarrierEveryTime()
		{
			KernelSeed128 seed = new KernelSeed128(0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL);
			short firstX;
			short firstY;
			short againX;
			short againY;
			int firstOrigin;
			int againOrigin;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobRules.TryDrawEntryCell(seed, "taf:settlement:kavvat", 7, KingdomZoneStep.West, 80, 25, out firstX, out firstY, out fault));
			Assert.IsTrue(KingdomJobRules.TryDrawEntryCell(seed, "taf:settlement:kavvat", 7, KingdomZoneStep.West, 80, 25, out againX, out againY, out fault));
			Assert.AreEqual(firstX, againX);
			Assert.AreEqual(firstY, againY);
			Assert.IsTrue(KingdomJobRules.TryDrawOrigin(seed, "taf:settlement:kavvat", 7, 6, out firstOrigin, out fault));
			Assert.IsTrue(KingdomJobRules.TryDrawOrigin(seed, "taf:settlement:kavvat", 7, 6, out againOrigin, out fault));
			Assert.AreEqual(firstOrigin, againOrigin);
			Assert.IsTrue(firstOrigin >= 1 && firstOrigin <= 6);
		}

		/// <summary>
		/// A draw is a pure function of its coordinates, so a different job, a different city or a
		/// different realm is a different carrier — and adding or removing one draw cannot perturb
		/// the other, because each has its own semantic draw index.
		/// </summary>
		[Test]
		public void DifferentJobsAndDifferentRealmsDrawDifferently()
		{
			KernelSeed128 one = new KernelSeed128(1UL, 2UL);
			KernelSeed128 other = new KernelSeed128(3UL, 4UL);
			Assert.AreNotEqual(EntryCell(one, "taf:settlement:kavvat", 7), EntryCell(one, "taf:settlement:kavvat", 8));
			Assert.AreNotEqual(EntryCell(one, "taf:settlement:kavvat", 7), EntryCell(one, "taf:settlement:ubuk", 7));
			Assert.AreNotEqual(EntryCell(one, "taf:settlement:kavvat", 7), EntryCell(other, "taf:settlement:kavvat", 7));
		}

		private static int EntryCell(KernelSeed128 seed, string city, int jobId)
		{
			short x;
			short y;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobRules.TryDrawEntryCell(seed, city, jobId, KingdomZoneStep.West, 80, 25, out x, out y, out fault));
			return x * 1000 + y;
		}

		/// <summary>A drawn entry cell is on the named wall and never in a corner, because a corner
		/// cell is where two walls meet and the engine's connection maps it to neither.</summary>
		[Test]
		public void ADrawnEntryCellIsOnTheWallAndNeverInACorner()
		{
			KernelSeed128 seed = new KernelSeed128(11UL, 13UL);
			KingdomCityFault fault;
			for (int job = 1; job <= 64; job++)
			{
				short x;
				short y;
				Assert.IsTrue(KingdomJobRules.TryDrawEntryCell(seed, "taf:settlement:kavvat", job, KingdomZoneStep.West, 80, 25, out x, out y, out fault));
				Assert.AreEqual(0, x);
				Assert.IsTrue(y > 0 && y < 24, "job " + job + " drew a corner");
				Assert.IsTrue(KingdomJobRules.TryDrawEntryCell(seed, "taf:settlement:kavvat", job, KingdomZoneStep.North, 80, 25, out x, out y, out fault));
				Assert.AreEqual(0, y);
				Assert.IsTrue(x > 0 && x < 79, "job " + job + " drew a corner");
			}
		}

		/// <summary>A job id of zero is not an ordinal, and a zone too small to have a wall is not a
		/// zone. Both refuse rather than returning a corner.</summary>
		[Test]
		public void TheDrawsRefuseNonsenseRatherThanInventingACell()
		{
			KernelSeed128 seed = default(KernelSeed128);
			short x;
			short y;
			int origin;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomJobRules.TryDrawEntryCell(seed, "taf:settlement:kavvat", 0, KingdomZoneStep.West, 80, 25, out x, out y, out fault));
			Assert.IsFalse(KingdomJobRules.TryDrawEntryCell(seed, "taf:settlement:kavvat", 1, KingdomZoneStep.West, 2, 25, out x, out y, out fault));
			Assert.IsFalse(KingdomJobRules.TryDrawEntryCell(seed, "taf:settlement:kavvat", 1, KingdomZoneStep.None, 80, 25, out x, out y, out fault));
			Assert.IsFalse(KingdomJobRules.TryDrawEntryCell(seed, "taf:settlement:kavvat", 1, KingdomZoneStep.Down, 80, 25, out x, out y, out fault));
			Assert.IsFalse(KingdomJobRules.TryDrawOrigin(seed, "taf:settlement:kavvat", 1, 0, out origin, out fault));
			Assert.AreEqual(KingdomResidentRules.NoOrigin, origin);
		}

		// ==================================================================================
		// The table, and what closure means
		// ==================================================================================

		/// <summary>
		/// §3.8: a closed job is evicted at once, so <b>absence from the table is proof of
		/// closure</b>. There is no second "closed" list to keep in step with the first.
		/// </summary>
		[Test]
		public void AClosedJobIsEvictedAndThatIsTheWholeOfClosure()
		{
			KingdomJobTable table;
			KingdomJobTable next;
			KingdomJobRow closed;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobTable.TryCreate(new KingdomJobRow[0], out table, out fault));
			Assert.IsTrue(table.TryOpen(Delivery(4), out next, out fault));
			Assert.AreEqual(1, next.Count);
			Assert.IsTrue(next.Holds(7));
			Assert.IsTrue(next.TryClose(7, out table, out closed, out fault));
			Assert.AreEqual(0, table.Count);
			Assert.IsFalse(table.Holds(7));
			Assert.AreEqual(4, closed.CargoAmount, "the eviction hands back what the carrier was holding");
			Assert.IsFalse(table.TryClose(7, out next, out closed, out fault));
			Assert.AreEqual(KingdomCityFault.UnknownBinding, fault);
		}

		/// <summary>Copy-on-write: opening, replacing and closing publish a new table and leave the
		/// caller's byte-identical, so a refusal anywhere costs nothing.</summary>
		[Test]
		public void TheTableIsCopyOnWriteAndARefusalPublishesNothing()
		{
			KingdomJobTable table;
			KingdomJobTable opened;
			KingdomJobTable again;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobTable.TryCreate(new KingdomJobRow[0], out table, out fault));
			Assert.IsTrue(table.TryOpen(Delivery(4), out opened, out fault));
			Assert.AreEqual(0, table.Count, "the original table did not change");
			Assert.IsFalse(opened.TryOpen(Delivery(4), out again, out fault), "one job id, one job");
			Assert.AreEqual(KingdomCityFault.DuplicateBinding, fault);
			Assert.IsNull(again);

			KingdomJobRow landed = Delivery(4).WithCargoLanded();
			Assert.IsTrue(opened.TryReplace(landed, out again, out fault));
			KingdomJobRow read;
			Assert.IsTrue(again.TryGet(7, out read));
			Assert.AreEqual(0, read.CargoAmount);
			Assert.IsTrue(opened.TryGet(7, out read));
			Assert.AreEqual(4, read.CargoAmount, "the table it was derived from is untouched");
		}

		/// <summary>§3.8 caps open jobs at sixteen, realm-wide, and the cap is a refusal rather than
		/// a queue: the load is not lost, it is still on the road, which is where it already
		/// was.</summary>
		[Test]
		public void SixteenOpenJobsIsTheRealmsCeiling()
		{
			Assert.AreEqual(16, KingdomJobRules.MaxOpenJobs);
			KingdomJobRow[] rows = new KingdomJobRow[KingdomJobRules.MaxOpenJobs + 1];
			for (int i = 0; i < rows.Length; i++)
			{
				rows[i] = new KingdomJobRow(i + 1, KingdomJobKind.Delivery, KingdomStockKind.Food, 1, Westward, Here,
					Start, 1, KingdomJobStatus.Open, 1, 0, new KingdomLeg[0], 0);
			}
			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomJobTable.TryCreate(rows, out table, out fault));
			Assert.AreEqual(KingdomCityFault.RowCapExceeded, fault);
		}

		/// <summary>A porter is two reify units — one body mint and one container fill — out of the
		/// ordinary eight-unit budget (§3.6, §3.7).</summary>
		[Test]
		public void APorterIsTwoUnitsOfTheOrdinaryBudget()
		{
			Assert.AreEqual(2, KingdomJobRules.PorterUnits);
			Assert.IsTrue(KingdomJobRules.PorterUnits <= KingdomBudgetRules.ReifyUnitsPerTurn);
			Assert.IsTrue(KingdomJobRules.PorterUnits <= KingdomBudgetRules.ReifyHeavyMintsPerTurn + 1);
		}

		// ==================================================================================
		// The registry, and what a save can lose
		// ==================================================================================

		/// <summary>A job written out and read back is the same job, legs and all: the itinerary is
		/// what answers where a carrier is, so a save that lost a leg would be a save with a porter
		/// nobody can place.</summary>
		[Test]
		public void TheRegistryRoundTripsJobsAndTheirLegs()
		{
			KingdomJobRegistry registry = new KingdomJobRegistry();
			KingdomJobTable table;
			KingdomJobTable opened;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobTable.TryCreate(new KingdomJobRow[0], out table, out fault));
			Assert.IsTrue(table.TryOpen(Delivery(5), out opened, out fault));
			Assert.IsTrue(registry.TryPublish(opened, out fault));

			KingdomJobTable read;
			Assert.IsTrue(registry.TryRead(out read, out fault));
			Assert.AreEqual(1, read.Count);
			KingdomJobRow row;
			Assert.IsTrue(read.TryGet(7, out row));
			Assert.AreEqual(3, row.LegCount);
			Assert.AreEqual(5, row.CargoAmount);
			Assert.AreEqual(Westward, row.SourceZoneId);
			Assert.AreEqual(Here, row.DestZoneId);
			for (int i = 0; i < 3; i++)
			{
				KingdomLeg before;
				KingdomLeg after;
				Assert.IsTrue(Delivery(5).TryLeg(i, out before));
				Assert.IsTrue(row.TryLeg(i, out after));
				Assert.AreEqual(before.ZoneId, after.ZoneId);
				Assert.AreEqual(before.EnterX, after.EnterX);
				Assert.AreEqual(before.ExitY, after.ExitY);
				Assert.AreEqual(before.PathLength, after.PathLength);
				Assert.AreEqual(before.DepartTick, after.DepartTick);
				Assert.AreEqual(before.ArriveTick, after.ArriveTick);
			}
		}

		/// <summary>Ids are minted in order off the realm's counter and never reused, because a job
		/// id is the ordinal its draws hang off: a reused one would put two carriers on one
		/// journey.</summary>
		[Test]
		public void JobIdsAreMintedInOrderAndNeverReused()
		{
			KingdomJobRegistry registry = new KingdomJobRegistry();
			Assert.AreEqual(1, registry.MintJobId());
			Assert.AreEqual(2, registry.MintJobId());
			Assert.AreEqual(3, registry.MintJobId());
			Assert.AreEqual(3, registry.JobCounter);
		}

		/// <summary>
		/// A book read from an older save is repaired rather than trusted: ragged columns truncate,
		/// a job whose declared legs are not all present is dropped whole, and legs nobody claims go
		/// with it. Half an itinerary is a carrier with no answer to where they are.
		/// </summary>
		[Test]
		public void ARaggedRegistryIsRepairedAndNeverHalfRead()
		{
			KingdomJobRegistry registry = new KingdomJobRegistry();
			registry.JobIds.Add(1);
			registry.JobIds.Add(2);
			registry.Kinds.Add((int)KingdomJobKind.Delivery);
			registry.Kinds.Add((int)KingdomJobKind.Delivery);
			registry.Cargos.Add((int)KingdomStockKind.Food);
			registry.Cargos.Add((int)KingdomStockKind.Food);
			registry.CargoAmounts.Add(3);
			registry.CargoAmounts.Add(4);
			registry.SourceZoneIds.Add(Westward);
			registry.SourceZoneIds.Add(Westward);
			registry.DestZoneIds.Add(Here);
			registry.DestZoneIds.Add(Here);
			registry.StartTicks.Add(Start);
			registry.StartTicks.Add(Start);
			registry.WalkTicksPerCell.Add(1);
			registry.WalkTicksPerCell.Add(1);
			registry.Statuses.Add(0);
			registry.Statuses.Add(0);
			registry.OriginCodes.Add(1);
			registry.OriginCodes.Add(1);
			registry.DepositLegIndexes.Add(0);
			registry.DepositLegIndexes.Add(0);
			registry.LegCounts.Add(1);
			registry.LegCounts.Add(2);
			// One leg, for two jobs that between them claim three.
			registry.LegZoneIds.Add(Here);
			registry.LegEnterX.Add(0);
			registry.LegEnterY.Add(1);
			registry.LegExitX.Add(2);
			registry.LegExitY.Add(3);
			registry.LegLengths.Add(4);
			registry.LegDepartTicks.Add(Start);
			registry.LegArriveTicks.Add(Start + 4L);

			registry.Normalize();
			Assert.AreEqual(1, registry.Count, "the job whose legs were missing is gone whole");
			Assert.AreEqual(1, registry.JobIds[0]);
			Assert.AreEqual(1, registry.LegZoneIds.Count);

			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsTrue(registry.TryRead(out table, out fault));
			KingdomJobRow row;
			Assert.IsTrue(table.TryGet(1, out row));
			Assert.AreEqual(1, row.LegCount);
		}

		/// <summary>A registry that came back holding one job id twice is a registry that can put
		/// two carriers on one journey, so the duplicate is dropped and the first row wins.</summary>
		[Test]
		public void ADuplicateJobIdIsDropped()
		{
			KingdomJobRegistry registry = new KingdomJobRegistry();
			for (int i = 0; i < 2; i++)
			{
				registry.JobIds.Add(4);
				registry.Kinds.Add((int)KingdomJobKind.Delivery);
				registry.Cargos.Add((int)KingdomStockKind.Food);
				registry.CargoAmounts.Add(i + 1);
				registry.SourceZoneIds.Add(Westward);
				registry.DestZoneIds.Add(Here);
				registry.StartTicks.Add(Start);
				registry.WalkTicksPerCell.Add(1);
				registry.Statuses.Add(0);
				registry.OriginCodes.Add(1);
				registry.DepositLegIndexes.Add(0);
				registry.LegCounts.Add(0);
			}
			registry.Normalize();
			Assert.AreEqual(1, registry.Count);
			Assert.AreEqual(1, registry.CargoAmounts[0], "the first row wins because it is the one every earlier session answered with");
		}

		/// <summary>A null column is an absent named field and becomes an empty one, and a negative
		/// counter is not a counter.</summary>
		[Test]
		public void NullColumnsAndANegativeCounterAreRepaired()
		{
			KingdomJobRegistry registry = new KingdomJobRegistry();
			registry.JobIds = null;
			registry.LegZoneIds = null;
			registry.JobCounter = -5;
			registry.Normalize();
			Assert.AreEqual(0, registry.Count);
			Assert.AreEqual(0, registry.JobCounter);
			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsTrue(registry.TryRead(out table, out fault));
			Assert.AreEqual(0, table.Count);
		}

		/// <summary>
		/// <b>Pass 32 step 90d3 — stand in their way.</b> §3.7's re-projection rule: <i>only the
		/// unstarted remainder of an itinerary may move.</i> A leg already begun keeps its
		/// <c>DepartTick</c>; the current leg's <c>ArriveTick</c> and every later leg shift by the
		/// same signed delta. So a porter body-blocked for ten turns arrives ten turns later and
		/// everything downstream shifts by ten — no rubber-banding, no catch-up sprint, no time
		/// travel.
		/// </summary>
		[Test]
		public void BlockingAPorterShiftsTheRemainderAndNeverTheHistory()
		{
			KingdomJobRow job = Delivery(9);
			KingdomLeg[] before = job.Legs();
			KingdomLeg[] after;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryReproject(before, job.LegCount, 0, 10L, out after, out fault));

			Assert.AreEqual(before[0].DepartTick, after[0].DepartTick, "a leg already begun keeps its departure");
			Assert.AreEqual(before[0].ArriveTick + 10L, after[0].ArriveTick);
			for (int i = 1; i < job.LegCount; i++)
			{
				Assert.AreEqual(before[i].DepartTick + 10L, after[i].DepartTick);
				Assert.AreEqual(before[i].ArriveTick + 10L, after[i].ArriveTick);
			}
			// The whole journey is exactly ten ticks longer. Nothing sprints to make it up.
			long was = before[job.LegCount - 1].ArriveTick - before[0].DepartTick;
			long now = after[job.LegCount - 1].ArriveTick - after[0].DepartTick;
			Assert.AreEqual(was + 10L, now);
			Assert.IsTrue(KingdomItineraryRules.TryValidate(after, job.LegCount, out fault));
			Assert.AreEqual(before[0].ArriveTick, job.Legs()[0].ArriveTick, "copy-on-write: the row it came from is untouched");
		}

		/// <summary>
		/// Block one indefinitely and the job stops being re-projectable and starts being a story:
		/// §3.7 fails a job whose elapsed exceeds <b>twice</b> its projected duration.
		/// </summary>
		[Test]
		public void BlockingAPorterForeverFailsTheJobRatherThanExtendingItForever()
		{
			KingdomJobRow job = Delivery(9);
			KingdomLeg last;
			Assert.IsTrue(job.TryLeg(job.LegCount - 1, out last));
			long projected = last.ArriveTick - Start;
			bool overrun;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryHasOverrun(job.Legs(), job.LegCount, last.ArriveTick, out overrun, out fault));
			Assert.IsFalse(overrun, "arriving on time is not an overrun");
			Assert.IsTrue(KingdomItineraryRules.TryHasOverrun(job.Legs(), job.LegCount,
				Start + KingdomItineraryRules.FailAtProjectedDurationMultiple * projected + 1L, out overrun, out fault));
			Assert.IsTrue(overrun);
			Assert.AreEqual(2, KingdomItineraryRules.FailAtProjectedDurationMultiple);
		}

		[Test]
		public void RegistryPublicSaveColumnsKeepTheirExactMetadataShapeAndOrder()
		{
			Type registry = typeof(KingdomJobRegistry);
			Assert.AreEqual("ThousandAndFirst.Simulation.City.KingdomJobRegistry",
				registry.FullName);
			Assert.IsTrue(registry.IsPublic);
			Assert.IsTrue(registry.IsDefined(typeof(SerializableAttribute), false));
			Assert.IsFalse(registry.IsSealed);

			System.Reflection.FieldInfo[] fields = registry.GetFields(
				System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.Public);
			Array.Sort(fields, delegate(System.Reflection.FieldInfo left,
				System.Reflection.FieldInfo right)
			{
				return left.MetadataToken.CompareTo(right.MetadataToken);
			});
			string[] expected = new string[]
			{
				"JobCounter", "JobIds", "Kinds", "Cargos", "CargoAmounts",
				"SourceZoneIds", "DestZoneIds", "StartTicks", "WalkTicksPerCell",
				"Statuses", "OriginCodes", "DepositLegIndexes", "SubjectIds",
				"SubjectNames", "TargetNames", "DueTicks", "WaterCosts",
				"ProvisionCosts", "OutcomeCodes", "DeliverySourceEndpointIds",
				"DeliverySourceObjectIds", "DeliverySourceXs", "DeliverySourceYs",
				"DeliveryTargetEndpointIds", "DeliveryTargetObjectIds",
				"DeliveryTargetXs", "DeliveryTargetYs", "DeliverySourceBeforeAmounts",
				"DeliveryTripIds", "DeliveryStopOrdinals", "DeliveryPhases",
				"DeliveryCargoAuthorityKinds", "DeliveryOwnerOperationIds",
				"DeliveryOwnerManifestVersions", "DeliveryOwnerManifestDigests",
				"DeliveryOwnerManifestRevisions", "DeliveryManifestSourceStarts",
				"DeliveryManifestSourceCounts", "DeliveryTargetBeforeAmounts",
				"DeliveryTargetReceiptStates", "LegCounts", "LegZoneIds",
				"LegEnterX", "LegEnterY", "LegExitX", "LegExitY", "LegLengths",
				"LegDepartTicks", "LegArriveTicks"
			};
			Assert.AreEqual(expected.Length, fields.Length);
			for (int i = 0; i < expected.Length; i++)
				Assert.AreEqual(expected[i], fields[i].Name, "field " + i);
			Assert.AreEqual(typeof(int), fields[0].FieldType);
			Assert.AreEqual(typeof(System.Collections.Generic.List<int>),
				fields[1].FieldType);
			Assert.AreEqual(typeof(System.Collections.Generic.List<string>),
				fields[5].FieldType);
			Assert.AreEqual(typeof(System.Collections.Generic.List<long>),
				fields[7].FieldType);
		}

		[Test]
		public void JobAndDeliveryEnumMetadataRemainAppendOnly()
		{
			Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomJobKind)));
			Assert.AreEqual(0, (int)KingdomJobKind.None);
			Assert.AreEqual(1, (int)KingdomJobKind.Delivery);
			Assert.AreEqual(2, (int)KingdomJobKind.Expedition);
			Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomJobStatus)));
			Assert.AreEqual(0, (int)KingdomJobStatus.Open);
			Assert.AreEqual(1, (int)KingdomJobStatus.Delivered);
			Assert.AreEqual(2, (int)KingdomJobStatus.Failed);
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomDeliveryPhase)));
			CollectionAssert.AreEqual(new int[] { 0, 1, 2, 3, 4, 5 },
				Array.ConvertAll((KingdomDeliveryPhase[])Enum.GetValues(
					typeof(KingdomDeliveryPhase)), value => (int)value));
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(
				typeof(KingdomDeliveryCargoAuthority)));
			CollectionAssert.AreEqual(new int[] { 0, 1 },
				Array.ConvertAll((KingdomDeliveryCargoAuthority[])Enum.GetValues(
					typeof(KingdomDeliveryCargoAuthority)), value => (int)value));
		}

		[Test]
		public void LogicalSourceKeepsDeclarationAndMutationOrder()
		{
			string source = KingdomJobRegistryLogicalSource.Read();
			string[] ordered = new string[]
			{
				"public enum KingdomJobKind : byte",
				"internal readonly partial struct KingdomJobRow",
				"internal static partial class KingdomJobRules",
				"internal sealed partial class KingdomJobTable",
				"public partial class KingdomJobRegistry",
				"public void Normalize()",
				"internal bool TryRead(out KingdomJobTable table",
				"internal bool TryPublish(KingdomJobTable table",
				"internal static class KingdomRealmJobWireFixture"
			};
			int prior = -1;
			for (int i = 0; i < ordered.Length; i++)
			{
				int at = source.IndexOf(ordered[i], StringComparison.Ordinal);
				Assert.Greater(at, prior, ordered[i]);
				prior = at;
			}
		}
	}
}
#endif
