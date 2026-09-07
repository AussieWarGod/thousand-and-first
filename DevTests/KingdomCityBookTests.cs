#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The carrier. LIVING-CITY-ARCHITECTURE §1.3: the rules layer stays frozen and total, the
	/// carrier is written by exactly one publisher in one assignment, and a book read out of a save
	/// is repaired rather than trusted. These tests are the round trip and the repairs.
	/// </summary>
	public class KingdomCityBookTests
	{
		private static KingdomStocks Stocks(long water, long waterCap, long food, long foodCap)
		{
			return new KingdomStocks(
				new KingdomStockPair(water, waterCap),
				new KingdomStockPair(food, foodCap),
				new KingdomStockPair(0L, 0L));
		}

		private static KingdomCityState Peopled()
		{
			KingdomZoneRow[] zones = new KingdomZoneRow[2]
			{
				new KingdomZoneRow("taf:zone:a", 3, 700L, Stocks(40L, 120L, 8L, 30L), 5, 2, 6, 7, -12, 3, 0),
				new KingdomZoneRow("taf:zone:b", 0, 900L, Stocks(11L, 60L, 0L, 12L), 1, 0, 2, 0, 0, 0, 0)
			};
			KingdomWorkRow[] works = new KingdomWorkRow[1]
			{
				new KingdomWorkRow(4242, "taf:zone:a", 12, 34, "taf:design:cistern", 87, 0, 800L,
					new KingdomWorkRunState(KingdomWorkKind.Growing, 2, 15, 1500L))
			};
			KingdomResidentRow[] residents = new KingdomResidentRow[1]
			{
				new KingdomResidentRow(9, "Ptoh", KingdomResidentRules.OriginCode("the moon"),
					3, 400L, 4242, 4242, 1, KingdomDayShape.Field,
					KingdomResidentStanding.Abroad, KingdomStandingCause.Followed, "taf:zone:b",
					new KingdomBrinkWindow(true, 410L, 415L),
					new KingdomBrinkWindow(true, 420L, KingdomBrinkRules.Unwarned), "Mechanimists", 1,
					KingdomCreedRules.EncodeKept(new List<string> { "Joppa", "Barathrumites" }),
					"the moon", "3 of Niv, 1000 AR")
			};
			KingdomClockRow[] clocks = new KingdomClockRow[1]
			{
				new KingdomClockRow(KingdomClockKind.Harvest, 1800L, 7)
			};
			KingdomCityState state;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 950L, Stocks(51L, 180L, 8L, 42L), zones, works, residents, clocks, out state, out fault), fault.ToString());
			KingdomCityState told;
			ClassicAssert.IsTrue(state.TryTell(new KingdomToldRow(KingdomToldKind.Harvest, 640L, 1, 2, "taf:zone:a", 3), out told, out fault));
			return told;
		}

		/// <summary>Every row family the model holds survives the round trip. A carrier that
		/// silently drops a family the state already carries is how a wave loses a city.</summary>
		[Test]
		public void EveryRowFamilyRoundTripsThroughTheColumns()
		{
			KingdomCityState before = Peopled();
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(before, out fault), fault.ToString());
			KingdomCityState after;
			ClassicAssert.IsTrue(book.TryRead(out after, out fault), fault.ToString());

			ClassicAssert.AreEqual(before.SettlementId, after.SettlementId);
			ClassicAssert.AreEqual(before.ProcessedThroughTick, after.ProcessedThroughTick);
			ClassicAssert.AreEqual(before.Stocks.Water.Level, after.Stocks.Water.Level);
			ClassicAssert.AreEqual(before.Stocks.Food.Capacity, after.Stocks.Food.Capacity);
			ClassicAssert.AreEqual(before.ZoneCount, after.ZoneCount);
			ClassicAssert.AreEqual(before.WorkCount, after.WorkCount);
			ClassicAssert.AreEqual(before.ResidentCount, after.ResidentCount);
			ClassicAssert.AreEqual(before.ClockCount, after.ClockCount);
			ClassicAssert.AreEqual(before.ToldCount, after.ToldCount);

			KingdomZoneRow zoneBefore;
			KingdomZoneRow zoneAfter;
			ClassicAssert.IsTrue(before.TryZone(0, out zoneBefore));
			ClassicAssert.IsTrue(after.TryZone(0, out zoneAfter));
			ClassicAssert.AreEqual(zoneBefore.ZoneId, zoneAfter.ZoneId);
			ClassicAssert.AreEqual(zoneBefore.DistrictCode, zoneAfter.DistrictCode);
			ClassicAssert.AreEqual(zoneBefore.LastReadTick, zoneAfter.LastReadTick);
			ClassicAssert.AreEqual(zoneBefore.Stocks.Water.Level, zoneAfter.Stocks.Water.Level);
			ClassicAssert.AreEqual(zoneBefore.Stocks.Food.Capacity, zoneAfter.Stocks.Food.Capacity);
			ClassicAssert.AreEqual(zoneBefore.Roofs, zoneAfter.Roofs);
			ClassicAssert.AreEqual(zoneBefore.Defence, zoneAfter.Defence);
			ClassicAssert.AreEqual(zoneBefore.WaterCarry, zoneAfter.WaterCarry);
			ClassicAssert.AreEqual(zoneBefore.FoodCarry, zoneAfter.FoodCarry);
			ClassicAssert.AreEqual(-12, zoneAfter.OwedWater, "a standing draw must survive the save; that is what makes it a debt");
			ClassicAssert.AreEqual(3, zoneAfter.OwedFood, "a landing and a draw stand at once on one row");

			KingdomWorkRow workBefore;
			KingdomWorkRow workAfter;
			ClassicAssert.IsTrue(before.TryWork(0, out workBefore));
			ClassicAssert.IsTrue(after.TryWork(0, out workAfter));
			ClassicAssert.AreEqual(workBefore.WorkId, workAfter.WorkId);
			ClassicAssert.AreEqual(workBefore.AnchorX, workAfter.AnchorX);
			ClassicAssert.AreEqual(workBefore.DesignKey, workAfter.DesignKey);
			ClassicAssert.AreEqual(workBefore.ConditionPercent, workAfter.ConditionPercent);
			ClassicAssert.AreEqual(workBefore.RunState.Kind, workAfter.RunState.Kind);
			ClassicAssert.AreEqual(workBefore.RunState.Stage, workAfter.RunState.Stage);
			ClassicAssert.AreEqual(workBefore.RunState.NextTick, workAfter.RunState.NextTick);

			KingdomResidentRow personBefore;
			KingdomResidentRow personAfter;
			ClassicAssert.IsTrue(before.TryResident(0, out personBefore));
			ClassicAssert.IsTrue(after.TryResident(0, out personAfter));
			ClassicAssert.AreEqual(personBefore.Name, personAfter.Name);
			ClassicAssert.AreEqual(personBefore.Standing, personAfter.Standing);
			ClassicAssert.AreEqual(personBefore.Cause, personAfter.Cause);
			ClassicAssert.AreEqual(personBefore.DayShape, personAfter.DayShape);
			ClassicAssert.AreEqual(personBefore.BoundZoneId, personAfter.BoundZoneId);
			ClassicAssert.AreEqual("the moon", personAfter.Origin,
				"arbitrary exact origin must not collapse to its closed code");
			ClassicAssert.AreEqual("3 of Niv, 1000 AR", personAfter.Arrived,
				"frozen presentation evidence survives save round-trip");
			// Both brink windows, in full. A carrier that round-tripped "a brink stands" but lost
			// the tick the window is anchored on would hand every warned settler a fresh deadline
			// on every save, which is the failure the columns were retyped to make impossible.
			ClassicAssert.AreEqual(personBefore.RoofBrink.Stands, personAfter.RoofBrink.Stands);
			ClassicAssert.AreEqual(personBefore.RoofBrink.ReachedTick, personAfter.RoofBrink.ReachedTick);
			ClassicAssert.AreEqual(personBefore.RoofBrink.WarnedTick, personAfter.RoofBrink.WarnedTick);
			ClassicAssert.AreEqual(personBefore.CreedBrink.Stands, personAfter.CreedBrink.Stands);
			ClassicAssert.AreEqual(personBefore.CreedBrink.ReachedTick, personAfter.CreedBrink.ReachedTick);
			ClassicAssert.AreEqual(personBefore.CreedBrink.WarnedTick, personAfter.CreedBrink.WarnedTick);
			ClassicAssert.AreEqual(personBefore.CreedToward, personAfter.CreedToward);
			ClassicAssert.AreEqual(personBefore.CreedChannel, personAfter.CreedChannel);
			// Addendum 16's recorded fact. A book that lost it would hand the alignment gate a city
			// whose people had never believed anything, which is the one state that HIDES designs
			// rather than refusing them -- so the loss would read as the works never having existed.
			ClassicAssert.AreEqual(personBefore.KeptCreeds, personAfter.KeptCreeds);
			CollectionAssert.AreEqual(new[] { "Joppa", "Barathrumites" }, KingdomCreedRules.DecodeKept(personAfter.KeptCreeds));

			KingdomToldRow toldBefore;
			KingdomToldRow toldAfter;
			ClassicAssert.IsTrue(before.TryTold(0, out toldBefore));
			ClassicAssert.IsTrue(after.TryTold(0, out toldAfter));
			ClassicAssert.AreEqual(toldBefore.Kind, toldAfter.Kind);
			ClassicAssert.AreEqual(toldBefore.Tick, toldAfter.Tick);
			ClassicAssert.AreEqual(toldBefore.PlaceZoneId, toldAfter.PlaceZoneId);
		}

		// ---- The brink storage layer (W2) -----------------------------------------------------

		/// <summary>
		/// The swap, at the column. <c>KingdomBrink</c>'s whole storage layer is now this pair of
		/// calls, so what the property bag used to hold has to round-trip through them exactly:
		/// three distinguishable states, both windows apart, and the creed the conversion at the
		/// end of the window will be picked from.
		/// </summary>
		[Test]
		public void ABrinkWrittenByIdReadsBackAsItWasWritten()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());

			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			ClassicAssert.IsTrue(book.TryReadBrink(9, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel));
			ClassicAssert.IsTrue(stands);
			ClassicAssert.AreEqual(410L, reached);
			ClassicAssert.AreEqual(415L, warned);
			ClassicAssert.IsNull(toward, "a roof brink has no creed");

			ClassicAssert.IsTrue(book.TryReadBrink(9, BrinkKind.Creed, out stands, out reached, out warned, out toward, out channel));
			ClassicAssert.IsTrue(stands);
			ClassicAssert.AreEqual(420L, reached);
			ClassicAssert.AreEqual(KingdomBrinkRules.Unwarned, warned, "a recorded brink nobody has been told about has no deadline");
			ClassicAssert.AreEqual("Mechanimists", toward);
			ClassicAssert.AreEqual(1, channel);
		}

		/// <summary>Warning somebody stamps the anchor and never redates their loss, and it reaches
		/// only the brink it was aimed at.</summary>
		[Test]
		public void WarningOneBrinkLeavesTheOtherWhereItWas()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			ClassicAssert.IsTrue(book.TryWriteBrink(9, BrinkKind.Creed, stands: true, 420L, 1000L, "Mechanimists", 1));

			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			ClassicAssert.IsTrue(book.TryReadBrink(9, BrinkKind.Creed, out stands, out reached, out warned, out toward, out channel));
			ClassicAssert.AreEqual(420L, reached);
			ClassicAssert.AreEqual(1000L, warned);
			ClassicAssert.IsTrue(book.TryReadBrink(9, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel));
			ClassicAssert.AreEqual(415L, warned, "warning a creed brink must not touch a roof brink");
		}

		/// <summary>A lifted brink leaves nothing behind for a later read to half-believe. Rule 2:
		/// if the cause returns the founder gets the whole window again.</summary>
		[Test]
		public void ALiftedBrinkClearsItsOwnFields()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			ClassicAssert.IsTrue(book.TryWriteBrink(9, BrinkKind.Creed, stands: false, 0L, KingdomBrinkRules.Unwarned, null, 0));

			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			ClassicAssert.IsTrue(book.TryReadBrink(9, BrinkKind.Creed, out stands, out reached, out warned, out toward, out channel));
			ClassicAssert.IsFalse(stands);
			ClassicAssert.AreEqual(0L, reached);
			ClassicAssert.AreEqual(KingdomBrinkRules.Unwarned, warned);
			ClassicAssert.IsNull(toward);
			ClassicAssert.AreEqual(0, channel);
		}

		/// <summary>A settler this book has no row for is not this book's to answer about — which is
		/// how the realm's other city gets asked next.</summary>
		[TestCase(0)]
		[TestCase(404)]
		public void ABookAnswersOnlyForItsOwnResidents(int residentId)
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			ClassicAssert.IsFalse(book.TryReadBrink(residentId, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel));
			ClassicAssert.IsFalse(book.TryWriteBrink(residentId, BrinkKind.Roof, stands: true, 1L, 2L, null, 0));
		}

		/// <summary>The realm's own brink is not a settler's, and asking a row for one is refused
		/// rather than answered with the roof's.</summary>
		[Test]
		public void ARowIsNotAskedForTheRealmsBrink()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			ClassicAssert.IsFalse(book.TryReadBrink(9, BrinkKind.City, out stands, out reached, out warned, out toward, out channel));
			ClassicAssert.IsFalse(book.TryWriteBrink(9, BrinkKind.City, stands: true, 1L, 2L, null, 0));
		}

		/// <summary>Ragged resident columns out of an older save are truncated to the shortest, and
		/// the brink accessors repair before they index rather than reading off the end of a
		/// column.</summary>
		[Test]
		public void RaggedResidentColumnsAreRepairedBeforeABrinkIsRead()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			book.ResidentIds.Add(11);
			book.ResidentNames.Add("Nobody");
			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			ClassicAssert.IsFalse(book.TryReadBrink(11, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel),
				"a row half of whose fields are missing is not a row");
			ClassicAssert.AreEqual(1, book.ResidentCount);
			ClassicAssert.IsTrue(book.TryReadBrink(9, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel));
			ClassicAssert.IsTrue(stands);
		}

		/// <summary>Schema-v2 saves predate exact origin/arrival presentation columns. Migration
		/// fills only what the old closed code proves and retains the resident row; it never parses a
		/// display date into a second clock.</summary>
		[Test]
		public void V2ResidentRowsGainPresentationColumnsWithoutBeingDropped()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			book.SchemaVersion = 2;
			book.ResidentOrigins.Clear();
			book.ResidentArrived.Clear();

			book.Normalize();

			ClassicAssert.AreEqual(KingdomCityRules.SchemaVersion, book.SchemaVersion);
			ClassicAssert.AreEqual(1, book.ResidentCount);
			KingdomCityState state;
			ClassicAssert.IsTrue(book.TryRead(out state, out fault), fault.ToString());
			ClassicAssert.IsTrue(state.TryResident(0, out KingdomResidentRow row));
			ClassicAssert.AreEqual("", row.Origin,
				"an arbitrary v2 origin cannot be invented from NoOrigin");
			ClassicAssert.AreEqual("", row.Arrived,
				"v2 stored only the tick; no presentation string may be invented");
			ClassicAssert.AreEqual(400L, row.ArrivedTick);
		}

		/// <summary>A standing and a cause that disagree are repaired toward the STANDING, because
		/// the standing is what every consumer branches on: a living settler must never carry a
		/// death clause into a memorial.</summary>
		[Test]
		public void AStandingAndACauseThatDisagreeAreRepairedTowardTheStanding()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			book.ResidentStandings[0] = (int)KingdomResidentStanding.Resident;
			book.Normalize();
			ClassicAssert.AreEqual((int)KingdomStandingCause.None, book.ResidentCauses[0]);

			book.ResidentStandings[0] = (int)KingdomResidentStanding.Dead;
			book.Normalize();
			ClassicAssert.AreEqual((int)KingdomStandingCause.Unwitnessed, book.ResidentCauses[0],
				"a death nobody witnessed is told as exactly that, never invented");
		}

		/// <summary>A book nobody has written to is an empty city, not a fault.</summary>
		[Test]
		public void AFreshBookReadsAsACityWithNothingInIt()
		{
			KingdomCityState state;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(new KingdomCityBook().TryRead(out state, out fault), fault.ToString());
			ClassicAssert.AreEqual(0, state.ZoneCount);
			ClassicAssert.AreEqual(0, state.RowCount);
			ClassicAssert.AreEqual(0L, state.ProcessedThroughTick);
		}

		/// <summary>Publishing twice leaves the book holding the SECOND state and nothing of the
		/// first. The columns are rewritten, never appended to.</summary>
		[Test]
		public void PublishingRewritesTheColumnsRatherThanAppendingToThem()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault));
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault));
			ClassicAssert.AreEqual(2, book.ZoneCount);
			ClassicAssert.AreEqual(1, book.WorkCount);
			ClassicAssert.AreEqual(1, book.ToldCount);
		}

		[Test]
		public void PublishingNothingIsRefusedAndTheBookIsUntouched()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault));
			ClassicAssert.IsFalse(book.TryPublish(null, out fault));
			ClassicAssert.AreEqual(KingdomCityFault.NullArgument, fault);
			ClassicAssert.AreEqual(2, book.ZoneCount, "a refused publish must leave the book byte-identical");
		}

		/// <summary>An absent named field arrives as a null column. It becomes an empty one rather
		/// than throwing inside the engine's block-skip recovery, which would cost the city.</summary>
		[Test]
		public void ANullColumnIsRepairedRatherThanThrown()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault));
			book.ZoneRoofs = null;
			book.ToldTicks = null;
			book.Normalize();
			ClassicAssert.AreEqual(0, book.ZoneCount, "a zone row missing a field is not a zone row");
			ClassicAssert.AreEqual(0, book.ToldCount);
		}

		/// <summary>
		/// Ragged columns are truncated to the shortest. A reader that trusted the longest column
		/// would invent a zone out of a default id, and nothing is invented for ground the game has
		/// never looked at.
		/// </summary>
		[Test]
		public void RaggedColumnsAreTruncatedToTheShortest()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault));
			book.ZoneIds.Add("taf:zone:ghost");
			book.ZoneDistrictCodes.Add(1);
			book.Normalize();
			ClassicAssert.AreEqual(2, book.ZoneCount);
			ClassicAssert.AreEqual("taf:zone:b", book.ZoneIds[1], "the half-written row is the one that goes");
		}

		/// <summary>No dimension of this model grows (§1.4). Rows past a cap are dropped on the
		/// way in rather than refused on the way out.</summary>
		[Test]
		public void RowsPastTheirCapAreDropped()
		{
			KingdomCityBook book = new KingdomCityBook();
			for (int i = 0; i < KingdomCityState.MaxZones + 3; i++)
			{
				book.ZoneIds.Add("taf:zone:" + i);
				book.ZoneDistrictCodes.Add(0);
				book.ZoneLastReadTicks.Add(100L + i);
				book.ZoneWaterLevels.Add(1L);
				book.ZoneWaterCapacities.Add(2L);
				book.ZoneFoodLevels.Add(0L);
				book.ZoneFoodCapacities.Add(0L);
				book.ZoneMaterialsLevels.Add(0L);
				book.ZoneMaterialsCapacities.Add(0L);
				book.ZoneRoofs.Add(0);
				book.ZoneDefences.Add(0);
				book.ZoneWaterCarries.Add(0);
				book.ZoneFoodCarries.Add(0);
				book.ZoneOwedWater.Add(0);
				book.ZoneOwedFood.Add(0);
				book.ZoneOwedMaterials.Add(0);
			}
			book.Normalize();
			ClassicAssert.AreEqual(KingdomCityState.MaxZones, book.ZoneCount);
		}

		/// <summary>The ring forgets its OLDEST lines, never its newest: a book that came back with
		/// more than the ring holds keeps the end of the story.</summary>
		[Test]
		public void AnOverlongToldLogKeepsItsNewestLines()
		{
			KingdomCityBook book = new KingdomCityBook();
			for (int i = 0; i < KingdomCityState.MaxToldEntries + 5; i++)
			{
				book.ToldKinds.Add((int)KingdomToldKind.Harvest);
				book.ToldTicks.Add(1000L + i);
				book.ToldSubjectsA.Add(i);
				book.ToldSubjectsB.Add(0);
				book.ToldPlaceZoneIds.Add("taf:zone:a");
				book.ToldOutcomes.Add(0);
			}
			book.Normalize();
			ClassicAssert.AreEqual(KingdomCityState.MaxToldEntries, book.ToldCount);
			ClassicAssert.AreEqual(1005L, book.ToldTicks[0], "the ring dropped the wrong end");
			ClassicAssert.AreEqual(1000L + KingdomCityState.MaxToldEntries + 4, book.ToldTicks[book.ToldCount - 1]);
		}

		/// <summary>A stamp below zero is a corrupt reading and not a model in debt: the book fails
		/// closed to "nothing reckoned yet" rather than refusing a whole city.</summary>
		[Test]
		public void ACorruptStampFailsClosedRatherThanRefusingTheCity()
		{
			KingdomCityBook book = new KingdomCityBook();
			book.ProcessedThroughTick = -5L;
			book.SettlementId = null;
			book.Normalize();
			ClassicAssert.AreEqual(0L, book.ProcessedThroughTick);
			ClassicAssert.AreEqual("", book.SettlementId);
			KingdomCityState state;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryRead(out state, out fault));
		}

		/// <summary>The lookup every re-plumbed sighting reader goes through.</summary>
		[Test]
		public void AZoneRowIsFoundByItsIdAndNotByPosition()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault));
			int index;
			ClassicAssert.IsTrue(book.TryZoneRow("taf:zone:b", out index));
			ClassicAssert.AreEqual(1, index);
			ClassicAssert.IsFalse(book.TryZoneRow("taf:zone:never", out index));
			ClassicAssert.IsFalse(book.TryZoneRow(null, out index));
		}

		[Test]
		public void PilgrimOpportunityNormalizesWithoutLosingItsFrozenIdentity()
		{
			KingdomCityBook book = new KingdomCityBook
			{
				PilgrimLoudness = 2,
				PilgrimState = (int)KingdomLocusRules.PilgrimState.Standing,
				PilgrimSequence = 7,
				PilgrimCauseTick = 12000L,
				PilgrimCause = "the Ides feast kept at Tamsketh",
				PilgrimObjectId = "body:pilgrim:7",
				PilgrimName = "Aeru",
				PilgrimPlaceName = "Tamsketh",
				PilgrimGreeted = 1
			};
			book.Normalize();
			ClassicAssert.AreEqual((int)KingdomLocusRules.PilgrimState.Standing, book.PilgrimState);
			ClassicAssert.AreEqual("body:pilgrim:7", book.PilgrimObjectId);
			ClassicAssert.AreEqual("Aeru", book.PilgrimName);
			ClassicAssert.AreEqual("Tamsketh", book.PilgrimPlaceName);
			ClassicAssert.AreEqual(1, book.PilgrimGreeted);
		}

		[Test]
		public void MalformedPilgrimOpportunityFailsClosedAndCannotMintADuplicateBody()
		{
			KingdomCityBook book = new KingdomCityBook
			{
				PilgrimLoudness = int.MaxValue,
				PilgrimState = 999,
				PilgrimSequence = -4,
				PilgrimCauseTick = -1L,
				PilgrimCause = "invented",
				PilgrimObjectId = "wrong body",
				PilgrimName = new string('x', KingdomLocusRules.MaxPilgrimNameChars + 1),
				PilgrimPlaceName = "wrong place",
				PilgrimGreeted = 8
			};
			book.Normalize();
			ClassicAssert.AreEqual(KingdomLocusRules.PilgrimStoryThreshold - 1,
				book.PilgrimLoudness);
			ClassicAssert.AreEqual((int)KingdomLocusRules.PilgrimState.None, book.PilgrimState);
			ClassicAssert.AreEqual(0, book.PilgrimSequence);
			ClassicAssert.AreEqual(0L, book.PilgrimCauseTick);
			ClassicAssert.AreEqual("", book.PilgrimCause);
			ClassicAssert.AreEqual("", book.PilgrimObjectId);
			ClassicAssert.AreEqual("", book.PilgrimName);
			ClassicAssert.AreEqual("", book.PilgrimPlaceName);
			ClassicAssert.AreEqual(0, book.PilgrimGreeted);
		}

		[Test]
		public void WaitingPilgrimCannotClaimAStaleBodyOrGreetedOutcome()
		{
			KingdomCityBook book = new KingdomCityBook
			{
				PilgrimState = (int)KingdomLocusRules.PilgrimState.Waiting,
				PilgrimSequence = 3,
				PilgrimCauseTick = 9000L,
				PilgrimCause = "the festival of Ut yara Ux kept at Tamsketh",
				PilgrimObjectId = "stale body",
				PilgrimName = "Aeru",
				PilgrimPlaceName = "Tamsketh",
				PilgrimGreeted = 1
			};
			book.Normalize();
			ClassicAssert.AreEqual("", book.PilgrimObjectId);
			ClassicAssert.AreEqual("Aeru", book.PilgrimName,
				"placement retry should keep the already-generated identity");
			ClassicAssert.AreEqual(0, book.PilgrimGreeted);
		}

		// ---- The read seam refuses a value this build has no member for -----------------------

		/// <summary>The persisted <c>int</c> column a corrupt-value case aims at, by name. A switch
		/// and not reflection, so a renamed column fails here at compile time.</summary>
		private static List<int> Column(KingdomCityBook book, string column)
		{
			switch (column)
			{
			case "WorkKinds": return book.WorkKinds;
			case "WorkStages": return book.WorkStages;
			case "WorkAnchorsX": return book.WorkAnchorsX;
			case "WorkAnchorsY": return book.WorkAnchorsY;
			case "ResidentJobRoles": return book.ResidentJobRoles;
			case "ResidentDayShapes": return book.ResidentDayShapes;
			case "ResidentStandings": return book.ResidentStandings;
			case "ResidentCauses": return book.ResidentCauses;
			case "ResidentCreedChannels": return book.ResidentCreedChannels;
			case "ClockKinds": return book.ClockKinds;
			case "ToldKinds": return book.ToldKinds;
			default:
				Assert.Fail("no such column: " + column);
				return null;
			}
		}

		private static void AssertReadRefused(string column, int value)
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			Column(book, column)[0] = value;
			KingdomCityState state;
			ClassicAssert.IsFalse(book.TryRead(out state, out fault), column + " = " + value + " must not read");
			ClassicAssert.AreEqual(KingdomCityFault.InvalidIndex, fault);
			ClassicAssert.IsNull(state, "a refused read publishes nothing");
		}

		/// <summary>
		/// A column value no member of this build's vocabulary answers to is refused at the seam,
		/// never narrowed into one. An unchecked cast into a byte-backed enum truncates, so 256
		/// reads as member 0 and 259 as <c>Expedition</c>: a corrupt save, or one written by a later
		/// build, would load as a healthy city telling a different story. The doctrine is the
		/// clock's own (<c>TryWithProcessedThroughTick</c>): refuse a reading rather than repair it.
		/// </summary>
		[TestCase("WorkKinds", (int)KingdomWorkKind.Construction + 1)]
		[TestCase("WorkKinds", 256)]
		[TestCase("WorkKinds", -1)]
		[TestCase("ResidentDayShapes", (int)KingdomDayShape.Shrine + 1)]
		[TestCase("ResidentDayShapes", 256)]
		[TestCase("ResidentDayShapes", -1)]
		[TestCase("ResidentStandings", (int)KingdomResidentStanding.Expedition + 1)]
		[TestCase("ResidentStandings", 259)]
		[TestCase("ResidentStandings", -1)]
		[TestCase("ResidentCauses", (int)KingdomStandingCause.Astray + 1)]
		[TestCase("ResidentCauses", 261)]
		[TestCase("ResidentCauses", -1)]
		[TestCase("ClockKinds", (int)KingdomClockKind.Raid + 1)]
		[TestCase("ClockKinds", 256)]
		[TestCase("ClockKinds", -1)]
		[TestCase("ToldKinds", (int)KingdomToldKind.Brownout + 1)]
		[TestCase("ToldKinds", 257)]
		[TestCase("ToldKinds", -1)]
		public void AValueNoVocabularyMemberAnswersToRefusesTheReadRatherThanTruncating(string column, int value)
		{
			AssertReadRefused(column, value);
		}

		/// <summary>A value that does not fit the slot its row keeps it in &mdash; a <c>byte</c>
		/// stage, role or channel, a <c>short</c> anchor &mdash; is refused rather than wrapped:
		/// <c>(byte)256</c> is 0 and <c>(short)32768</c> is <c>-32768</c>, and a work anchored off
		/// the map by a wrapped coordinate is the same lie as a mislabelled standing.</summary>
		[TestCase("WorkStages", 256)]
		[TestCase("WorkStages", -1)]
		[TestCase("ResidentJobRoles", 256)]
		[TestCase("ResidentJobRoles", -1)]
		[TestCase("ResidentCreedChannels", 256)]
		[TestCase("ResidentCreedChannels", -1)]
		[TestCase("WorkAnchorsX", (int)short.MaxValue + 1)]
		[TestCase("WorkAnchorsX", (int)short.MinValue - 1)]
		[TestCase("WorkAnchorsY", (int)short.MaxValue + 1)]
		[TestCase("WorkAnchorsY", (int)short.MinValue - 1)]
		public void AValueThatDoesNotFitItsNarrowSlotRefusesTheReadRatherThanWrapping(string column, int value)
		{
			AssertReadRefused(column, value);
		}

		/// <summary>The widest member every vocabulary defines, and the top and bottom of every
		/// narrow slot, still read: the seam refuses only what has no meaning, and a value that
		/// reads is left exactly as it was written.</summary>
		[TestCase("WorkKinds", (int)KingdomWorkKind.Construction)]
		[TestCase("WorkStages", (int)byte.MaxValue)]
		[TestCase("WorkAnchorsX", (int)short.MaxValue)]
		[TestCase("WorkAnchorsX", (int)short.MinValue)]
		[TestCase("WorkAnchorsY", (int)short.MaxValue)]
		[TestCase("WorkAnchorsY", (int)short.MinValue)]
		[TestCase("ResidentJobRoles", (int)byte.MaxValue)]
		[TestCase("ResidentDayShapes", (int)KingdomDayShape.Shrine)]
		[TestCase("ResidentStandings", (int)KingdomResidentStanding.Expedition)]
		[TestCase("ResidentCauses", (int)KingdomStandingCause.Astray)]
		[TestCase("ResidentCreedChannels", (int)byte.MaxValue)]
		[TestCase("ClockKinds", (int)KingdomClockKind.Raid)]
		[TestCase("ToldKinds", (int)KingdomToldKind.Brownout)]
		public void TheWidestValueAColumnDefinesStillReads(string column, int value)
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			Column(book, column)[0] = value;
			KingdomCityState state;
			ClassicAssert.IsTrue(book.TryRead(out state, out fault), column + " = " + value + ": " + fault);
			ClassicAssert.AreEqual(KingdomCityFault.None, fault);
			ClassicAssert.AreEqual(value, Column(book, column)[0], "a value that reads is left as it was written");
		}

		/// <summary>The boundary row, read back through the model. The check runs before the casts
		/// and changes none of them: what was always narrowed still narrows to the same member.</summary>
		[Test]
		public void ABoundaryRowReadsBackThroughTheSameCasts()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			book.WorkKinds[0] = (int)KingdomWorkKind.Construction;
			book.WorkStages[0] = byte.MaxValue;
			book.WorkAnchorsX[0] = short.MaxValue;
			book.WorkAnchorsY[0] = short.MinValue;
			book.ResidentJobRoles[0] = byte.MaxValue;
			book.ResidentDayShapes[0] = (int)KingdomDayShape.Shrine;
			book.ResidentStandings[0] = (int)KingdomResidentStanding.Expedition;
			book.ResidentCauses[0] = (int)KingdomStandingCause.None;
			book.ResidentCreedChannels[0] = byte.MaxValue;
			book.ClockKinds[0] = (int)KingdomClockKind.Raid;
			book.ToldKinds[0] = (int)KingdomToldKind.Brownout;
			KingdomCityState state;
			ClassicAssert.IsTrue(book.TryRead(out state, out fault), fault.ToString());

			KingdomWorkRow work;
			ClassicAssert.IsTrue(state.TryWork(0, out work));
			ClassicAssert.AreEqual(KingdomWorkKind.Construction, work.RunState.Kind);
			ClassicAssert.AreEqual(byte.MaxValue, work.RunState.Stage);
			ClassicAssert.AreEqual(short.MaxValue, work.AnchorX);
			ClassicAssert.AreEqual(short.MinValue, work.AnchorY);
			KingdomResidentRow person;
			ClassicAssert.IsTrue(state.TryResident(0, out person));
			ClassicAssert.AreEqual(byte.MaxValue, person.JobRole);
			ClassicAssert.AreEqual(KingdomDayShape.Shrine, person.DayShape);
			ClassicAssert.AreEqual(KingdomResidentStanding.Expedition, person.Standing);
			ClassicAssert.AreEqual(KingdomStandingCause.None, person.Cause);
			ClassicAssert.AreEqual(byte.MaxValue, person.CreedChannel);
			KingdomClockRow clock;
			ClassicAssert.IsTrue(state.TryClock(0, out clock));
			ClassicAssert.AreEqual(KingdomClockKind.Raid, clock.Kind);
			KingdomToldRow told;
			ClassicAssert.IsTrue(state.TryTold(0, out told));
			ClassicAssert.AreEqual(KingdomToldKind.Brownout, told.Kind);
		}

		/// <summary>The standing-toward-cause repair reads neither value it cannot name. Without
		/// the guard, 258 would be narrowed to <c>Dead</c> on its way into <c>CauseFits</c> and the
		/// row's honest cause rewritten toward a standing it never had &mdash; a repair made from a
		/// truncated reading, before the read refused the row. Both columns stay as written.</summary>
		[TestCase("ResidentStandings", 258)]
		[TestCase("ResidentCauses", 300)]
		public void NormalizeLeavesAStandingOrCauseItCannotNameForTheReadToRefuse(string column, int value)
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			ClassicAssert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			Column(book, column)[0] = value;
			int standing = book.ResidentStandings[0];
			int cause = book.ResidentCauses[0];
			book.Normalize();
			ClassicAssert.AreEqual(standing, book.ResidentStandings[0]);
			ClassicAssert.AreEqual(cause, book.ResidentCauses[0],
				"a cause must not be repaired toward a standing the build cannot name, nor a nameless cause narrowed into one");
			KingdomCityState state;
			ClassicAssert.IsFalse(book.TryRead(out state, out fault));
			ClassicAssert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}
	}
}
#endif
