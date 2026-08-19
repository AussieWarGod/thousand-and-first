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

		[TestCase(0, 0)]
		[TestCase(3, 0)]
		[TestCase(4, 1)]
		[TestCase(7, 1)]
		[TestCase(8, 2)]
		[TestCase(50, 12)]
		public void UpkeepDrams(int population, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.UpkeepDrams(population));
		}

		[TestCase(0, 1200L, 0)]
		[TestCase(8, 1200L, 2)]
		[TestCase(8, 600L, 0)]
		[TestCase(8, 3600L, 6)]
		[TestCase(8, 12000L, 6)]
		[TestCase(20, 6000L, 15)]
		[TestCase(20, 0L, 0)]
		[TestCase(20, -100L, 0)]
		public void UpkeepForElapsed(int population, long elapsed, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.UpkeepForElapsed(population, elapsed));
		}

		[TestCase(0L, 0)]
		[TestCase(600L, 0)]
		[TestCase(1200L, 1)]
		[TestCase(3600L, 3)]
		[TestCase(120000L, 3)]
		[TestCase(-500L, 0)]
		public void HeartbeatDays(long elapsed, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.HeartbeatDays(elapsed));
		}

		[TestCase(0L, 5000L, 5000L)]
		[TestCase(1000L, 1599L, 1000L)]
		[TestCase(1000L, 2200L, 2200L)]
		[TestCase(1000L, 2800L, 2200L)]
		[TestCase(1000L, 4600L, 4600L)]
		[TestCase(1000L, 5800L, 5800L)]
		[TestCase(5000L, 4000L, 4000L)]
		public void HeartbeatCheckpoint(long previous, long current, long expected)
		{
			Assert.AreEqual(expected, KingdomRules.HeartbeatCheckpoint(previous, current));
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

		[TestCase(4, 1200L, KingdomRules.StoresPolicy.Thrift, 0)]
		[TestCase(4, 3600L, KingdomRules.StoresPolicy.Thrift, 0)]
		[TestCase(8, 1200L, KingdomRules.StoresPolicy.Thrift, 1)]
		[TestCase(8, 3600L, KingdomRules.StoresPolicy.Thrift, 3)]
		[TestCase(8, 120000L, KingdomRules.StoresPolicy.Plenty, 6)]
		public void PolicyUpkeepForElapsed(int population, long elapsed, KingdomRules.StoresPolicy stores, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.PolicyUpkeepForElapsed(population, elapsed, stores));
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

		[TestCase(0, 16)]
		[TestCase(4, 16)]
		[TestCase(40, 80)]
		public void ThirstPetitionTarget(int population, int expected)
		{
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
		[TestCase(GrowthStage.Camp, 3, 0, 10, true, 2, "withering quarters the seal")]
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
			Assert.AreEqual(honest, hoarded, "banking water before the end must not buy a better inheritance");
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
				Assert.AreEqual(first, KingdomRules.InterregnumRoll(seed), "the same legacy in the same world must always fare the same, or generation could be rerolled for a better inheritance");
				Assert.IsTrue(first >= 0 && first <= 99, "roll out of range at seed " + seed);
			}

			Assert.AreEqual(KingdomRules.InterregnumRoll(long.MaxValue), KingdomRules.InterregnumRoll(long.MaxValue), "extreme seeds stay deterministic");
			Assert.IsTrue(KingdomRules.InterregnumRoll(long.MinValue) >= 0, "extreme seeds stay in range");

			var seen = new System.Collections.Generic.HashSet<int>();
			for (long seed = 0L; seed < 400L; seed++)
			{
				seen.Add(KingdomRules.InterregnumRoll(seed));
			}
			Assert.IsTrue(seen.Count > 60, "the draw must actually vary between worlds, saw only " + seen.Count + " distinct values");
		}

		[TestCase(100, 0, 12, false, KingdomRules.InheritedState.Held)]
		[TestCase(100, 60, 12, false, KingdomRules.InheritedState.Held)]
		[TestCase(100, 99, 12, false, KingdomRules.InheritedState.Held)]
		[TestCase(50, 10, 12, false, KingdomRules.InheritedState.Faded)]
		[TestCase(50, 41, 12, false, KingdomRules.InheritedState.Abandoned)]
		[TestCase(50, 75, 12, false, KingdomRules.InheritedState.Abandoned)]
		[TestCase(13, 99, 3, false, KingdomRules.InheritedState.Ruins)]
		[TestCase(0, 99, 0, false, KingdomRules.InheritedState.Ruins)]
		public void ResolveInheritedState(int vigour, int roll, int population, bool withered, KingdomRules.InheritedState expected)
		{
			Assert.AreEqual(expected, KingdomRules.ResolveInheritedState(vigour, roll, population, withered));
		}

		[Test]
		public void TheEmptySettlementFloorOverridesTheDraw()
		{
			Assert.AreEqual(KingdomRules.InheritedState.Abandoned, KingdomRules.ResolveInheritedState(100, 0, 0, false), "a settlement sealed with nobody in it is never found inhabited");
			Assert.AreEqual(KingdomRules.InheritedState.Held, KingdomRules.ResolveInheritedState(100, 99, 12, false), "a city sealed at full vigour survives the worst draw there is");
			Assert.AreEqual(KingdomRules.InheritedState.Ruins, KingdomRules.ResolveInheritedState(0, 40, 0, false), "a settlement sealed at nothing survives no draw at all");
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
								KingdomRules.InheritedState state = KingdomRules.ResolveInheritedState(vigour, roll, population, true);
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
			Assert.AreEqual(KingdomRules.InheritedState.Held, KingdomRules.ResolveInheritedState(KingdomRules.HoldsAt, 0, 12, false));
			Assert.AreEqual(KingdomRules.InheritedState.Faded, KingdomRules.ResolveInheritedState(KingdomRules.HoldsAt - 1, 0, 12, false));
			Assert.AreEqual(KingdomRules.InheritedState.Faded, KingdomRules.ResolveInheritedState(KingdomRules.FadesAt, 0, 12, false));
			Assert.AreEqual(KingdomRules.InheritedState.Abandoned, KingdomRules.ResolveInheritedState(KingdomRules.FadesAt - 1, 0, 12, false));
			Assert.AreEqual(KingdomRules.InheritedState.Abandoned, KingdomRules.ResolveInheritedState(KingdomRules.EmptiesAt, 0, 12, false));
			Assert.AreEqual(KingdomRules.InheritedState.Ruins, KingdomRules.ResolveInheritedState(KingdomRules.EmptiesAt - 1, 0, 12, false));
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
				Assert.IsFalse(KingdomRules.WorksSurvive(state), "an unrecognised state must not promise intact works");
				Assert.AreEqual(KingdomRules.RuinStandingFloorPercent, KingdomRules.StandingPercent(state, 0), "an unrecognised state must not promise intact structures");
			}
		}

		[Test]
		public void CastGarbageStageCannotOverflowOrEarnACitysStanding()
		{
			int city = KingdomRules.SealedVigour(GrowthStage.City, 0, 0, 0, false);
			Assert.AreEqual(city, KingdomRules.SealedVigour((GrowthStage)int.MaxValue, 0, 0, 0, false), "a cast-garbage stage clamps to City rather than overflowing");
			Assert.AreEqual(KingdomRules.SealedVigour(GrowthStage.Camp, 0, 0, 0, false), KingdomRules.SealedVigour((GrowthStage)int.MinValue, 0, 0, 0, false), "and clamps up to Camp rather than going negative");
		}

		[Test]
		public void ResolveInheritedStateClampsRatherThanThrows()
		{
			Assert.AreEqual(KingdomRules.InheritedState.Held, KingdomRules.ResolveInheritedState(int.MaxValue, int.MinValue, 12, false), "out-of-range inputs clamp");
			Assert.AreEqual(KingdomRules.InheritedState.Ruins, KingdomRules.ResolveInheritedState(int.MinValue, int.MaxValue, 0, false), "and clamp the other way");
		}

		[Test]
		public void InheritanceIsIdempotentFromSealAndSeed()
		{
			int vigour = KingdomRules.SealedVigour(GrowthStage.Town, 30, 15, 200, false);
			for (long seed = 1L; seed <= 200L; seed++)
			{
				int roll = KingdomRules.InterregnumRoll(seed);
				KingdomRules.InheritedState first = KingdomRules.ResolveInheritedState(vigour, roll, 30, false);
				KingdomRules.InheritedState again = KingdomRules.ResolveInheritedState(vigour, KingdomRules.InterregnumRoll(seed), 30, false);
				Assert.AreEqual(first, again, "importing the same legacy twice must produce the same settlement, at seed " + seed);
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
					KingdomRules.InheritedState state = KingdomRules.ResolveInheritedState(vigour, roll, 12, false);
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
		public void WorksSurvive(KingdomRules.InheritedState state, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.WorksSurvive(state));
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

		[TestCase(0, KingdomRules.RaidOutcome.Overrun, 35)]
		[TestCase(5, KingdomRules.RaidOutcome.Plundered, 20)]
		[TestCase(10, KingdomRules.RaidOutcome.Plundered, 5)]
		[TestCase(99, KingdomRules.RaidOutcome.Plundered, 5)]
		[TestCase(0, KingdomRules.RaidOutcome.Repelled, 0)]
		public void RaidCasualtyChance(int defence, KingdomRules.RaidOutcome outcome, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.RaidCasualtyChance(defence, outcome));
		}

		[TestCase(0, 100, 100, 0)]
		[TestCase(5, 100, 100, 10)]
		[TestCase(5, 3, 100, 3)]
		[TestCase(5, 100, 4, 4)]
		[TestCase(5, 0, 100, 0)]
		[TestCase(5, 100, 0, 0)]
		[TestCase(50, 30, 200, 30)]
		public void FetchableDrams(int population, int openWater, int storageSpace, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.FetchableDrams(population, openWater, storageSpace));
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
	}
}
#endif
