#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomRulesTests
	{
		[TestCase(GrowthStage.Camp, 50)]
		[TestCase(GrowthStage.Steading, 40)]
		[TestCase(GrowthStage.Village, 30)]
		[TestCase(GrowthStage.Town, 20)]
		[TestCase(GrowthStage.City, 10)]
		public void SpilloverPercent(GrowthStage stage, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.SpilloverPercent(stage));
		}

		[TestCase(100, GrowthStage.Camp, 50)]
		[TestCase(10, GrowthStage.Camp, 5)]
		[TestCase(-10, GrowthStage.Camp, -5)]
		[TestCase(1, GrowthStage.Camp, 0)]
		[TestCase(-1, GrowthStage.Camp, 0)]
		[TestCase(0, GrowthStage.Camp, 0)]
		[TestCase(100, GrowthStage.Steading, 40)]
		[TestCase(75, GrowthStage.Town, 15)]
		[TestCase(100, GrowthStage.City, 10)]
		[TestCase(-200, GrowthStage.City, -20)]
		public void SpilloverDelta(int repDelta, GrowthStage stage, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.SpilloverDelta(repDelta, stage));
		}

		[TestCase(0, 3600L)]
		[TestCase(1, 4200L)]
		[TestCase(10, 9600L)]
		[TestCase(50, 33600L)]
		public void ArrivalIntervalTicks(int population, long expected)
		{
			Assert.AreEqual(expected, KingdomRules.ArrivalIntervalTicks(population));
		}

		// A camp drinks one dram per settler per day. It used to be a quarter of that, which is
		// why the water economy could never bind against fetch.
		[TestCase(0, 0)]
		[TestCase(3, 3)]
		[TestCase(4, 4)]
		[TestCase(7, 7)]
		[TestCase(8, 8)]
		[TestCase(50, 50)]
		public void UpkeepDrams(int population, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.UpkeepDrams(population));
		}

		[TestCase(0, 1200L)]
		[TestCase(8, 1200L)]
		[TestCase(8, 600L)]
		[TestCase(8, 3600L)]
		[TestCase(8, 12000L)]
		[TestCase(20, 6000L)]
		[TestCase(20, 0L)]
		[TestCase(20, -100L)]
		public void UpkeepForElapsed(int population, long elapsed)
		{
			// Whole days, all of them. Expressed against the daily rate so retuning upkeep cannot
			// quietly invalidate what this claims to prove.
			int expected = KingdomRules.UpkeepDrams(population) * KingdomRules.ElapsedDays(elapsed);
			Assert.AreEqual(expected, KingdomRules.UpkeepForElapsed(population, elapsed));
		}

		// --- The uncapping (Addendum 8 clause 1) ------------------------------------------------

		[Test]
		public void UpkeepForElapsed_ChargesTheWholeAbsence()
		{
			// Derived from the doctrine, not from a table: a settlement drinks every day it
			// exists, so the bill for N days is N times the bill for one, at any N.
			int oneDay = KingdomRules.UpkeepForElapsed(20, KingdomRules.TicksPerDay);
			Assert.Greater(oneDay, 0, "the fixture has to cost something for this to mean anything");
			foreach (int days in new int[5] { 3, 4, 30, 90, 400 })
			{
				Assert.AreEqual(oneDay * days, KingdomRules.UpkeepForElapsed(20, KingdomRules.TicksPerDay * days),
					days + " days away cost something other than " + days + " days of drinking");
			}
		}

		[Test]
		public void UpkeepForElapsed_HasNoStepAtTheOldForgivenessBoundary()
		{
			// The cap sat at three days. The exact place a forgiveness ceiling would show up is
			// the step from the last charged day to the first forgiven one, so pin the boundary
			// itself rather than trusting a distant value.
			int daily = KingdomRules.UpkeepDrams(20);
			for (int days = 1; days <= 6; days++)
			{
				Assert.AreEqual(daily * days, KingdomRules.UpkeepForElapsed(20, KingdomRules.TicksPerDay * days),
					"day " + days + " of the absence was not charged like the ones before it");
			}
		}

		[Test]
		public void UpkeepForElapsed_SaturatesRatherThanWrapping()
		{
			// A bill is never a debt. An elapsed too long to bill in an int asks for "everything
			// there is" -- which the stores answer by handing over everything there is -- instead
			// of wrapping into a negative amount they would silently GAIN.
			long enormous = KingdomRules.TicksPerDay * 3000000000L;
			Assert.AreEqual(int.MaxValue, KingdomRules.UpkeepForElapsed(60, enormous));
			Assert.Greater(KingdomRules.UpkeepForElapsed(60, enormous), KingdomRules.UpkeepForElapsed(60, KingdomRules.TicksPerDay * 400));
		}

		[Test]
		public void UpkeepForElapsed_FailsClosedOnAnElapsedThatCannotBeReal()
		{
			// Past what the kernel's checked arithmetic can fold, the answer is zero days rather
			// than a guess: a corrupt stamp must not mint a debt, and zero mints nothing.
			Assert.AreEqual(0, KingdomRules.UpkeepForElapsed(60, long.MaxValue));
			Assert.AreEqual(0, KingdomRules.UpkeepForElapsed(60, -1L));
		}

		[TestCase(0L, 0)]
		[TestCase(600L, 0)]
		[TestCase(1200L, 1)]
		[TestCase(3600L, 3)]
		[TestCase(4800L, 4)]
		[TestCase(120000L, 100)]
		[TestCase(-500L, 0)]
		public void ElapsedDays(long elapsed, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.ElapsedDays(elapsed));
		}

		[Test]
		public void ElapsedDays_FailsClosedRatherThanWrapping()
		{
			// The kernel's checked arithmetic refuses an elapsed it cannot fold. Zero is the safe
			// answer, because zero days mints no debt.
			Assert.AreEqual(0, KingdomRules.ElapsedDays(long.MinValue));
			Assert.GreaterOrEqual(KingdomRules.ElapsedDays(long.MaxValue), 0);
		}

		[TestCase(0L, 5000L, 5000L)]
		[TestCase(1000L, 1599L, 1000L)]
		[TestCase(1000L, 2200L, 2200L)]
		[TestCase(1000L, 2800L, 2200L)]
		[TestCase(1000L, 4600L, 4600L)]
		[TestCase(1000L, 5800L, 5800L)]
		[TestCase(5000L, 4000L, 4000L)]
		public void AdvanceCheckpoint(long previous, long current, long expected)
		{
			Assert.AreEqual(expected, KingdomRules.AdvanceCheckpoint(previous, current));
		}

		[Test]
		public void AdvanceCheckpoint_KeepsThePartDayInsteadOfReanchoring()
		{
			// The forgiveness the retired cap performed was physically this: past three days the
			// checkpoint jumped to now, so the unbilled remainder vanished. Now it advances by
			// exactly the days charged, and the leftover survives to be charged later.
			long start = 1000L;
			long now = start + KingdomRules.TicksPerDay * 90 + 500L;
			long advanced = KingdomRules.AdvanceCheckpoint(start, now);
			Assert.AreEqual(start + KingdomRules.TicksPerDay * 90, advanced);
			Assert.AreEqual(500L, now - advanced, "the part-day was thrown away instead of carried");
		}

		[Test]
		public void AdvanceCheckpoint_AndElapsedDaysAgreeAtEveryLength()
		{
			// The pair has one contract: whatever ElapsedDays charged, AdvanceCheckpoint spends,
			// and nothing else moves. If these two ever disagree, time is either free or billed
			// twice.
			long start = 4000L;
			foreach (int days in new int[6] { 0, 1, 3, 4, 30, 365 })
			{
				long now = start + KingdomRules.TicksPerDay * days + 700L;
				Assert.AreEqual(days, KingdomRules.ElapsedDays(now - start));
				Assert.AreEqual(start + KingdomRules.TicksPerDay * days, KingdomRules.AdvanceCheckpoint(start, now));
			}
		}

		[Test]
		public void TheAbsenceCapIsGoneFromTheSubstrateEntirely()
		{
			// The last of the forgiveness. LegacyAbsenceCap, HeartbeatDays and
			// HeartbeatCheckpoint were the holding pen for the rows P1 could not reach; every one
			// of them (roads, power, the three material workers, mending, dissent) now reads
			// ElapsedDays and AdvanceCheckpoint, so the pen is empty and removed.
			//
			// Reflection rather than a compile error, because a compile error is what you get for
			// FIVE MINUTES and this is a rule about what may come back. Nothing in KingdomRules
			// may ever again offer a caller a day count that forgives time; a counter that must
			// not run in absence says so with a labour term or an attended-pass window.
			System.Reflection.MethodInfo[] methods = typeof(KingdomRules).GetMethods(
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
			foreach (System.Reflection.MethodInfo method in methods)
			{
				Assert.AreNotEqual("HeartbeatDays", method.Name, "the capped day count came back");
				Assert.AreNotEqual("HeartbeatCheckpoint", method.Name, "the forgiving checkpoint came back");
			}
			Assert.IsNull(typeof(KingdomRules).GetField("LegacyAbsenceCap"), "the absence cap came back");
		}

		[TestCase(0L, 0)]
		[TestCase(600L, 0)]
		[TestCase(1200L, 1)]
		[TestCase(3600L, 3)]
		[TestCase(120000L, 100)]
		[TestCase(-500L, 0)]
		public void ElapsedDays_ChargesTheWholeStretchWhereTheCapChargedThree(long elapsed, int expected)
		{
			// The same table the capped pair was pinned at, with the one row that used to read 3
			// now reading 100. That row IS the rework: a hundred days away is a hundred days.
			Assert.AreEqual(expected, KingdomRules.ElapsedDays(elapsed));
		}

		[Test]
		public void ElapsedDays_DoesNotSpecialCaseAnUnplantedStampAndCallersMust()
		{
			// The trap the uncapping sets. A "last resolved" stamp is zero until something plants
			// it, and now - 0 is the whole age of the world, not "no time passed". The substrate
			// answers the question it was asked, honestly and enormously; every caller has to
			// plant its stamp before it counts. KingdomGrowth's fetch does exactly that, and had
			// to be reordered to do it -- under the retired cap this read three days and nobody
			// could see the bug.
			long anOldWorld = KingdomRules.TicksPerDay * 250;
			Assert.AreEqual(250, KingdomRules.ElapsedDays(anOldWorld - 0L));
			Assert.AreEqual(0, KingdomRules.ElapsedDays(anOldWorld - anOldWorld));
		}

		[Test]
		public void ReserveDays_IsAQuantityAndNotAClock()
		{
			// The retired constant did two jobs under one name. This is the surviving one: how
			// deep a cushion the discretionary spenders leave behind. It says nothing about how
			// much elapsed time anything is willing to look at, and the proof is that the upkeep
			// bill ignores it entirely.
			Assert.AreEqual(3, KingdomRules.ReserveDays);
			Assert.AreEqual(KingdomRules.UpkeepDrams(20) * 10,
				KingdomRules.UpkeepForElapsed(20, KingdomRules.TicksPerDay * 10),
				"the reserve depth leaked into the bill");
		}

		// --- Time x labour (Addendum 8 clause 2) -------------------------------------------------

		[TestCase(0, 100, 0)]
		[TestCase(10, 0, 0)]
		[TestCase(10, -5, 0)]
		[TestCase(10, 100, 10)]
		[TestCase(10, 150, 10)]
		[TestCase(10, 50, 5)]
		[TestCase(10, 25, 2)]
		[TestCase(3, 25, 0)]
		[TestCase(-4, 100, 0)]
		public void ActivityDays(int days, int effectiveness, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.ActivityDays(days, effectiveness));
		}

		[Test]
		public void ActivityDays_AreNeverMoreThanTheDaysThatPassed()
		{
			// Labour cannot mint time. Whatever the effectiveness, a stretch never yields more
			// working days than there were days.
			for (int effectiveness = 0; effectiveness <= 200; effectiveness += 25)
			{
				Assert.LessOrEqual(KingdomRules.ActivityDays(40, effectiveness), 40);
			}
		}

		[TestCase(0L, 100, 0L)]
		[TestCase(1000L, 0, 0L)]
		[TestCase(1000L, 100, 1000L)]
		[TestCase(1000L, 50, 500L)]
		[TestCase(999L, 50, 499L)]
		[TestCase(1000L, 25, 250L)]
		[TestCase(-20L, 100, 0L)]
		public void LabouredTicks(long elapsed, int effectiveness, long expected)
		{
			Assert.AreEqual(expected, KingdomRules.LabouredTicks(elapsed, effectiveness));
		}

		[Test]
		public void LabouredTicks_IsExactAndDoesNotOverflow()
		{
			// Scaled by halves and quarters rather than multiplied first, so a very long stretch
			// gives an answer instead of a wrapped one.
			Assert.AreEqual(long.MaxValue / 2, KingdomRules.LabouredTicks(long.MaxValue, 50), 1L);
			Assert.GreaterOrEqual(KingdomRules.LabouredTicks(long.MaxValue, 99), 0L);
		}

		[TestCase(0, 0)]
		[TestCase(1, 50)]
		[TestCase(2, 100)]
		[TestCase(5, 100)]
		[TestCase(-3, 0)]
		public void RaisingEffectiveness(int freeHands, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.RaisingEffectiveness(freeHands));
		}

		[Test]
		public void RaisingEffectiveness_IsZeroWithNobodyFreeSoAnEmptySettlementRaisesNothing()
		{
			// The author's ruling: a scaffold nobody works on does not rise. Stated here as the
			// arithmetic the scaffold reads -- no hands, no labour ticks, at any elapsed.
			Assert.AreEqual(0, KingdomRules.RaisingEffectiveness(0));
			Assert.AreEqual(0L, KingdomRules.LabouredTicks(KingdomRules.TicksPerDay * 400, KingdomRules.RaisingEffectiveness(0)));
		}

		[Test]
		public void ARaisingTakesItsAuthoredDurationWhenCrewedAndNeverFinishesWhenNobodyIsThere()
		{
			// The scaffold banks its design's BuildTicks and spends elapsed time against it at the
			// pace its crew manages. Stated here as the arithmetic, since the part itself is
			// engine-coupled: a whole crew recovers the authored duration exactly (so this wave
			// does not quietly slow every build in the game), half a crew takes twice as long,
			// and an empty settlement never gets there at any length of absence.
			long authored = 3600L;
			Assert.AreEqual(authored, KingdomRules.LabouredTicks(authored, KingdomRules.RaisingEffectiveness(KingdomRules.RaisingHandsWanted)));
			Assert.AreEqual(authored, KingdomRules.LabouredTicks(authored * 2, KingdomRules.RaisingEffectiveness(1)));
			foreach (long elapsed in new long[4] { authored, authored * 10, KingdomRules.TicksPerDay * 400, KingdomRules.TicksPerDay * 4000 })
			{
				Assert.AreEqual(0L, KingdomRules.LabouredTicks(elapsed, KingdomRules.RaisingEffectiveness(0)),
					"an empty settlement raised something over " + elapsed + " ticks");
			}
		}

		[Test]
		public void RaisingShortfallLine_SaysNothingWhenTheCrewIsWhole()
		{
			Assert.IsNull(KingdomRules.RaisingShortfallLine("stone house", KingdomRules.RaisingHandsWanted));
			Assert.IsNull(KingdomRules.RaisingShortfallLine("stone house", KingdomRules.RaisingHandsWanted + 3));
		}

		[Test]
		public void RaisingShortfallLine_NamesTheWorkAndTheReason()
		{
			string none = KingdomRules.RaisingShortfallLine("stone house", 0);
			StringAssert.Contains("stone house", none);
			StringAssert.Contains("nobody", none);
			string few = KingdomRules.RaisingShortfallLine("stone house", 1);
			StringAssert.Contains("stone house", few);
			Assert.AreNotEqual(none, few, "an empty crew and a short one give the founder the same sentence");
		}

		[TestCase(GrowthStage.Camp, 1)]
		[TestCase(GrowthStage.Steading, 2)]
		[TestCase(GrowthStage.Village, 3)]
		[TestCase(GrowthStage.Town, 5)]
		[TestCase(GrowthStage.City, 7)]
		public void ShopTierForStage(GrowthStage stage, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.ShopTierForStage(stage));
		}

		[TestCase(0, 0, false)]
		[TestCase(0, 1, true)]
		[TestCase(3, 3, false)]
		[TestCase(3, 4, true)]
		[TestCase(10, 2, false)]
		public void HasRoomToHouse(int population, int beds, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.HasRoomToHouse(population, beds));
		}

		[Test]
		public void AssignCrewFillsInPriorityOrder()
		{
			int[] crew = KingdomRules.AssignCrew(5, new int[3] { 2, 2, 2 });
			Assert.AreEqual(2, crew[0]);
			Assert.AreEqual(2, crew[1]);
			Assert.AreEqual(1, crew[2], "the last work runs shorthanded on what is left");

			int[] threshold = KingdomRules.AssignCrew(5, new int[3] { 2, 2, 2 }, new bool[3] { false, false, true });
			Assert.AreEqual(0, threshold[2], "an all-or-nothing work takes nobody rather than run short");

			int[] spill = KingdomRules.AssignCrew(5, new int[3] { 2, 4, 1 }, new bool[3] { false, true, false });
			Assert.AreEqual(2, spill[0]);
			Assert.AreEqual(0, spill[1], "threshold work skipped");
			Assert.AreEqual(1, spill[2], "hands it refused pass down the line");

			Assert.AreEqual(0, KingdomRules.AssignCrew(5, null).Length);
			Assert.AreEqual(0, KingdomRules.AssignCrew(-3, new int[1] { 1 })[0]);
		}

		[TestCase(0, 0, 100)]
		[TestCase(0, 2, 0)]
		[TestCase(1, 2, 50)]
		[TestCase(2, 2, 100)]
		[TestCase(3, 2, 100)]
		[TestCase(1, 3, 33)]
		[TestCase(2, 3, 66)]
		public void CrewEffectiveness(int assigned, int needed, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.CrewEffectiveness(assigned, needed));
		}

		[TestCase("threshold", true)]
		[TestCase("scaled", false)]
		[TestCase(null, false)]
		public void IsThresholdManning(string manning, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.IsThresholdManning(manning));
		}

		[TestCase(10000L, KingdomRules.GatePolicy.Open, KingdomRules.StoresPolicy.Plenty, 10000L)]
		[TestCase(10000L, KingdomRules.GatePolicy.Guarded, KingdomRules.StoresPolicy.Plenty, 14000L)]
		[TestCase(10000L, KingdomRules.GatePolicy.Open, KingdomRules.StoresPolicy.Thrift, 13000L)]
		[TestCase(10000L, KingdomRules.GatePolicy.Guarded, KingdomRules.StoresPolicy.Thrift, 18200L)]
		public void PolicyInterval(long baseInterval, KingdomRules.GatePolicy gate, KingdomRules.StoresPolicy stores, long expected)
		{
			Assert.AreEqual(expected, KingdomRules.PolicyInterval(baseInterval, gate, stores));
		}

		[TestCase(12, KingdomRules.StoresPolicy.Plenty, 12)]
		[TestCase(12, KingdomRules.StoresPolicy.Thrift, 9)]
		[TestCase(0, KingdomRules.StoresPolicy.Thrift, 0)]
		public void PolicyUpkeep(int baseUpkeep, KingdomRules.StoresPolicy stores, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.PolicyUpkeep(baseUpkeep, stores));
		}

		[TestCase(4, 1200L, KingdomRules.StoresPolicy.Thrift)]
		[TestCase(4, 3600L, KingdomRules.StoresPolicy.Thrift)]
		[TestCase(8, 1200L, KingdomRules.StoresPolicy.Thrift)]
		[TestCase(8, 3600L, KingdomRules.StoresPolicy.Thrift)]
		[TestCase(8, 120000L, KingdomRules.StoresPolicy.Plenty)]
		public void PolicyUpkeepForElapsed(int population, long elapsed, KingdomRules.StoresPolicy stores)
		{
			// Policy applies to the daily rate before the days multiply, so cost never changes
			// with how often the founder walks in.
			int expected = KingdomRules.PolicyUpkeep(KingdomRules.UpkeepDrams(population), stores) * KingdomRules.ElapsedDays(elapsed);
			Assert.AreEqual(expected, KingdomRules.PolicyUpkeepForElapsed(population, elapsed, stores));
		}

		[Test]
		public void PolicyUpkeepForElapsed_ThriftAlwaysCostsLessOrTheSame()
		{
			long elapsed = KingdomRules.TicksPerDay * 3;
			Assert.LessOrEqual(
				KingdomRules.PolicyUpkeepForElapsed(40, elapsed, KingdomRules.StoresPolicy.Thrift),
				KingdomRules.PolicyUpkeepForElapsed(40, elapsed, KingdomRules.StoresPolicy.Plenty));
		}

		[TestCase(6, 0, 6)]
		[TestCase(6, 1, 9)]
		[TestCase(6, 2, 13)]
		[TestCase(6, 3, 19)]
		[TestCase(6, 4, 28)]
		[TestCase(6, 9, 28)]
		public void TributeDemand(int baseDrams, int deferred, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.TributeDemand(baseDrams, deferred));
		}

		[TestCase(250, 0, true)]
		[TestCase(600, 0, true)]
		[TestCase(249, 0, false)]
		[TestCase(600, 1, false)]
		[TestCase(-500, 0, false)]
		public void CanTalkDown(int standing, int deferred, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.CanTalkDown(standing, deferred));
		}

		[TestCase(0, 0, 0, 0, 0, false, 0, KingdomRules.PetitionKind.None)]
		[TestCase(2, 8, 10, 0, 0, true, 0, KingdomRules.PetitionKind.Thirst)]
		[TestCase(100, 8, 8, 0, 0, true, 0, KingdomRules.PetitionKind.Shelter)]
		[TestCase(100, 8, 20, 0, 0, false, 2, KingdomRules.PetitionKind.Memorial)]
		[TestCase(100, 8, 20, 0, -400, true, 0, KingdomRules.PetitionKind.Peace)]
		[TestCase(100, 8, 20, 2, 0, true, 0, KingdomRules.PetitionKind.Craft)]
		[TestCase(100, 8, 20, 0, 0, true, 0, KingdomRules.PetitionKind.None)]
		public void ChoosePetition(int stored, int pop, int beds, int idle, int worst, bool shrine, int dead, KingdomRules.PetitionKind expected)
		{
			Assert.AreEqual(expected, KingdomRules.ChoosePetition(stored, pop, beds, idle, worst, shrine, dead));
		}

		[TestCase(KingdomRules.PetitionKind.Thirst, 40, 40, 8, 20, 0, 0, true, true)]
		[TestCase(KingdomRules.PetitionKind.Thirst, 40, 39, 8, 20, 0, 0, true, false)]
		[TestCase(KingdomRules.PetitionKind.Shelter, 0, 0, 8, 9, 0, 0, true, true)]
		[TestCase(KingdomRules.PetitionKind.Shelter, 0, 0, 8, 8, 0, 0, true, false)]
		[TestCase(KingdomRules.PetitionKind.Memorial, 0, 0, 8, 20, 0, 0, true, true)]
		[TestCase(KingdomRules.PetitionKind.Memorial, 0, 0, 8, 20, 0, 0, false, false)]
		[TestCase(KingdomRules.PetitionKind.Peace, -100, 0, 8, 20, 0, -100, true, true)]
		[TestCase(KingdomRules.PetitionKind.Peace, -100, 0, 8, 20, 0, -300, true, false)]
		[TestCase(KingdomRules.PetitionKind.Craft, 0, 0, 8, 20, 0, 0, true, true)]
		[TestCase(KingdomRules.PetitionKind.Craft, 0, 0, 8, 20, 3, 0, true, false)]
		[TestCase(KingdomRules.PetitionKind.None, 0, 0, 8, 20, 0, 0, true, false)]
		public void IsPetitionMet(KingdomRules.PetitionKind kind, int target, int stored, int pop, int beds, int idle, int standing, bool shrine, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.IsPetitionMet(kind, target, stored, pop, beds, idle, standing, shrine));
		}

		[TestCase(0)]
		[TestCase(4)]
		[TestCase(40)]
		public void ThirstPetitionTarget(int population)
		{
			// Eight days of drinking, with a floor so a tiny camp is still asked for something
			// worth fetching. Derived, so retuning upkeep does not falsify the claim.
			int expected = KingdomRules.UpkeepDrams(population) * 8;
			if (expected < 16)
			{
				expected = 16;
			}
			Assert.AreEqual(expected, KingdomRules.ThirstPetitionTarget(population));
		}

		[TestCase(1000L, 2000L, 500L, 0)]
		[TestCase(2000L, 2000L, 500L, 1)]
		[TestCase(2600L, 2000L, 500L, 2)]
		[TestCase(3100L, 2000L, 500L, 3)]
		[TestCase(99000L, 2000L, 500L, 3)]
		[TestCase(2000L, 2000L, 0L, 0)]
		public void BankedCycles(long now, long due, long interval, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.BankedCycles(now, due, interval));
		}

		[TestCase("the cistern you raised", 100L, "the hills", "word of the cistern you raised reached the hills")]
		[TestCase("the cistern you raised", 99000L, "the hills", "word of shared water reached the hills")]
		[TestCase(null, 0L, "the hills", "word of shared water reached the hills")]
		[TestCase("", 0L, "the hills", "word of shared water reached the hills")]
		public void ArrivalReason(string deed, long age, string origin, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.ArrivalReason(deed, age, origin));
		}

		[Test]
		public void LedgerDigestReportsWhatMoved()
		{
			KingdomLedger ledger = new KingdomLedger();
			Assert.IsFalse(ledger.Any, "an empty ledger has nothing to report");
			ledger.Arrivals = 2;
			ledger.Delivered = 6;
			ledger.UpkeepDrawn = 3;
			ledger.Note("something happened");
			Assert.IsTrue(ledger.Any);
			string digest = ledger.Digest("Kavvat", 4);
			Assert.IsTrue(digest.Contains("Kavvat"));
			Assert.IsTrue(digest.Contains("4 days"));
			Assert.IsTrue(digest.Contains("something happened"));
			Assert.IsTrue(digest.Contains("6 delivered under charter"));
			ledger.Reset();
			Assert.IsFalse(ledger.Any, "reset clears the ledger between visits");
			Assert.IsTrue(ledger.Digest("Kavvat", 1).Contains("nothing moved"));
		}

		[Test]
		public void LedgerAccountingAloneIsReportable()
		{
			KingdomLedger ledger = new KingdomLedger();
			Assert.IsFalse(ledger.Any);
			ledger.Fetched = 4;
			Assert.IsTrue(ledger.Any);
			ledger.Reset();
			ledger.UpkeepDrawn = 1;
			Assert.IsTrue(ledger.Any);
			ledger.Reset();
			ledger.ArrivalCost = 2;
			Assert.IsTrue(ledger.Any);
		}

		[TestCase("cask rack (holds 64 drams)", "cask rack")]
		[TestCase("great cistern (holds 256 drams)", "great cistern")]
		[TestCase("communal bunk", "communal bunk")]
		[TestCase("", "")]
		[TestCase(null, null)]
		public void StripParenthetical(string input, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.StripParenthetical(input));
		}

		[Test]
		public void OutsiderVariesBeyondPrefix()
		{
			string a = KingdomRules.ComposeOutsider("the well ran dry", 0);
			string b = KingdomRules.ComposeOutsider("the well ran dry", 6);
			Assert.AreNotEqual(a, b);
			Assert.IsTrue(a.StartsWith("It is said that"));
			Assert.IsTrue(b.StartsWith("It is said that"));
		}

		[TestCase(0, 0, GrowthStage.Camp)]
		[TestCase(4, 1000, GrowthStage.Camp)]
		[TestCase(5, 15, GrowthStage.Camp)]
		[TestCase(5, 16, GrowthStage.Steading)]
		[TestCase(11, 1000, GrowthStage.Steading)]
		[TestCase(12, 63, GrowthStage.Steading)]
		[TestCase(12, 64, GrowthStage.Village)]
		[TestCase(25, 255, GrowthStage.Village)]
		[TestCase(25, 256, GrowthStage.Town)]
		[TestCase(50, 1023, GrowthStage.Town)]
		[TestCase(50, 1024, GrowthStage.City)]
		[TestCase(100, 500, GrowthStage.Town)]
		[TestCase(100, 0, GrowthStage.Camp)]
		public void StageFor(int population, int capacity, GrowthStage expected)
		{
			Assert.AreEqual(expected, KingdomRules.StageFor(population, capacity));
		}

		[TestCase(GrowthStage.Camp, 3, 0, 10, false, 4, "a new camp: three people, ten drams, nothing built")]
		[TestCase(GrowthStage.Steading, 8, 3, 40, false, 29, "a steading behind a palisade")]
		[TestCase(GrowthStage.Town, 30, 15, 200, false, 90, "a walled town with full cisterns")]
		[TestCase(GrowthStage.City, 60, 40, 4000, false, 100, "a great city seals at the ceiling")]
		[TestCase(GrowthStage.Camp, 3, 0, 10, true, 2, "withering halves the seal")]
		[TestCase(GrowthStage.Camp, 0, 0, 0, false, 0, "nothing built and nobody in it seals at nothing")]
		[TestCase(GrowthStage.Camp, -5, -2, -10, false, 0, "negative inputs cannot add vigour")]
		public void SealedVigour(GrowthStage stage, int population, int defence, int stored, bool withered, int expected, string why)
		{
			Assert.AreEqual(expected, KingdomRules.SealedVigour(stage, population, defence, stored, withered), why);
		}

		[Test]
		public void SealedVigourIsBoundedAgainstOverflowAndHoarding()
		{
			Assert.AreEqual(100, KingdomRules.SealedVigour(GrowthStage.City, int.MaxValue, int.MaxValue, int.MaxValue, false), "no input combination may exceed the ceiling");
			Assert.AreEqual(0, KingdomRules.SealedVigour(GrowthStage.Camp, int.MinValue, int.MinValue, int.MinValue, false), "no input combination may go below zero");

			int honest = KingdomRules.SealedVigour(GrowthStage.Town, 30, 15, 200, false);
			int hoarded = KingdomRules.SealedVigour(GrowthStage.Town, 30, 15, 2000000, false);
			Assert.AreEqual(honest, hoarded, "stores past the cap must not buy a better inheritance, however much is banked before the end");
		}

		[Test]
		public void SealedVigourNeverFallsWhenTheSettlementGrows()
		{
			int previous = -1;
			for (int population = 0; population <= 60; population++)
			{
				int vigour = KingdomRules.SealedVigour(GrowthStage.Village, population, 4, 120, false);
				Assert.IsTrue(vigour >= previous, "one more settler must never lower the seal (at population " + population + ")");
				previous = vigour;
			}

			previous = -1;
			for (int defence = 0; defence <= 40; defence++)
			{
				int vigour = KingdomRules.SealedVigour(GrowthStage.Village, 12, defence, 120, false);
				Assert.IsTrue(vigour >= previous, "one more point of defence must never lower the seal (at defence " + defence + ")");
				previous = vigour;
			}

			previous = -1;
			for (GrowthStage stage = GrowthStage.Camp; stage <= GrowthStage.City; stage++)
			{
				int vigour = KingdomRules.SealedVigour(stage, 12, 4, 120, false);
				Assert.IsTrue(vigour >= previous, "growing a stage must never lower the seal (at " + stage + ")");
				previous = vigour;
			}
		}

		[Test]
		public void InterregnumRollIsDeterministicAndInRange()
		{
			for (long seed = -5000L; seed <= 5000L; seed += 37L)
			{
				int first = KingdomRules.InterregnumRoll(seed);
				Assert.AreEqual(first, KingdomRules.InterregnumRoll(seed), "a legacy must always draw the same fate, or promotion could be rerolled for a better inheritance");
				Assert.IsTrue(first >= 0 && first <= 99, "roll out of range at seed " + seed);
			}

			Assert.AreEqual(KingdomRules.InterregnumRoll(long.MaxValue), KingdomRules.InterregnumRoll(long.MaxValue), "extreme seeds stay deterministic");
			Assert.IsTrue(KingdomRules.InterregnumRoll(long.MinValue) >= 0, "extreme seeds stay in range");

			var seen = new System.Collections.Generic.HashSet<int>();
			for (long seed = 0L; seed < 400L; seed++)
			{
				seen.Add(KingdomRules.InterregnumRoll(seed));
			}
			Assert.IsTrue(seen.Count > 60, "the draw must actually vary between lineages, saw only " + seen.Count + " distinct values");
		}

		[TestCase(100, 0, 12, KingdomRules.InheritedState.Held)]
		[TestCase(100, 60, 12, KingdomRules.InheritedState.Held)]
		[TestCase(100, 99, 12, KingdomRules.InheritedState.Held)]
		[TestCase(50, 10, 12, KingdomRules.InheritedState.Faded)]
		[TestCase(50, 42, 12, KingdomRules.InheritedState.Abandoned)]
		[TestCase(50, 75, 12, KingdomRules.InheritedState.Abandoned)]
		[TestCase(13, 99, 3, KingdomRules.InheritedState.Ruins)]
		[TestCase(0, 99, 0, KingdomRules.InheritedState.Ruins)]
		public void ResolveInheritedState(int vigour, int roll, int population, KingdomRules.InheritedState expected)
		{
			Assert.AreEqual(expected, KingdomRules.ResolveInheritedState(vigour, roll, population));
		}

		[Test]
		public void TheEmptySettlementFloorOverridesTheDraw()
		{
			Assert.AreEqual(KingdomRules.InheritedState.Abandoned, KingdomRules.ResolveInheritedState(100, 0, 0), "a settlement sealed with nobody in it is never found inhabited");
			Assert.AreEqual(KingdomRules.InheritedState.Held, KingdomRules.ResolveInheritedState(100, 99, 12), "a city sealed at full vigour survives the worst draw there is");
			Assert.AreEqual(KingdomRules.InheritedState.Ruins, KingdomRules.ResolveInheritedState(0, 40, 0), "a settlement sealed at nothing survives no draw at all");
		}

		/// <summary>
		/// There is no explicit "withered is never Held" branch, because the arithmetic already
		/// guarantees it. This sweeps every seal a withered settlement can actually reach, rather
		/// than asserting the rule on an input <see cref="KingdomRules.SealedVigour"/> can never
		/// produce - which is how the previous version hid that its floor was dead code.
		/// </summary>
		[Test]
		public void NoWitheredSealCanEverBeFoundHeld()
		{
			int highest = 0;
			bool sawFaded = false;
			for (int population = 0; population <= KingdomRules.MaxPopulation; population++)
			{
				for (int defence = 0; defence <= 40; defence += 4)
				{
					for (int stored = 0; stored <= 4000; stored += 250)
					{
						for (GrowthStage stage = GrowthStage.Camp; stage <= GrowthStage.City; stage++)
						{
							int vigour = KingdomRules.SealedVigour(stage, population, defence, stored, true);
							if (vigour > highest)
							{
								highest = vigour;
							}
							for (int roll = 0; roll <= 99; roll += 11)
							{
								KingdomRules.InheritedState state = KingdomRules.ResolveInheritedState(vigour, roll, population);
								Assert.AreNotEqual(KingdomRules.InheritedState.Held, state, "a withered seal resolved to Held at vigour " + vigour + ", roll " + roll);
								if (state == KingdomRules.InheritedState.Faded)
								{
									sawFaded = true;
								}
							}
						}
					}
				}
			}
			Assert.IsTrue(highest < KingdomRules.HoldsAt, "the withered ceiling (" + highest + ") must sit below the holding threshold (" + KingdomRules.HoldsAt + ") for the invariant to hold without a branch");
			Assert.IsTrue(sawFaded, "a large withered settlement must still be able to be found thinned but lived in, or the ladder has lost a rung");
		}

		[Test]
		public void SealBoundariesSitExactlyWhereTheConstantsSay()
		{
			Assert.AreEqual(KingdomRules.InheritedState.Held, KingdomRules.ResolveInheritedState(KingdomRules.HoldsAt, 0, 12));
			Assert.AreEqual(KingdomRules.InheritedState.Faded, KingdomRules.ResolveInheritedState(KingdomRules.HoldsAt - 1, 0, 12));
			Assert.AreEqual(KingdomRules.InheritedState.Faded, KingdomRules.ResolveInheritedState(KingdomRules.FadesAt, 0, 12));
			Assert.AreEqual(KingdomRules.InheritedState.Abandoned, KingdomRules.ResolveInheritedState(KingdomRules.FadesAt - 1, 0, 12));
			Assert.AreEqual(KingdomRules.InheritedState.Abandoned, KingdomRules.ResolveInheritedState(KingdomRules.EmptiesAt, 0, 12));
			Assert.AreEqual(KingdomRules.InheritedState.Ruins, KingdomRules.ResolveInheritedState(KingdomRules.EmptiesAt - 1, 0, 12));
		}

		[Test]
		public void WaterCapBoundarySitsExactlyWhereTheConstantsSay()
		{
			int atCap = KingdomRules.VigourFromWaterCap * KingdomRules.VigourWaterPerPoint;
			int justUnder = KingdomRules.SealedVigour(GrowthStage.Camp, 0, 0, atCap - KingdomRules.VigourWaterPerPoint, false);
			int exactly = KingdomRules.SealedVigour(GrowthStage.Camp, 0, 0, atCap, false);
			int far = KingdomRules.SealedVigour(GrowthStage.Camp, 0, 0, atCap * 100, false);
			Assert.AreEqual(KingdomRules.VigourFromWaterCap - 1, justUnder, "one point below the cap");
			Assert.AreEqual(KingdomRules.VigourFromWaterCap, exactly, "the cap is reached exactly at " + atCap + " drams");
			Assert.AreEqual(KingdomRules.VigourFromWaterCap, far, "and never exceeded, however much is hoarded");
		}

		/// <summary>
		/// Checks that raising any single input never lowers the seal, over a grid covering all
		/// four axes and both withering states.
		/// <para>
		/// Named for what it proves, not for what would sound stronger. This walks every stage,
		/// both withering states, population in steps of one, defence in twos and stores in
		/// twenty-fourths, and at each point confirms that a step along any axis is non-decreasing.
		/// It is not every point in the continuous domain - stores alone would make that millions
		/// of evaluations - so it is a dense grid rather than a proof, and the formula being
		/// visibly additive is what makes the gap acceptable.
		/// </para>
		/// <para>
		/// The invariant matters because it is what stops a founder improving their own
		/// inheritance by destroying part of the settlement before the end. An earlier version of
		/// this test swept only the population axis while claiming the whole domain, and an
		/// earlier version still passed while the water term divided stores by population - the
		/// exact defect the sweep exists to catch.
		/// </para>
		/// </summary>
		[Test]
		public void SealedVigourNeverFallsWhenAnySingleInputRises()
		{
			foreach (bool withered in new bool[2] { false, true })
			{
				for (GrowthStage stage = GrowthStage.Camp; stage <= GrowthStage.City; stage++)
				{
					for (int defence = 0; defence <= 40; defence += 2)
					{
						for (int stored = 0; stored <= 1200; stored += 24)
						{
							for (int population = 0; population <= KingdomRules.MaxPopulation; population++)
							{
								int here = KingdomRules.SealedVigour(stage, population, defence, stored, withered);
								Assert.IsTrue(here >= 0 && here <= KingdomRules.MaxSealedVigour, "seal out of range at " + stage + "/" + population + "/" + defence + "/" + stored);

								string where = " at " + stage + ", pop " + population + ", defence " + defence + ", stored " + stored + ", withered " + withered;
								Assert.IsTrue(KingdomRules.SealedVigour(stage, population + 1, defence, stored, withered) >= here, "one more settler lowered the seal" + where);
								Assert.IsTrue(KingdomRules.SealedVigour(stage, population, defence + 1, stored, withered) >= here, "one more point of defence lowered the seal" + where);
								Assert.IsTrue(KingdomRules.SealedVigour(stage, population, defence, stored + 1, withered) >= here, "one more dram lowered the seal" + where);
								if (stage < GrowthStage.City)
								{
									Assert.IsTrue(KingdomRules.SealedVigour(stage + 1, population, defence, stored, withered) >= here, "growing a stage lowered the seal" + where);
								}
							}
						}
					}
				}
			}
		}

		[Test]
		public void TheWorstDrawCostsExactlyTheNamedSwing()
		{
			int best = KingdomRules.SealedVigour(GrowthStage.Town, 30, 15, 300, false);
			KingdomRules.InheritedState atBestDraw = KingdomRules.ResolveInheritedState(best, 0, 30);
			KingdomRules.InheritedState atWorstDraw = KingdomRules.ResolveInheritedState(best, 99, 30);
			Assert.AreNotEqual(atBestDraw, atWorstDraw, "the swing must actually move a mid-range seal");

			// The constant is named for the points it costs; at /100 the worst draw could only
			// ever take 39 of a declared 40, which is a small lie every later reader re-derives.
			int justAboveThreshold = KingdomRules.HoldsAt + KingdomRules.InterregnumSwing;
			Assert.AreEqual(KingdomRules.InheritedState.Held, KingdomRules.ResolveInheritedState(justAboveThreshold, 99, 12), "a seal exactly the swing above the threshold survives the worst draw");
			Assert.AreEqual(KingdomRules.InheritedState.Faded, KingdomRules.ResolveInheritedState(justAboveThreshold - 1, 99, 12), "and one point below it does not");
		}

		[Test]
		public void CastGarbageStateFailsClosedRatherThanGrantingASettlement()
		{
			KingdomRules.InheritedState[] garbage = new KingdomRules.InheritedState[3]
			{
				(KingdomRules.InheritedState)(-1),
				(KingdomRules.InheritedState)int.MinValue,
				(KingdomRules.InheritedState)int.MaxValue
			};
			foreach (KingdomRules.InheritedState state in garbage)
			{
				Assert.IsFalse(KingdomRules.IsKnownState(state), "unrecognised state " + (int)state);
				Assert.AreEqual(0, KingdomRules.InheritedPopulation(40, state), "an unrecognised state must not hand back a population");
				Assert.IsFalse(KingdomRules.AllWorksSurvive(state), "an unrecognised state must not promise intact works");
				Assert.AreEqual(KingdomRules.RuinStandingFloorPercent, KingdomRules.StandingPercent(state, 0), "an unrecognised state must not promise intact structures");
			}
		}

		[Test]
		public void CastGarbageStageCannotOverflowOrEarnACitysStanding()
		{
			int camp = KingdomRules.SealedVigour(GrowthStage.Camp, 12, 0, 0, false);
			foreach (int garbage in new int[5] { -1, 5, 99, int.MinValue, int.MaxValue })
			{
				Assert.AreEqual(camp, KingdomRules.SealedVigour((GrowthStage)garbage, 12, 0, 0, false), "an out-of-domain stage (" + garbage + ") must contribute nothing, not the best case: clamping high values to City hands garbage a city's standing, which is the outcome the guard exists to prevent");
			}
			Assert.IsTrue(KingdomRules.SealedVigour((GrowthStage)int.MaxValue, 60, 40, 4000, false) < KingdomRules.SealedVigour(GrowthStage.City, 60, 40, 4000, false), "and must never match a real City");
		}

		[Test]
		public void ResolveInheritedStateClampsRatherThanThrows()
		{
			Assert.AreEqual(KingdomRules.InheritedState.Held, KingdomRules.ResolveInheritedState(int.MaxValue, int.MinValue, 12), "out-of-range inputs clamp");
			Assert.AreEqual(KingdomRules.InheritedState.Ruins, KingdomRules.ResolveInheritedState(int.MinValue, int.MaxValue, 0), "and clamp the other way");
		}

		[Test]
		public void InheritanceIsDeterministicFromSealAndSeed()
		{
			int vigour = KingdomRules.SealedVigour(GrowthStage.Town, 30, 15, 200, false);
			for (long seed = 1L; seed <= 200L; seed++)
			{
				int roll = KingdomRules.InterregnumRoll(seed);
				KingdomRules.InheritedState first = KingdomRules.ResolveInheritedState(vigour, roll, 30);
				KingdomRules.InheritedState again = KingdomRules.ResolveInheritedState(vigour, KingdomRules.InterregnumRoll(seed), 30);
				Assert.AreEqual(first, again, "resolving the same legacy twice must produce the same settlement, at seed " + seed);
			}
		}

		[Test]
		public void AStrongerSealNeverYieldsAWorseInheritance()
		{
			for (int roll = 0; roll <= 99; roll += 7)
			{
				KingdomRules.InheritedState previous = KingdomRules.InheritedState.Ruins;
				for (int vigour = 0; vigour <= 100; vigour++)
				{
					KingdomRules.InheritedState state = KingdomRules.ResolveInheritedState(vigour, roll, 12);
					Assert.IsTrue(state <= previous, "raising the seal must never worsen the outcome (vigour " + vigour + ", roll " + roll + ")");
					previous = state;
				}
			}
		}

		[TestCase(10, KingdomRules.InheritedState.Held, 10)]
		[TestCase(10, KingdomRules.InheritedState.Faded, 5)]
		[TestCase(1, KingdomRules.InheritedState.Faded, 1)]
		[TestCase(10, KingdomRules.InheritedState.Abandoned, 0)]
		[TestCase(10, KingdomRules.InheritedState.Ruins, 0)]
		[TestCase(0, KingdomRules.InheritedState.Held, 0)]
		public void InheritedPopulation(int population, KingdomRules.InheritedState state, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.InheritedPopulation(population, state));
		}

		[TestCase(KingdomRules.InheritedState.Held, true)]
		[TestCase(KingdomRules.InheritedState.Faded, true)]
		[TestCase(KingdomRules.InheritedState.Abandoned, true)]
		[TestCase(KingdomRules.InheritedState.Ruins, false)]
		public void AllWorksSurvive(KingdomRules.InheritedState state, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.AllWorksSurvive(state));
		}

		[Test]
		public void OnlyRuinsTakeStructuresDownAndEvenThenNotAllOfThem()
		{
			Assert.AreEqual(100, KingdomRules.StandingPercent(KingdomRules.InheritedState.Held, 50));
			Assert.AreEqual(100, KingdomRules.StandingPercent(KingdomRules.InheritedState.Faded, 50));
			Assert.AreEqual(100, KingdomRules.StandingPercent(KingdomRules.InheritedState.Abandoned, 99), "abandoned is intact and derelict, never damaged - empty is the point of it");

			Assert.AreEqual(KingdomRules.RuinStandingCeilingPercent, KingdomRules.StandingPercent(KingdomRules.InheritedState.Ruins, 0), "the kindest interregnum leaves the most standing");
			Assert.AreEqual(KingdomRules.RuinStandingFloorPercent, KingdomRules.StandingPercent(KingdomRules.InheritedState.Ruins, 99), "the harshest leaves the least");

			int previous = 101;
			for (int roll = 0; roll <= 99; roll++)
			{
				int standing = KingdomRules.StandingPercent(KingdomRules.InheritedState.Ruins, roll);
				Assert.IsTrue(standing <= previous, "standing must never rise as adversity rises, at roll " + roll);
				Assert.IsTrue(standing >= KingdomRules.RuinStandingFloorPercent, "a ruin must stay legible as a place, at roll " + roll);
				Assert.IsTrue(standing <= KingdomRules.RuinStandingCeilingPercent, "a ruin must still read as ruined, at roll " + roll);
				previous = standing;
			}

			Assert.AreEqual(KingdomRules.RuinStandingCeilingPercent, KingdomRules.StandingPercent(KingdomRules.InheritedState.Ruins, int.MinValue), "out-of-range rolls clamp");
			Assert.AreEqual(KingdomRules.RuinStandingFloorPercent, KingdomRules.StandingPercent(KingdomRules.InheritedState.Ruins, int.MaxValue), "and clamp rather than wrap - a modulo would turn 150 into a mild 50");
		}

		[TestCase(0, 3, KingdomRules.RaidOutcome.Overrun)]
		[TestCase(4, 3, KingdomRules.RaidOutcome.Plundered)]
		[TestCase(12, 5, KingdomRules.RaidOutcome.Plundered)]
		[TestCase(12, 4, KingdomRules.RaidOutcome.Repelled)]
		[TestCase(20, 5, KingdomRules.RaidOutcome.Repelled)]
		[TestCase(11, 2, KingdomRules.RaidOutcome.Plundered)]
		public void ResolveRaid(int defence, int raidSize, KingdomRules.RaidOutcome expected)
		{
			Assert.AreEqual(expected, KingdomRules.ResolveRaid(defence, raidSize));
		}

		[TestCase(24, 0, KingdomRules.RaidOutcome.Overrun, 24)]
		[TestCase(24, 3, KingdomRules.RaidOutcome.Plundered, 19)]
		[TestCase(24, 6, KingdomRules.RaidOutcome.Plundered, 15)]
		[TestCase(24, 20, KingdomRules.RaidOutcome.Plundered, 4)]
		[TestCase(24, 12, KingdomRules.RaidOutcome.Repelled, 0)]
		public void RaidPlunder(int baseDrams, int defence, KingdomRules.RaidOutcome outcome, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.RaidPlunder(baseDrams, defence, outcome));
		}

		// Hands, not heads; and a rate, not a windfall. One day of five free hands is ten drams,
		// bounded by the pool and by the room left to put it in.
		[TestCase(0, 100, 100, 1, 0)]
		[TestCase(5, 100, 100, 1, 10)]
		[TestCase(5, 3, 100, 1, 3)]
		[TestCase(5, 100, 4, 1, 4)]
		[TestCase(5, 0, 100, 1, 0)]
		[TestCase(5, 100, 0, 1, 0)]
		[TestCase(50, 30, 200, 1, 30)]
		[TestCase(5, 100, 100, 3, 30)]
		[TestCase(5, 100, 100, 0, 0)]
		[TestCase(5, 100, 100, -2, 0)]
		public void FetchableDrams(int hands, int openWater, int storageSpace, int days, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.FetchableDrams(hands, openWater, storageSpace, days));
		}

		[Test]
		public void FetchableDrams_IsARateSoWalkingInAndOutCannotPrintWater()
		{
			// The defect this signature exists to prevent: fetch used to be charged once per zone
			// activation with no clock, so a founder could step out and back in to fetch again
			// without limit. Zero elapsed days must fetch nothing, however many times it is asked.
			for (int i = 0; i < 10; i++)
			{
				Assert.AreEqual(0, KingdomRules.FetchableDrams(20, 1000, 1000, 0));
			}
		}

		[Test]
		public void FetchableDrams_HandsOnWorksAreNotCarryingBuckets()
		{
			// Twenty citizens with fifteen crewing works fetch as five, not as twenty. Staffing a
			// mill has to cost something or it is not a choice.
			Assert.AreEqual(10, KingdomRules.FetchableDrams(5, 1000, 1000, 1));
			Assert.Less(KingdomRules.FetchableDrams(5, 1000, 1000, 1), KingdomRules.FetchableDrams(20, 1000, 1000, 1));
		}

		[Test]
		public void FetchableDrams_RunsTheWholeAbsenceSoSupplyCanMeetAnUncappedBill()
		{
			// Both halves of the water economy read the same uncapped elapsed now. If fetch
			// stopped at the retired three-day cap while upkeep did not, every absence would be
			// a guaranteed loss no staffing could answer -- which is the failure mode the
			// uncapping is most likely to introduce.
			Assert.AreEqual(2 * KingdomRules.FetchDramsPerSettler * 90,
				KingdomRules.FetchableDrams(2, 100000, 100000, 90));
			for (int days = 1; days <= 6; days++)
			{
				Assert.AreEqual(days * KingdomRules.FetchDramsPerSettler * 2,
					KingdomRules.FetchableDrams(2, 100000, 100000, days),
					"day " + days + " of the absence fetched nothing");
			}
		}

		[Test]
		public void FetchableDrams_IsStillBoundedByRealWaterAndRealRoom()
		{
			// Uncapping the clock must not uncap the haul: what is actually there and what will
			// actually fit are the only ceilings, and they still bite at any length.
			Assert.AreEqual(40, KingdomRules.FetchableDrams(20, 40, 100000, 400), "drank a pool that was not there");
			Assert.AreEqual(15, KingdomRules.FetchableDrams(20, 100000, 15, 400), "stored more than the cisterns hold");
			Assert.AreEqual(0, KingdomRules.FetchableDrams(20, 0, 100000, 400), "a dry site fetched something");
		}

		[Test]
		public void FetchableDrams_CampFeedsItselfOverAnyAbsenceOnWateredGround()
		{
			// The doctrine's floor: Camp is self-sustaining. A five-person camp with three on the
			// water detail must out-fetch its own uncapped drinking at every absence length, or
			// the smallest settlement in the game is not viable and nothing above it is either.
			foreach (int days in new int[5] { 1, 3, 10, 90, 400 })
			{
				int fetched = KingdomRules.FetchableDrams(3, 100000, 100000, days);
				int drunk = KingdomRules.UpkeepForElapsed(5, KingdomRules.TicksPerDay * days);
				Assert.GreaterOrEqual(fetched, drunk, "a camp went backwards over " + days + " days");
			}
		}

		[TestCase(20, GrowthStage.Camp, 20)]
		[TestCase(20, GrowthStage.Steading, 24)]
		[TestCase(20, GrowthStage.Village, 30)]
		[TestCase(20, GrowthStage.Town, 36)]
		[TestCase(20, GrowthStage.City, 44)]
		[TestCase(0, GrowthStage.City, 0)]
		public void UpkeepDrams_ScalesWithWhatTheSettlementHasBecome(int population, GrowthStage stage, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.UpkeepDrams(population, stage));
		}

		[Test]
		public void UpkeepDrams_NeverFallsAsASettlementGrows()
		{
			int previous = 0;
			foreach (GrowthStage stage in System.Enum.GetValues(typeof(GrowthStage)))
			{
				int now = KingdomRules.UpkeepDrams(30, stage);
				Assert.GreaterOrEqual(now, previous, "upkeep fell going up to " + stage);
				previous = now;
			}
		}

		[Test]
		public void MaxBuildingsForStage_NeverShrinksAsASettlementGrows()
		{
			int previous = 0;
			foreach (GrowthStage stage in System.Enum.GetValues(typeof(GrowthStage)))
			{
				int now = KingdomRules.MaxBuildingsForStage(stage);
				Assert.GreaterOrEqual(now, previous, "the plan shrank on growing into " + stage);
				previous = now;
			}
		}

		[Test]
		public void MaxBuildingsForStage_LeavesRoomForACityToActuallyBeBuilt()
		{
			// A City wants 50 settlers and 1024 storage: 13 bunks plus 4 great cisterns before a
			// single civic building, a shop, a work or a wall. The old flat forty could not even
			// house it.
			int bunks = (50 + KingdomRules.BedsPerBunk - 1) / KingdomRules.BedsPerBunk;
			int cisterns = 1024 / 256;
			Assert.Greater(KingdomRules.MaxBuildingsForStage(GrowthStage.City), (bunks + cisterns) * 3,
				"a City has no room left over for being a city");
		}

		[Test]
		public void BedsPerBunk_MakesTheCityStageReachableAtAll()
		{
			// City wants 50 settlers and 1024 storage. Four to a bunk leaves its staged plot budget
			// mostly free for the works that make it a city.
			int bunks = (50 + KingdomRules.BedsPerBunk - 1) / KingdomRules.BedsPerBunk;
			int cisterns = 1024 / 256;
			Assert.LessOrEqual(bunks + cisterns,
				KingdomRules.MaxBuildingsForStage(GrowthStage.City),
				"a City still cannot be built within the building cap");
		}

		[TestCase(0, GrowthStage.Village, 10, KingdomRules.ThirstOutcome.Sustained)]
		[TestCase(1, GrowthStage.Village, 10, KingdomRules.ThirstOutcome.Warned)]
		[TestCase(2, GrowthStage.Village, 10, KingdomRules.ThirstOutcome.Emigration)]
		[TestCase(3, GrowthStage.Village, 10, KingdomRules.ThirstOutcome.Withering)]
		[TestCase(9, GrowthStage.Village, 10, KingdomRules.ThirstOutcome.Withering)]
		[TestCase(3, GrowthStage.Camp, 10, KingdomRules.ThirstOutcome.Emigration)]
		[TestCase(2, GrowthStage.Village, 2, KingdomRules.ThirstOutcome.Warned)]
		[TestCase(2, GrowthStage.Village, 1, KingdomRules.ThirstOutcome.Warned)]
		public void ResolveThirst(int dryStreak, GrowthStage stage, int population, KingdomRules.ThirstOutcome expected)
		{
			Assert.AreEqual(expected, KingdomRules.ResolveThirst(dryStreak, stage, population));
		}

		[TestCase("you poured the first water", "Reegan", "Reegan poured the first water")]
		[TestCase("your cistern ran dry", "Reegan", "Reegan's cistern ran dry")]
		[TestCase("the well ran dry", "Reegan", "the well ran dry")]
		[TestCase("", "Reegan", "")]
		[TestCase(null, "Reegan", null)]
		public void ToThirdPerson(string text, string founder, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.ToThirdPerson(text, founder));
		}

		[TestCase("agrarian", "vinelands")]
		[TestCase("academy", "scriptorium")]
		[TestCase("garrison", "watch")]
		[TestCase("nonesuch", "nonesuch")]
		public void DistrictName(string district, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.DistrictName(district));
		}

		[TestCase("agrarian", true)]
		[TestCase("market", true)]
		[TestCase("academy", true)]
		[TestCase("necropolis", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		public void IsValidDistrict(string district, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.IsValidDistrict(district));
		}

		[TestCase(0, null, 3600L)]
		[TestCase(0, "market", 3240L)]
		[TestCase(10, "market", 8640L)]
		[TestCase(10, "shrine", 9600L)]
		public void ArrivalIntervalWithDistrict(int population, string district, long expected)
		{
			Assert.AreEqual(expected, KingdomRules.ArrivalIntervalTicks(population, district));
		}

		[TestCase("well", "the well", "Well", "4", "1200", "common", null, null, true)]
		[TestCase("well", "the well", "Well", "4", "1200", "", "storage", "Steading", true)]
		[TestCase("well", "the well", "Well", "4", "1200", null, null, "village", true)]
		[TestCase(null, "the well", "Well", "4", "1200", "common", null, null, false)]
		[TestCase("well", null, "Well", "4", "1200", "common", null, null, false)]
		[TestCase("well", "the well", null, "4", "1200", "common", null, null, false)]
		[TestCase("well", "the well", "Well", "abc", "1200", "common", null, null, false)]
		[TestCase("well", "the well", "Well", "-1", "1200", "common", null, null, false)]
		[TestCase("well", "the well", "Well", "4", "0", "common", null, null, false)]
		[TestCase("well", "the well", "Well", "4", "1200", "common", null, "metropolis", false)]
		[TestCase("well", "the well", "Well", "4", "1200", "common", null, "7", false)]
		public void TryParseBuildAttributes(string key, string display, string blueprint, string cost, string ticks, string styles, string category, string minStage, bool expectedOk)
		{
			bool ok = KingdomRules.TryParseBuildAttributes(key, display, blueprint, cost, ticks, styles, category, minStage, null, null, null, out var entry, out var error);
			Assert.AreEqual(expectedOk, ok);
			if (ok)
			{
				Assert.AreEqual(string.IsNullOrEmpty(styles) ? "common" : styles, entry.Styles);
				Assert.AreEqual(string.IsNullOrEmpty(category) ? "civic" : category, entry.Category);
				if (!string.IsNullOrEmpty(minStage))
				{
					Assert.AreEqual(minStage.ToLower(), entry.MinStage.ToString().ToLower());
				}
				else
				{
					Assert.AreEqual(GrowthStage.Camp, entry.MinStage);
				}
				Assert.IsNull(error);
			}
			else
			{
				Assert.IsNotNull(error);
			}
		}

		[TestCase(null, 0, true)]
		[TestCase("0", 0, true)]
		[TestCase("6", 6, true)]
		[TestCase("-1", 0, false)]
		[TestCase("watch", 0, false)]
		public void TryParseBuildDefence(string defence, int expectedDefence, bool expectedOk)
		{
			bool ok = KingdomRules.TryParseBuildAttributes("wall", "wall", "Wall", "4", "1200", "all", "defense", null, null, null, defence, out var entry, out var error);
			Assert.AreEqual(expectedOk, ok);
			if (ok)
			{
				Assert.AreEqual(expectedDefence, entry.Defence);
				Assert.IsNull(error);
			}
			else
			{
				Assert.IsNotNull(error);
			}
		}

		[TestCase("common", "common", true)]
		[TestCase("all", "anything", true)]
		[TestCase(null, "anything", true)]
		[TestCase("", "anything", true)]
		[TestCase("common,fungal", "fungal", true)]
		[TestCase("common, fungal", "fungal", true)]
		[TestCase("common,fungal", "eater", false)]
		[TestCase("fungal", "common", false)]
		public void StyleAllows(string entryStyles, string cityStyle, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.StyleAllows(entryStyles, cityStyle));
		}

		[TestCase(GrowthStage.Camp, 0)]
		[TestCase(GrowthStage.Steading, 2)]
		[TestCase(GrowthStage.Village, 3)]
		[TestCase(GrowthStage.Town, 4)]
		[TestCase(GrowthStage.City, 5)]
		public void RaidSize(GrowthStage stage, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.RaidSize(stage));
		}

		[Test]
		public void RaiderTables()
		{
			Assert.IsNotNull(KingdomRules.RaiderTableFor("Snapjaws"));
			Assert.IsNull(KingdomRules.RaiderTableFor("Joppa"));
			Assert.IsNull(KingdomRules.RaiderTableFor(null));
		}

		[TestCase("route", "water charter", "250", "6", "3600", "DromadTrader1", true)]
		[TestCase("route", "water charter", "250", "6", "3600", "", true)]
		[TestCase("route", "water charter", "250", "0", "3600", null, true)]
		[TestCase(null, "water charter", "250", "6", "3600", null, false)]
		[TestCase("route", null, "250", "6", "3600", null, false)]
		[TestCase("route", "water charter", "abc", "6", "3600", null, false)]
		[TestCase("route", "water charter", "250", "-1", "3600", null, false)]
		[TestCase("route", "water charter", "250", "6", "0", null, false)]
		public void TryParseDealAttributes(string key, string display, string minStanding, string income, string interval, string caravan, bool expectedOk)
		{
			bool ok = KingdomRules.TryParseDealAttributes(key, display, minStanding, income, interval, caravan, out var entry, out var error);
			Assert.AreEqual(expectedOk, ok);
			if (ok)
			{
				Assert.AreEqual(string.IsNullOrEmpty(caravan) ? "DromadTrader1" : caravan, entry.CaravanBlueprint);
				Assert.IsNull(error);
			}
			else
			{
				Assert.IsNotNull(error);
			}
		}

		[TestCase("hello happened", 0, "It is said that hello happened, though the tellers disagree on the year.")]
		[TestCase("hello happened", 5, "Some deny that hello happened, though the tellers disagree on the year.")]
		[TestCase("hello happened", 6, "It is said that hello happened, and the water in the telling is always sweeter.")]
		[TestCase("hello happened", -1, "Some deny that hello happened, though the tellers disagree on the year.")]
		[TestCase("hello happened", 35, "Some deny that hello happened.")]
		public void ComposeOutsider(string text, int roll, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.ComposeOutsider(text, roll));
		}

		[TestCase("JoppaWorld.11.22.1.1.10", true, "JoppaWorld", 34, 67, 10)]
		[TestCase("JoppaWorld.0.0.0.0.10", true, "JoppaWorld", 0, 0, 10)]
		[TestCase("JoppaWorld.5.3.2.1.15", true, "JoppaWorld", 17, 10, 15)]
		[TestCase("NorthSheva.1.1.1.1", false, null, 0, 0, 0)]
		[TestCase("garbage", false, null, 0, 0, 0)]
		[TestCase("", false, null, 0, 0, 0)]
		[TestCase(null, false, null, 0, 0, 0)]
		[TestCase("JoppaWorld.a.22.1.1.10", false, null, 0, 0, 0)]
		public void TryParseZoneID(string zoneID, bool expectedOk, string world, int gx, int gy, int z)
		{
			bool ok = KingdomRules.TryParseZoneID(zoneID, out var w, out var x, out var y, out var depth);
			Assert.AreEqual(expectedOk, ok);
			if (expectedOk)
			{
				Assert.AreEqual(world, w);
				Assert.AreEqual(gx, x);
				Assert.AreEqual(gy, y);
				Assert.AreEqual(z, depth);
			}
		}

		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.2.10", true)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.2.2.10", true)]
		[TestCase("JoppaWorld.11.22.2.1.10", "JoppaWorld.12.22.0.1.10", true)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.10", false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.11", false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.23.1.1.10", false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "OtherWorld.11.22.1.2.10", false)]
		[TestCase("garbage", "JoppaWorld.11.22.1.2.10", false)]
		public void ZonesAdjacent(string a, string b, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.ZonesAdjacent(a, b));
		}

		[TestCase("Joppa:100", true, "Joppa", 100)]
		[TestCase("Gyre Wights:-50", true, "Gyre Wights", -50)]
		[TestCase("Barathrumites: 250 ", true, "Barathrumites", 250)]
		[TestCase("SultanCult1:0", true, "SultanCult1", 0)]
		[TestCase("a:b:5", true, "a:b", 5)]
		[TestCase("nocolon", false, null, 0)]
		[TestCase(":100", false, null, 0)]
		[TestCase("Joppa:", false, null, 0)]
		[TestCase("Joppa:abc", false, null, 0)]
		[TestCase("", false, null, 0)]
		[TestCase(null, false, null, 0)]
		public void TryParseFactionAmount(string parameter, bool expectedOk, string expectedFaction, int expectedAmount)
		{
			bool ok = KingdomRules.TryParseFactionAmount(parameter, out var faction, out var amount);
			Assert.AreEqual(expectedOk, ok);
			if (expectedOk)
			{
				Assert.AreEqual(expectedFaction, faction);
				Assert.AreEqual(expectedAmount, amount);
			}
		}

		// ==================================================================================
		// The one deadline helper, and the three callers folded onto it.
		// ==================================================================================

		[Test]
		public void RestampDeadline_LeavesADeadlineThatHasNotComeDueAlone()
		{
			long due = KingdomRules.TicksPerDay * 10;
			Assert.AreEqual(due, KingdomRules.RestampDeadline(due, due - 1L, 600L, 0));
			Assert.AreEqual(due, KingdomRules.RestampDeadline(due, 0L, 600L, 1));
		}

		[Test]
		public void RestampDeadline_WithNoBandSpendsTheDeadlineTheInstantItComesDue()
		{
			// The manifest's window and the arrival queue both read a zero band: there is no
			// version of "close enough" for a load already standing in the sand, or for a slot
			// that has come and gone.
			long due = KingdomRules.TicksPerDay * 10;
			Assert.AreEqual(due + 600L, KingdomRules.RestampDeadline(due, due, 600L, 0));
			Assert.AreEqual(due + 1L + 600L, KingdomRules.RestampDeadline(due, due + 1L, 600L, 0));
		}

		[Test]
		public void RestampDeadline_ABandHoldsTheDeadlineForExactlyThatManyDays()
		{
			// A founder inside the band was there to see it and the caller fires it as it
			// stands; one outside gets a fresh full window from the moment of witnessing. The
			// boundary is inclusive, which is the shipped raid behaviour to the tick.
			long due = KingdomRules.TicksPerDay * 10;
			long onTheEdge = due + KingdomRules.TicksPerDay;
			Assert.AreEqual(due, KingdomRules.RestampDeadline(due, onTheEdge, 600L, 1),
				"a day past the deadline stopped counting as witnessed");
			Assert.AreEqual(onTheEdge + 1L + 600L, KingdomRules.RestampDeadline(due, onTheEdge + 1L, 600L, 1),
				"a tick past the band did not re-stamp");
		}

		[Test]
		public void RestampDeadline_NeverWrapsIntoThePast()
		{
			// A wrapped deadline would read as long overdue and fire on the spot, which is the
			// one outcome the whole helper exists to prevent.
			Assert.AreEqual(long.MaxValue, KingdomRules.RestampDeadline(0L, long.MaxValue - 5L, 600L, 0));
			Assert.GreaterOrEqual(KingdomRules.RestampDeadline(0L, long.MaxValue - 5L, 600L, 0), long.MaxValue - 5L);
		}

		[Test]
		public void RestampDeadline_ANonPositiveLeadPutsItAtNowRatherThanBehindIt()
		{
			Assert.AreEqual(5000L, KingdomRules.RestampDeadline(1000L, 5000L, 0L, 0));
			Assert.AreEqual(5000L, KingdomRules.RestampDeadline(1000L, 5000L, -600L, 0));
		}

		[Test]
		public void TheRaidCallerKeepsItsOwnDayOfGraceAndItsOwnLead()
		{
			// Caller 1 of 3. The band was a bare "> TicksPerDay" written inline at the
			// comparison; it is the same width, named, and now shared. Raiders who came within
			// the day of the warning running out find somebody home; raiders who came a season
			// early wait rather than looting in the dark.
			long due = KingdomRules.TicksPerDay * 20;
			Assert.AreEqual(due, KingdomRules.RestampDeadline(due, due + KingdomRules.TicksPerDay, KingdomRules.RaidWarningLeadTicks, KingdomRules.RaidWitnessGraceDays),
				"a raid a day overdue stopped resolving");
			long season = due + KingdomRules.TicksPerDay * 90;
			Assert.AreEqual(season + KingdomRules.RaidWarningLeadTicks,
				KingdomRules.RestampDeadline(due, season, KingdomRules.RaidWarningLeadTicks, KingdomRules.RaidWitnessGraceDays),
				"a raid ninety days overdue did not buy a fresh window from the homecoming");
			Assert.AreEqual(1, KingdomRules.RaidWitnessGraceDays, "the raid's band changed width");
		}

		[Test]
		public void TheManifestCallerTurnsBackWithNoBandAtAll()
		{
			// Caller 2 of 3. ManifestExpired is strictly past the deadline, so any overshoot at
			// all turns the load back and re-stamps a full window from the witnessing.
			long due = KingdomRules.TicksPerDay * 10;
			long now = due + 1L;
			Assert.AreEqual(now + KingdomManifestRules.ManifestWindowTicks,
				KingdomRules.RestampDeadline(due, now, KingdomManifestRules.ManifestWindowTicks, 0));
			Assert.AreEqual(KingdomManifestRules.ManifestWindowTicks,
				KingdomRules.RestampDeadline(due, now, KingdomManifestRules.ManifestWindowTicks, 0) - now,
				"the second window was not a full window");
		}

		[Test]
		public void TheArrivalCallerBurnsTheOvershootRatherThanBankingIt()
		{
			// Caller 3 of 3. A hundred days of unseated arrival slots is a settler at the gate,
			// never a hundred of them: the queue re-stamps a whole fresh interval from now and
			// the overshoot is gone.
			long interval = KingdomRules.ArrivalIntervalTicks(12);
			long due = KingdomRules.TicksPerDay * 5;
			long longAway = due + KingdomRules.TicksPerDay * 100;
			Assert.AreEqual(longAway + interval, KingdomRules.RestampDeadline(due, longAway, interval, 0));
			Assert.AreEqual(due + interval, KingdomRules.RestampDeadline(due, due, interval, 0),
				"a slot due exactly now was not spent");
		}

		// ==================================================================================
		// Visitors through an absence.
		// ==================================================================================

		[Test]
		public void PassagesThrough_ReportsNothingBeforeTheFirstIsDue()
		{
			KingdomRules.Passages none = KingdomRules.PassagesThrough(5000L, 4999L, 1200L, 400L);
			Assert.AreEqual(0, none.Departed);
			Assert.AreEqual(0L, none.StandingSince);
			Assert.AreEqual(5000L, none.NextDueTick);
			Assert.AreEqual(0L, none.LastDepartedTick);
		}

		[Test]
		public void PassagesThrough_AnUnplantedClockHasNotStartedAndNothingHasHappened()
		{
			// The same trap ElapsedDays sets: a zero stamp is not "no time passed", it is the age
			// of the world. Every visitor clock plants its stamp before it counts, and this
			// answers nothing rather than a thousand arrivals if one ever forgets.
			KingdomRules.Passages none = KingdomRules.PassagesThrough(0L, KingdomRules.TicksPerDay * 400, 1200L, 400L);
			Assert.AreEqual(0, none.Departed);
			Assert.AreEqual(0L, none.StandingSince);
		}

		[Test]
		public void PassagesThrough_OneStandingAtTheGateWhenTheirPatienceHasNotRunOut()
		{
			// The arrival landed inside its own patience of now, so it is still waiting -- and
			// nobody before it is, because the interval is longer than the patience.
			long due = 10000L;
			long interval = KingdomRules.TicksPerDay * 3;
			long patience = KingdomRules.TicksPerDay / 3;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(due, due + 100L, interval, patience);
			Assert.AreEqual(0, passages.Departed);
			Assert.AreEqual(due, passages.StandingSince);
			Assert.AreEqual(due + interval, passages.NextDueTick);
		}

		[Test]
		public void PassagesThrough_ASeasonAwayIsASeasonOfPeopleComingAndGoing()
		{
			// Addendum 8 clause 1 for visitors. Ninety days at a three-day cadence is thirty
			// arrivals, every one of whom waited out a third of a day at a gate nobody answered.
			long interval = KingdomRules.TicksPerDay * 3;
			long patience = KingdomRules.TicksPerDay / 3;
			long due = interval;
			// Half a day past the last arrival, which is past a third-of-a-day patience: nobody
			// is left standing, so the whole run is departures.
			long now = due + KingdomRules.TicksPerDay * 90 + KingdomRules.TicksPerDay / 2;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(due, now, interval, patience);
			Assert.AreEqual(31, passages.Departed, "the road stopped running while nobody watched");
			Assert.AreEqual(0L, passages.StandingSince, "somebody was left standing at the gate for a season");
			Assert.Greater(passages.NextDueTick, now, "the next one was already overdue on arrival");
		}

		[Test]
		public void PassagesThrough_DatesTheLastDepartureHonestly()
		{
			// The honest trace. The last one who came and went did so on a real day, and that day
			// is what the homecoming quotes -- not the day the founder walked in.
			long interval = KingdomRules.TicksPerDay * 3;
			long patience = KingdomRules.TicksPerDay / 3;
			long due = interval;
			long now = due + KingdomRules.TicksPerDay * 30;
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(due, now, interval, patience);
			Assert.Greater(passages.Departed, 0);
			int daysAgo = KingdomRules.ElapsedDays(now - passages.LastDepartedTick);
			Assert.GreaterOrEqual(daysAgo, 0);
			Assert.LessOrEqual(daysAgo, 3, "the last passage was dated further back than one interval");
			Assert.AreEqual(0, KingdomRules.PassagesThrough(due, due - 1L, interval, patience).LastDepartedTick,
				"a run with nobody in it still dated somebody");
		}

		[Test]
		public void PassagesThrough_NeverLeavesMoreThanOneStanding()
		{
			// The bound, and where it comes from: patience shorter than the interval. Both
			// shipped visitor clocks keep that relation, and this is what it buys.
			long interval = KingdomRules.TicksPerDay * 7;
			long patience = KingdomRules.TicksPerDay * 2;
			for (long away = 0; away <= KingdomRules.TicksPerDay * 120; away += KingdomRules.TicksPerDay / 4)
			{
				KingdomRules.Passages passages = KingdomRules.PassagesThrough(interval, interval + away, interval, patience);
				Assert.GreaterOrEqual(passages.Departed, 0);
				if (passages.StandingSince > 0L)
				{
					Assert.Less(interval + away - passages.StandingSince, patience, "somebody stood past their own patience");
				}
			}
		}

		[Test]
		public void PassagesThrough_EveryArrivalIsEitherDepartedOrStanding()
		{
			// Nobody is invented and nobody is lost: the count of departures plus the one still
			// there is exactly the number of times the clock came due.
			long interval = KingdomRules.TicksPerDay * 3;
			long patience = KingdomRules.TicksPerDay / 3;
			for (long away = 0; away <= KingdomRules.TicksPerDay * 60; away += KingdomRules.TicksPerDay / 5)
			{
				long now = interval + away;
				KingdomRules.Passages passages = KingdomRules.PassagesThrough(interval, now, interval, patience);
				long expected = (now - interval) / interval + 1L;
				Assert.AreEqual(expected, passages.Departed + ((passages.StandingSince > 0L) ? 1 : 0));
			}
		}

		[Test]
		public void PassagesThrough_RefusesANonsenseIntervalRatherThanDividingByIt()
		{
			Assert.AreEqual(0, KingdomRules.PassagesThrough(1000L, 90000L, 0L, 400L).Departed);
			Assert.AreEqual(0, KingdomRules.PassagesThrough(1000L, 90000L, -3L, 400L).Departed);
		}

		[Test]
		public void PassagesThrough_WithNoPatienceNobodyEverWaits()
		{
			// A visitor kind that does not wait at all departs the instant they arrive, and the
			// answer is all-departed rather than a null-patience visitor standing forever.
			KingdomRules.Passages passages = KingdomRules.PassagesThrough(1200L, 1200L, 1200L, 0L);
			Assert.AreEqual(1, passages.Departed);
			Assert.AreEqual(0L, passages.StandingSince);
		}
	}
}
#endif
