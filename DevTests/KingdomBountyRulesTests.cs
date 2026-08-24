#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomBountyRulesTests
	{
		private const string Settlement = "taf:settlement:testville";

		private const string OtherSettlement = "taf:settlement:othertown";

		private static string ReadRepoSource(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static List<string> Roster(params string[] Names)
		{
			return new List<string>(Names);
		}

		// --- The task tables ------------------------------------------------------------------

		[Test]
		public void TaskCount_MatchesTheEnumSoANewTaskCannotBeForgottenInTheTables()
		{
			Assert.AreEqual(KingdomBountyRules.TaskCount, Enum.GetValues(typeof(BountyTask)).Length);
			Assert.AreEqual(KingdomBountyRules.TaskCount, KingdomBountyRules.TaskKeys.Length);
			Assert.AreEqual(KingdomBountyRules.TaskCount, KingdomBountyRules.TaskNames.Length);
			Assert.AreEqual(KingdomBountyRules.TaskCount, KingdomBountyRules.TaskTasteCategories.Length);
			Assert.AreEqual(KingdomBountyRules.TaskCount, KingdomBountyRules.TakeBaseChance.Length);
		}

		[Test]
		public void TaskKeysAndNames_AreAllDistinct()
		{
			HashSet<string> keys = new HashSet<string>(KingdomBountyRules.TaskKeys, StringComparer.Ordinal);
			HashSet<string> names = new HashSet<string>(KingdomBountyRules.TaskNames, StringComparer.Ordinal);
			Assert.AreEqual(KingdomBountyRules.TaskCount, keys.Count);
			Assert.AreEqual(KingdomBountyRules.TaskCount, names.Count);
		}

		[TestCase(BountyTask.Clearance, "clearance")]
		[TestCase(BountyTask.Fetch, "fetch")]
		[TestCase(BountyTask.Manning, "manning")]
		[TestCase(BountyTask.Scouting, "scouting")]
		public void TaskKey_NamesItsOwnTask(BountyTask task, string expected)
		{
			Assert.AreEqual(expected, KingdomBountyRules.TaskKey(task));
		}

		[Test]
		public void TaskKeyAndName_FallBackRatherThanThrowOnAValueOutsideTheEnum()
		{
			Assert.AreEqual(KingdomBountyRules.TaskKeys[0], KingdomBountyRules.TaskKey((BountyTask)99));
			Assert.AreEqual(KingdomBountyRules.TaskNames[0], KingdomBountyRules.TaskName((BountyTask)99));
			Assert.AreEqual(KingdomBountyRules.TaskKeys[0], KingdomBountyRules.TaskKey((BountyTask)(-1)));
		}

		[TestCase(BountyTask.Clearance)]
		[TestCase(BountyTask.Fetch)]
		[TestCase(BountyTask.Manning)]
		[TestCase(BountyTask.Scouting)]
		public void TasteIndexFor_ResolvesIntoTheCeremonysOwnFamilyList(BountyTask task)
		{
			int index = KingdomBountyRules.TasteIndexFor(task);
			Assert.IsTrue(index >= 0, "task " + task + " names a family the ceremony does not carry");
			Assert.IsTrue(index < KingdomCeremonyRules.TasteCategories.Length);
			Assert.AreEqual(KingdomBountyRules.TaskTasteCategories[(int)task], KingdomCeremonyRules.TasteCategories[index]);
		}

		[Test]
		public void TasteIndexFor_ReportsMinusOneForATaskOutsideTheEnumRatherThanIndexZero()
		{
			Assert.AreEqual(-1, KingdomBountyRules.TasteIndexFor((BountyTask)99));
		}

		[Test]
		public void TasteIndexFor_GivesEachTaskItsOwnFamily()
		{
			HashSet<int> seen = new HashSet<int>();
			for (int i = 0; i < KingdomBountyRules.TaskCount; i++)
			{
				Assert.IsTrue(seen.Add(KingdomBountyRules.TasteIndexFor((BountyTask)i)), "two tasks share a taste family");
			}
		}

		// --- The price ------------------------------------------------------------------------

		[TestCase(-100, KingdomBountyRules.MinPrice)]
		[TestCase(0, KingdomBountyRules.MinPrice)]
		[TestCase(1, 1)]
		[TestCase(20, 20)]
		[TestCase(40, 40)]
		[TestCase(41, KingdomBountyRules.MaxPrice)]
		[TestCase(int.MaxValue, KingdomBountyRules.MaxPrice)]
		public void ClampPrice_FoldsAnythingIntoAPayablePrice(int given, int expected)
		{
			Assert.AreEqual(expected, KingdomBountyRules.ClampPrice(given));
		}

		[Test]
		public void SuggestedPrice_StaysInsideTheBoundsForEveryTaskAndEveryMagnitude()
		{
			int[] magnitudes = new int[6] { -5, 0, 1, 12, 200, int.MaxValue / 2 };
			for (int i = 0; i < KingdomBountyRules.TaskCount; i++)
			{
				for (int j = 0; j < magnitudes.Length; j++)
				{
					int price = KingdomBountyRules.SuggestedPrice((BountyTask)i, magnitudes[j]);
					Assert.IsTrue(price >= KingdomBountyRules.MinPrice && price <= KingdomBountyRules.MaxPrice,
						"task " + (BountyTask)i + " magnitude " + magnitudes[j] + " suggested " + price);
				}
			}
		}

		[Test]
		public void SuggestedPrice_AsksMoreForMoreGroundAndMoreLoads()
		{
			Assert.IsTrue(KingdomBountyRules.SuggestedPrice(BountyTask.Clearance, 40) > KingdomBountyRules.SuggestedPrice(BountyTask.Clearance, 4));
			Assert.IsTrue(KingdomBountyRules.SuggestedPrice(BountyTask.Fetch, 30) > KingdomBountyRules.SuggestedPrice(BountyTask.Fetch, 3));
		}

		[Test]
		public void SuggestedPrice_IgnoresMagnitudeForTheTwoTasksThatHaveNoSize()
		{
			Assert.AreEqual(KingdomBountyRules.SuggestedPrice(BountyTask.Manning, 0), KingdomBountyRules.SuggestedPrice(BountyTask.Manning, 900));
			Assert.AreEqual(KingdomBountyRules.SuggestedPrice(BountyTask.Scouting, 0), KingdomBountyRules.SuggestedPrice(BountyTask.Scouting, 900));
		}

		// --- Who reads it ---------------------------------------------------------------------

		[Test]
		public void PersonOrdinal_AlwaysSetsTheTopBitSoAPersonDrawCanNeverLandOnATickDraw()
		{
			string[] names = new string[5] { "Aeru", "Voss", "", null, "a very long settler name indeed" };
			for (int i = 0; i < names.Length; i++)
			{
				ulong ordinal = KingdomBountyRules.PersonOrdinal(names[i]);
				Assert.IsTrue((ordinal & 0x8000000000000000uL) != 0uL, "top bit clear for '" + names[i] + "'");
				Assert.IsTrue(ordinal > (ulong)long.MaxValue, "ordinal is reachable by a tick count");
			}
		}

		[Test]
		public void PersonOrdinal_IsStableForOneNameAndDistinctBetweenNames()
		{
			Assert.AreEqual(KingdomBountyRules.PersonOrdinal("Aeru"), KingdomBountyRules.PersonOrdinal("Aeru"));
			Assert.AreNotEqual(KingdomBountyRules.PersonOrdinal("Aeru"), KingdomBountyRules.PersonOrdinal("Voss"));
			Assert.AreNotEqual(KingdomBountyRules.PersonOrdinal("Aeru"), KingdomBountyRules.PersonOrdinal("aeru"));
			Assert.AreNotEqual(KingdomBountyRules.PersonOrdinal("ab"), KingdomBountyRules.PersonOrdinal("ba"));
		}

		[Test]
		public void PersonOrdinal_TreatsNullAndEmptyAsTheSameStableOrdinalRatherThanThrowing()
		{
			Assert.AreEqual(KingdomBountyRules.PersonOrdinal(null), KingdomBountyRules.PersonOrdinal(""));
		}

		[TestCase(0, 0, 0)]
		[TestCase(1, 0, KingdomBountyRules.AppetiteEager)]
		[TestCase(0, 1, KingdomBountyRules.AppetiteEager)]
		[TestCase(1, 1, KingdomBountyRules.AppetiteReluctant)]
		[TestCase(2, 0, KingdomBountyRules.AppetiteReluctant)]
		[TestCase(3, 0, 0)]
		[TestCase(4, 3, KingdomBountyRules.AppetiteEager)]
		[TestCase(-3, -3, 0)]
		[TestCase(-3, 1, KingdomBountyRules.AppetiteEager)]
		public void TraitAppetite_ReadsThePairRatherThanWhatThePairSays(int virtueIndex, int flawIndex, int expected)
		{
			Assert.AreEqual(expected, KingdomBountyRules.TraitAppetite(virtueIndex, flawIndex));
		}

		[Test]
		public void TraitAppetite_CoversAllThreeDispositionsAcrossThePairsTheCeremonyCanDraw()
		{
			HashSet<int> seen = new HashSet<int>();
			for (int virtueIndex = 0; virtueIndex < 8; virtueIndex++)
			{
				for (int flawIndex = 0; flawIndex < 8; flawIndex++)
				{
					seen.Add(KingdomBountyRules.TraitAppetite(virtueIndex, flawIndex));
				}
			}
			Assert.AreEqual(3, seen.Count, "no notable is ever eager, or none is ever reluctant");
		}

		// --- The chances ----------------------------------------------------------------------

		[Test]
		public void ReadChancePercent_RisesWithThePriceAndStopsAtTheCeiling()
		{
			Assert.AreEqual(KingdomBountyRules.ReadBaseChance + KingdomBountyRules.ReadChancePerDram, KingdomBountyRules.ReadChancePercent(1));
			Assert.IsTrue(KingdomBountyRules.ReadChancePercent(12) > KingdomBountyRules.ReadChancePercent(3));
			Assert.AreEqual(KingdomBountyRules.ReadChanceCeiling, KingdomBountyRules.ReadChancePercent(KingdomBountyRules.MaxPrice));
		}

		[Test]
		public void ReadChancePercent_ClampsThePriceBeforeReadingIt()
		{
			Assert.AreEqual(KingdomBountyRules.ReadChancePercent(1), KingdomBountyRules.ReadChancePercent(0));
			Assert.AreEqual(KingdomBountyRules.ReadChancePercent(1), KingdomBountyRules.ReadChancePercent(-40));
			Assert.AreEqual(KingdomBountyRules.ReadChancePercent(KingdomBountyRules.MaxPrice), KingdomBountyRules.ReadChancePercent(9999));
		}

		[Test]
		public void ReadChancePercent_NeverReachesCertainty()
		{
			for (int price = -5; price <= 60; price++)
			{
				Assert.IsTrue(KingdomBountyRules.ReadChancePercent(price) <= KingdomBountyRules.ReadChanceCeiling);
				Assert.IsTrue(KingdomBountyRules.ReadChancePercent(price) < 100);
			}
		}

		[TestCase(BountyTask.Clearance)]
		[TestCase(BountyTask.Fetch)]
		[TestCase(BountyTask.Manning)]
		[TestCase(BountyTask.Scouting)]
		public void TakeChancePercent_StartsFromItsOwnTasksBase(BountyTask task)
		{
			Assert.AreEqual(KingdomBountyRules.TakeBaseChance[(int)task] + KingdomBountyRules.TakeChancePerDram,
				KingdomBountyRules.TakeChancePercent(task, 1, Notable: false, TasteMatched: false, Appetite: 0));
		}

		[Test]
		public void TakeChancePercent_EveryShadeActuallyMovesTheNumber()
		{
			int plain = KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, false, false, 0);
			Assert.AreEqual(plain + KingdomBountyRules.TakeTasteBonus, KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, false, true, 0));
			Assert.AreEqual(plain + KingdomBountyRules.TakeNotableBonus, KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, true, false, 0));
			Assert.AreEqual(plain + KingdomBountyRules.TakeAppetiteWeight, KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, false, false, 1));
			Assert.AreEqual(plain - KingdomBountyRules.TakeAppetiteWeight, KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, false, false, -1));
			Assert.IsTrue(KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 20, false, false, 0) > plain);
		}

		[Test]
		public void TakeChancePercent_StaysBetweenItsFloorAndCeilingForEveryCombination()
		{
			for (int taskIndex = 0; taskIndex < KingdomBountyRules.TaskCount; taskIndex++)
			{
				for (int price = -20; price <= 80; price += 4)
				{
					for (int appetite = -3; appetite <= 3; appetite++)
					{
						for (int mask = 0; mask < 4; mask++)
						{
							int chance = KingdomBountyRules.TakeChancePercent((BountyTask)taskIndex, price, (mask & 1) != 0, (mask & 2) != 0, appetite);
							Assert.IsTrue(chance >= KingdomBountyRules.TakeChanceFloor, "below the floor: " + chance);
							Assert.IsTrue(chance <= KingdomBountyRules.TakeChanceCeiling, "above the ceiling: " + chance);
						}
					}
				}
			}
		}

		[Test]
		public void TakeChancePercent_ClampsThePriceSoAHugeOfferCannotBuyCertainty()
		{
			Assert.AreEqual(KingdomBountyRules.TakeChancePercent(BountyTask.Fetch, KingdomBountyRules.MaxPrice, true, true, 1),
				KingdomBountyRules.TakeChancePercent(BountyTask.Fetch, 100000, true, true, 1));
		}

		// --- Resolving a pass -----------------------------------------------------------------

		[Test]
		public void Resolve_IsIdenticalForIdenticalCoordinates()
		{
			List<string> roster = Roster("Aeru", "Voss", "Kest");
			for (int pass = 0; pass < 8; pass++)
			{
				KingdomBountyRules.BountyAttempt first = KingdomBountyRules.Resolve(Settlement, 4200L, pass, roster, BountyTask.Fetch, 12);
				KingdomBountyRules.BountyAttempt second = KingdomBountyRules.Resolve(Settlement, 4200L, pass, roster, BountyTask.Fetch, 12);
				Assert.AreEqual(first.Outcome, second.Outcome);
				Assert.AreEqual(first.Name, second.Name);
				Assert.AreEqual(first.RosterIndex, second.RosterIndex);
				Assert.AreEqual(first.VirtueIndex, second.VirtueIndex);
				Assert.AreEqual(first.FlawIndex, second.FlawIndex);
				Assert.AreEqual(first.TasteMatched, second.TasteMatched);
			}
		}

		[Test]
		public void Resolve_DiffersBetweenSettlementsThatShareEverythingElse()
		{
			List<string> roster = Roster("Aeru", "Voss", "Kest");
			int differences = 0;
			for (int pass = 0; pass < 40; pass++)
			{
				KingdomBountyRules.BountyAttempt here = KingdomBountyRules.Resolve(Settlement, 900L, pass, roster, BountyTask.Clearance, 8);
				KingdomBountyRules.BountyAttempt there = KingdomBountyRules.Resolve(OtherSettlement, 900L, pass, roster, BountyTask.Clearance, 8);
				if (here.Outcome != there.Outcome || here.Name != there.Name)
				{
					differences++;
				}
			}
			Assert.IsTrue(differences > 0, "two settlements drew the same notice the same way every pass");
		}

		[Test]
		public void Resolve_AdvancesWithThePassRatherThanRepeatingOneAnswerForever()
		{
			List<string> roster = Roster("Aeru", "Voss", "Kest");
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int pass = 0; pass < 60; pass++)
			{
				KingdomBountyRules.BountyAttempt attempt = KingdomBountyRules.Resolve(Settlement, 1200L, pass, roster, BountyTask.Clearance, 8);
				seen.Add(attempt.Outcome + "/" + (attempt.Name ?? "-"));
			}
			Assert.IsTrue(seen.Count > 1, "every pass drew the same reader and the same answer");
		}

		[Test]
		public void Resolve_ReportsNobodyTriedWhenThereIsNobodyOnTheRoster()
		{
			KingdomBountyRules.BountyAttempt empty = KingdomBountyRules.Resolve(Settlement, 1200L, 0, new List<string>(), BountyTask.Fetch, 40);
			Assert.AreEqual(BountyOutcome.NobodyTried, empty.Outcome);
			Assert.IsNull(empty.Name);
			Assert.AreEqual(-1, empty.RosterIndex);
			KingdomBountyRules.BountyAttempt none = KingdomBountyRules.Resolve(Settlement, 1200L, 0, null, BountyTask.Fetch, 40);
			Assert.AreEqual(BountyOutcome.NobodyTried, none.Outcome);
		}

		[Test]
		public void Resolve_FailsClosedAndCostsNothingWhenTheKernelRefusesTheKey()
		{
			List<string> roster = Roster("Aeru", "Voss");
			for (int pass = 0; pass < 20; pass++)
			{
				Assert.AreEqual(BountyOutcome.NobodyTried, KingdomBountyRules.Resolve(null, 1200L, pass, roster, BountyTask.Fetch, 40).Outcome);
				Assert.AreEqual(BountyOutcome.NobodyTried, KingdomBountyRules.Resolve("no", 1200L, pass, roster, BountyTask.Fetch, 40).Outcome);
				Assert.AreEqual(BountyOutcome.NobodyTried, KingdomBountyRules.Resolve("NOT THE GRAMMAR", 1200L, pass, roster, BountyTask.Fetch, 40).Outcome);
			}
		}

		[Test]
		public void Resolve_NamesSomebodyOnTheRosterWheneverItReportsAnythingButNobodyTried()
		{
			List<string> roster = Roster("Aeru", "Voss", "Kest");
			for (int pass = 0; pass < 120; pass++)
			{
				KingdomBountyRules.BountyAttempt attempt = KingdomBountyRules.Resolve(Settlement, 3600L, pass, roster, BountyTask.Scouting, 20);
				if (attempt.Outcome == BountyOutcome.NobodyTried)
				{
					Assert.IsNull(attempt.Name);
					continue;
				}
				Assert.IsTrue(attempt.RosterIndex >= 0 && attempt.RosterIndex < roster.Count);
				Assert.AreEqual(roster[attempt.RosterIndex], attempt.Name);
			}
		}

		[Test]
		public void Resolve_DrawsFromTheWholeRosterAndNotJustItsHead()
		{
			List<string> roster = Roster("Aeru", "Voss", "Kest");
			HashSet<string> readers = new HashSet<string>(StringComparer.Ordinal);
			for (int pass = 0; pass < 300; pass++)
			{
				KingdomBountyRules.BountyAttempt attempt = KingdomBountyRules.Resolve(Settlement, 2400L, pass, roster, BountyTask.Fetch, KingdomBountyRules.MaxPrice);
				if (attempt.Name != null)
				{
					readers.Add(attempt.Name);
				}
			}
			Assert.AreEqual(3, readers.Count, "the notice only ever reached " + readers.Count + " of the three settlers");
		}

		[Test]
		public void Resolve_ARicherPriceIsTakenMoreOften()
		{
			List<string> roster = Roster("Aeru", "Voss", "Kest");
			int cheap = 0;
			int rich = 0;
			for (int pass = 0; pass < 400; pass++)
			{
				if (KingdomBountyRules.Resolve(Settlement, 6000L, pass, roster, BountyTask.Manning, KingdomBountyRules.MinPrice).Outcome == BountyOutcome.Taken)
				{
					cheap++;
				}
				if (KingdomBountyRules.Resolve(Settlement, 6000L, pass, roster, BountyTask.Manning, KingdomBountyRules.MaxPrice).Outcome == BountyOutcome.Taken)
				{
					rich++;
				}
			}
			Assert.IsTrue(rich > cheap, "the posted price bought nothing: cheap=" + cheap + " rich=" + rich);
		}

		[Test]
		public void Resolve_ProducesRefusalsAsWellAsTakings()
		{
			List<string> roster = Roster("Aeru", "Voss", "Kest");
			int refusals = 0;
			int takings = 0;
			for (int pass = 0; pass < 300; pass++)
			{
				BountyOutcome outcome = KingdomBountyRules.Resolve(Settlement, 7200L, pass, roster, BountyTask.Scouting, 12).Outcome;
				if (outcome == BountyOutcome.Refused)
				{
					refusals++;
				}
				if (outcome == BountyOutcome.Taken)
				{
					takings++;
				}
			}
			Assert.IsTrue(refusals > 0, "nobody ever refused a posted notice");
			Assert.IsTrue(takings > 0, "nobody ever took a posted notice");
		}

		[Test]
		public void Resolve_ClampsAWildPassIndexRatherThanOverflowingTheDrawIndex()
		{
			List<string> roster = Roster("Aeru");
			KingdomBountyRules.BountyAttempt capped = KingdomBountyRules.Resolve(Settlement, 1200L, KingdomBountyRules.MaxPasses, roster, BountyTask.Fetch, 8);
			KingdomBountyRules.BountyAttempt beyond = KingdomBountyRules.Resolve(Settlement, 1200L, int.MaxValue, roster, BountyTask.Fetch, 8);
			Assert.AreEqual(capped.Outcome, beyond.Outcome);
			KingdomBountyRules.BountyAttempt negative = KingdomBountyRules.Resolve(Settlement, 1200L, -7, roster, BountyTask.Fetch, 8);
			KingdomBountyRules.BountyAttempt zero = KingdomBountyRules.Resolve(Settlement, 1200L, 0, roster, BountyTask.Fetch, 8);
			Assert.AreEqual(zero.Outcome, negative.Outcome);
		}

		[Test]
		public void Resolve_ReportsTheTasteMatchTheCeremonyItselfWouldReport()
		{
			int wanted = KingdomBountyRules.TasteIndexFor(BountyTask.Fetch);
			string tasteful = null;
			string indifferent = null;
			for (int i = 0; i < 500 && (tasteful == null || indifferent == null); i++)
			{
				string name = "settler" + i;
				bool has = KingdomCeremonyRules.ChooseTastes(Settlement, KingdomBountyRules.PersonOrdinal(name)).Contains(wanted);
				if (has && tasteful == null)
				{
					tasteful = name;
				}
				if (!has && indifferent == null)
				{
					indifferent = name;
				}
			}
			Assert.IsNotNull(tasteful, "no settler in five hundred ever wanted the stores kept ahead of need");
			Assert.IsNotNull(indifferent);
			int read = 0;
			for (int pass = 0; pass < 60; pass++)
			{
				KingdomBountyRules.BountyAttempt yes = KingdomBountyRules.Resolve(Settlement, 8400L, pass, Roster(tasteful), BountyTask.Fetch, KingdomBountyRules.MaxPrice);
				if (yes.Name != null)
				{
					read++;
					Assert.IsTrue(yes.TasteMatched, "a settler who stated the task's own taste was not credited with it");
				}
				KingdomBountyRules.BountyAttempt no = KingdomBountyRules.Resolve(Settlement, 8401L, pass, Roster(indifferent), BountyTask.Fetch, KingdomBountyRules.MaxPrice);
				if (no.Name != null)
				{
					Assert.IsFalse(no.TasteMatched, "a settler who never stated the task's taste was credited with it anyway");
				}
			}
			Assert.IsTrue(read > 0, "nobody read the notice at all, so nothing was proved");
		}

		[Test]
		public void Resolve_ATasteMatchMakesTheNoticeLikelierToBeTaken()
		{
			int wanted = KingdomBountyRules.TasteIndexFor(BountyTask.Manning);
			string tasteful = null;
			string indifferent = null;
			for (int i = 0; i < 500 && (tasteful == null || indifferent == null); i++)
			{
				string name = "hand" + i;
				bool has = KingdomCeremonyRules.ChooseTastes(Settlement, KingdomBountyRules.PersonOrdinal(name)).Contains(wanted);
				if (has && tasteful == null)
				{
					tasteful = name;
				}
				if (!has && indifferent == null)
				{
					indifferent = name;
				}
			}
			int virtueIndex;
			int flawIndex;
			KingdomCeremonyRules.ChooseLeaderTraits(Settlement, KingdomBountyRules.PersonOrdinal(tasteful), out virtueIndex, out flawIndex);
			int keen = KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 8, false, true, KingdomBountyRules.TraitAppetite(virtueIndex, flawIndex));
			int plain = KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 8, false, false, KingdomBountyRules.TraitAppetite(virtueIndex, flawIndex));
			Assert.IsTrue(keen > plain, "stating a taste for the work bought nothing");
		}

		// --- The frontier ---------------------------------------------------------------------

		[Test]
		public void TryNeighbour_GivesEightDistinctNeighboursAndNeverTheZoneItself()
		{
			HashSet<string> offsets = new HashSet<string>(StringComparer.Ordinal);
			for (int step = 0; step < KingdomBountyRules.NeighbourCount; step++)
			{
				int x;
				int y;
				Assert.IsTrue(KingdomBountyRules.TryNeighbour(10, 10, step, out x, out y));
				Assert.IsFalse(x == 10 && y == 10, "step " + step + " named the zone itself");
				Assert.IsTrue(offsets.Add((x - 10) + "," + (y - 10)), "step " + step + " repeated another step");
			}
			Assert.AreEqual(KingdomBountyRules.NeighbourCount, offsets.Count);
		}

		[Test]
		public void TryNeighbour_RefusesAStepOutsideTheEight()
		{
			int x;
			int y;
			Assert.IsFalse(KingdomBountyRules.TryNeighbour(10, 10, -1, out x, out y));
			Assert.IsFalse(KingdomBountyRules.TryNeighbour(10, 10, KingdomBountyRules.NeighbourCount, out x, out y));
		}

		[Test]
		public void TryNeighbour_RefusesGroundOffTheNorthAndWestEdgesOfTheWorld()
		{
			int refused = 0;
			for (int step = 0; step < KingdomBountyRules.NeighbourCount; step++)
			{
				int x;
				int y;
				if (!KingdomBountyRules.TryNeighbour(0, 0, step, out x, out y))
				{
					refused++;
					continue;
				}
				Assert.IsTrue(x >= 0 && y >= 0);
			}
			Assert.AreEqual(5, refused, "the corner of the world let a scout walk off it");
		}

		[TestCase(0, 0, 0)]
		[TestCase(2, 0, 2)]
		[TestCase(3, 1, 0)]
		[TestCase(11, 3, 2)]
		public void TrySplitGlobal_FoldsBackIntoAParasangAndAZone(int global, int expectedParasang, int expectedZone)
		{
			int parasang;
			int zone;
			Assert.IsTrue(KingdomBountyRules.TrySplitGlobal(global, out parasang, out zone));
			Assert.AreEqual(expectedParasang, parasang);
			Assert.AreEqual(expectedZone, zone);
			Assert.AreEqual(global, parasang * KingdomBountyRules.ZonesPerParasang + zone);
		}

		[Test]
		public void TrySplitGlobal_RefusesNegativeGroundRatherThanNamingAZoneThatExists()
		{
			int parasang;
			int zone;
			Assert.IsFalse(KingdomBountyRules.TrySplitGlobal(-1, out parasang, out zone));
			Assert.IsFalse(KingdomBountyRules.TrySplitGlobal(-3, out parasang, out zone));
		}

		[Test]
		public void TryPickFrontier_RefusesWhenThereIsNothingToPickFrom()
		{
			int index;
			Assert.IsFalse(KingdomBountyRules.TryPickFrontier(Settlement, 1200L, 0, 0, out index));
			Assert.IsFalse(KingdomBountyRules.TryPickFrontier(Settlement, 1200L, 0, -4, out index));
		}

		[Test]
		public void TryPickFrontier_StaysInRangeAndIsStable()
		{
			for (int pass = 0; pass < 40; pass++)
			{
				int first;
				int second;
				Assert.IsTrue(KingdomBountyRules.TryPickFrontier(Settlement, 1200L, pass, 7, out first));
				Assert.IsTrue(KingdomBountyRules.TryPickFrontier(Settlement, 1200L, pass, 7, out second));
				Assert.AreEqual(first, second);
				Assert.IsTrue(first >= 0 && first < 7);
			}
		}

		[Test]
		public void TryPickFrontier_ReachesMoreThanOneEdgeAcrossPasses()
		{
			HashSet<int> picked = new HashSet<int>();
			for (int pass = 0; pass < 60; pass++)
			{
				int index;
				KingdomBountyRules.TryPickFrontier(Settlement, 5000L, pass, 5, out index);
				picked.Add(index);
			}
			Assert.IsTrue(picked.Count > 1, "every scout reported the same edge");
		}

		[Test]
		public void TryPickFrontier_FallsBackToARealCandidateWhenTheKernelRefuses()
		{
			int index;
			Assert.IsTrue(KingdomBountyRules.TryPickFrontier(null, 1200L, 0, 4, out index));
			Assert.AreEqual(0, index);
		}

		// --- How long the work takes ----------------------------------------------------------

		[TestCase(-4, KingdomBountyRules.HaulBaseDays)]
		[TestCase(0, KingdomBountyRules.HaulBaseDays)]
		[TestCase(7, 1)]
		[TestCase(8, 2)]
		[TestCase(24, 4)]
		[TestCase(400, KingdomBountyRules.HaulMaxDays)]
		public void HaulDays_ScalesWithTheLoadAndStopsAtItsCap(int units, int expected)
		{
			Assert.AreEqual(expected, KingdomBountyRules.HaulDays(units));
		}

		[Test]
		public void WorkDays_ClearanceHasNoClockBecauseTheGangsOwnEffortIsItsClock()
		{
			Assert.AreEqual(0, KingdomBountyRules.WorkDays(BountyTask.Clearance, 40));
		}

		[Test]
		public void WorkDays_EveryOtherTaskRunsForARealNumberOfDays()
		{
			Assert.AreEqual(KingdomBountyRules.HaulDays(16), KingdomBountyRules.WorkDays(BountyTask.Fetch, 16));
			Assert.AreEqual(KingdomBountyRules.ManningSeasonDays, KingdomBountyRules.WorkDays(BountyTask.Manning, 0));
			Assert.AreEqual(KingdomBountyRules.ScoutDays, KingdomBountyRules.WorkDays(BountyTask.Scouting, 0));
			Assert.IsTrue(KingdomBountyRules.ManningSeasonDays > KingdomBountyRules.ScoutDays);
		}

		// --- Saying why, once -----------------------------------------------------------------

		[TestCase(BountyBlock.None, false)]
		[TestCase(BountyBlock.NobodyToTry, false)]
		[TestCase(BountyBlock.NothingStanding, true)]
		[TestCase(BountyBlock.PileEmpty, true)]
		[TestCase(BountyBlock.NowhereToCarry, false)]
		[TestCase(BountyBlock.NoWorks, true)]
		[TestCase(BountyBlock.NoIdleWork, false)]
		[TestCase(BountyBlock.NoFrontier, true)]
		[TestCase(BountyBlock.StoresCannotPay, false)]
		public void IsPermanent_SeparatesWhatCanLiftFromWhatNeverWill(BountyBlock block, bool expected)
		{
			Assert.AreEqual(expected, KingdomBountyRules.IsPermanent(block));
		}

		[Test]
		public void BlockReason_SaysNothingOnlyWhenThereIsNothingToSay()
		{
			Assert.IsNull(KingdomBountyRules.BlockReason(BountyBlock.None, BountyTask.Fetch, "Ulu"));
			foreach (BountyBlock block in Enum.GetValues(typeof(BountyBlock)))
			{
				if (block == BountyBlock.None)
				{
					continue;
				}
				string reason = KingdomBountyRules.BlockReason(block, BountyTask.Fetch, "Ulu");
				Assert.IsFalse(string.IsNullOrEmpty(reason), "block " + block + " stalls in silence");
				Assert.IsTrue(reason.EndsWith("."), "block " + block + " is not a sentence: " + reason);
			}
		}

		[Test]
		public void BlockReason_AReasonThatWillNeverLiftSaysSoRatherThanReadingLikeAWait()
		{
			foreach (BountyBlock block in Enum.GetValues(typeof(BountyBlock)))
			{
				if (!KingdomBountyRules.IsPermanent(block))
				{
					continue;
				}
				string reason = KingdomBountyRules.BlockReason(block, BountyTask.Clearance, "Ulu");
				Assert.IsTrue(reason.Contains("No one will ever claim it"), "block " + block + " reads as a wait: " + reason);
			}
		}

		[Test]
		public void BlockReason_NamesTheSettlementAndSurvivesHavingNoNameForIt()
		{
			Assert.IsTrue(KingdomBountyRules.BlockReason(BountyBlock.NoIdleWork, BountyTask.Manning, "Ulu").Contains("Ulu"));
			Assert.IsTrue(KingdomBountyRules.BlockReason(BountyBlock.NoIdleWork, BountyTask.Manning, null).Contains("the settlement"));
			Assert.IsTrue(KingdomBountyRules.BlockReason(BountyBlock.NoIdleWork, BountyTask.Manning, "").Contains("the settlement"));
		}

		[Test]
		public void BlockReason_NamesTheTaskSoAFounderWithThreeNoticesKnowsWhichWentQuiet()
		{
			string clearance = KingdomBountyRules.BlockReason(BountyBlock.NobodyToTry, BountyTask.Clearance, "Ulu");
			string scouting = KingdomBountyRules.BlockReason(BountyBlock.NobodyToTry, BountyTask.Scouting, "Ulu");
			Assert.AreNotEqual(clearance, scouting);
			Assert.IsTrue(clearance.Contains(KingdomBountyRules.TaskName(BountyTask.Clearance)));
			Assert.IsTrue(scouting.Contains(KingdomBountyRules.TaskName(BountyTask.Scouting)));
		}

		// --- The prose ------------------------------------------------------------------------

		[TestCase(BountyTask.Clearance)]
		[TestCase(BountyTask.Fetch)]
		[TestCase(BountyTask.Manning)]
		[TestCase(BountyTask.Scouting)]
		public void NoticeText_StatesThePriceAndReadsDifferentlyForEachTask(BountyTask task)
		{
			string text = KingdomBountyRules.NoticeText(task, 7, null);
			Assert.IsTrue(text.Contains("7 drams"), "the notice does not say what it pays: " + text);
			Assert.AreNotEqual(KingdomBountyRules.NoticeText(BountyTask.Clearance, 7, null), KingdomBountyRules.NoticeText(BountyTask.Scouting, 7, null));
		}

		[Test]
		public void NoticeText_CountsOneDramAsOneDram()
		{
			Assert.IsTrue(KingdomBountyRules.NoticeText(BountyTask.Fetch, 1, null).Contains("1 dram of"));
			Assert.IsFalse(KingdomBountyRules.NoticeText(BountyTask.Fetch, 1, null).Contains("1 drams"));
		}

		[Test]
		public void NoticeText_ClampsThePriceItPrintsSoNoNoticeEverPromisesNothing()
		{
			Assert.IsTrue(KingdomBountyRules.NoticeText(BountyTask.Fetch, 0, null).Contains(KingdomBountyRules.MinPrice + " dram"));
			Assert.IsTrue(KingdomBountyRules.NoticeText(BountyTask.Fetch, 9999, null).Contains(KingdomBountyRules.MaxPrice + " drams"));
		}

		[Test]
		public void NoticeText_CarriesTheDetailClauseWhenThereIsOneAndReadsWholeWhenThereIsNot()
		{
			Assert.IsTrue(KingdomBountyRules.NoticeText(BountyTask.Clearance, 5, "The cord runs round 25 paces of it.").Contains("25 paces"));
			Assert.IsFalse(KingdomBountyRules.NoticeText(BountyTask.Clearance, 5, null).EndsWith(" "));
		}

		[Test]
		public void PostedChronicle_IsALowerCaseClauseWithNoTrailingPeriod()
		{
			string line = KingdomBountyRules.PostedChronicle("Ulu", BountyTask.Clearance, 5);
			Assert.IsFalse(line.EndsWith("."));
			Assert.AreEqual(char.ToLowerInvariant(line[0]), line[0]);
			Assert.IsTrue(line.Contains("Ulu"));
			Assert.IsTrue(line.Contains("5 drams"));
		}

		[Test]
		public void RefusedChronicle_NamesTheSettlerAndGivesTheirOwnDrawnFlawAsTheReason()
		{
			string line = KingdomBountyRules.RefusedChronicle("Aeru", BountyTask.Fetch, 3);
			Assert.IsTrue(line.StartsWith("Aeru "));
			Assert.IsTrue(line.Contains(KingdomCeremonyRules.FlawText(3)));
			Assert.IsFalse(line.EndsWith("."));
		}

		[Test]
		public void RefusedChronicle_NeverLosesTheDeedWhenTheNameIsMissing()
		{
			Assert.IsTrue(KingdomBountyRules.RefusedChronicle(null, BountyTask.Fetch, 0).StartsWith("somebody "));
			Assert.IsTrue(KingdomBountyRules.RefusedChronicle("", BountyTask.Fetch, 0).StartsWith("somebody "));
		}

		[Test]
		public void TakenChronicle_NamesTheSettlerTheirVirtueAndWhetherItWasTheirOwnTaste()
		{
			string plain = KingdomBountyRules.TakenChronicle("Voss", BountyTask.Manning, 2, TasteMatched: false);
			string tasted = KingdomBountyRules.TakenChronicle("Voss", BountyTask.Manning, 2, TasteMatched: true);
			Assert.AreNotEqual(plain, tasted);
			Assert.IsTrue(tasted.Contains("the very thing they had said they wanted to see"));
			Assert.IsTrue(plain.Contains(KingdomCeremonyRules.VirtueText(2)));
			Assert.IsTrue(plain.StartsWith("Voss "));
		}

		[Test]
		public void PaidChronicle_SaysWhatLeftTheStoresAndInFrontOfWhom()
		{
			string line = KingdomBountyRules.PaidChronicle("Kest", "Ulu", BountyTask.Scouting, 6);
			Assert.IsTrue(line.Contains("Kest"));
			Assert.IsTrue(line.Contains("Ulu"));
			Assert.IsTrue(line.Contains("6 drams"));
			Assert.IsFalse(line.EndsWith("."));
		}

		[Test]
		public void OwedChronicle_StatesTheDebtPlainlyRatherThanWritingItOff()
		{
			string part = KingdomBountyRules.OwedChronicle("Kest", "Ulu", 3, 5);
			Assert.IsTrue(part.Contains("3 drams"));
			Assert.IsTrue(part.Contains("5 still owed"));
			string none = KingdomBountyRules.OwedChronicle("Kest", "Ulu", 0, 8);
			Assert.IsTrue(none.Contains("not a dram"));
			Assert.AreNotEqual(part, none);
		}

		[Test]
		public void OwedLedgerNote_NamesTheCreditorAndTheAmountAndPromisesIt()
		{
			string note = KingdomBountyRules.OwedLedgerNote("Kest", 4);
			Assert.IsTrue(note.Contains("Kest"));
			Assert.IsTrue(note.Contains("4 drams"));
			Assert.IsTrue(note.Contains("the day the stores can cover it"));
			Assert.IsTrue(KingdomBountyRules.OwedLedgerNote(null, 1).Contains("1 dram "));
		}

		[Test]
		public void WithdrawnChronicle_RemembersARefusalAndSaysNothingIsAskedBack()
		{
			string unclaimed = KingdomBountyRules.WithdrawnChronicle("Ulu", BountyTask.Fetch, Claimed: false, Name: null);
			string claimed = KingdomBountyRules.WithdrawnChronicle("Ulu", BountyTask.Fetch, Claimed: true, Name: "Aeru");
			Assert.AreNotEqual(unclaimed, claimed);
			Assert.IsTrue(unclaimed.Contains("unclaimed and unpaid for"));
			Assert.IsTrue(claimed.Contains("Aeru"));
			Assert.IsTrue(claimed.Contains("nobody was made to give anything back"));
		}

		[Test]
		public void WithdrawnChronicle_FallsBackToTheUnclaimedTellingWhenNobodyIsNamed()
		{
			Assert.AreEqual(KingdomBountyRules.WithdrawnChronicle("Ulu", BountyTask.Fetch, false, null),
				KingdomBountyRules.WithdrawnChronicle("Ulu", BountyTask.Fetch, true, null));
		}

		[Test]
		public void ScoutChronicle_NamesTheGroundWhenItHasOneAndStaysASentenceWhenItDoesNot()
		{
			string named = KingdomBountyRules.ScoutChronicle("Kest", "Ulu", "a salt marsh");
			Assert.IsTrue(named.Contains("a salt marsh"));
			Assert.IsTrue(named.Contains("Kest"));
			string unnamed = KingdomBountyRules.ScoutChronicle("Kest", "Ulu", null);
			Assert.IsFalse(unnamed.EndsWith(":"));
			Assert.AreNotEqual(named, unnamed);
		}

		[Test]
		public void ScoutDeed_IsALowerCaseNounPhraseFitForTheArrivalGrammar()
		{
			string deed = KingdomBountyRules.ScoutDeed("Ulu");
			Assert.AreEqual(char.ToLowerInvariant(deed[0]), deed[0]);
			Assert.IsFalse(deed.EndsWith("."));
			Assert.IsTrue(deed.Contains("Ulu"));
		}

		// --- Durable lifecycle fault decisions --------------------------------------------

		[Test]
		public void NoticeEventId_IsStableBoundedAndDistinctPerNotice()
		{
			string first = KingdomBountyRules.NoticeEventId("Notice / 41");
			Assert.AreEqual(first, KingdomBountyRules.NoticeEventId("Notice / 41"));
			Assert.AreNotEqual(first, KingdomBountyRules.NoticeEventId("Notice / 42"));
			Assert.IsTrue(KingdomBountyRules.IsNoticeEventId(first));
			Assert.LessOrEqual(first.Length, 180);
		}

		[Test]
		public void TransferAction_ReloadNeverContinuesAMutationIntent()
		{
			Assert.AreEqual(BountyTransferAction.Bind, KingdomBountyRules.TransferAction(
				BountyTransferPhase.None, BountyTransferLocation.SourceOnly));
			Assert.AreEqual(BountyTransferAction.Remove, KingdomBountyRules.TransferAction(
				BountyTransferPhase.Bound, BountyTransferLocation.SourceOnly));
			Assert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.RemoveIntent, BountyTransferLocation.Detached));
			Assert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.Detached, BountyTransferLocation.Detached));
			Assert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.AddIntent, BountyTransferLocation.DestinationOnly));
			Assert.AreEqual(BountyTransferAction.Confirm, KingdomBountyRules.TransferAction(
				BountyTransferPhase.Arrived, BountyTransferLocation.DestinationOnly));
			Assert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.RemoveIntent, BountyTransferLocation.SourceOnly),
				"an interrupted remove callback may have restored the source");
			Assert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.AddIntent, BountyTransferLocation.Detached),
				"an interrupted add callback may have detached the item again");
			Assert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.Arrived, BountyTransferLocation.Both));
		}

		[Test]
		public void ObservePayment_DistinguishesOriginalCompleteMixedAndUncertainReceipts()
		{
			int proved;
			int[] original = new int[2] { 10, 8 };
			int[] allocation = new int[2] { 3, 3 };
			bool[] same = new bool[2] { true, true };
			bool[] pure = new bool[2] { true, true };
			Assert.AreEqual(BountyPaymentObservation.Original, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 10, 8 }, allocation, same, pure, out proved));
			Assert.AreEqual(0, proved);
			Assert.AreEqual(BountyPaymentObservation.Debited, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 7, 5 }, allocation, same, pure, out proved));
			Assert.AreEqual(6, proved);
			Assert.AreEqual(BountyPaymentObservation.Mixed, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 7, 8 }, allocation, same, pure, out proved));
			Assert.AreEqual(3, proved);
			Assert.AreEqual(BountyPaymentObservation.Uncertain, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 9, 8 }, allocation, same, pure, out proved));
			Assert.AreEqual(0, proved);
			Assert.AreEqual(BountyPaymentObservation.Uncertain, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 7, 5 }, allocation,
				new bool[2] { true, false }, pure, out proved));
		}

		[Test]
		public void PaymentAction_NeverDebitsAgainAfterAnIntent()
		{
			Assert.AreEqual(BountyPaymentAction.Bind, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.None, BountyPaymentObservation.Malformed));
			Assert.AreEqual(BountyPaymentAction.Debit, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.Bound, BountyPaymentObservation.Original));
			Assert.AreEqual(BountyPaymentAction.Quarantine, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.DebitIntent, BountyPaymentObservation.Original));
			Assert.AreEqual(BountyPaymentAction.Quarantine, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.DebitIntent, BountyPaymentObservation.Debited));
			Assert.AreEqual(BountyPaymentAction.Quarantine, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.DebitIntent, BountyPaymentObservation.Mixed));
			Assert.AreEqual(BountyPaymentAction.Wait, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.Credited, BountyPaymentObservation.Original));
		}

		[Test]
		public void ValidLifecycleScalars_RejectsMalformedReloadStateFailClosed()
		{
			Assert.IsTrue(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 3,
				true, "Aeru", 2, KingdomBountyRules.NoticeEventStream("41"), 1200L,
				false, 7, 0, 0, 0, 0));
			Assert.IsFalse(KingdomBountyRules.ValidLifecycleScalars(99, 8, 0, false, null,
				0, null, 0L, false, 0, 0, 0, 0, 0));
			Assert.IsFalse(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 9,
				false, null, 0, null, 0L, false, 0, 0, 0, 0, 0));
			Assert.IsFalse(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 0,
				true, null, 0, null, 0L, false, 0, 0, 0, 0, 0));
			Assert.IsFalse(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 0,
				false, null, 2, "bad-stream", 1200L, false, 0, 0, 0, 0, 0));
			Assert.IsFalse(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 0,
				false, null, 0, null, 0L, false, 0, 99, 0, 0, 0));
		}

		[Test]
		public void BountySource_WiresLiveFramesBeforeExactPaymentAndOneShotTerminalCleanup()
		{
			string source = ReadRepoSource("Quests/KingdomBounty.cs");
			int paymentIntent = source.IndexOf(
				"Data.PaymentPhase = (int)BountyPaymentPhase.DebitIntent", StringComparison.Ordinal);
			int paymentCall = source.IndexOf("bool committed = debit.Commit()", paymentIntent,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(paymentIntent, 0);
			Assert.Greater(paymentCall, paymentIntent);
			StringAssert.Contains("TryCaptureBoundPayment(Data, Z, Survey, Notice", source);
			StringAssert.Contains("ObserveCapturedPayment(frame", source);
			StringAssert.Contains("ReferenceEquals(vessel.ComponentLiquids, Frame.Dictionaries[i])", source);
			StringAssert.Contains("ReferenceEquals(Frame.Survey.Stores, Frame.Stores)", source);
			StringAssert.Contains("PendingWorkerResidentId = ResidentIdFor", source);
			StringAssert.Contains("KingdomChronicle.RecordOnce", source);
			int cleanupIntent = source.IndexOf(
				"Data.TerminalPhase = (int)BountyTerminalPhase.CleanupAttempting",
				StringComparison.Ordinal);
			int cleanupCall = source.IndexOf("InvokeCleanupOnce(Notice, false)", cleanupIntent,
				StringComparison.Ordinal);
			Assert.Greater(cleanupCall, cleanupIntent);
			Assert.AreEqual(1, Count(source, ".Obliterate("));
			int recovery = source.IndexOf("else if (phase == BountyTerminalPhase.CleanupAttempting)",
				cleanupCall, StringComparison.Ordinal);
			int recoveryEnd = source.IndexOf("\n\t\t}\n", recovery, StringComparison.Ordinal);
			Assert.Greater(recovery, cleanupCall);
			Assert.IsFalse(source.Substring(recovery, recoveryEnd - recovery).Contains("InvokeCleanupOnce"));
		}

		[Test]
		public void SavedRowParsers_CapRawTextRowsAndFieldsBeforeSplit()
		{
			int[] numbers;
			string[] ids;
			Assert.IsTrue(KingdomBountyRules.TryCanonicalIntRows("0|7|2147483647", out numbers));
			Assert.AreEqual(3, numbers.Length);
			Assert.IsFalse(KingdomBountyRules.TryCanonicalIntRows("01", out numbers));
			Assert.IsFalse(KingdomBountyRules.TryCanonicalIntRows(
				new string('1', KingdomBountyRules.MaxPaymentRowsChars + 1), out numbers));
			Assert.IsFalse(KingdomBountyRules.TryCanonicalIntRows(
				new string('|', KingdomBountyRules.MaxPaymentRows), out numbers));
			Assert.IsTrue(KingdomBountyRules.TryObjectIdRows("vessel-1|vessel-2", out ids));
			Assert.IsFalse(KingdomBountyRules.TryObjectIdRows("same|same", out ids));
			string rules = ReadRepoSource("Quests/KingdomBountyRules.cs");
			Assert.Less(rules.IndexOf("Text.Length > MaxPaymentRowsChars", StringComparison.Ordinal),
				rules.IndexOf("Text.Split('|')", StringComparison.Ordinal));
		}

		[Test]
		public void UninspectableSinkRecovery_IsExplicitLossNotClaimedDelivery()
		{
			Assert.AreEqual(BountySinkDisposition.Lost,
				KingdomBountyRules.RecoverUninspectable(BountySinkDisposition.Attempting));
			Assert.AreEqual(BountySinkDisposition.Pending,
				KingdomBountyRules.RecoverUninspectable(BountySinkDisposition.Pending));
			Assert.IsTrue(KingdomBountyRules.SinkSettled(BountySinkDisposition.Delivered));
			Assert.IsTrue(KingdomBountyRules.SinkSettled(BountySinkDisposition.Skipped));
			Assert.IsTrue(KingdomBountyRules.SinkSettled(BountySinkDisposition.Lost));
			Assert.IsFalse(KingdomBountyRules.SinkSettled(BountySinkDisposition.Attempting));
		}

		private static int Count(string Text, string Needle)
		{
			int count = 0;
			for (int at = 0; ; )
			{
				at = Text.IndexOf(Needle, at, StringComparison.Ordinal);
				if (at < 0) return count;
				count++;
				at += Needle.Length;
			}
		}
	}
}
#endif
