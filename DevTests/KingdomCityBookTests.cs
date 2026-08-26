#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
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
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 950L, Stocks(51L, 180L, 8L, 42L), zones, works, residents, clocks, out state, out fault), fault.ToString());
			KingdomCityState told;
			Assert.IsTrue(state.TryTell(new KingdomToldRow(KingdomToldKind.Harvest, 640L, 1, 2, "taf:zone:a", 3), out told, out fault));
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
			Assert.IsTrue(book.TryPublish(before, out fault), fault.ToString());
			KingdomCityState after;
			Assert.IsTrue(book.TryRead(out after, out fault), fault.ToString());

			Assert.AreEqual(before.SettlementId, after.SettlementId);
			Assert.AreEqual(before.ProcessedThroughTick, after.ProcessedThroughTick);
			Assert.AreEqual(before.Stocks.Water.Level, after.Stocks.Water.Level);
			Assert.AreEqual(before.Stocks.Food.Capacity, after.Stocks.Food.Capacity);
			Assert.AreEqual(before.ZoneCount, after.ZoneCount);
			Assert.AreEqual(before.WorkCount, after.WorkCount);
			Assert.AreEqual(before.ResidentCount, after.ResidentCount);
			Assert.AreEqual(before.ClockCount, after.ClockCount);
			Assert.AreEqual(before.ToldCount, after.ToldCount);

			KingdomZoneRow zoneBefore;
			KingdomZoneRow zoneAfter;
			Assert.IsTrue(before.TryZone(0, out zoneBefore));
			Assert.IsTrue(after.TryZone(0, out zoneAfter));
			Assert.AreEqual(zoneBefore.ZoneId, zoneAfter.ZoneId);
			Assert.AreEqual(zoneBefore.DistrictCode, zoneAfter.DistrictCode);
			Assert.AreEqual(zoneBefore.LastReadTick, zoneAfter.LastReadTick);
			Assert.AreEqual(zoneBefore.Stocks.Water.Level, zoneAfter.Stocks.Water.Level);
			Assert.AreEqual(zoneBefore.Stocks.Food.Capacity, zoneAfter.Stocks.Food.Capacity);
			Assert.AreEqual(zoneBefore.Roofs, zoneAfter.Roofs);
			Assert.AreEqual(zoneBefore.Defence, zoneAfter.Defence);
			Assert.AreEqual(zoneBefore.WaterCarry, zoneAfter.WaterCarry);
			Assert.AreEqual(zoneBefore.FoodCarry, zoneAfter.FoodCarry);
			Assert.AreEqual(-12, zoneAfter.OwedWater, "a standing draw must survive the save; that is what makes it a debt");
			Assert.AreEqual(3, zoneAfter.OwedFood, "a landing and a draw stand at once on one row");

			KingdomWorkRow workBefore;
			KingdomWorkRow workAfter;
			Assert.IsTrue(before.TryWork(0, out workBefore));
			Assert.IsTrue(after.TryWork(0, out workAfter));
			Assert.AreEqual(workBefore.WorkId, workAfter.WorkId);
			Assert.AreEqual(workBefore.AnchorX, workAfter.AnchorX);
			Assert.AreEqual(workBefore.DesignKey, workAfter.DesignKey);
			Assert.AreEqual(workBefore.ConditionPercent, workAfter.ConditionPercent);
			Assert.AreEqual(workBefore.RunState.Kind, workAfter.RunState.Kind);
			Assert.AreEqual(workBefore.RunState.Stage, workAfter.RunState.Stage);
			Assert.AreEqual(workBefore.RunState.NextTick, workAfter.RunState.NextTick);

			KingdomResidentRow personBefore;
			KingdomResidentRow personAfter;
			Assert.IsTrue(before.TryResident(0, out personBefore));
			Assert.IsTrue(after.TryResident(0, out personAfter));
			Assert.AreEqual(personBefore.Name, personAfter.Name);
			Assert.AreEqual(personBefore.Standing, personAfter.Standing);
			Assert.AreEqual(personBefore.Cause, personAfter.Cause);
			Assert.AreEqual(personBefore.DayShape, personAfter.DayShape);
			Assert.AreEqual(personBefore.BoundZoneId, personAfter.BoundZoneId);
			Assert.AreEqual("the moon", personAfter.Origin,
				"arbitrary exact origin must not collapse to its closed code");
			Assert.AreEqual("3 of Niv, 1000 AR", personAfter.Arrived,
				"frozen presentation evidence survives save round-trip");
			// Both brink windows, in full. A carrier that round-tripped "a brink stands" but lost
			// the tick the window is anchored on would hand every warned settler a fresh deadline
			// on every save, which is the failure the columns were retyped to make impossible.
			Assert.AreEqual(personBefore.RoofBrink.Stands, personAfter.RoofBrink.Stands);
			Assert.AreEqual(personBefore.RoofBrink.ReachedTick, personAfter.RoofBrink.ReachedTick);
			Assert.AreEqual(personBefore.RoofBrink.WarnedTick, personAfter.RoofBrink.WarnedTick);
			Assert.AreEqual(personBefore.CreedBrink.Stands, personAfter.CreedBrink.Stands);
			Assert.AreEqual(personBefore.CreedBrink.ReachedTick, personAfter.CreedBrink.ReachedTick);
			Assert.AreEqual(personBefore.CreedBrink.WarnedTick, personAfter.CreedBrink.WarnedTick);
			Assert.AreEqual(personBefore.CreedToward, personAfter.CreedToward);
			Assert.AreEqual(personBefore.CreedChannel, personAfter.CreedChannel);
			// Addendum 16's recorded fact. A book that lost it would hand the alignment gate a city
			// whose people had never believed anything, which is the one state that HIDES designs
			// rather than refusing them -- so the loss would read as the works never having existed.
			Assert.AreEqual(personBefore.KeptCreeds, personAfter.KeptCreeds);
			CollectionAssert.AreEqual(new[] { "Joppa", "Barathrumites" }, KingdomCreedRules.DecodeKept(personAfter.KeptCreeds));

			KingdomToldRow toldBefore;
			KingdomToldRow toldAfter;
			Assert.IsTrue(before.TryTold(0, out toldBefore));
			Assert.IsTrue(after.TryTold(0, out toldAfter));
			Assert.AreEqual(toldBefore.Kind, toldAfter.Kind);
			Assert.AreEqual(toldBefore.Tick, toldAfter.Tick);
			Assert.AreEqual(toldBefore.PlaceZoneId, toldAfter.PlaceZoneId);
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
			Assert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());

			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			Assert.IsTrue(book.TryReadBrink(9, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel));
			Assert.IsTrue(stands);
			Assert.AreEqual(410L, reached);
			Assert.AreEqual(415L, warned);
			Assert.IsNull(toward, "a roof brink has no creed");

			Assert.IsTrue(book.TryReadBrink(9, BrinkKind.Creed, out stands, out reached, out warned, out toward, out channel));
			Assert.IsTrue(stands);
			Assert.AreEqual(420L, reached);
			Assert.AreEqual(KingdomBrinkRules.Unwarned, warned, "a recorded brink nobody has been told about has no deadline");
			Assert.AreEqual("Mechanimists", toward);
			Assert.AreEqual(1, channel);
		}

		/// <summary>Warning somebody stamps the anchor and never redates their loss, and it reaches
		/// only the brink it was aimed at.</summary>
		[Test]
		public void WarningOneBrinkLeavesTheOtherWhereItWas()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			Assert.IsTrue(book.TryWriteBrink(9, BrinkKind.Creed, stands: true, 420L, 1000L, "Mechanimists", 1));

			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			Assert.IsTrue(book.TryReadBrink(9, BrinkKind.Creed, out stands, out reached, out warned, out toward, out channel));
			Assert.AreEqual(420L, reached);
			Assert.AreEqual(1000L, warned);
			Assert.IsTrue(book.TryReadBrink(9, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel));
			Assert.AreEqual(415L, warned, "warning a creed brink must not touch a roof brink");
		}

		/// <summary>A lifted brink leaves nothing behind for a later read to half-believe. Rule 2:
		/// if the cause returns the founder gets the whole window again.</summary>
		[Test]
		public void ALiftedBrinkClearsItsOwnFields()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			Assert.IsTrue(book.TryWriteBrink(9, BrinkKind.Creed, stands: false, 0L, KingdomBrinkRules.Unwarned, null, 0));

			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			Assert.IsTrue(book.TryReadBrink(9, BrinkKind.Creed, out stands, out reached, out warned, out toward, out channel));
			Assert.IsFalse(stands);
			Assert.AreEqual(0L, reached);
			Assert.AreEqual(KingdomBrinkRules.Unwarned, warned);
			Assert.IsNull(toward);
			Assert.AreEqual(0, channel);
		}

		/// <summary>A settler this book has no row for is not this book's to answer about — which is
		/// how the realm's other city gets asked next.</summary>
		[TestCase(0)]
		[TestCase(404)]
		public void ABookAnswersOnlyForItsOwnResidents(int residentId)
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			Assert.IsFalse(book.TryReadBrink(residentId, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel));
			Assert.IsFalse(book.TryWriteBrink(residentId, BrinkKind.Roof, stands: true, 1L, 2L, null, 0));
		}

		/// <summary>The realm's own brink is not a settler's, and asking a row for one is refused
		/// rather than answered with the roof's.</summary>
		[Test]
		public void ARowIsNotAskedForTheRealmsBrink()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			Assert.IsFalse(book.TryReadBrink(9, BrinkKind.City, out stands, out reached, out warned, out toward, out channel));
			Assert.IsFalse(book.TryWriteBrink(9, BrinkKind.City, stands: true, 1L, 2L, null, 0));
		}

		/// <summary>Ragged resident columns out of an older save are truncated to the shortest, and
		/// the brink accessors repair before they index rather than reading off the end of a
		/// column.</summary>
		[Test]
		public void RaggedResidentColumnsAreRepairedBeforeABrinkIsRead()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			book.ResidentIds.Add(11);
			book.ResidentNames.Add("Nobody");
			bool stands;
			long reached;
			long warned;
			string toward;
			int channel;
			Assert.IsFalse(book.TryReadBrink(11, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel),
				"a row half of whose fields are missing is not a row");
			Assert.AreEqual(1, book.ResidentCount);
			Assert.IsTrue(book.TryReadBrink(9, BrinkKind.Roof, out stands, out reached, out warned, out toward, out channel));
			Assert.IsTrue(stands);
		}

		/// <summary>Schema-v2 saves predate exact origin/arrival presentation columns. Migration
		/// fills only what the old closed code proves and retains the resident row; it never parses a
		/// display date into a second clock.</summary>
		[Test]
		public void V2ResidentRowsGainPresentationColumnsWithoutBeingDropped()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			book.SchemaVersion = 2;
			book.ResidentOrigins.Clear();
			book.ResidentArrived.Clear();

			book.Normalize();

			Assert.AreEqual(KingdomCityRules.SchemaVersion, book.SchemaVersion);
			Assert.AreEqual(1, book.ResidentCount);
			KingdomCityState state;
			Assert.IsTrue(book.TryRead(out state, out fault), fault.ToString());
			Assert.IsTrue(state.TryResident(0, out KingdomResidentRow row));
			Assert.AreEqual("", row.Origin,
				"an arbitrary v2 origin cannot be invented from NoOrigin");
			Assert.AreEqual("", row.Arrived,
				"v2 stored only the tick; no presentation string may be invented");
			Assert.AreEqual(400L, row.ArrivedTick);
		}

		/// <summary>A standing and a cause that disagree are repaired toward the STANDING, because
		/// the standing is what every consumer branches on: a living settler must never carry a
		/// death clause into a memorial.</summary>
		[Test]
		public void AStandingAndACauseThatDisagreeAreRepairedTowardTheStanding()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault), fault.ToString());
			book.ResidentStandings[0] = (int)KingdomResidentStanding.Resident;
			book.Normalize();
			Assert.AreEqual((int)KingdomStandingCause.None, book.ResidentCauses[0]);

			book.ResidentStandings[0] = (int)KingdomResidentStanding.Dead;
			book.Normalize();
			Assert.AreEqual((int)KingdomStandingCause.Unwitnessed, book.ResidentCauses[0],
				"a death nobody witnessed is told as exactly that, never invented");
		}

		/// <summary>A book nobody has written to is an empty city, not a fault.</summary>
		[Test]
		public void AFreshBookReadsAsACityWithNothingInIt()
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(new KingdomCityBook().TryRead(out state, out fault), fault.ToString());
			Assert.AreEqual(0, state.ZoneCount);
			Assert.AreEqual(0, state.RowCount);
			Assert.AreEqual(0L, state.ProcessedThroughTick);
		}

		/// <summary>Publishing twice leaves the book holding the SECOND state and nothing of the
		/// first. The columns are rewritten, never appended to.</summary>
		[Test]
		public void PublishingRewritesTheColumnsRatherThanAppendingToThem()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			Assert.AreEqual(2, book.ZoneCount);
			Assert.AreEqual(1, book.WorkCount);
			Assert.AreEqual(1, book.ToldCount);
		}

		[Test]
		public void PublishingNothingIsRefusedAndTheBookIsUntouched()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			Assert.IsFalse(book.TryPublish(null, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.AreEqual(2, book.ZoneCount, "a refused publish must leave the book byte-identical");
		}

		/// <summary>An absent named field arrives as a null column. It becomes an empty one rather
		/// than throwing inside the engine's block-skip recovery, which would cost the city.</summary>
		[Test]
		public void ANullColumnIsRepairedRatherThanThrown()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			book.ZoneRoofs = null;
			book.ToldTicks = null;
			book.Normalize();
			Assert.AreEqual(0, book.ZoneCount, "a zone row missing a field is not a zone row");
			Assert.AreEqual(0, book.ToldCount);
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
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			book.ZoneIds.Add("taf:zone:ghost");
			book.ZoneDistrictCodes.Add(1);
			book.Normalize();
			Assert.AreEqual(2, book.ZoneCount);
			Assert.AreEqual("taf:zone:b", book.ZoneIds[1], "the half-written row is the one that goes");
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
			Assert.AreEqual(KingdomCityState.MaxZones, book.ZoneCount);
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
			Assert.AreEqual(KingdomCityState.MaxToldEntries, book.ToldCount);
			Assert.AreEqual(1005L, book.ToldTicks[0], "the ring dropped the wrong end");
			Assert.AreEqual(1000L + KingdomCityState.MaxToldEntries + 4, book.ToldTicks[book.ToldCount - 1]);
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
			Assert.AreEqual(0L, book.ProcessedThroughTick);
			Assert.AreEqual("", book.SettlementId);
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(book.TryRead(out state, out fault));
		}

		/// <summary>The lookup every re-plumbed sighting reader goes through.</summary>
		[Test]
		public void AZoneRowIsFoundByItsIdAndNotByPosition()
		{
			KingdomCityBook book = new KingdomCityBook();
			KingdomCityFault fault;
			Assert.IsTrue(book.TryPublish(Peopled(), out fault));
			int index;
			Assert.IsTrue(book.TryZoneRow("taf:zone:b", out index));
			Assert.AreEqual(1, index);
			Assert.IsFalse(book.TryZoneRow("taf:zone:never", out index));
			Assert.IsFalse(book.TryZoneRow(null, out index));
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
			Assert.AreEqual((int)KingdomLocusRules.PilgrimState.Standing, book.PilgrimState);
			Assert.AreEqual("body:pilgrim:7", book.PilgrimObjectId);
			Assert.AreEqual("Aeru", book.PilgrimName);
			Assert.AreEqual("Tamsketh", book.PilgrimPlaceName);
			Assert.AreEqual(1, book.PilgrimGreeted);
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
			Assert.AreEqual(KingdomLocusRules.PilgrimStoryThreshold - 1,
				book.PilgrimLoudness);
			Assert.AreEqual((int)KingdomLocusRules.PilgrimState.None, book.PilgrimState);
			Assert.AreEqual(0, book.PilgrimSequence);
			Assert.AreEqual(0L, book.PilgrimCauseTick);
			Assert.AreEqual("", book.PilgrimCause);
			Assert.AreEqual("", book.PilgrimObjectId);
			Assert.AreEqual("", book.PilgrimName);
			Assert.AreEqual("", book.PilgrimPlaceName);
			Assert.AreEqual(0, book.PilgrimGreeted);
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
			Assert.AreEqual("", book.PilgrimObjectId);
			Assert.AreEqual("Aeru", book.PilgrimName,
				"placement retry should keep the already-generated identity");
			Assert.AreEqual(0, book.PilgrimGreeted);
		}
	}
}
#endif
