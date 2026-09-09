#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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

		private static void AssertEnumAbi(Type Type, string Names)
		{
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(Type), Type.Name);
			ClassicAssert.AreEqual("ThousandAndFirst." + Type.Name, Type.FullName);
			ClassicAssert.AreEqual(Names, string.Join("|", Enum.GetNames(Type)), Type.Name);
			Array values = Enum.GetValues(Type);
			for (int i = 0; i < values.Length; i++)
			{
				ClassicAssert.AreEqual(i, Convert.ToInt32(values.GetValue(i)), Type.Name + "[" + i + "]");
			}
		}

		[Test]
		public void PersistedEnumsAndNestedAttemptAbi_RemainExactAfterDecomposition()
		{
			AssertEnumAbi(typeof(BountyTask), "Clearance|Fetch|Manning|Scouting");
			AssertEnumAbi(typeof(BountyBlock), "None|NobodyToTry|NothingStanding|PileEmpty|NowhereToCarry|NoWorks|NoIdleWork|NoFrontier|StoresCannotPay|ManningTargetLost|ManningWorkerAbsent|NoFreeHands");
			AssertEnumAbi(typeof(BountyOutcome), "NobodyTried|Refused|Taken");
			AssertEnumAbi(typeof(BountyTakePhase), "None|Bound|TaskIntent|TaskDone|ChronicleDone|LedgerIntent|LedgerDone|MessageIntent|MessageDone|Complete|Quarantined");
			AssertEnumAbi(typeof(BountyTransferPhase), "None|Bound|RemoveIntent|Detached|AddIntent|Arrived|Quarantined");
			AssertEnumAbi(typeof(BountyTransferLocation), "Missing|SourceOnly|Detached|DestinationOnly|Both|Elsewhere");
			AssertEnumAbi(typeof(BountyTransferAction), "Wait|Bind|Remove|Add|Confirm|Quarantine");
			AssertEnumAbi(typeof(BountySinkDisposition), "None|Pending|Attempting|Delivered|Skipped|Lost");
			AssertEnumAbi(typeof(BountyPostPhase), "None|Bound|ChronicleDone|MessageSettled|Complete");
			AssertEnumAbi(typeof(BountyWithdrawPhase), "None|Bound|MarkCleared|ChronicleDone|MessageSettled|CleanupAttempting|CleanupLost");
			AssertEnumAbi(typeof(BountyPaymentPhase), "None|Bound|DebitIntent|Debited|Credited|Quarantined");
			AssertEnumAbi(typeof(BountyPaymentObservation), "Malformed|Original|Debited|Mixed|Uncertain");
			AssertEnumAbi(typeof(BountyPaymentAction), "Wait|Bind|Debit|Credit|Quarantine");
			AssertEnumAbi(typeof(BountyTerminalPhase), "None|ChronicleDone|LedgerIntent|LedgerDone|MessageIntent|MessageDone|CleanupAttempting|CleanupLost");

			Type attempt = typeof(KingdomBountyRules.BountyAttempt);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomBountyRules+BountyAttempt", attempt.FullName);
			ClassicAssert.AreEqual(typeof(bool), attempt.GetField("Determined").FieldType);
			ClassicAssert.AreEqual(typeof(BountyOutcome), attempt.GetField("Outcome").FieldType);
			ClassicAssert.AreEqual(typeof(string), attempt.GetField("Name").FieldType);
			ClassicAssert.AreEqual(typeof(int), attempt.GetField("RosterIndex").FieldType);
			ClassicAssert.AreEqual(typeof(int), attempt.GetField("VirtueIndex").FieldType);
			ClassicAssert.AreEqual(typeof(int), attempt.GetField("FlawIndex").FieldType);
			ClassicAssert.AreEqual(typeof(bool), attempt.GetField("TasteMatched").FieldType);
			ClassicAssert.AreEqual(7, attempt.GetFields().Length);
		}

		// --- The task tables ------------------------------------------------------------------

		[Test]
		public void TaskCount_MatchesTheEnumSoANewTaskCannotBeForgottenInTheTables()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.TaskCount, Enum.GetValues(typeof(BountyTask)).Length);
			ClassicAssert.AreEqual(KingdomBountyRules.TaskCount, KingdomBountyRules.TaskKeys.Length);
			ClassicAssert.AreEqual(KingdomBountyRules.TaskCount, KingdomBountyRules.TaskNames.Length);
			ClassicAssert.AreEqual(KingdomBountyRules.TaskCount, KingdomBountyRules.TaskTasteCategories.Length);
			ClassicAssert.AreEqual(KingdomBountyRules.TaskCount, KingdomBountyRules.TakeBaseChance.Length);
		}

		[Test]
		public void TaskKeysAndNames_AreAllDistinct()
		{
			HashSet<string> keys = new HashSet<string>(KingdomBountyRules.TaskKeys, StringComparer.Ordinal);
			HashSet<string> names = new HashSet<string>(KingdomBountyRules.TaskNames, StringComparer.Ordinal);
			ClassicAssert.AreEqual(KingdomBountyRules.TaskCount, keys.Count);
			ClassicAssert.AreEqual(KingdomBountyRules.TaskCount, names.Count);
		}

		[TestCase(BountyTask.Clearance, "clearance")]
		[TestCase(BountyTask.Fetch, "fetch")]
		[TestCase(BountyTask.Manning, "manning")]
		[TestCase(BountyTask.Scouting, "scouting")]
		public void TaskKey_NamesItsOwnTask(BountyTask task, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomBountyRules.TaskKey(task));
		}

		[Test]
		public void TaskKeyAndName_FallBackRatherThanThrowOnAValueOutsideTheEnum()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.TaskKeys[0], KingdomBountyRules.TaskKey((BountyTask)99));
			ClassicAssert.AreEqual(KingdomBountyRules.TaskNames[0], KingdomBountyRules.TaskName((BountyTask)99));
			ClassicAssert.AreEqual(KingdomBountyRules.TaskKeys[0], KingdomBountyRules.TaskKey((BountyTask)(-1)));
		}

		[TestCase(BountyTask.Clearance)]
		[TestCase(BountyTask.Fetch)]
		[TestCase(BountyTask.Manning)]
		[TestCase(BountyTask.Scouting)]
		public void TasteIndexFor_ResolvesIntoTheCeremonysOwnFamilyList(BountyTask task)
		{
			int index = KingdomBountyRules.TasteIndexFor(task);
			ClassicAssert.IsTrue(index >= 0, "task " + task + " names a family the ceremony does not carry");
			ClassicAssert.IsTrue(index < KingdomCeremonyRules.TasteCategories.Length);
			ClassicAssert.AreEqual(KingdomBountyRules.TaskTasteCategories[(int)task], KingdomCeremonyRules.TasteCategories[index]);
		}

		[Test]
		public void TasteIndexFor_ReportsMinusOneForATaskOutsideTheEnumRatherThanIndexZero()
		{
			ClassicAssert.AreEqual(-1, KingdomBountyRules.TasteIndexFor((BountyTask)99));
		}

		[Test]
		public void TasteIndexFor_GivesEachTaskItsOwnFamily()
		{
			HashSet<int> seen = new HashSet<int>();
			for (int i = 0; i < KingdomBountyRules.TaskCount; i++)
			{
				ClassicAssert.IsTrue(seen.Add(KingdomBountyRules.TasteIndexFor((BountyTask)i)), "two tasks share a taste family");
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
			ClassicAssert.AreEqual(expected, KingdomBountyRules.ClampPrice(given));
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
					ClassicAssert.IsTrue(price >= KingdomBountyRules.MinPrice && price <= KingdomBountyRules.MaxPrice,
						"task " + (BountyTask)i + " magnitude " + magnitudes[j] + " suggested " + price);
				}
			}
		}

		[Test]
		public void SuggestedPrice_AsksMoreForMoreGroundAndMoreLoads()
		{
			ClassicAssert.IsTrue(KingdomBountyRules.SuggestedPrice(BountyTask.Clearance, 40) > KingdomBountyRules.SuggestedPrice(BountyTask.Clearance, 4));
			ClassicAssert.IsTrue(KingdomBountyRules.SuggestedPrice(BountyTask.Fetch, 30) > KingdomBountyRules.SuggestedPrice(BountyTask.Fetch, 3));
		}

		[Test]
		public void SuggestedPrice_IgnoresMagnitudeForTheTwoTasksThatHaveNoSize()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.SuggestedPrice(BountyTask.Manning, 0), KingdomBountyRules.SuggestedPrice(BountyTask.Manning, 900));
			ClassicAssert.AreEqual(KingdomBountyRules.SuggestedPrice(BountyTask.Scouting, 0), KingdomBountyRules.SuggestedPrice(BountyTask.Scouting, 900));
		}

		// --- Who reads it ---------------------------------------------------------------------

		[Test]
		public void PersonOrdinal_AlwaysSetsTheTopBitSoAPersonDrawCanNeverLandOnATickDraw()
		{
			string[] names = new string[5] { "Aeru", "Voss", "", null, "a very long settler name indeed" };
			for (int i = 0; i < names.Length; i++)
			{
				ulong ordinal = KingdomBountyRules.PersonOrdinal(names[i]);
				ClassicAssert.IsTrue((ordinal & 0x8000000000000000uL) != 0uL, "top bit clear for '" + names[i] + "'");
				ClassicAssert.IsTrue(ordinal > (ulong)long.MaxValue, "ordinal is reachable by a tick count");
			}
		}

		[Test]
		public void PersonOrdinal_IsStableForOneNameAndDistinctBetweenNames()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.PersonOrdinal("Aeru"), KingdomBountyRules.PersonOrdinal("Aeru"));
			ClassicAssert.AreNotEqual(KingdomBountyRules.PersonOrdinal("Aeru"), KingdomBountyRules.PersonOrdinal("Voss"));
			ClassicAssert.AreNotEqual(KingdomBountyRules.PersonOrdinal("Aeru"), KingdomBountyRules.PersonOrdinal("aeru"));
			ClassicAssert.AreNotEqual(KingdomBountyRules.PersonOrdinal("ab"), KingdomBountyRules.PersonOrdinal("ba"));
		}

		[Test]
		public void PersonOrdinal_TreatsNullAndEmptyAsTheSameStableOrdinalRatherThanThrowing()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.PersonOrdinal(null), KingdomBountyRules.PersonOrdinal(""));
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
			ClassicAssert.AreEqual(expected, KingdomBountyRules.TraitAppetite(virtueIndex, flawIndex));
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
			ClassicAssert.AreEqual(3, seen.Count, "no notable is ever eager, or none is ever reluctant");
		}

		// --- The chances ----------------------------------------------------------------------

		[Test]
		public void ReadChancePercent_RisesWithThePriceAndStopsAtTheCeiling()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.ReadBaseChance + KingdomBountyRules.ReadChancePerDram, KingdomBountyRules.ReadChancePercent(1));
			ClassicAssert.IsTrue(KingdomBountyRules.ReadChancePercent(12) > KingdomBountyRules.ReadChancePercent(3));
			ClassicAssert.AreEqual(KingdomBountyRules.ReadChanceCeiling, KingdomBountyRules.ReadChancePercent(KingdomBountyRules.MaxPrice));
		}

		[Test]
		public void ReadChancePercent_ClampsThePriceBeforeReadingIt()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.ReadChancePercent(1), KingdomBountyRules.ReadChancePercent(0));
			ClassicAssert.AreEqual(KingdomBountyRules.ReadChancePercent(1), KingdomBountyRules.ReadChancePercent(-40));
			ClassicAssert.AreEqual(KingdomBountyRules.ReadChancePercent(KingdomBountyRules.MaxPrice), KingdomBountyRules.ReadChancePercent(9999));
		}

		[Test]
		public void ReadChancePercent_NeverReachesCertainty()
		{
			for (int price = -5; price <= 60; price++)
			{
				ClassicAssert.IsTrue(KingdomBountyRules.ReadChancePercent(price) <= KingdomBountyRules.ReadChanceCeiling);
				ClassicAssert.IsTrue(KingdomBountyRules.ReadChancePercent(price) < 100);
			}
		}

		[TestCase(BountyTask.Clearance)]
		[TestCase(BountyTask.Fetch)]
		[TestCase(BountyTask.Manning)]
		[TestCase(BountyTask.Scouting)]
		public void TakeChancePercent_StartsFromItsOwnTasksBase(BountyTask task)
		{
			ClassicAssert.AreEqual(KingdomBountyRules.TakeBaseChance[(int)task] + KingdomBountyRules.TakeChancePerDram,
				KingdomBountyRules.TakeChancePercent(task, 1, Notable: false, TasteMatched: false, Appetite: 0));
		}

		[Test]
		public void TakeChancePercent_EveryShadeActuallyMovesTheNumber()
		{
			int plain = KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, false, false, 0);
			ClassicAssert.AreEqual(plain + KingdomBountyRules.TakeTasteBonus, KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, false, true, 0));
			ClassicAssert.AreEqual(plain + KingdomBountyRules.TakeNotableBonus, KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, true, false, 0));
			ClassicAssert.AreEqual(plain + KingdomBountyRules.TakeAppetiteWeight, KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, false, false, 1));
			ClassicAssert.AreEqual(plain - KingdomBountyRules.TakeAppetiteWeight, KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 1, false, false, -1));
			ClassicAssert.IsTrue(KingdomBountyRules.TakeChancePercent(BountyTask.Manning, 20, false, false, 0) > plain);
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
							ClassicAssert.IsTrue(chance >= KingdomBountyRules.TakeChanceFloor, "below the floor: " + chance);
							ClassicAssert.IsTrue(chance <= KingdomBountyRules.TakeChanceCeiling, "above the ceiling: " + chance);
						}
					}
				}
			}
		}

		[Test]
		public void TakeChancePercent_ClampsThePriceSoAHugeOfferCannotBuyCertainty()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.TakeChancePercent(BountyTask.Fetch, KingdomBountyRules.MaxPrice, true, true, 1),
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
				ClassicAssert.AreEqual(first.Outcome, second.Outcome);
				ClassicAssert.AreEqual(first.Name, second.Name);
				ClassicAssert.AreEqual(first.RosterIndex, second.RosterIndex);
				ClassicAssert.AreEqual(first.VirtueIndex, second.VirtueIndex);
				ClassicAssert.AreEqual(first.FlawIndex, second.FlawIndex);
				ClassicAssert.AreEqual(first.TasteMatched, second.TasteMatched);
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
			ClassicAssert.IsTrue(differences > 0, "two settlements drew the same notice the same way every pass");
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
			ClassicAssert.IsTrue(seen.Count > 1, "every pass drew the same reader and the same answer");
		}

		[Test]
		public void Resolve_ReportsNobodyTriedWhenThereIsNobodyOnTheRoster()
		{
			KingdomBountyRules.BountyAttempt empty = KingdomBountyRules.Resolve(Settlement, 1200L, 0, new List<string>(), BountyTask.Fetch, 40);
			ClassicAssert.AreEqual(BountyOutcome.NobodyTried, empty.Outcome);
			ClassicAssert.IsNull(empty.Name);
			ClassicAssert.AreEqual(-1, empty.RosterIndex);
			KingdomBountyRules.BountyAttempt none = KingdomBountyRules.Resolve(Settlement, 1200L, 0, null, BountyTask.Fetch, 40);
			ClassicAssert.AreEqual(BountyOutcome.NobodyTried, none.Outcome);
		}

		[Test]
		public void Resolve_FailsClosedAndCostsNothingWhenTheKernelRefusesTheKey()
		{
			List<string> roster = Roster("Aeru", "Voss");
			for (int pass = 0; pass < 20; pass++)
			{
				ClassicAssert.AreEqual(BountyOutcome.NobodyTried, KingdomBountyRules.Resolve(null, 1200L, pass, roster, BountyTask.Fetch, 40).Outcome);
				ClassicAssert.AreEqual(BountyOutcome.NobodyTried, KingdomBountyRules.Resolve("no", 1200L, pass, roster, BountyTask.Fetch, 40).Outcome);
				ClassicAssert.AreEqual(BountyOutcome.NobodyTried, KingdomBountyRules.Resolve("NOT THE GRAMMAR", 1200L, pass, roster, BountyTask.Fetch, 40).Outcome);
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
					ClassicAssert.IsNull(attempt.Name);
					continue;
				}
				ClassicAssert.IsTrue(attempt.RosterIndex >= 0 && attempt.RosterIndex < roster.Count);
				ClassicAssert.AreEqual(roster[attempt.RosterIndex], attempt.Name);
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
			ClassicAssert.AreEqual(3, readers.Count, "the notice only ever reached " + readers.Count + " of the three settlers");
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
			ClassicAssert.IsTrue(rich > cheap, "the posted price bought nothing: cheap=" + cheap + " rich=" + rich);
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
			ClassicAssert.IsTrue(refusals > 0, "nobody ever refused a posted notice");
			ClassicAssert.IsTrue(takings > 0, "nobody ever took a posted notice");
		}

		[Test]
		public void Resolve_ClampsAWildPassIndexRatherThanOverflowingTheDrawIndex()
		{
			List<string> roster = Roster("Aeru");
			KingdomBountyRules.BountyAttempt capped = KingdomBountyRules.Resolve(Settlement, 1200L, KingdomBountyRules.MaxPasses, roster, BountyTask.Fetch, 8);
			KingdomBountyRules.BountyAttempt beyond = KingdomBountyRules.Resolve(Settlement, 1200L, int.MaxValue, roster, BountyTask.Fetch, 8);
			ClassicAssert.AreEqual(capped.Outcome, beyond.Outcome);
			KingdomBountyRules.BountyAttempt negative = KingdomBountyRules.Resolve(Settlement, 1200L, -7, roster, BountyTask.Fetch, 8);
			KingdomBountyRules.BountyAttempt zero = KingdomBountyRules.Resolve(Settlement, 1200L, 0, roster, BountyTask.Fetch, 8);
			ClassicAssert.AreEqual(zero.Outcome, negative.Outcome);
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
			ClassicAssert.IsNotNull(tasteful, "no settler in five hundred ever wanted the stores kept ahead of need");
			ClassicAssert.IsNotNull(indifferent);
			int read = 0;
			for (int pass = 0; pass < 60; pass++)
			{
				KingdomBountyRules.BountyAttempt yes = KingdomBountyRules.Resolve(Settlement, 8400L, pass, Roster(tasteful), BountyTask.Fetch, KingdomBountyRules.MaxPrice);
				if (yes.Name != null)
				{
					read++;
					ClassicAssert.IsTrue(yes.TasteMatched, "a settler who stated the task's own taste was not credited with it");
				}
				KingdomBountyRules.BountyAttempt no = KingdomBountyRules.Resolve(Settlement, 8401L, pass, Roster(indifferent), BountyTask.Fetch, KingdomBountyRules.MaxPrice);
				if (no.Name != null)
				{
					ClassicAssert.IsFalse(no.TasteMatched, "a settler who never stated the task's taste was credited with it anyway");
				}
			}
			ClassicAssert.IsTrue(read > 0, "nobody read the notice at all, so nothing was proved");
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
			ClassicAssert.IsTrue(keen > plain, "stating a taste for the work bought nothing");
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
				ClassicAssert.IsTrue(KingdomBountyRules.TryNeighbour(10, 10, step, out x, out y));
				ClassicAssert.IsFalse(x == 10 && y == 10, "step " + step + " named the zone itself");
				ClassicAssert.IsTrue(offsets.Add((x - 10) + "," + (y - 10)), "step " + step + " repeated another step");
			}
			ClassicAssert.AreEqual(KingdomBountyRules.NeighbourCount, offsets.Count);
		}

		[Test]
		public void TryNeighbour_RefusesAStepOutsideTheEight()
		{
			int x;
			int y;
			ClassicAssert.IsFalse(KingdomBountyRules.TryNeighbour(10, 10, -1, out x, out y));
			ClassicAssert.IsFalse(KingdomBountyRules.TryNeighbour(10, 10, KingdomBountyRules.NeighbourCount, out x, out y));
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
				ClassicAssert.IsTrue(x >= 0 && y >= 0);
			}
			ClassicAssert.AreEqual(5, refused, "the corner of the world let a scout walk off it");
		}

		[TestCase(0, 0, 0)]
		[TestCase(2, 0, 2)]
		[TestCase(3, 1, 0)]
		[TestCase(11, 3, 2)]
		public void TrySplitGlobal_FoldsBackIntoAParasangAndAZone(int global, int expectedParasang, int expectedZone)
		{
			int parasang;
			int zone;
			ClassicAssert.IsTrue(KingdomBountyRules.TrySplitGlobal(global, out parasang, out zone));
			ClassicAssert.AreEqual(expectedParasang, parasang);
			ClassicAssert.AreEqual(expectedZone, zone);
			ClassicAssert.AreEqual(global, parasang * KingdomBountyRules.ZonesPerParasang + zone);
		}

		[Test]
		public void TrySplitGlobal_RefusesNegativeGroundRatherThanNamingAZoneThatExists()
		{
			int parasang;
			int zone;
			ClassicAssert.IsFalse(KingdomBountyRules.TrySplitGlobal(-1, out parasang, out zone));
			ClassicAssert.IsFalse(KingdomBountyRules.TrySplitGlobal(-3, out parasang, out zone));
		}

		[Test]
		public void TryPickFrontier_RefusesWhenThereIsNothingToPickFrom()
		{
			int index;
			ClassicAssert.IsFalse(KingdomBountyRules.TryPickFrontier(Settlement, 1200L, 0, 0, out index));
			ClassicAssert.IsFalse(KingdomBountyRules.TryPickFrontier(Settlement, 1200L, 0, -4, out index));
		}

		[Test]
		public void TryPickFrontier_StaysInRangeAndIsStable()
		{
			for (int pass = 0; pass < 40; pass++)
			{
				int first;
				int second;
				ClassicAssert.IsTrue(KingdomBountyRules.TryPickFrontier(Settlement, 1200L, pass, 7, out first));
				ClassicAssert.IsTrue(KingdomBountyRules.TryPickFrontier(Settlement, 1200L, pass, 7, out second));
				ClassicAssert.AreEqual(first, second);
				ClassicAssert.IsTrue(first >= 0 && first < 7);
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
			ClassicAssert.IsTrue(picked.Count > 1, "every scout reported the same edge");
		}

		[Test]
		public void TryPickFrontier_FallsBackToARealCandidateWhenTheKernelRefuses()
		{
			int index;
			ClassicAssert.IsTrue(KingdomBountyRules.TryPickFrontier(null, 1200L, 0, 4, out index));
			ClassicAssert.AreEqual(0, index);
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
			ClassicAssert.AreEqual(expected, KingdomBountyRules.HaulDays(units));
		}

		[Test]
		public void WorkDays_ClearanceHasNoClockBecauseTheGangsOwnEffortIsItsClock()
		{
			ClassicAssert.AreEqual(0, KingdomBountyRules.WorkDays(BountyTask.Clearance, 40));
		}

		[Test]
		public void WorkDays_EveryOtherTaskRunsForARealNumberOfDays()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.HaulDays(16), KingdomBountyRules.WorkDays(BountyTask.Fetch, 16));
			ClassicAssert.AreEqual(KingdomBountyRules.ManningSeasonDays, KingdomBountyRules.WorkDays(BountyTask.Manning, 0));
			ClassicAssert.AreEqual(KingdomBountyRules.ScoutDays, KingdomBountyRules.WorkDays(BountyTask.Scouting, 0));
			ClassicAssert.IsTrue(KingdomBountyRules.ManningSeasonDays > KingdomBountyRules.ScoutDays);
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
		[TestCase(BountyBlock.ManningTargetLost, true)]
		[TestCase(BountyBlock.ManningWorkerAbsent, false)]
		[TestCase(BountyBlock.NoFreeHands, false)]
		public void IsPermanent_SeparatesWhatCanLiftFromWhatNeverWill(BountyBlock block, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomBountyRules.IsPermanent(block));
		}

		[Test]
		public void BlockReason_SaysNothingOnlyWhenThereIsNothingToSay()
		{
			ClassicAssert.IsNull(KingdomBountyRules.BlockReason(BountyBlock.None, BountyTask.Fetch, "Ulu"));
			foreach (BountyBlock block in Enum.GetValues(typeof(BountyBlock)))
			{
				if (block == BountyBlock.None)
				{
					continue;
				}
				string reason = KingdomBountyRules.BlockReason(block, BountyTask.Fetch, "Ulu");
				ClassicAssert.IsFalse(string.IsNullOrEmpty(reason), "block " + block + " stalls in silence");
				ClassicAssert.IsTrue(reason.EndsWith("."), "block " + block + " is not a sentence: " + reason);
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
				ClassicAssert.IsTrue(reason.Contains("No one will ever claim it")
					|| reason.Contains("take the notice down"),
					"block " + block + " reads as a wait: " + reason);
			}
		}

		[Test]
		public void BlockReason_NamesTheSettlementAndSurvivesHavingNoNameForIt()
		{
			ClassicAssert.IsTrue(KingdomBountyRules.BlockReason(BountyBlock.NoIdleWork, BountyTask.Manning, "Ulu").Contains("Ulu"));
			ClassicAssert.IsTrue(KingdomBountyRules.BlockReason(BountyBlock.NoIdleWork, BountyTask.Manning, null).Contains("the settlement"));
			ClassicAssert.IsTrue(KingdomBountyRules.BlockReason(BountyBlock.NoIdleWork, BountyTask.Manning, "").Contains("the settlement"));
			ClassicAssert.IsTrue(KingdomBountyRules.BlockReason(BountyBlock.ManningTargetLost,
				BountyTask.Manning, "Ulu").Contains("exact work"));
			ClassicAssert.IsTrue(KingdomBountyRules.BlockReason(BountyBlock.ManningWorkerAbsent,
				BountyTask.Manning, "Ulu").Contains("clock is stopped"));
			ClassicAssert.IsTrue(KingdomBountyRules.BlockReason(BountyBlock.NoFreeHands,
				BountyTask.Manning, "Ulu").Contains("ordinary work pool"));
		}

		[Test]
		public void BlockReason_NamesTheTaskSoAFounderWithThreeNoticesKnowsWhichWentQuiet()
		{
			string clearance = KingdomBountyRules.BlockReason(BountyBlock.NobodyToTry, BountyTask.Clearance, "Ulu");
			string scouting = KingdomBountyRules.BlockReason(BountyBlock.NobodyToTry, BountyTask.Scouting, "Ulu");
			ClassicAssert.AreNotEqual(clearance, scouting);
			ClassicAssert.IsTrue(clearance.Contains(KingdomBountyRules.TaskName(BountyTask.Clearance)));
			ClassicAssert.IsTrue(scouting.Contains(KingdomBountyRules.TaskName(BountyTask.Scouting)));
		}

		// --- The prose ------------------------------------------------------------------------

		[TestCase(BountyTask.Clearance)]
		[TestCase(BountyTask.Fetch)]
		[TestCase(BountyTask.Manning)]
		[TestCase(BountyTask.Scouting)]
		public void NoticeText_StatesThePriceAndReadsDifferentlyForEachTask(BountyTask task)
		{
			string text = KingdomBountyRules.NoticeText(task, 7, null);
			ClassicAssert.IsTrue(text.Contains("7 drams"), "the notice does not say what it pays: " + text);
			ClassicAssert.AreNotEqual(KingdomBountyRules.NoticeText(BountyTask.Clearance, 7, null), KingdomBountyRules.NoticeText(BountyTask.Scouting, 7, null));
		}

		[Test]
		public void NoticeText_CountsOneDramAsOneDram()
		{
			ClassicAssert.IsTrue(KingdomBountyRules.NoticeText(BountyTask.Fetch, 1, null).Contains("1 dram of"));
			ClassicAssert.IsFalse(KingdomBountyRules.NoticeText(BountyTask.Fetch, 1, null).Contains("1 drams"));
		}

		[Test]
		public void NoticeText_ClampsThePriceItPrintsSoNoNoticeEverPromisesNothing()
		{
			ClassicAssert.IsTrue(KingdomBountyRules.NoticeText(BountyTask.Fetch, 0, null).Contains(KingdomBountyRules.MinPrice + " dram"));
			ClassicAssert.IsTrue(KingdomBountyRules.NoticeText(BountyTask.Fetch, 9999, null).Contains(KingdomBountyRules.MaxPrice + " drams"));
		}

		[Test]
		public void NoticeText_CarriesTheDetailClauseWhenThereIsOneAndReadsWholeWhenThereIsNot()
		{
			ClassicAssert.IsTrue(KingdomBountyRules.NoticeText(BountyTask.Clearance, 5, "The cord runs round 25 paces of it.").Contains("25 paces"));
			ClassicAssert.IsFalse(KingdomBountyRules.NoticeText(BountyTask.Clearance, 5, null).EndsWith(" "));
		}

		[Test]
		public void PostedChronicle_IsALowerCaseClauseWithNoTrailingPeriod()
		{
			string line = KingdomBountyRules.PostedChronicle("Ulu", BountyTask.Clearance, 5);
			ClassicAssert.IsFalse(line.EndsWith("."));
			ClassicAssert.AreEqual(char.ToLowerInvariant(line[0]), line[0]);
			ClassicAssert.IsTrue(line.Contains("Ulu"));
			ClassicAssert.IsTrue(line.Contains("5 drams"));
		}

		[Test]
		public void RefusedChronicle_NamesTheSettlerAndGivesTheirOwnDrawnFlawAsTheReason()
		{
			string line = KingdomBountyRules.RefusedChronicle("Aeru", BountyTask.Fetch, 3);
			ClassicAssert.IsTrue(line.StartsWith("Aeru "));
			ClassicAssert.IsTrue(line.Contains(KingdomCeremonyRules.FlawText(3)));
			ClassicAssert.IsFalse(line.EndsWith("."));
		}

		[Test]
		public void RefusedChronicle_NeverLosesTheDeedWhenTheNameIsMissing()
		{
			ClassicAssert.IsTrue(KingdomBountyRules.RefusedChronicle(null, BountyTask.Fetch, 0).StartsWith("somebody "));
			ClassicAssert.IsTrue(KingdomBountyRules.RefusedChronicle("", BountyTask.Fetch, 0).StartsWith("somebody "));
		}

		[Test]
		public void TakenChronicle_NamesTheSettlerTheirVirtueAndWhetherItWasTheirOwnTaste()
		{
			string plain = KingdomBountyRules.TakenChronicle("Voss", BountyTask.Manning, 2, TasteMatched: false);
			string tasted = KingdomBountyRules.TakenChronicle("Voss", BountyTask.Manning, 2, TasteMatched: true);
			ClassicAssert.AreNotEqual(plain, tasted);
			ClassicAssert.IsTrue(tasted.Contains("the very thing they had said they wanted to see"));
			ClassicAssert.IsTrue(plain.Contains(KingdomCeremonyRules.VirtueText(2)));
			ClassicAssert.IsTrue(plain.StartsWith("Voss "));
		}

		[Test]
		public void PaidChronicle_SaysWhatLeftTheStoresAndInFrontOfWhom()
		{
			string line = KingdomBountyRules.PaidChronicle("Kest", "Ulu", BountyTask.Scouting, 6);
			ClassicAssert.IsTrue(line.Contains("Kest"));
			ClassicAssert.IsTrue(line.Contains("Ulu"));
			ClassicAssert.IsTrue(line.Contains("6 drams"));
			ClassicAssert.IsFalse(line.EndsWith("."));
		}

		[Test]
		public void OwedChronicle_StatesTheDebtPlainlyRatherThanWritingItOff()
		{
			string part = KingdomBountyRules.OwedChronicle("Kest", "Ulu", 3, 5);
			ClassicAssert.IsTrue(part.Contains("3 drams"));
			ClassicAssert.IsTrue(part.Contains("5 still owed"));
			string none = KingdomBountyRules.OwedChronicle("Kest", "Ulu", 0, 8);
			ClassicAssert.IsTrue(none.Contains("not a dram"));
			ClassicAssert.AreNotEqual(part, none);
		}

		[Test]
		public void OwedLedgerNote_NamesTheCreditorAndTheAmountAndPromisesIt()
		{
			string note = KingdomBountyRules.OwedLedgerNote("Kest", 4);
			ClassicAssert.IsTrue(note.Contains("Kest"));
			ClassicAssert.IsTrue(note.Contains("4 drams"));
			ClassicAssert.IsTrue(note.Contains("the day the stores can cover it"));
			ClassicAssert.IsTrue(KingdomBountyRules.OwedLedgerNote(null, 1).Contains("1 dram "));
		}

		[Test]
		public void WithdrawnChronicle_RemembersARefusalAndSaysNothingIsAskedBack()
		{
			string unclaimed = KingdomBountyRules.WithdrawnChronicle("Ulu", BountyTask.Fetch, Claimed: false, Name: null);
			string claimed = KingdomBountyRules.WithdrawnChronicle("Ulu", BountyTask.Fetch, Claimed: true, Name: "Aeru");
			ClassicAssert.AreNotEqual(unclaimed, claimed);
			ClassicAssert.IsTrue(unclaimed.Contains("unclaimed and unpaid for"));
			ClassicAssert.IsTrue(claimed.Contains("Aeru"));
			ClassicAssert.IsTrue(claimed.Contains("nobody was made to give anything back"));
		}

		[Test]
		public void WithdrawnChronicle_FallsBackToTheUnclaimedTellingWhenNobodyIsNamed()
		{
			ClassicAssert.AreEqual(KingdomBountyRules.WithdrawnChronicle("Ulu", BountyTask.Fetch, false, null),
				KingdomBountyRules.WithdrawnChronicle("Ulu", BountyTask.Fetch, true, null));
		}

		[Test]
		public void ScoutChronicle_NamesTheGroundWhenItHasOneAndStaysASentenceWhenItDoesNot()
		{
			string named = KingdomBountyRules.ScoutChronicle("Kest", "Ulu", "a salt marsh");
			ClassicAssert.IsTrue(named.Contains("a salt marsh"));
			ClassicAssert.IsTrue(named.Contains("Kest"));
			string unnamed = KingdomBountyRules.ScoutChronicle("Kest", "Ulu", null);
			ClassicAssert.IsFalse(unnamed.EndsWith(":"));
			ClassicAssert.AreNotEqual(named, unnamed);
		}

		[Test]
		public void ScoutDeed_IsALowerCaseNounPhraseFitForTheArrivalGrammar()
		{
			string deed = KingdomBountyRules.ScoutDeed("Ulu");
			ClassicAssert.AreEqual(char.ToLowerInvariant(deed[0]), deed[0]);
			ClassicAssert.IsFalse(deed.EndsWith("."));
			ClassicAssert.IsTrue(deed.Contains("Ulu"));
		}

		// --- Durable lifecycle fault decisions --------------------------------------------

		[Test]
		public void NoticeEventId_IsStableBoundedAndDistinctPerNotice()
		{
			string first = KingdomBountyRules.NoticeEventId("Notice / 41");
			ClassicAssert.AreEqual(first, KingdomBountyRules.NoticeEventId("Notice / 41"));
			ClassicAssert.AreNotEqual(first, KingdomBountyRules.NoticeEventId("Notice / 42"));
			ClassicAssert.IsTrue(KingdomBountyRules.IsNoticeEventId(first));
			ClassicAssert.LessOrEqual(first.Length, 180);
		}

		[Test]
		public void TransferAction_ReloadNeverContinuesAMutationIntent()
		{
			ClassicAssert.AreEqual(BountyTransferAction.Bind, KingdomBountyRules.TransferAction(
				BountyTransferPhase.None, BountyTransferLocation.SourceOnly));
			ClassicAssert.AreEqual(BountyTransferAction.Remove, KingdomBountyRules.TransferAction(
				BountyTransferPhase.Bound, BountyTransferLocation.SourceOnly));
			ClassicAssert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.RemoveIntent, BountyTransferLocation.Detached));
			ClassicAssert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.Detached, BountyTransferLocation.Detached));
			ClassicAssert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.AddIntent, BountyTransferLocation.DestinationOnly));
			ClassicAssert.AreEqual(BountyTransferAction.Confirm, KingdomBountyRules.TransferAction(
				BountyTransferPhase.Arrived, BountyTransferLocation.DestinationOnly));
			ClassicAssert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.RemoveIntent, BountyTransferLocation.SourceOnly),
				"an interrupted remove callback may have restored the source");
			ClassicAssert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
				BountyTransferPhase.AddIntent, BountyTransferLocation.Detached),
				"an interrupted add callback may have detached the item again");
			ClassicAssert.AreEqual(BountyTransferAction.Quarantine, KingdomBountyRules.TransferAction(
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
			ClassicAssert.AreEqual(BountyPaymentObservation.Original, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 10, 8 }, allocation, same, pure, out proved));
			ClassicAssert.AreEqual(0, proved);
			ClassicAssert.AreEqual(BountyPaymentObservation.Debited, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 7, 5 }, allocation, same, pure, out proved));
			ClassicAssert.AreEqual(6, proved);
			ClassicAssert.AreEqual(BountyPaymentObservation.Mixed, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 7, 8 }, allocation, same, pure, out proved));
			ClassicAssert.AreEqual(3, proved);
			ClassicAssert.AreEqual(BountyPaymentObservation.Uncertain, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 9, 8 }, allocation, same, pure, out proved));
			ClassicAssert.AreEqual(0, proved);
			ClassicAssert.AreEqual(BountyPaymentObservation.Uncertain, KingdomBountyRules.ObservePayment(
				6, original, new int[2] { 7, 5 }, allocation,
				new bool[2] { true, false }, pure, out proved));
		}

		[Test]
		public void PaymentAction_NeverDebitsAgainAfterAnIntent()
		{
			ClassicAssert.AreEqual(BountyPaymentAction.Bind, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.None, BountyPaymentObservation.Malformed));
			ClassicAssert.AreEqual(BountyPaymentAction.Debit, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.Bound, BountyPaymentObservation.Original));
			ClassicAssert.AreEqual(BountyPaymentAction.Quarantine, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.DebitIntent, BountyPaymentObservation.Original));
			ClassicAssert.AreEqual(BountyPaymentAction.Quarantine, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.DebitIntent, BountyPaymentObservation.Debited));
			ClassicAssert.AreEqual(BountyPaymentAction.Quarantine, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.DebitIntent, BountyPaymentObservation.Mixed));
			ClassicAssert.AreEqual(BountyPaymentAction.Wait, KingdomBountyRules.PaymentAction(
				BountyPaymentPhase.Credited, BountyPaymentObservation.Original));
		}

		[Test]
		public void ValidLifecycleScalars_RejectsMalformedReloadStateFailClosed()
		{
			ClassicAssert.IsTrue(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 3,
				true, "Aeru", 2, KingdomBountyRules.NoticeEventStream("41"), 1200L,
				false, 7, 0, 0, 0, 0));
			ClassicAssert.IsFalse(KingdomBountyRules.ValidLifecycleScalars(99, 8, 0, false, null,
				0, null, 0L, false, 0, 0, 0, 0, 0));
			ClassicAssert.IsFalse(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 9,
				false, null, 0, null, 0L, false, 0, 0, 0, 0, 0));
			ClassicAssert.IsFalse(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 0,
				true, null, 0, null, 0L, false, 0, 0, 0, 0, 0));
			ClassicAssert.IsFalse(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 0,
				false, null, 2, "bad-stream", 1200L, false, 0, 0, 0, 0, 0));
			ClassicAssert.IsFalse(KingdomBountyRules.ValidLifecycleScalars((int)BountyTask.Fetch, 8, 0,
				false, null, 0, null, 0L, false, 0, 99, 0, 0, 0));
		}

		[Test]
		public void BountyLogicalAuthority_PinsNoticeFieldsPublicSurfaceAndNestedIdentities()
		{
			string source = KingdomBountyLogicalSource.Read();
			string notice = ReadRepoSource("Quests/r_KingdomNotice.cs");
			ClassicAssert.AreEqual(22, KingdomBountyLogicalSource.FileCount);
			ClassicAssert.AreEqual(2, Count(source, "public partial class r_KingdomNotice"));
			ClassicAssert.AreEqual(20, Count(source, "public static partial class KingdomBounty"));
			ClassicAssert.AreEqual(1, Count(source, "\t\tprivate sealed class CleanupFrame"));
			ClassicAssert.AreEqual(1, Count(source, "\t\tprivate sealed class InventoryFrame"));
			ClassicAssert.AreEqual(1, Count(source, "\t\tprivate sealed class PaymentFrame"));
			StringAssert.Contains("[Serializable]\n\tpublic partial class r_KingdomNotice : IPart", notice);

			CollectionAssert.AreEqual(new[]
			{
				"int TaskCode", "int Price", "int Paid", "long PostedTick", "int Passes",
				"int ScheduleVersion", "string EventStreamId", "long NextAttemptTick",
				"bool AttemptScheduleExhausted", "string WorkerName", "long TakenTick",
				"long DueTick", "int Magnitude", "bool Done", "int X1", "int Y1", "int X2",
				"int Y2", "string PileId", "int AnnouncedBlock", "bool StakeFailedAnnounced",
				"bool RefusalTold", "string LifecycleId", "bool LifecycleQuarantined",
				"string QuarantineReason", "bool QuarantineTold", "int QuarantineLedgerState",
				"int QuarantineMessageState", "int PostPhase", "string PostZoneId", "int PostCellX",
				"int PostCellY", "int PostPileCellX", "int PostPileCellY", "string PostChronicleLine",
				"string PostMessageLine", "int PostMessageState", "int StakeCleanupState",
				"int WithdrawPhase", "string WithdrawChronicleLine", "string WithdrawMessageLine",
				"string WithdrawPileId", "string WithdrawZoneId", "int WithdrawCellX",
				"int WithdrawCellY", "int WithdrawPileCellX", "int WithdrawPileCellY",
				"int WithdrawMessageState", "int TakePhase", "long PendingAttemptTick",
				"string PendingWorkerName", "int PendingWorkerResidentId", "int PendingVirtueIndex",
				"int PendingFlawIndex", "bool PendingTasteMatched", "bool PendingAttemptConsumed",
				"int TakeLedgerState", "int TakeMessageState", "int ManningVersion",
				"string ManningWorkId", "string ManningWorkName", "int WorkerResidentId",
				"long ManningServedTicks", "long ManningCheckpointTick", "bool ManningAssigned",
				"string ManningOptionRecord", "int ManningResidentEpoch", "int ManningWorkEpoch",
				"int TransferPhase",
				"string TransferItemId", "string TransferSourceId", "string TransferDestinationId",
				"int TransferUnits", "int TransferTotalBefore", "int TransferredUnits", "int HaulPhase",
				"int ScoutPhase", "string ScoutZoneId", "string ScoutGround", "int ScoutDeedState",
				"int PaymentPhase", "int PaymentAmount", "int PaymentPaidBefore", "int PaymentProved",
				"string PaymentZoneId", "string PaymentVesselIds", "string PaymentOriginalVolumes",
				"string PaymentMaxVolumes", "string PaymentAllocations", "int TerminalPhase",
				"int CompletionPhase", "string CompletionExtra", "int CompletionLedgerState",
				"int TerminalLedgerState", "int TerminalMessageState"
			}, PublicFieldRows(notice));

			AssertOrdered(source,
				"public override void Write(GameObject Basis, SerializationWriter Writer)",
				"public override void Read(GameObject Basis, SerializationReader Reader)",
				"public static bool Enabled =>",
				"public const string NoticeBlueprint = \"r_KingdomNotice\";",
				"public const string FetchMarkProperty = \"KingdomFetchNotice\";",
				"public static Func<KingdomSystem, GameObject, string, long, bool> HaulHook;",
				"public static void OpenNotices(KingdomSystem System, GameObject Founder)",
				"public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)",
				"public static List<GameObject> Notices(Zone Z)",
				"public static List<string> Frontier(KingdomSystem System)");
		}

		[Test]
		public void BountyLogicalAuthority_PinsDurableTransactionMutationOrder()
		{
			string source = KingdomBountyLogicalSource.Read();
			AssertOrdered(source,
				"Data.WithdrawPhase = (int)BountyWithdrawPhase.MarkCleared",
				"KingdomChronicle.RecordOnce(System, EventId(Data, \"withdrawn\")",
				"Data.WithdrawPhase = (int)BountyWithdrawPhase.ChronicleDone",
				"DeliverMessage(ref Data.WithdrawMessageState, Data.WithdrawMessageLine)",
				"Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupAttempting",
				"InvokeCleanupOnce(Notice, false)",
				"data.PostPhase = (int)BountyPostPhase.Bound",
				"acceptedNotice = cell.AddObject(notice)",
				"KingdomGovernanceScope.Commit(\"post bounty\")",
				"KingdomChronicle.RecordOnce(System, EventId(Data, \"posted\")",
				"Data.PostPhase = (int)BountyPostPhase.ChronicleDone",
				"DeliverMessage(ref Data.PostMessageState, Data.PostMessageLine)",
				"Data.TakePhase = (int)BountyTakePhase.TaskIntent",
				"Data.TakePhase = (int)BountyTakePhase.TaskDone",
				"KingdomChronicle.RecordOnce(System, EventId(Data, \"taken\")",
				"Data.TakePhase = (int)BountyTakePhase.LedgerIntent",
				"DeliverLedger(System, ref Data.TakeLedgerState",
				"Data.TakePhase = (int)BountyTakePhase.MessageIntent",
				"DeliverMessage(ref Data.TakeMessageState",
				"Data.TransferPhase = (int)BountyTransferPhase.RemoveIntent",
				"sourceFrame.Part.RemoveObject(item)",
				"Data.TransferPhase = (int)BountyTransferPhase.Detached",
				"Data.TransferPhase = (int)BountyTransferPhase.AddIntent",
				"destinationFrame.Part.AddObject(item, Silent: true, NoStack: true)",
				"Data.TransferPhase = (int)BountyTransferPhase.Arrived",
				"Data.PaymentPhase = (int)BountyPaymentPhase.DebitIntent",
				"bool committed = debit.Commit()",
				"Data.PaymentPhase = (int)BountyPaymentPhase.Debited",
				"Data.PaymentPhase = (int)BountyPaymentPhase.Credited",
				"KingdomChronicle.RecordOnce(System, EventId(Data, \"paid\")",
				"Data.TerminalPhase = (int)BountyTerminalPhase.LedgerIntent",
				"DeliverLedger(System, ref Data.TerminalLedgerState",
				"Data.TerminalPhase = (int)BountyTerminalPhase.MessageIntent",
				"DeliverMessage(ref Data.TerminalMessageState",
				"Data.TerminalPhase = (int)BountyTerminalPhase.CleanupAttempting",
				"InvokeCleanupOnce(Notice, false)");
		}

		[Test]
		public void FrontierExcludesClaimsFromEveryAuthoritativeNonSeatCity()
		{
			string frontier = ReadRepoSource("Quests/KingdomBounty.ReadingGround.cs");
			string topology = ReadRepoSource("Core/KingdomSystem.z08.SettlementTopology.cs");
			StringAssert.Contains("System.NonSeatClaimsZone(id)", frontier);
			ClassicAssert.IsFalse(frontier.Contains("System.Away"));
			StringAssert.Contains("for (int i = 0; i < SettlementTopology.Count; i++)",
				topology);
			StringAssert.Contains(
				"SettlementTopology.Get(i)?.ClaimedZones?.Contains(ZoneId) == true",
				topology);
		}

		[Test]
		public void BoundFetchTransfer_ReprovesPurposeCargoBeforeRemovalMoveAndCredit()
		{
			string transfer = ReadRepoSource("Quests/KingdomBounty.Transfer.cs");
			const string protectedEvidence =
				"TryObjectGraphAvailableForOrdinaryTransfer(item, out _)";
			ClassicAssert.AreEqual(5, Count(transfer, protectedEvidence));
			AssertOrdered(transfer, "private static bool ContinueTransfer(",
				protectedEvidence, protectedEvidence,
				"Data.TransferPhase = (int)BountyTransferPhase.RemoveIntent",
				"sourceFrame.Part.RemoveObject(item)", protectedEvidence,
				"Data.TransferPhase = (int)BountyTransferPhase.AddIntent", protectedEvidence,
				"destinationFrame.Part.AddObject(item, Silent: true, NoStack: true)",
				protectedEvidence,
				"Data.TransferPhase = (int)BountyTransferPhase.Arrived",
				"Data.TransferredUnits = totalBefore + units");
			StringAssert.DoesNotContain(".SetIntProperty(", transfer);
			StringAssert.DoesNotContain(".SetStringProperty(", transfer);
		}

		[Test]
		public void BoundFetchTransfer_ProvesTheDetachedHolderOnceAndTheArrivalAfterTheAdd()
		{
			string transfer = ReadRepoSource("Quests/KingdomBounty.Transfer.cs");
			// The removal step is the ONLY step that may demand a detached holder, because
			// Inventory.AddObject assigns the destination before the add callback returns.
			// Re-demanding it afterwards made the arrival proof unsatisfiable, so every
			// successful carry quarantined instead of being credited.
			ClassicAssert.AreEqual(1, Count(transfer, "!InventoryMinusExact(sourceFrame, item, units)"));
			ClassicAssert.AreEqual(1, Count(transfer,
				"!InventoryMinusListExact(sourceFrame, item, units)"));
			ClassicAssert.AreEqual(1, Count(transfer,
				"!InventoryPlusExact(destinationFrame, item, units)"));
			AssertOrdered(transfer,
				"private static bool InventoryMinusListExact(",
				"private static bool InventoryMinusExact(",
				"TransferOwnerExact(BountyTransferPhase.RemoveIntent",
				"private static bool InventoryPlusExact(",
				"TransferOwnerExact(BountyTransferPhase.AddIntent",
				"Data.TransferPhase = (int)BountyTransferPhase.RemoveIntent",
				"!InventoryMinusExact(sourceFrame, item, units)",
				"destinationFrame.Part.AddObject(item, Silent: true, NoStack: true)",
				"!InventoryMinusListExact(sourceFrame, item, units)",
				"!InventoryPlusExact(destinationFrame, item, units)",
				"Data.TransferredUnits = totalBefore + units");
			StringAssert.DoesNotContain("Removed.InInventory != null", transfer);
		}

		[Test]
		public void FetchHolderLaw_AcceptsADetachedRemovalAndAnArrivalHeldByTheDestination()
		{
			ClassicAssert.IsTrue(KingdomBountyRules.TransferOwnerExact(
				BountyTransferPhase.RemoveIntent, BountyTransferLocation.Detached));
			ClassicAssert.IsTrue(KingdomBountyRules.TransferOwnerExact(
				BountyTransferPhase.AddIntent, BountyTransferLocation.DestinationOnly));
		}

		[Test]
		public void FetchHolderLaw_RefusesADetachedOrForeignHolderAfterTheAdd()
		{
			// The shipped defect: the arrival proof demanded the detached holder the engine
			// had already replaced, so a completed carry could never satisfy it.
			ClassicAssert.IsFalse(KingdomBountyRules.TransferOwnerExact(
				BountyTransferPhase.AddIntent, BountyTransferLocation.Detached));
			ClassicAssert.IsFalse(KingdomBountyRules.TransferOwnerExact(
				BountyTransferPhase.AddIntent, BountyTransferLocation.Elsewhere));
			ClassicAssert.IsFalse(KingdomBountyRules.TransferOwnerExact(
				BountyTransferPhase.AddIntent, BountyTransferLocation.SourceOnly));
			ClassicAssert.IsFalse(KingdomBountyRules.TransferOwnerExact(
				BountyTransferPhase.RemoveIntent, BountyTransferLocation.Elsewhere));
			ClassicAssert.IsFalse(KingdomBountyRules.TransferOwnerExact(
				BountyTransferPhase.RemoveIntent, BountyTransferLocation.DestinationOnly));
			foreach (BountyTransferPhase phase in new[] { BountyTransferPhase.None,
				BountyTransferPhase.Bound, BountyTransferPhase.Detached,
				BountyTransferPhase.Arrived, BountyTransferPhase.Quarantined })
			{
				foreach (BountyTransferLocation holder in
					Enum.GetValues(typeof(BountyTransferLocation)))
				{
					ClassicAssert.IsFalse(KingdomBountyRules.TransferOwnerExact(phase, holder),
						phase + "/" + holder);
				}
			}
		}

		[Test]
		public void FetchSubtractionLaw_AcceptsOneExactCarryAndNamesTheDroppedSlot()
		{
			string[] captured = { "item-a", "item-b", "item-c" };
			int[] capturedCounts = { 4, 7, 2 };
			string[] observed = { "item-a", "item-c" };
			int[] observedCounts = { 4, 2 };
			int removed;
			ClassicAssert.IsTrue(KingdomBountyRules.TransferRowsMinus(captured, capturedCounts,
				observed, observedCounts, "item-b", 7, out removed));
			ClassicAssert.AreEqual(1, removed);
			ClassicAssert.AreEqual(Sum(capturedCounts) - 7, Sum(observedCounts),
				"the carried units are exactly what the source lost");
			int removedFirst;
			ClassicAssert.IsTrue(KingdomBountyRules.TransferRowsMinus(captured, capturedCounts,
				new[] { "item-b", "item-c" }, new[] { 7, 2 }, "item-a", 4, out removedFirst));
			ClassicAssert.AreEqual(0, removedFirst);
		}

		[Test]
		public void FetchSubtractionLaw_RefusesAMutatedRemainingListOrCount()
		{
			string[] captured = { "item-a", "item-b", "item-c" };
			int[] capturedCounts = { 4, 7, 2 };
			int removed;
			ClassicAssert.IsFalse(KingdomBountyRules.TransferRowsMinus(captured, capturedCounts,
				new[] { "item-c", "item-a" }, new[] { 2, 4 }, "item-b", 7, out removed),
				"a reordered remainder is not a subtraction");
			ClassicAssert.AreEqual(-1, removed);
			ClassicAssert.IsFalse(KingdomBountyRules.TransferRowsMinus(captured, capturedCounts,
				new[] { "item-a", "item-c" }, new[] { 4, 3 }, "item-b", 7, out removed),
				"a surviving row whose count moved is not a subtraction");
			ClassicAssert.IsFalse(KingdomBountyRules.TransferRowsMinus(captured, capturedCounts,
				new[] { "item-a", "item-c", "item-d" }, new[] { 4, 2, 1 }, "item-b", 7,
				out removed), "an added row is not a subtraction");
			ClassicAssert.IsFalse(KingdomBountyRules.TransferRowsMinus(captured, capturedCounts,
				new[] { "item-a" }, new[] { 4 }, "item-b", 7, out removed),
				"two rows lost is not one subtraction");
			ClassicAssert.IsFalse(KingdomBountyRules.TransferRowsMinus(captured, capturedCounts,
				new[] { "item-a", "item-c" }, new[] { 4, 2 }, "item-b", 6, out removed),
				"the moved row must carry the credited units");
			ClassicAssert.IsFalse(KingdomBountyRules.TransferRowsMinus(captured, capturedCounts,
				new[] { "item-a", "item-b" }, new[] { 4, 7 }, "item-d", 1, out removed),
				"a moved identity the capture never held is refused");
		}

		[Test]
		public void FetchSubtractionLaw_RefusesADuplicateMovedIdentity()
		{
			string[] captured = { "item-a", "item-b", "item-a" };
			int[] capturedCounts = { 4, 7, 4 };
			int removed;
			ClassicAssert.IsFalse(KingdomBountyRules.TransferRowsMinus(captured, capturedCounts,
				new[] { "item-b", "item-a" }, new[] { 7, 4 }, "item-a", 4, out removed),
				"which occurrence left is not decidable, so no subtraction is proved");
			ClassicAssert.AreEqual(-1, removed);
		}

		private static int Sum(int[] Rows)
		{
			int total = 0;
			for (int i = 0; i < Rows.Length; i++) total += Rows[i];
			return total;
		}

		[Test]
		public void BountySource_WiresLiveFramesBeforeExactPaymentAndOneShotTerminalCleanup()
		{
			string source = KingdomBountyLogicalSource.Read();
			int paymentIntent = source.IndexOf(
				"Data.PaymentPhase = (int)BountyPaymentPhase.DebitIntent", StringComparison.Ordinal);
			int paymentCall = source.IndexOf("bool committed = debit.Commit()", paymentIntent,
				StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(paymentIntent, 0);
			ClassicAssert.Greater(paymentCall, paymentIntent);
			StringAssert.Contains("TryCaptureBoundPayment(Data, Z, Survey, Notice", source);
			StringAssert.Contains("ObserveCapturedPayment(frame", source);
			StringAssert.Contains("ReferenceEquals(vessel.ComponentLiquids, Frame.Dictionaries[i])", source);
			StringAssert.Contains("ReferenceEquals(Frame.Survey.Stores, Frame.Stores)", source);
			StringAssert.Contains("PendingWorkerResidentId = ResidentId", source);
			StringAssert.Contains("ReaderResidentId(residentIds, residentNames", source);
			StringAssert.Contains("KingdomBountyManningRules.TryAccrue", source);
			StringAssert.Contains("TryAssignWorks(Survey.Works, pool, available", ReadRepoSource(
				"Growth/KingdomGrowth.z15.WorkAssignment.cs"));
			StringAssert.DoesNotContain("ManOneWork", source);
			StringAssert.DoesNotContain("SetIntProperty(\"KingdomEffectiveness\", 100)", source);
			StringAssert.Contains("KingdomChronicle.RecordOnce", source);
			int cleanupIntent = source.IndexOf(
				"Data.TerminalPhase = (int)BountyTerminalPhase.CleanupAttempting",
				StringComparison.Ordinal);
			int cleanupCall = source.IndexOf("InvokeCleanupOnce(Notice, false)", cleanupIntent,
				StringComparison.Ordinal);
			ClassicAssert.Greater(cleanupCall, cleanupIntent);
			ClassicAssert.AreEqual(1, Count(source, ".Obliterate("));
			int recovery = source.IndexOf("else if (phase == BountyTerminalPhase.CleanupAttempting)",
				cleanupCall, StringComparison.Ordinal);
			int recoveryEnd = source.IndexOf("\n\t\t}\n", recovery, StringComparison.Ordinal);
			ClassicAssert.Greater(recovery, cleanupCall);
			ClassicAssert.IsFalse(source.Substring(recovery, recoveryEnd - recovery).Contains("InvokeCleanupOnce"));
		}

		[Test]
		public void SavedRowParsers_CapRawTextRowsAndFieldsBeforeSplit()
		{
			int[] numbers;
			string[] ids;
			ClassicAssert.IsTrue(KingdomBountyRules.TryCanonicalIntRows("0|7|2147483647", out numbers));
			ClassicAssert.AreEqual(3, numbers.Length);
			ClassicAssert.IsFalse(KingdomBountyRules.TryCanonicalIntRows("01", out numbers));
			ClassicAssert.IsFalse(KingdomBountyRules.TryCanonicalIntRows(
				new string('1', KingdomBountyRules.MaxPaymentRowsChars + 1), out numbers));
			ClassicAssert.IsFalse(KingdomBountyRules.TryCanonicalIntRows(
				new string('|', KingdomBountyRules.MaxPaymentRows), out numbers));
			ClassicAssert.IsTrue(KingdomBountyRules.TryObjectIdRows("vessel-1|vessel-2", out ids));
			ClassicAssert.IsFalse(KingdomBountyRules.TryObjectIdRows("same|same", out ids));
			string rules = ReadRepoSource("Quests/KingdomBountyRules.cs");
			ClassicAssert.Less(rules.IndexOf("Text.Length > MaxPaymentRowsChars", StringComparison.Ordinal),
				rules.IndexOf("Text.Split('|')", StringComparison.Ordinal));
		}

		[Test]
		public void UninspectableSinkRecovery_IsExplicitLossNotClaimedDelivery()
		{
			ClassicAssert.AreEqual(BountySinkDisposition.Lost,
				KingdomBountyRules.RecoverUninspectable(BountySinkDisposition.Attempting));
			ClassicAssert.AreEqual(BountySinkDisposition.Pending,
				KingdomBountyRules.RecoverUninspectable(BountySinkDisposition.Pending));
			ClassicAssert.IsTrue(KingdomBountyRules.SinkSettled(BountySinkDisposition.Delivered));
			ClassicAssert.IsTrue(KingdomBountyRules.SinkSettled(BountySinkDisposition.Skipped));
			ClassicAssert.IsTrue(KingdomBountyRules.SinkSettled(BountySinkDisposition.Lost));
			ClassicAssert.IsFalse(KingdomBountyRules.SinkSettled(BountySinkDisposition.Attempting));
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

		private static string[] PublicFieldRows(string Source)
		{
			List<string> rows = new List<string>();
			string[] lines = Source.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (line.StartsWith("public ", StringComparison.Ordinal)
					&& line.EndsWith(";", StringComparison.Ordinal) && !line.Contains("("))
				{
					rows.Add(line.Substring(7, line.Length - 8));
				}
			}
			return rows.ToArray();
		}

		private static void AssertOrdered(string Source, params string[] Needles)
		{
			int cursor = 0;
			for (int i = 0; i < Needles.Length; i++)
			{
				int next = Source.IndexOf(Needles[i], cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(next, cursor, Needles[i]);
				cursor = next + Needles[i].Length;
			}
		}
	}
}
#endif
