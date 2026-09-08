#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	public class KingdomMaterialRulesTests
	{
		private static KingdomMaterialTally Tally(params int[] Amounts)
		{
			KingdomMaterialTally tally = new KingdomMaterialTally();
			for (int i = 0; i < Amounts.Length && i < KingdomMaterialRules.MaterialCount; i++)
			{
				tally.Set((KingdomMaterial)i, Amounts[i]);
			}
			return tally;
		}

		// --- The vocabulary -------------------------------------------------------------------

		[Test]
		public void MaterialCount_MatchesTheEnum()
		{
			// A seventh material added to the enum without growing the tallies would silently
			// stop being counted, spent, or stored anywhere.
			ClassicAssert.AreEqual(Enum.GetValues(typeof(KingdomMaterial)).Length, KingdomMaterialRules.MaterialCount);
			ClassicAssert.AreEqual(KingdomMaterialRules.MaterialCount, KingdomMaterialRules.MaterialKeys.Length);
			ClassicAssert.AreEqual(KingdomMaterialRules.MaterialCount, KingdomMaterialRules.MaterialNames.Length);
			ClassicAssert.AreEqual(KingdomMaterialRules.MaterialCount, KingdomMaterialRules.WallMaterialThreshold.Length);
			ClassicAssert.AreEqual(KingdomMaterialRules.MaterialCount, KingdomMaterialRules.WallMaterialPreference.Length);
		}

		[Test]
		public void StandingCount_MatchesTheEnum()
		{
			ClassicAssert.AreEqual(Enum.GetValues(typeof(KingdomStanding)).Length, KingdomMaterialRules.StandingCount);
			ClassicAssert.AreEqual(KingdomMaterialRules.StandingCount, KingdomMaterialRules.StandingEffort.Length);
			ClassicAssert.AreEqual(KingdomMaterialRules.StandingCount, KingdomMaterialRules.StandingYield.Length);
		}

		[Test]
		public void MaterialKeysAndNames_AreDistinctAndNonEmpty()
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomMaterialRules.MaterialKeys[i]), "key " + i);
				ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomMaterialRules.MaterialNames[i]), "name " + i);
				for (int j = i + 1; j < KingdomMaterialRules.MaterialCount; j++)
				{
					ClassicAssert.AreNotEqual(KingdomMaterialRules.MaterialKeys[i], KingdomMaterialRules.MaterialKeys[j]);
					ClassicAssert.AreNotEqual(KingdomMaterialRules.MaterialNames[i], KingdomMaterialRules.MaterialNames[j]);
				}
			}
		}

		[Test]
		public void EveryMaterial_RoundTripsThroughItsOwnKey()
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial material = (KingdomMaterial)i;
				ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterial(KingdomMaterialRules.MaterialKey(material), out var parsed), KingdomMaterialRules.MaterialKey(material));
				ClassicAssert.AreEqual(material, parsed);
			}
		}

		[TestCase("timber", KingdomMaterial.Timber)]
		[TestCase("TIMBER", KingdomMaterial.Timber)]
		[TestCase("  stone  ", KingdomMaterial.Stone)]
		[TestCase("scrap", KingdomMaterial.Scrap)]
		[TestCase("scrap metal", KingdomMaterial.Scrap)]
		[TestCase("ScrapMetal", KingdomMaterial.Scrap)]
		[TestCase("marble", KingdomMaterial.Marble)]
		[TestCase("mud", KingdomMaterial.Mud)]
		[TestCase("brush", KingdomMaterial.Brush)]
		[TestCase("canvas", KingdomMaterial.Brush)]
		[TestCase("CANVAS", KingdomMaterial.Brush)]
		public void TryParseMaterial_ReadsTheVocabulary(string key, KingdomMaterial expected)
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterial(key, out var material));
			ClassicAssert.AreEqual(expected, material);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		[TestCase("wood")]
		[TestCase("iron")]
		[TestCase("timbers")]
		[TestCase("scrap crystal")]
		public void TryParseMaterial_RefusesAnythingElse(string key)
		{
			ClassicAssert.IsFalse(KingdomMaterialRules.TryParseMaterial(key, out _), key ?? "null");
		}

		// --- Material costs out of third-party XML ---------------------------------------------

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void TryParseMaterialCost_AbsentIsAnEmptyCostAndNotAnError(string text)
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterialCost(text, out var cost, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.IsTrue(cost.IsEmpty());
			// The whole compatibility guarantee: a design with no Materials attribute is covered
			// by an empty stockpile, forever.
			ClassicAssert.IsTrue(KingdomMaterialRules.Covers(new KingdomMaterialTally(), cost));
		}

		[Test]
		public void TryParseMaterialCost_ReadsSeveralTerms()
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterialCost("timber:8, stone:4 ,marble:1", out var cost, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.AreEqual(8, cost.Get(KingdomMaterial.Timber));
			ClassicAssert.AreEqual(4, cost.Get(KingdomMaterial.Stone));
			ClassicAssert.AreEqual(1, cost.Get(KingdomMaterial.Marble));
			ClassicAssert.AreEqual(0, cost.Get(KingdomMaterial.Scrap));
			ClassicAssert.AreEqual(13, cost.Total());
		}

		[TestCase("wood:4")]
		[TestCase("timber")]
		[TestCase("timber:")]
		[TestCase(":4")]
		[TestCase("timber:0")]
		[TestCase("timber:-2")]
		[TestCase("timber:many")]
		[TestCase("timber:4,timber:2")]
		[TestCase("timber:4,")]
		[TestCase("timber:4,,stone:2")]
		public void TryParseMaterialCost_RejectsMalformedTextWhole(string text)
		{
			ClassicAssert.IsFalse(KingdomMaterialRules.TryParseMaterialCost(text, out var cost, out var error), text);
			ClassicAssert.IsNotNull(error, text);
			// Nothing is half-parsed: a bad attribute leaves an empty cost rather than the terms
			// that happened to come before the bad one.
			ClassicAssert.IsTrue(cost.IsEmpty(), text);
		}

		[Test]
		public void ShippedUpgradeMaterialBillsAreTransitionSpecificPhysicalAdditions()
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(TestMain.ReadRepositoryText("KingdomBuildings.xml"));
			Dictionary<string, XmlElement> buildings = new Dictionary<string, XmlElement>(
				StringComparer.OrdinalIgnoreCase);
			HashSet<string> routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (XmlElement building in document.GetElementsByTagName("building"))
			{
				string key = building.GetAttribute("Key");
				if (!string.IsNullOrEmpty(key)) buildings[key] = building;
			}
			int transitions = 0;
			foreach (KeyValuePair<string, XmlElement> pair in buildings)
			{
				string successorKey = pair.Value.GetAttribute("UpgradesTo");
				if (string.IsNullOrEmpty(successorKey)) continue;
				transitions++;
				routes.Add(pair.Key + "->" + successorKey);
				ClassicAssert.IsTrue(buildings.TryGetValue(successorKey, out XmlElement successor),
					pair.Key + " names a missing successor");
				string authored = pair.Value.GetAttribute("UpgradeMaterials");
				ClassicAssert.IsFalse(string.IsNullOrWhiteSpace(authored),
					pair.Key + " -> " + successorKey + " has no material gate");
				ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterialCost(
					pair.Value.GetAttribute("Materials"), out KingdomMaterialTally before,
					out string beforeError), pair.Key + ": " + beforeError);
				ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterialCost(
					successor.GetAttribute("Materials"), out KingdomMaterialTally after,
					out string afterError), successorKey + ": " + afterError);
				ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterialCost(authored,
					out KingdomMaterialTally additions, out string additionsError),
					pair.Key + ": " + additionsError);
				for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				{
					KingdomMaterial material = (KingdomMaterial)i;
					int expected = Math.Max(0, after.Get(material) - before.Get(material));
					ClassicAssert.AreEqual(expected, additions.Get(material), pair.Key + " -> "
						+ successorKey + " must charge only the added "
						+ KingdomMaterialRules.MaterialKey(material));
				}
			}
			CollectionAssert.IsSubsetOf(new string[]
			{
				"fieldrows->grange", "forge->forgehall",
				"robotchargebay->robotservicebay", "watchhouse->barracks"
			}, routes, "the reviewed food, craft, robot, and garrison progressions must remain explicit");
			ClassicAssert.AreEqual(23, transitions,
				"new upgrade chains need an authored addition bill and an explicit census review");
		}

		[Test]
		public void ImprovementRuntimePricesThePredecessorTransitionNotTheSuccessorDesign()
		{
			string materials = KingdomMaterialsLogicalSource.Read();
			string upgrade = KingdomUpgradeLogicalSource.Read();
			StringAssert.Contains("UpgradeCostFor(PredecessorKey)", materials);
			StringAssert.Contains("CanPayUpgrade(Z, Predecessor.Key, out _)", upgrade);
			StringAssert.Contains("ReserveUpgradePayment(Z, A.Key)", upgrade);
			StringAssert.Contains("ReserveTransitionPayment(Z, transitionMaterials)", upgrade);
			StringAssert.Contains("UpgradeCostFor(A.Key)", upgrade);
			StringAssert.DoesNotContain("UpgradeCostFor(A.SuccessorKey)", upgrade);
		}

		// --- The tally ------------------------------------------------------------------------

		[Test]
		public void Tally_ClampsAtZeroRatherThanGoingNegative()
		{
			KingdomMaterialTally tally = new KingdomMaterialTally();
			tally.Add(KingdomMaterial.Timber, 3);
			tally.Add(KingdomMaterial.Timber, -10);
			ClassicAssert.AreEqual(0, tally.Get(KingdomMaterial.Timber));
			tally.Set(KingdomMaterial.Stone, -5);
			ClassicAssert.AreEqual(0, tally.Get(KingdomMaterial.Stone));
			ClassicAssert.IsTrue(tally.IsEmpty());
		}

		[Test]
		public void Tally_CopyIsIndependent()
		{
			KingdomMaterialTally tally = Tally(0, 0, 6);
			KingdomMaterialTally copy = tally.Copy();
			copy.Add(KingdomMaterial.Timber, 10);
			ClassicAssert.AreEqual(6, tally.Get(KingdomMaterial.Timber));
			ClassicAssert.AreEqual(16, copy.Get(KingdomMaterial.Timber));
		}

		[Test]
		public void Tally_AddAllSumsAndToleratesNull()
		{
			KingdomMaterialTally tally = Tally(0, 0, 2, 3);
			tally.AddAll(Tally(0, 0, 5));
			tally.AddAll(null);
			ClassicAssert.AreEqual(7, tally.Get(KingdomMaterial.Timber));
			ClassicAssert.AreEqual(3, tally.Get(KingdomMaterial.Stone));
		}

		[TestCase(100, 8)]
		[TestCase(50, 4)]
		[TestCase(49, 3)]
		[TestCase(0, 0)]
		[TestCase(-10, 0)]
		public void Tally_ScaledRoundsDown(int percent, int expected)
		{
			ClassicAssert.AreEqual(expected, Tally(0, 0, 8).Scaled(percent).Get(KingdomMaterial.Timber));
		}

		[Test]
		public void Tally_DescribeReadsAsProseAndIsNullWhenEmpty()
		{
			ClassicAssert.IsNull(new KingdomMaterialTally().Describe());
			ClassicAssert.AreEqual("6 timber", Tally(0, 0, 6).Describe());
			ClassicAssert.AreEqual("6 timber and 2 cut stone", Tally(0, 0, 6, 2).Describe());
			ClassicAssert.AreEqual("1 mud, 6 timber and 2 cut stone", Tally(1, 0, 6, 2).Describe());
		}

		[Test]
		public void Covers_IsPerMaterialAndNotATotal()
		{
			// A stockpile full of stone does not pay for timber, however much of it there is.
			ClassicAssert.IsFalse(KingdomMaterialRules.Covers(Tally(0, 0, 0, 100), Tally(0, 0, 4)));
			ClassicAssert.IsTrue(KingdomMaterialRules.Covers(Tally(0, 0, 4), Tally(0, 0, 4)));
			ClassicAssert.IsFalse(KingdomMaterialRules.Covers(Tally(0, 0, 3), Tally(0, 0, 4)));
			ClassicAssert.IsTrue(KingdomMaterialRules.Covers(null, new KingdomMaterialTally()));
			ClassicAssert.IsTrue(KingdomMaterialRules.Covers(Tally(0, 0, 1), null));
			ClassicAssert.IsFalse(KingdomMaterialRules.Covers(null, Tally(0, 0, 1)));
		}

		[Test]
		public void Missing_NamesOnlyTheShortfall()
		{
			KingdomMaterialTally missing = KingdomMaterialRules.Missing(Tally(0, 0, 3, 9), Tally(0, 0, 8, 4));
			ClassicAssert.AreEqual(5, missing.Get(KingdomMaterial.Timber));
			ClassicAssert.AreEqual(0, missing.Get(KingdomMaterial.Stone));
			ClassicAssert.IsTrue(KingdomMaterialRules.Missing(Tally(0, 0, 8), Tally(0, 0, 8)).IsEmpty());
		}

		// --- Clearance: what removal costs and earns -------------------------------------------

		[TestCase(0, 60)]
		[TestCase(15, 60)]
		[TestCase(50, 60)]
		[TestCase(51, 100)]
		[TestCase(200, 100)]
		[TestCase(201, 140)]
		[TestCase(1000, 140)]
		[TestCase(1001, 200)]
		[TestCase(6000, 200)]
		[TestCase(6001, 300)]
		[TestCase(26000, 300)]
		public void HardnessPercent_MatchesTheVanillaBands(int hitpoints, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.HardnessPercent(hitpoints));
		}

		[Test]
		public void HardnessPercent_NeverFallsAsAThingGetsHarder()
		{
			int previous = KingdomMaterialRules.HardnessPercent(-100);
			ClassicAssert.Greater(previous, 0);
			for (int hitpoints = 0; hitpoints <= 30000; hitpoints += 7)
			{
				int percent = KingdomMaterialRules.HardnessPercent(hitpoints);
				ClassicAssert.GreaterOrEqual(percent, previous, "hitpoints=" + hitpoints);
				previous = percent;
			}
		}

		[Test]
		public void ClearanceEffort_BareGroundIsFixedWhateverHardnessIsPassed()
		{
			// Nothing stands on bare ground, so nothing about it can be hard. A mutation that let
			// hardness through here would make an empty field cost as much as a granite ridge.
			int flat = KingdomMaterialRules.ClearanceEffort(KingdomStanding.Nothing, 0);
			ClassicAssert.Greater(flat, 0);
			ClassicAssert.AreEqual(flat, KingdomMaterialRules.ClearanceEffort(KingdomStanding.Nothing, 26000));
		}

		[Test]
		public void ClearanceEffort_RisesWithHardnessAndNeverReachesZero()
		{
			for (int kind = 1; kind < KingdomMaterialRules.StandingCount; kind++)
			{
				KingdomStanding standing = (KingdomStanding)kind;
				int previous = 0;
				for (int hitpoints = 0; hitpoints <= 30000; hitpoints += 137)
				{
					int effort = KingdomMaterialRules.ClearanceEffort(standing, hitpoints);
					ClassicAssert.GreaterOrEqual(effort, 1, standing + "@" + hitpoints);
					ClassicAssert.GreaterOrEqual(effort, previous, standing + "@" + hitpoints);
					previous = effort;
				}
			}
		}

		[Test]
		public void ClearanceEffort_HarderStandingCostsMoreAtTheSameHardness()
		{
			// The ladder the founder can read off the ground: brush is cheaper than rubble is
			// cheaper than a tree is cheaper than rock.
			int previous = KingdomMaterialRules.ClearanceEffort(KingdomStanding.Nothing, 100);
			for (int kind = 1; kind < KingdomMaterialRules.StandingCount; kind++)
			{
				int effort = KingdomMaterialRules.ClearanceEffort((KingdomStanding)kind, 100);
				ClassicAssert.GreaterOrEqual(effort, previous, ((KingdomStanding)kind).ToString());
				previous = effort;
			}
		}

		[TestCase(KingdomStanding.Nothing, KingdomMaterial.Mud)]
		[TestCase(KingdomStanding.Brush, KingdomMaterial.Brush)]
		[TestCase(KingdomStanding.Rubble, KingdomMaterial.Stone)]
		[TestCase(KingdomStanding.Tree, KingdomMaterial.Timber)]
		[TestCase(KingdomStanding.Rock, KingdomMaterial.Stone)]
		[TestCase(KingdomStanding.Ruin, KingdomMaterial.Scrap)]
		[TestCase(KingdomStanding.MarbleSeam, KingdomMaterial.Marble)]
		public void YieldMaterial_IsWhatStoodThere(KingdomStanding standing, KingdomMaterial expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.YieldMaterial(standing));
		}

		[Test]
		public void YieldUnits_BareGroundYieldsNothingAndEverythingElseYieldsSomething()
		{
			// Bare ground's mud is counted against the rect by GroundMud, not per empty cell; a
			// mutation that gave it a per-cell yield would mint a hill of mud out of a field.
			ClassicAssert.AreEqual(0, KingdomMaterialRules.YieldUnits(KingdomStanding.Nothing));
			for (int kind = 1; kind < KingdomMaterialRules.StandingCount; kind++)
			{
				ClassicAssert.Greater(KingdomMaterialRules.YieldUnits((KingdomStanding)kind), 0, ((KingdomStanding)kind).ToString());
			}
		}

		[TestCase(0, 0)]
		[TestCase(-4, 0)]
		[TestCase(3, 0)]
		[TestCase(4, 1)]
		[TestCase(7, 1)]
		[TestCase(20, 5)]
		public void GroundMud_IsTheSpoilOfTheWholeRect(int cells, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.GroundMud(cells));
		}

		[TestCase(0, 0, 0)]
		[TestCase(5, 0, 5)]
		[TestCase(5, 2, 3)]
		[TestCase(5, 5, 0)]
		[TestCase(5, 9, 0)]
		[TestCase(-3, 0, 0)]
		public void FreeHands_AreWhoeverTheWaterAndTheWorksLeftOver(int population, int assigned, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.FreeHands(population, assigned));
		}

		[TestCase(0, 3, 0)]
		[TestCase(-2, 3, 0)]
		[TestCase(3, 0, 0)]
		[TestCase(3, -1, 0)]
		[TestCase(1, 1, KingdomMaterialRules.EffortPerHandPerDay)]
		[TestCase(3, 2, 60)]
		public void EffortWorked_IsHandsTimesDays(int hands, int days, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.EffortWorked(hands, days));
		}

		[Test]
		public void EffortWorked_IsCappedInHandsAndNotInDays()
		{
			// The gang is bounded, the calendar is not. A thousand settlers dig at the rate of
			// MaxClearingHands, and a stretch of days digs for every one of them.
			int capped = KingdomMaterialRules.EffortWorked(KingdomMaterialRules.MaxClearingHands, 1);
			ClassicAssert.AreEqual(capped, KingdomMaterialRules.EffortWorked(KingdomMaterialRules.MaxClearingHands + 1, 1));
			ClassicAssert.AreEqual(capped, KingdomMaterialRules.EffortWorked(1000, 1));
			ClassicAssert.Greater(capped, KingdomMaterialRules.EffortWorked(KingdomMaterialRules.MaxClearingHands - 1, 1));
		}

		[Test]
		public void EffortWorked_RunsTheWholeAbsenceBecauseTheGangDugThroughIt()
		{
			// The uncapping. A staked plot is dug through an absence exactly as it is dug through
			// a fortnight of visits, and linearly: there is no ceiling on days in here.
			int aFortnight = KingdomMaterialRules.EffortWorked(3, 14);
			ClassicAssert.AreEqual(KingdomMaterialRules.EffortWorked(3, 1) * 14, aFortnight);
			ClassicAssert.Greater(KingdomMaterialRules.EffortWorked(3, 400), aFortnight);
		}

		[Test]
		public void EffortWorked_IdleHandsRemoveNothingHoweverLongTheStretch()
		{
			// Clause 2, and what makes uncapping safe here: zero free hands is zero effort over
			// four hundred days. Every caller reads its hands gate before it spends its days.
			ClassicAssert.AreEqual(0, KingdomMaterialRules.EffortWorked(0, 400));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.EffortWorked(-4, 400));
		}

		[Test]
		public void EffortWorked_SaturatesRatherThanWrappingOnANonsenseStretch()
		{
			// A wrapped negative would ADD to the work left rather than take from it, which is a
			// staked plot that gets harder the longer nobody looks at it.
			ClassicAssert.AreEqual(int.MaxValue, KingdomMaterialRules.EffortWorked(KingdomMaterialRules.MaxClearingHands, int.MaxValue));
			ClassicAssert.Greater(KingdomMaterialRules.EffortWorked(2, int.MaxValue), 0);
		}

		[TestCase(0, 0)]
		[TestCase(-5, 0)]
		[TestCase(1, 1)]
		[TestCase(KingdomMaterialRules.EffortPerHandPerDay, 1)]
		[TestCase(KingdomMaterialRules.EffortPerHandPerDay + 1, 2)]
		[TestCase(KingdomMaterialRules.EffortPerHandPerDay * 4, 4)]
		public void DaysForOneHand_RoundsUpSoNoJobTakesNoTime(int effort, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.DaysForOneHand(effort));
		}

		[Test]
		public void DaysForOneHand_AgreesWithWhatOneHandActuallyWorksOff()
		{
			// The founder is quoted a number of days; a mutation that made the quote and the work
			// disagree would have a job finish early or late against its own promise.
			for (int effort = 1; effort <= 400; effort++)
			{
				int days = KingdomMaterialRules.DaysForOneHand(effort);
				ClassicAssert.GreaterOrEqual(KingdomMaterialRules.EffortWorked(1, days), effort, "effort=" + effort);
				if (days > 1)
				{
					ClassicAssert.Less(KingdomMaterialRules.EffortWorked(1, days - 1), effort, "effort=" + effort);
				}
			}
		}

		[Test]
		public void ClearanceConstants_AreAllPositive()
		{
			// A zeroed constant here would make clearing free, instant, or infinitely productive.
			ClassicAssert.Greater(KingdomMaterialRules.EffortPerHandPerDay, 0);
			ClassicAssert.Greater(KingdomMaterialRules.MaxClearingHands, 0);
			ClassicAssert.Greater(KingdomMaterialRules.MudPerCells, 0);
			for (int kind = 0; kind < KingdomMaterialRules.StandingCount; kind++)
			{
				ClassicAssert.Greater(KingdomMaterialRules.StandingEffort[kind], 0, "effort " + kind);
			}
		}

		// --- Striking -------------------------------------------------------------------------

		[TestCase(0, 0, KingdomMaterialRules.StrikeBaseEffort)]
		[TestCase(-5, -50, KingdomMaterialRules.StrikeBaseEffort)]
		[TestCase(6, 0, 38)]
		[TestCase(0, 40, 24)]
		[TestCase(6, 40, 42)]
		public void StrikeEffort_MatchesTheFormula(int units, int drams, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.StrikeEffort(units, drams));
		}

		[Test]
		public void StrikeEffort_NeverFallsBelowTheBaseAndNeverDecreases()
		{
			int previous = KingdomMaterialRules.StrikeEffort(0, 0);
			ClassicAssert.AreEqual(KingdomMaterialRules.StrikeBaseEffort, previous);
			for (int units = 0; units <= 60; units++)
			{
				int effort = KingdomMaterialRules.StrikeEffort(units, units * 3);
				ClassicAssert.GreaterOrEqual(effort, KingdomMaterialRules.StrikeBaseEffort, "units=" + units);
				ClassicAssert.GreaterOrEqual(effort, previous, "units=" + units);
				previous = effort;
			}
		}

		[Test]
		public void StrikeSalvage_ReturnsPartAndNeverAll()
		{
			ClassicAssert.Less(KingdomMaterialRules.StrikeSalvagePercent, 100);
			ClassicAssert.Greater(KingdomMaterialRules.StrikeSalvagePercent, 0);
			KingdomMaterialTally cost = Tally(0, 0, 8, 5, 1);
			KingdomMaterialTally salvage = KingdomMaterialRules.StrikeSalvage(cost);
			ClassicAssert.AreEqual(4, salvage.Get(KingdomMaterial.Timber));
			ClassicAssert.AreEqual(2, salvage.Get(KingdomMaterial.Stone));
			ClassicAssert.AreEqual(0, salvage.Get(KingdomMaterial.Marble));
			ClassicAssert.Less(salvage.Total(), cost.Total());
		}

		[Test]
		public void StrikeSalvage_OfAWaterOnlyBuildingIsNothing()
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.StrikeSalvage(new KingdomMaterialTally()).IsEmpty());
			ClassicAssert.IsTrue(KingdomMaterialRules.StrikeSalvage(null).IsEmpty());
		}

		[Test]
		public void StrikeSalvage_DoesNotMutateTheCostItReads()
		{
			KingdomMaterialTally cost = Tally(0, 0, 8);
			KingdomMaterialRules.StrikeSalvage(cost);
			ClassicAssert.AreEqual(8, cost.Get(KingdomMaterial.Timber));
		}

		// --- Wall material --------------------------------------------------------------------

		[Test]
		public void WallMaterial_MudIsTheFloorNothingFallsThrough()
		{
			ClassicAssert.AreEqual(0, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Mud]);
			ClassicAssert.AreEqual(KingdomMaterial.Mud, KingdomMaterialRules.WallMaterialFor(new KingdomMaterialTally(), null));
			ClassicAssert.AreEqual(KingdomMaterial.Mud, KingdomMaterialRules.WallMaterialFor(null, "moonstair"));
			ClassicAssert.IsTrue(KingdomMaterialRules.HasWallMaterial(new KingdomMaterialTally(), KingdomMaterial.Mud));
		}

		[Test]
		public void WallMaterial_EveryThresholdAboveMudIsARealBar()
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				if ((KingdomMaterial)i != KingdomMaterial.Mud)
				{
					ClassicAssert.Greater(KingdomMaterialRules.WallMaterialThreshold[i], 0, KingdomMaterialRules.MaterialKeys[i]);
				}
			}
		}

		[Test]
		public void WallMaterial_TakesTheRichestItHasQuarriedEnoughOf()
		{
			KingdomMaterialTally rich = new KingdomMaterialTally();
			rich.Set(KingdomMaterial.Marble, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Marble]);
			rich.Set(KingdomMaterial.Stone, 100);
			ClassicAssert.AreEqual(KingdomMaterial.Marble, KingdomMaterialRules.WallMaterialFor(rich, null));

			KingdomMaterialTally oneShort = new KingdomMaterialTally();
			oneShort.Set(KingdomMaterial.Marble, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Marble] - 1);
			oneShort.Set(KingdomMaterial.Stone, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Stone]);
			ClassicAssert.AreEqual(KingdomMaterial.Stone, KingdomMaterialRules.WallMaterialFor(oneShort, null));
		}

		[Test]
		public void WallMaterial_StyleTasteWinsWhenTheSettlementCanAffordIt()
		{
			KingdomMaterialTally stock = new KingdomMaterialTally();
			stock.Set(KingdomMaterial.Stone, 100);
			stock.Set(KingdomMaterial.Timber, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Timber]);
			ClassicAssert.AreEqual(KingdomMaterial.Timber, KingdomMaterialRules.WallMaterialFor(stock, "verdant"));
			// The same stock, no taste: stone outranks timber.
			ClassicAssert.AreEqual(KingdomMaterial.Stone, KingdomMaterialRules.WallMaterialFor(stock, "common"));
		}

		[Test]
		public void WallMaterial_UnmetTasteCostsNothingAndChangesNothing()
		{
			KingdomMaterialTally stock = new KingdomMaterialTally();
			stock.Set(KingdomMaterial.Stone, 100);
			ClassicAssert.AreEqual(KingdomMaterial.Stone, KingdomMaterialRules.WallMaterialFor(stock, "verdant"));
		}

		[TestCase("verdant", KingdomMaterial.Timber)]
		[TestCase("fungal", KingdomMaterial.Timber)]
		[TestCase("moonstair", KingdomMaterial.Marble)]
		[TestCase("gyre", KingdomMaterial.Marble)]
		[TestCase("eater", KingdomMaterial.Scrap)]
		public void TryStylePreference_KnowsTheStylesThatHaveATaste(string style, KingdomMaterial expected)
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryStylePreference(style, out var material), style);
			ClassicAssert.AreEqual(expected, material);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("common")]
		[TestCase("nonesuch")]
		public void TryStylePreference_LeavesTheRestToTheirStock(string style)
		{
			ClassicAssert.IsFalse(KingdomMaterialRules.TryStylePreference(style, out _), style ?? "null");
		}

		[Test]
		public void WallMaterialPreference_ListsEveryMaterialExactlyOnce()
		{
			bool[] seen = new bool[KingdomMaterialRules.MaterialCount];
			for (int i = 0; i < KingdomMaterialRules.WallMaterialPreference.Length; i++)
			{
				int index = (int)KingdomMaterialRules.WallMaterialPreference[i];
				ClassicAssert.IsFalse(seen[index], "listed twice: " + KingdomMaterialRules.MaterialKeys[index]);
				seen[index] = true;
			}
			for (int i = 0; i < seen.Length; i++)
			{
				ClassicAssert.IsTrue(seen[i], "never listed: " + KingdomMaterialRules.MaterialKeys[i]);
			}
			// Mud last: it is the answer only when nothing else is.
			ClassicAssert.AreEqual(KingdomMaterial.Mud, KingdomMaterialRules.WallMaterialPreference[KingdomMaterialRules.MaterialCount - 1]);
		}

		// --- The refined half: what a yard makes ------------------------------------------------

		[Test]
		public void EveryYard_MakesOneRefinedMaterialAndNoTwoMakeTheSame()
		{
			ClassicAssert.AreEqual(System.Enum.GetValues(typeof(KingdomYard)).Length, KingdomMaterialRules.YardCount);
			ClassicAssert.AreEqual(KingdomMaterialRules.YardCount, KingdomMaterialRules.YardKeys.Length);
			ClassicAssert.AreEqual(KingdomMaterialRules.YardCount, KingdomMaterialRules.YardNames.Length);
			ClassicAssert.AreEqual(KingdomMaterialRules.YardCount, KingdomMaterialRules.YardMakes.Length);
			ClassicAssert.AreEqual(KingdomMaterialRules.YardCount, KingdomMaterialRules.YardEats.Length);
			for (int i = 0; i < KingdomMaterialRules.YardCount; i++)
			{
				KingdomYard yard = (KingdomYard)i;
				ClassicAssert.IsTrue(KingdomMaterialRules.IsRefined(KingdomMaterialRules.MadeAt(yard)), KingdomMaterialRules.YardKey(yard));
				ClassicAssert.IsTrue(KingdomMaterialRules.TryYardFor(KingdomMaterialRules.MadeAt(yard), out var back));
				ClassicAssert.AreEqual(yard, back);
				for (int j = i + 1; j < KingdomMaterialRules.YardCount; j++)
				{
					ClassicAssert.AreNotEqual(KingdomMaterialRules.YardMakes[i], KingdomMaterialRules.YardMakes[j]);
					ClassicAssert.AreNotEqual(KingdomMaterialRules.YardKeys[i], KingdomMaterialRules.YardKeys[j]);
				}
			}
		}

		[Test]
		public void EveryRefinedMaterial_IsMadeAtExactlyOneYardAndNoRawOneIs()
		{
			int refined = 0;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial material = (KingdomMaterial)i;
				if (KingdomMaterialRules.IsRefined(material))
				{
					refined++;
					ClassicAssert.IsTrue(KingdomMaterialRules.TryYardFor(material, out _), KingdomMaterialRules.MaterialKey(material));
				}
				else
				{
					// A raw material a yard claimed to make would be mintable out of nothing.
					ClassicAssert.IsFalse(KingdomMaterialRules.TryYardFor(material, out _), KingdomMaterialRules.MaterialKey(material));
				}
			}
			ClassicAssert.AreEqual(KingdomMaterialRules.YardCount, refined);
		}

		[Test]
		public void NoYard_EatsWhatItMakes()
		{
			// A yard that ate its own output would be a loop that turns two of a thing into one of
			// the same thing, which is a way to destroy material by running a work.
			for (int i = 0; i < KingdomMaterialRules.YardCount; i++)
			{
				KingdomMaterial[] eats = KingdomMaterialRules.YardEats[i];
				ClassicAssert.IsTrue(eats.Length > 0, KingdomMaterialRules.YardKeys[i]);
				for (int j = 0; j < eats.Length; j++)
				{
					ClassicAssert.IsFalse(KingdomMaterialRules.IsRefined(eats[j]), KingdomMaterialRules.YardKeys[i]);
					ClassicAssert.AreNotEqual(KingdomMaterialRules.YardMakes[i], eats[j]);
				}
			}
		}

		[TestCase("shapedtimber", KingdomMaterial.ShapedTimber)]
		[TestCase("shaped timber", KingdomMaterial.ShapedTimber)]
		[TestCase("SAWN TIMBER", KingdomMaterial.ShapedTimber)]
		[TestCase("shapedstone", KingdomMaterial.ShapedStone)]
		[TestCase(" shaped stone ", KingdomMaterial.ShapedStone)]
		[TestCase("dressed stone", KingdomMaterial.ShapedStone)]
		[TestCase("workedmetal", KingdomMaterial.WorkedMetal)]
		[TestCase("worked metal", KingdomMaterial.WorkedMetal)]
		public void TryParseMaterial_ReadsTheRefinedVocabulary(string key, KingdomMaterial expected)
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterial(key, out var material), key);
			ClassicAssert.AreEqual(expected, material);
		}

		[TestCase("sawyer", KingdomYard.Sawyer)]
		[TestCase("MASON", KingdomYard.Mason)]
		[TestCase(" smelter ", KingdomYard.Smelter)]
		[TestCase("shapedtimber", KingdomYard.Sawyer)]
		[TestCase("shaped stone", KingdomYard.Mason)]
		[TestCase("workedmetal", KingdomYard.Smelter)]
		public void TryParseYard_ReadsBothSpellingsOfTheSameThing(string key, KingdomYard expected)
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseYard(key, out var yard), key);
			ClassicAssert.AreEqual(expected, yard);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		[TestCase("timber")]
		[TestCase("stone")]
		[TestCase("sawmill")]
		public void TryParseYard_RefusesRawMaterialsAndWordsItDoesNotKnow(string key)
		{
			// "timber" is a material a yard EATS, not a yard. A design declaring Refines="timber"
			// has said something that cannot be true and should be told so, not quietly filed as a
			// sawyer's yard.
			ClassicAssert.IsFalse(KingdomMaterialRules.TryParseYard(key, out _), key ?? "null");
		}

		[Test]
		public void RefinableFrom_ReachesForThePlainStockBeforeTheRareOne()
		{
			KingdomMaterialTally stock = new KingdomMaterialTally();
			stock.Set(KingdomMaterial.Stone, 4);
			stock.Set(KingdomMaterial.Marble, 40);
			ClassicAssert.AreEqual(4 / KingdomMaterialRules.RawPerRefined, KingdomMaterialRules.RefinableFrom(KingdomYard.Mason, stock, out var raw));
			ClassicAssert.AreEqual(KingdomMaterial.Stone, raw);
			// And only reaches for the marble when there is no ordinary stone left to dress.
			stock.Set(KingdomMaterial.Stone, KingdomMaterialRules.RawPerRefined - 1);
			ClassicAssert.AreEqual(40 / KingdomMaterialRules.RawPerRefined, KingdomMaterialRules.RefinableFrom(KingdomYard.Mason, stock, out raw));
			ClassicAssert.AreEqual(KingdomMaterial.Marble, raw);
		}

		[Test]
		public void RefinableFrom_IsNothingBelowOneWholeUnitOfStock()
		{
			KingdomMaterialTally stock = new KingdomMaterialTally();
			stock.Set(KingdomMaterial.Timber, KingdomMaterialRules.RawPerRefined - 1);
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinableFrom(KingdomYard.Sawyer, stock, out _));
			stock.Set(KingdomMaterial.Timber, KingdomMaterialRules.RawPerRefined);
			ClassicAssert.AreEqual(1, KingdomMaterialRules.RefinableFrom(KingdomYard.Sawyer, stock, out _));
		}

		[Test]
		public void RefinableFrom_ReadsAnEmptyStockAndANullOneAsNothing()
		{
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinableFrom(KingdomYard.Smelter, null, out _));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinableFrom(KingdomYard.Smelter, new KingdomMaterialTally(), out _));
		}

		[TestCase(0, 4, 100, 8)]
		[TestCase(2, 0, 100, 8)]
		[TestCase(2, 4, 100, 0)]
		public void RefinedThisPass_MakesNothingWithoutCrewDaysAndStock(int crew, int days, int capability, int refinable)
		{
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinedThisPass(crew, days, capability, refinable));
		}

		[Test]
		public void RefinedThisPass_NeverExceedsWhatTheStockCovers()
		{
			int made = KingdomMaterialRules.RefinedThisPass(6, 30, KingdomMaterialRules.MaxCapabilityPercent, 1);
			ClassicAssert.AreEqual(1, made);
		}

		[Test]
		public void RefinedThisPass_CeilingIsARateAndNotAVisitBudget()
		{
			// MaxRefinedPerPass became MaxRefinedPerDay, and this is what that buys: a grand
			// build waits on the yard RUNNING, never on the founder arriving. Thirty days of a
			// crew big enough to beat the bench's throughput finishes thirty days of it.
			int aDay = KingdomMaterialRules.RefinedThisPass(999, 1, 100, 99999);
			ClassicAssert.AreEqual(KingdomMaterialRules.MaxRefinedPerDay, aDay, "the day's throughput is the day's throughput");
			int thirtyDays = KingdomMaterialRules.RefinedThisPass(999, 30, 100, 99999);
			ClassicAssert.AreEqual(KingdomMaterialRules.MaxRefinedPerDay * 30, thirtyDays,
				"thirty days of running made one pass of work");
		}

		[Test]
		public void RefinedThisPass_RunsTheWholeAbsenceButNeverBeatsTheBench()
		{
			// Both halves of the rate. A long stretch is more work than a short one (clause 1),
			// and no crew however large beats the day's throughput on any single day.
			int short_ = KingdomMaterialRules.RefinedThisPass(4, 2, 100, 99999);
			int long_ = KingdomMaterialRules.RefinedThisPass(4, 900, 100, 99999);
			ClassicAssert.Greater(long_, short_, "a long stretch was not more work than a short one");
			for (int days = 1; days <= 40; days++)
			{
				ClassicAssert.LessOrEqual(
					KingdomMaterialRules.RefinedThisPass(999, days, KingdomMaterialRules.MaxCapabilityPercent, 99999),
					KingdomMaterialRules.MaxRefinedPerDay * days,
					"a crew beat the bench's own throughput");
			}
		}

		[Test]
		public void RefinedThisPass_AnUnstaffedYardShapesNothingHoweverLongTheStretch()
		{
			// Clause 2. The whole reason uncapping the yards changed only how much TIME can be
			// worked and never whether unstaffed work happens.
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinedThisPass(0, 900, 100, 99999));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinedThisPass(-2, 900, 100, 99999));
		}

		[Test]
		public void RefinedThisPass_DoesNotOverflowOnANonsenseStretch()
		{
			int made = KingdomMaterialRules.RefinedThisPass(999, int.MaxValue, KingdomMaterialRules.MaxCapabilityPercent, 99999);
			ClassicAssert.AreEqual(99999, made, "the raw stock stopped binding");
			ClassicAssert.GreaterOrEqual(made, 0);
		}

		[Test]
		public void RefinedThisPass_RewardsACapableCrewAndNeverPunishesAnOrdinaryOneToNothing()
		{
			int ordinary = KingdomMaterialRules.RefinedThisPass(2, 3, 100, 999);
			int deft = KingdomMaterialRules.RefinedThisPass(2, 3, KingdomMaterialRules.MaxCapabilityPercent, 999);
			int slow = KingdomMaterialRules.RefinedThisPass(2, 3, KingdomMaterialRules.MinCapabilityPercent, 999);
			ClassicAssert.IsTrue(deft > slow, "a deft crew must out-work a slow one");
			ClassicAssert.IsTrue(ordinary >= slow && deft >= ordinary);
			ClassicAssert.IsTrue(slow > 0, "a slow crew still works");
		}

		[Test]
		public void RawSpent_IsAlwaysWhatWasMadeTimesTheRatioAndNeverNegative()
		{
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RawSpentFor(0));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RawSpentFor(-3));
			ClassicAssert.AreEqual(3 * KingdomMaterialRules.RawPerRefined, KingdomMaterialRules.RawSpentFor(3));
			ClassicAssert.IsTrue(KingdomMaterialRules.RawPerRefined > 1, "refining that returned everything it ate would be free");
		}

		[Test]
		public void RefinedTotal_CountsOnlyWhatTheYardsMade()
		{
			KingdomMaterialTally tally = Tally(1, 1, 1, 1, 1, 1);
			ClassicAssert.AreEqual(0, tally.RefinedTotal());
			tally.Set(KingdomMaterial.ShapedStone, 3);
			tally.Set(KingdomMaterial.WorkedMetal, 2);
			ClassicAssert.AreEqual(5, tally.RefinedTotal());
		}

		// --- Crews have capability, read off who they are --------------------------------------

		[Test]
		public void CapabilityPercent_IsAHundredForAnOrdinaryPairOfHands()
		{
			ClassicAssert.AreEqual(100, KingdomMaterialRules.CapabilityPercent(KingdomMaterialRules.BaselineStat));
		}

		[Test]
		public void CapabilityPercent_ClimbsWithTheStatAndIsClampedBothWays()
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.CapabilityPercent(KingdomMaterialRules.BaselineStat + 2)
				> KingdomMaterialRules.CapabilityPercent(KingdomMaterialRules.BaselineStat));
			ClassicAssert.IsTrue(KingdomMaterialRules.CapabilityPercent(KingdomMaterialRules.BaselineStat - 2)
				< KingdomMaterialRules.CapabilityPercent(KingdomMaterialRules.BaselineStat));
			ClassicAssert.AreEqual(KingdomMaterialRules.MaxCapabilityPercent, KingdomMaterialRules.CapabilityPercent(200));
			ClassicAssert.AreEqual(KingdomMaterialRules.MinCapabilityPercent, KingdomMaterialRules.CapabilityPercent(-50));
		}

		[TestCase(KingdomYard.Sawyer, KingdomCapability.Muscle)]
		[TestCase(KingdomYard.Mason, KingdomCapability.Muscle)]
		[TestCase(KingdomYard.Smelter, KingdomCapability.Mind)]
		public void CapabilityFor_ReadsStonesWithMuscleAndFurnacesWithMind(KingdomYard yard, KingdomCapability expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.CapabilityFor(yard));
		}

		[Test]
		public void CrewCapability_ReadsTheStatTheWorkIsActuallyDoneWith()
		{
			int strong = KingdomMaterialRules.BaselineStat + 4;
			int clever = KingdomMaterialRules.BaselineStat + 4;
			// A yard of strong backs saws well and smelts ordinarily; the reverse for scribes.
			ClassicAssert.AreEqual(KingdomMaterialRules.CapabilityPercent(strong), KingdomMaterialRules.CrewCapability(KingdomYard.Sawyer, strong, KingdomMaterialRules.BaselineStat));
			ClassicAssert.AreEqual(100, KingdomMaterialRules.CrewCapability(KingdomYard.Smelter, strong, KingdomMaterialRules.BaselineStat));
			ClassicAssert.AreEqual(KingdomMaterialRules.CapabilityPercent(clever), KingdomMaterialRules.CrewCapability(KingdomYard.Smelter, KingdomMaterialRules.BaselineStat, clever));
			ClassicAssert.AreEqual(100, KingdomMaterialRules.CrewCapability(KingdomYard.Mason, KingdomMaterialRules.BaselineStat, clever));
		}

		[Test]
		public void CrewCapability_ReadsPeopleItCannotSeeAsOrdinaryRatherThanUseless()
		{
			ClassicAssert.AreEqual(100, KingdomMaterialRules.CrewCapability(KingdomYard.Mason, 0, 0));
			ClassicAssert.AreEqual(KingdomMaterialRules.BaselineStat, KingdomMaterialRules.AverageStat(null));
			ClassicAssert.AreEqual(KingdomMaterialRules.BaselineStat, KingdomMaterialRules.AverageStat(new List<int>()));
		}

		[Test]
		public void AverageStat_IsTheAverageRoundedDown()
		{
			ClassicAssert.AreEqual(15, KingdomMaterialRules.AverageStat(new List<int> { 14, 16, 17 }));
		}

		[Test]
		public void CapabilityWord_SaysSomethingDifferentAtEachEnd()
		{
			ClassicAssert.AreEqual("steady", KingdomMaterialRules.CapabilityWord(100));
			ClassicAssert.AreNotEqual("steady", KingdomMaterialRules.CapabilityWord(KingdomMaterialRules.MaxCapabilityPercent));
			ClassicAssert.AreNotEqual("steady", KingdomMaterialRules.CapabilityWord(KingdomMaterialRules.MinCapabilityPercent));
			ClassicAssert.AreNotEqual(KingdomMaterialRules.CapabilityWord(KingdomMaterialRules.MinCapabilityPercent),
				KingdomMaterialRules.CapabilityWord(KingdomMaterialRules.MaxCapabilityPercent));
		}

		// --- Bits, which are vanilla's own ------------------------------------------------------

		[Test]
		public void BitTiers_MatchTheGamesOwnTable()
		{
			// BitType.Init files twelve colours under nine levels and BitType.GetBitTier is the map:
			// R G B C are all tier zero, then r g b c K W Y M climb one by one. If the game ever
			// changes that, a cost written in tiers stops meaning what the catalogue thinks it does.
			ClassicAssert.AreEqual(KingdomMaterialRules.BitColours.Length, KingdomMaterialRules.BitColourTiers.Length);
			ClassicAssert.AreEqual("RGBCrgbcKWYM", KingdomMaterialRules.BitColours);
			int[] expected = new int[12] { 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 };
			for (int i = 0; i < expected.Length; i++)
			{
				ClassicAssert.IsTrue(KingdomMaterialRules.TryBitTier(KingdomMaterialRules.BitColours[i], out var tier));
				ClassicAssert.AreEqual(expected[i], tier, "colour " + KingdomMaterialRules.BitColours[i]);
			}
			ClassicAssert.AreEqual(9, KingdomMaterialRules.BitTierCount);
			ClassicAssert.AreEqual(KingdomMaterialRules.BitTierCount, KingdomMaterialRules.BitTierNames.Length);
		}

		[TestCase('x')]
		[TestCase('0')]
		[TestCase(' ')]
		[TestCase('9')]
		public void TryBitTier_RefusesAnythingThatIsNotOneOfTheGamesTwelve(char colour)
		{
			ClassicAssert.IsFalse(KingdomMaterialRules.TryBitTier(colour, out _), colour.ToString());
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void TryParseBitCost_AbsentIsAnEmptyCostAndNotAnError(string text)
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseBitCost(text, out var cost, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.IsTrue(cost.IsEmpty());
			ClassicAssert.IsTrue(KingdomMaterialRules.CoversBits(new KingdomBitTally(), cost));
			ClassicAssert.IsNull(cost.Describe());
		}

		[Test]
		public void TryParseBitCost_ReadsTiersAndCountsRepeats()
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseBitCost("0034", out var cost, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.AreEqual(2, cost.Get(0));
			ClassicAssert.AreEqual(1, cost.Get(3));
			ClassicAssert.AreEqual(1, cost.Get(4));
			ClassicAssert.AreEqual(0, cost.Get(1));
			ClassicAssert.AreEqual(4, cost.Total());
		}

		[Test]
		public void TryParseBitCost_ReadsTheGamesOwnColoursAndIgnoresPunctuation()
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseBitCost(" B B, b c ", out var cost, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.AreEqual(2, cost.Get(0), "two scrap-metal bits");
			ClassicAssert.AreEqual(1, cost.Get(3), "pure alloy");
			ClassicAssert.AreEqual(1, cost.Get(4), "pristine electronics");
		}

		[TestCase("9")]
		[TestCase("00x")]
		[TestCase("z")]
		public void TryParseBitCost_RejectsTheWholeAttributeRatherThanHalfReadingIt(string text)
		{
			ClassicAssert.IsFalse(KingdomMaterialRules.TryParseBitCost(text, out var cost, out var error), text);
			ClassicAssert.IsNotNull(error);
			ClassicAssert.IsTrue(cost.IsEmpty(), "nothing is half-parsed");
		}

		[Test]
		public void CoversBits_IsExactAtTheBoundary()
		{
			KingdomBitTally stock = new KingdomBitTally();
			stock.Set(0, 2);
			stock.Set(3, 1);
			KingdomBitTally cost = new KingdomBitTally();
			cost.Set(0, 2);
			cost.Set(3, 1);
			ClassicAssert.IsTrue(KingdomMaterialRules.CoversBits(stock, cost));
			cost.Set(3, 2);
			ClassicAssert.IsFalse(KingdomMaterialRules.CoversBits(stock, cost));
			// A higher tier is never spent in place of a lower one: bits are not change.
			stock.Set(4, 9);
			ClassicAssert.IsFalse(KingdomMaterialRules.CoversBits(stock, cost));
		}

		[Test]
		public void MissingBits_NamesOnlyTheShortfall()
		{
			KingdomBitTally stock = new KingdomBitTally();
			stock.Set(0, 1);
			KingdomBitTally cost = new KingdomBitTally();
			cost.Set(0, 3);
			cost.Set(5, 1);
			KingdomBitTally missing = KingdomMaterialRules.MissingBits(stock, cost);
			ClassicAssert.AreEqual(2, missing.Get(0));
			ClassicAssert.AreEqual(1, missing.Get(5));
			ClassicAssert.IsTrue(KingdomMaterialRules.CoversBits(null, new KingdomBitTally()));
			ClassicAssert.IsTrue(KingdomMaterialRules.MissingBits(stock, null).IsEmpty());
		}

		[Test]
		public void BitTally_CopiesAreIndependentAndScalingRoundsDown()
		{
			KingdomBitTally bits = new KingdomBitTally();
			bits.Set(0, 3);
			KingdomBitTally copy = bits.Copy();
			copy.Add(0, 5);
			ClassicAssert.AreEqual(3, bits.Get(0));
			ClassicAssert.AreEqual(8, copy.Get(0));
			ClassicAssert.AreEqual(1, bits.Scaled(50).Get(0));
			ClassicAssert.IsTrue(bits.Scaled(0).IsEmpty());
			// Never negative, however hard a caller subtracts.
			bits.Add(0, -99);
			ClassicAssert.AreEqual(0, bits.Get(0));
		}

		[Test]
		public void BitTally_DescribesItselfInTheGamesOwnWords()
		{
			KingdomBitTally bits = new KingdomBitTally();
			bits.Set(3, 2);
			string described = bits.Describe();
			ClassicAssert.IsNotNull(described);
			ClassicAssert.IsTrue(described.Contains("pure alloy"), described);
			ClassicAssert.IsTrue(described.Contains("2"), described);
		}

		// --- Exotic materials: rare finds ------------------------------------------------------

		[Test]
		public void ExoticVocabulary_IsSizedAgainstItsOwnEnum()
		{
			ClassicAssert.AreEqual(System.Enum.GetValues(typeof(KingdomExotic)).Length, KingdomMaterialRules.ExoticCount);
			ClassicAssert.AreEqual(KingdomMaterialRules.ExoticCount, KingdomMaterialRules.ExoticKeys.Length);
			ClassicAssert.AreEqual(KingdomMaterialRules.ExoticCount, KingdomMaterialRules.ExoticNames.Length);
			ClassicAssert.AreEqual(KingdomMaterialRules.ExoticCount, KingdomMaterialRules.ExoticPlurals.Length);
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomMaterialRules.ExoticKeys[i]));
				ClassicAssert.IsTrue(KingdomMaterialRules.TryParseExotic(KingdomMaterialRules.ExoticKeys[i], out var parsed));
				ClassicAssert.AreEqual((KingdomExotic)i, parsed);
				for (int j = i + 1; j < KingdomMaterialRules.ExoticCount; j++)
				{
					ClassicAssert.AreNotEqual(KingdomMaterialRules.ExoticKeys[i], KingdomMaterialRules.ExoticKeys[j]);
					ClassicAssert.AreNotEqual(KingdomMaterialRules.ExoticNames[i], KingdomMaterialRules.ExoticNames[j]);
				}
			}
		}

		[TestCase("gold", KingdomExotic.Gold)]
		[TestCase("Gold Nugget", KingdomExotic.Gold)]
		[TestCase("goldnugget", KingdomExotic.Gold)]
		[TestCase("bronze", KingdomExotic.Ingot)]
		[TestCase("ingot", KingdomExotic.Ingot)]
		[TestCase(" GEM ", KingdomExotic.Gem)]
		[TestCase("gemstone", KingdomExotic.Gem)]
		[TestCase("rough gemstones", KingdomExotic.Gem)]
		[TestCase("silver", KingdomExotic.Silver)]
		public void TryParseExotic_ReadsTheKeyTheNameAndThePlural(string key, KingdomExotic expected)
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseExotic(key, out var exotic), key);
			ClassicAssert.AreEqual(expected, exotic);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("timber")]
		[TestCase("zetachrome")]
		public void TryParseExotic_RefusesAnythingElse(string key)
		{
			ClassicAssert.IsFalse(KingdomMaterialRules.TryParseExotic(key, out _), key ?? "null");
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("  ")]
		public void TryParseExoticCost_AbsentIsAnEmptyCostAndNotAnError(string text)
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseExoticCost(text, out var cost, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.IsTrue(cost.IsEmpty());
			ClassicAssert.IsTrue(KingdomMaterialRules.CoversExotics(new KingdomExoticTally(), cost));
		}

		[Test]
		public void TryParseExoticCost_ReadsSeveralTerms()
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseExoticCost("gold:2, gem:1", out var cost, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.AreEqual(2, cost.Get(KingdomExotic.Gold));
			ClassicAssert.AreEqual(1, cost.Get(KingdomExotic.Gem));
			ClassicAssert.AreEqual(0, cost.Get(KingdomExotic.Silver));
		}

		[TestCase("gold")]
		[TestCase("gold:")]
		[TestCase("gold:0")]
		[TestCase("gold:-1")]
		[TestCase("gold:two")]
		[TestCase("gold:1,gold:2")]
		[TestCase("zetachrome:1")]
		[TestCase("gold:1,,gem:1")]
		public void TryParseExoticCost_RejectsTheWholeAttributeRatherThanHalfReadingIt(string text)
		{
			ClassicAssert.IsFalse(KingdomMaterialRules.TryParseExoticCost(text, out var cost, out var error), text);
			ClassicAssert.IsNotNull(error);
			ClassicAssert.IsTrue(cost.IsEmpty(), "nothing is half-parsed");
		}

		[Test]
		public void CoversAndMissingExotics_AreExactAtTheBoundary()
		{
			KingdomExoticTally stock = new KingdomExoticTally();
			stock.Set(KingdomExotic.Gem, 2);
			KingdomExoticTally cost = new KingdomExoticTally();
			cost.Set(KingdomExotic.Gem, 2);
			ClassicAssert.IsTrue(KingdomMaterialRules.CoversExotics(stock, cost));
			cost.Set(KingdomExotic.Gem, 3);
			ClassicAssert.IsFalse(KingdomMaterialRules.CoversExotics(stock, cost));
			ClassicAssert.AreEqual(1, KingdomMaterialRules.MissingExotics(stock, cost).Get(KingdomExotic.Gem));
			// One rare find is never substituted for another: a dome of gold is not a dome of bronze.
			stock.Set(KingdomExotic.Gold, 40);
			ClassicAssert.IsFalse(KingdomMaterialRules.CoversExotics(stock, cost));
		}

		[Test]
		public void ExoticTally_DescribesOneAsSingularAndTwoAsPlural()
		{
			KingdomExoticTally one = new KingdomExoticTally();
			one.Set(KingdomExotic.Gold, 1);
			ClassicAssert.AreEqual("1 " + KingdomMaterialRules.ExoticNames[(int)KingdomExotic.Gold], one.Describe());
			KingdomExoticTally two = new KingdomExoticTally();
			two.Set(KingdomExotic.Gold, 2);
			ClassicAssert.AreEqual("2 " + KingdomMaterialRules.ExoticPlurals[(int)KingdomExotic.Gold], two.Describe());
			ClassicAssert.IsNull(new KingdomExoticTally().Describe());
		}

		// --- Infrastructure gates construction -------------------------------------------------

		private static KingdomMaterialTally Cost(KingdomMaterial Material, int Units)
		{
			KingdomMaterialTally cost = new KingdomMaterialTally();
			cost.Set(Material, Units);
			return cost;
		}

		private static List<KingdomMaterialRules.KingdomYardStanding> Yards(KingdomYard Kind, bool Standing, bool Staffed, bool Headed)
		{
			return new List<KingdomMaterialRules.KingdomYardStanding>
			{
				new KingdomMaterialRules.KingdomYardStanding(Kind, Standing, Staffed, Headed)
			};
		}

		[TestCase(KingdomPlotRules.PlotSize.None)]
		[TestCase(KingdomPlotRules.PlotSize.Small)]
		[TestCase(KingdomPlotRules.PlotSize.Medium)]
		public void SmallWork_IsRaisedByWhoeverIsFree(KingdomPlotRules.PlotSize size)
		{
			ClassicAssert.IsFalse(KingdomMaterialRules.RequiresYard(size));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.YardsFor(size, Cost(KingdomMaterial.Stone, 40)).Count);
			ClassicAssert.IsTrue(KingdomMaterialRules.AllowsBuild(size, Cost(KingdomMaterial.Stone, 40), null, "hut", out var refusal));
			ClassicAssert.IsNull(refusal);
		}

		[Test]
		public void LargeWork_WantsTheYardItsMaterialImplies()
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.RequiresYard(KingdomPlotRules.PlotSize.Large));
			List<KingdomYard> wanted = KingdomMaterialRules.YardsFor(KingdomPlotRules.PlotSize.Large, Cost(KingdomMaterial.Stone, 40));
			ClassicAssert.AreEqual(1, wanted.Count);
			ClassicAssert.AreEqual(KingdomYard.Mason, wanted[0]);
			ClassicAssert.AreEqual(KingdomYard.Sawyer, KingdomMaterialRules.YardsFor(KingdomPlotRules.PlotSize.Large, Cost(KingdomMaterial.Timber, 40))[0]);
			ClassicAssert.AreEqual(KingdomYard.Smelter, KingdomMaterialRules.YardsFor(KingdomPlotRules.PlotSize.Large, Cost(KingdomMaterial.Scrap, 40))[0]);
		}

		[Test]
		public void ARefinedMaterialNamesItsOwnYardEvenWhenSomethingElseIsDominant()
		{
			KingdomMaterialTally cost = new KingdomMaterialTally();
			cost.Set(KingdomMaterial.Stone, 40);
			cost.Set(KingdomMaterial.ShapedTimber, 2);
			List<KingdomYard> wanted = KingdomMaterialRules.YardsFor(KingdomPlotRules.PlotSize.Large, cost);
			ClassicAssert.IsTrue(wanted.Contains(KingdomYard.Sawyer), "shaped timber comes off a saw-pit and nowhere else");
			ClassicAssert.IsFalse(wanted.Contains(KingdomYard.Mason), "the dominant-material rule only answers when nothing refined was named");
		}

		[Test]
		public void AWorkOfMudAndBrush_ImpliesNoYardAtAnySize()
		{
			KingdomMaterialTally cost = new KingdomMaterialTally();
			cost.Set(KingdomMaterial.Mud, 40);
			cost.Set(KingdomMaterial.Brush, 12);
			ClassicAssert.AreEqual(0, KingdomMaterialRules.YardsFor(KingdomPlotRules.PlotSize.Huge, cost).Count);
			ClassicAssert.IsFalse(KingdomMaterialRules.TryDominantYard(cost, out _));
			ClassicAssert.IsTrue(KingdomMaterialRules.AllowsBuild(KingdomPlotRules.PlotSize.Huge, cost, null, "earthwork", out _));
		}

		[Test]
		public void AWorkWithNoMaterialCostAtAll_IsNeverGated()
		{
			// Every design in the catalogue before materials existed, and every third-party one
			// that still costs water alone.
			ClassicAssert.AreEqual(0, KingdomMaterialRules.YardsFor(KingdomPlotRules.PlotSize.Huge, new KingdomMaterialTally()).Count);
			ClassicAssert.AreEqual(0, KingdomMaterialRules.YardsFor(KingdomPlotRules.PlotSize.Huge, null).Count);
			ClassicAssert.IsTrue(KingdomMaterialRules.AllowsBuild(KingdomPlotRules.PlotSize.Huge, null, null, "great work", out _));
		}

		[Test]
		public void TheDominantYard_IsTheOneMostOfTheWorkIsMadeOf()
		{
			KingdomMaterialTally cost = new KingdomMaterialTally();
			cost.Set(KingdomMaterial.Timber, 10);
			cost.Set(KingdomMaterial.Stone, 30);
			ClassicAssert.IsTrue(KingdomMaterialRules.TryDominantYard(cost, out var yard));
			ClassicAssert.AreEqual(KingdomYard.Mason, yard);
			cost.Set(KingdomMaterial.Timber, 60);
			ClassicAssert.IsTrue(KingdomMaterialRules.TryDominantYard(cost, out yard));
			ClassicAssert.AreEqual(KingdomYard.Sawyer, yard);
			// Marble counts toward the mason's tally: it is stone, and the mason is who dresses it.
			KingdomMaterialTally marble = Cost(KingdomMaterial.Marble, 30);
			ClassicAssert.IsTrue(KingdomMaterialRules.TryDominantYard(marble, out yard));
			ClassicAssert.AreEqual(KingdomYard.Mason, yard);
		}

		[Test]
		public void NoYardStanding_IsRefusedByTheNameOfTheYardThatIsMissing()
		{
			ClassicAssert.IsFalse(KingdomMaterialRules.AllowsBuild(KingdomPlotRules.PlotSize.Large, Cost(KingdomMaterial.Stone, 40), null, "temple", out var refusal));
			ClassicAssert.IsNotNull(refusal);
			ClassicAssert.IsTrue(refusal.Contains(KingdomMaterialRules.YardName(KingdomYard.Mason)), refusal);
			ClassicAssert.IsTrue(refusal.Contains("temple"), refusal);
		}

		[Test]
		public void AYardStandingIdle_IsADifferentRefusalFromNoYardAtAll()
		{
			KingdomMaterialTally cost = Cost(KingdomMaterial.Stone, 40);
			ClassicAssert.IsFalse(KingdomMaterialRules.AllowsBuild(KingdomPlotRules.PlotSize.Large, cost,
				Yards(KingdomYard.Mason, Standing: true, Staffed: false, Headed: true), "temple", out var idle));
			ClassicAssert.IsFalse(KingdomMaterialRules.AllowsBuild(KingdomPlotRules.PlotSize.Large, cost, null, "temple", out var none));
			ClassicAssert.AreNotEqual(none, idle, "a founder told the same sentence for both cannot act on either");
			ClassicAssert.IsTrue(idle.Contains(KingdomMaterialRules.YardName(KingdomYard.Mason)), idle);
		}

		[Test]
		public void AStaffedYard_CarriesALargeWork()
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.AllowsBuild(KingdomPlotRules.PlotSize.Large, Cost(KingdomMaterial.Stone, 40),
				Yards(KingdomYard.Mason, Standing: true, Staffed: true, Headed: false), "temple", out var refusal));
			ClassicAssert.IsNull(refusal);
		}

		[Test]
		public void AGrandWork_WantsTheYardHeadedAsWellAsStaffed()
		{
			ClassicAssert.IsTrue(KingdomMaterialRules.RequiresHeadedYard(KingdomPlotRules.PlotSize.Huge));
			ClassicAssert.IsFalse(KingdomMaterialRules.RequiresHeadedYard(KingdomPlotRules.PlotSize.Large));
			KingdomMaterialTally cost = Cost(KingdomMaterial.Stone, 90);
			ClassicAssert.IsFalse(KingdomMaterialRules.AllowsBuild(KingdomPlotRules.PlotSize.Huge, cost,
				Yards(KingdomYard.Mason, Standing: true, Staffed: true, Headed: false), "cathedral", out var refusal));
			ClassicAssert.IsNotNull(refusal);
			ClassicAssert.IsTrue(refusal.Contains(KingdomMaterialRules.YardName(KingdomYard.Mason)), refusal);
			ClassicAssert.IsTrue(KingdomMaterialRules.AllowsBuild(KingdomPlotRules.PlotSize.Huge, cost,
				Yards(KingdomYard.Mason, Standing: true, Staffed: true, Headed: true), "cathedral", out _));
		}

		[Test]
		public void TheGateAsksEveryYardTheDesignNamesAndNotJustTheFirst()
		{
			KingdomMaterialTally cost = new KingdomMaterialTally();
			cost.Set(KingdomMaterial.ShapedStone, 20);
			cost.Set(KingdomMaterial.WorkedMetal, 8);
			List<KingdomMaterialRules.KingdomYardStanding> yards = new List<KingdomMaterialRules.KingdomYardStanding>
			{
				new KingdomMaterialRules.KingdomYardStanding(KingdomYard.Mason, Standing: true, Staffed: true, Headed: true)
			};
			ClassicAssert.AreEqual(2, KingdomMaterialRules.YardsFor(KingdomPlotRules.PlotSize.Large, cost).Count);
			ClassicAssert.IsFalse(KingdomMaterialRules.AllowsBuild(KingdomPlotRules.PlotSize.Large, cost, yards, "waterworks", out var refusal));
			ClassicAssert.IsTrue(refusal.Contains(KingdomMaterialRules.YardName(KingdomYard.Smelter)), refusal);
		}

		[Test]
		public void StandingOf_ReadsAYardThatStandsNowhereAsStandingNowhere()
		{
			KingdomMaterialRules.KingdomYardStanding standing = KingdomMaterialRules.StandingOf(null, KingdomYard.Sawyer);
			ClassicAssert.AreEqual(KingdomYard.Sawyer, standing.Yard);
			ClassicAssert.IsFalse(standing.Standing);
			ClassicAssert.IsFalse(standing.Staffed);
			ClassicAssert.IsFalse(standing.Headed);
		}

		[Test]
		public void YardRequirementLine_SaysNothingWhenNothingIsAsked()
		{
			ClassicAssert.IsNull(KingdomMaterialRules.YardRequirementLine(KingdomPlotRules.PlotSize.Small, Cost(KingdomMaterial.Stone, 40)));
			string large = KingdomMaterialRules.YardRequirementLine(KingdomPlotRules.PlotSize.Large, Cost(KingdomMaterial.Stone, 40));
			ClassicAssert.IsNotNull(large);
			ClassicAssert.IsTrue(large.Contains(KingdomMaterialRules.YardName(KingdomYard.Mason)), large);
			string huge = KingdomMaterialRules.YardRequirementLine(KingdomPlotRules.PlotSize.Huge, Cost(KingdomMaterial.Stone, 90));
			ClassicAssert.AreNotEqual(large, huge, "a grand work asks for more than a large one and should say so");
		}

		// --- Wear from events, and what mending it costs ----------------------------------------

		[Test]
		public void Wear_IsClampedAndNeverKillsAWork()
		{
			ClassicAssert.AreEqual(0, KingdomMaterialRules.AddWear(0, 0));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.AddWear(-10, -10));
			ClassicAssert.AreEqual(KingdomMaterialRules.MaxWearPercent, KingdomMaterialRules.AddWear(KingdomMaterialRules.MaxWearPercent, 40));
			ClassicAssert.AreEqual(KingdomMaterialRules.MaxWearPercent, KingdomMaterialRules.AddWear(0, 9999));
			// The floor is the whole ruling: a damaged work runs reduced and never dies.
			ClassicAssert.IsTrue(KingdomMaterialRules.ConditionPercent(9999) > 0);
			ClassicAssert.AreEqual(100 - KingdomMaterialRules.MaxWearPercent, KingdomMaterialRules.ConditionPercent(9999));
			ClassicAssert.AreEqual(100, KingdomMaterialRules.ConditionPercent(0));
			ClassicAssert.AreEqual(100, KingdomMaterialRules.ConditionPercent(-5));
		}

		[Test]
		public void ConditionWord_ReadsWorseAsTheWearClimbs()
		{
			ClassicAssert.AreEqual("sound", KingdomMaterialRules.ConditionWord(0));
			ClassicAssert.AreNotEqual("sound", KingdomMaterialRules.ConditionWord(1));
			ClassicAssert.AreNotEqual(KingdomMaterialRules.ConditionWord(10), KingdomMaterialRules.ConditionWord(50));
		}

		[Test]
		public void RepairCost_IsAShareOfWhatTheWorkWasBuiltFromAndNeverTheWholeThingAgain()
		{
			KingdomMaterialTally build = new KingdomMaterialTally();
			build.Set(KingdomMaterial.Stone, 40);
			build.Set(KingdomMaterial.Timber, 10);
			KingdomMaterialTally repair = KingdomMaterialRules.RepairCost(build, 50);
			ClassicAssert.AreEqual(20, repair.Get(KingdomMaterial.Stone));
			ClassicAssert.AreEqual(5, repair.Get(KingdomMaterial.Timber));
			ClassicAssert.IsTrue(repair.Total() < build.Total());
			// A sound work costs nothing to mend, and a work built of nothing is mended for nothing.
			ClassicAssert.IsTrue(KingdomMaterialRules.RepairCost(build, 0).IsEmpty());
			ClassicAssert.IsTrue(KingdomMaterialRules.RepairCost(new KingdomMaterialTally(), 50).IsEmpty());
			ClassicAssert.IsTrue(KingdomMaterialRules.RepairCost(null, 50).IsEmpty());
		}

		[Test]
		public void RepairBits_PricesTheCertifiedHalfInTheSameStockItWasBuiltFrom()
		{
			KingdomBitTally build = new KingdomBitTally();
			build.Set(0, 4);
			build.Set(4, 2);
			KingdomBitTally repair = KingdomMaterialRules.RepairBits(build, 50);
			ClassicAssert.AreEqual(2, repair.Get(0));
			ClassicAssert.AreEqual(1, repair.Get(4));
			ClassicAssert.IsTrue(KingdomMaterialRules.RepairBits(build, 0).IsEmpty());
			ClassicAssert.IsTrue(KingdomMaterialRules.RepairBits(null, 50).IsEmpty());
		}

		[Test]
		public void RepairEffort_IsNeverFreeForAnyWearAtAll()
		{
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RepairEffort(10, 0));
			ClassicAssert.IsTrue(KingdomMaterialRules.RepairEffort(0, 10) >= 1);
			ClassicAssert.IsTrue(KingdomMaterialRules.RepairEffort(20, 10) > KingdomMaterialRules.RepairEffort(4, 10));
		}

		[Test]
		public void DamageLine_SaysNothingAboutASoundWorkAndNamesTheStateOfAWornOne()
		{
			ClassicAssert.IsNull(KingdomMaterialRules.DamageLine("mill", 0));
			string line = KingdomMaterialRules.DamageLine("mill", 30);
			ClassicAssert.IsNotNull(line);
			ClassicAssert.IsTrue(line.Contains("mill"), line);
			ClassicAssert.IsTrue(line.Contains(KingdomMaterialRules.ConditionPercent(30).ToString()), line);
			ClassicAssert.IsTrue(line.Contains(KingdomMaterialRules.ConditionWord(30)), line);
		}

		[Test]
		public void JoinPhrases_ReadsLikeAPersonWroteIt()
		{
			ClassicAssert.IsNull(KingdomMaterialRules.JoinPhrases(null));
			ClassicAssert.IsNull(KingdomMaterialRules.JoinPhrases(new List<string>()));
			ClassicAssert.AreEqual("one", KingdomMaterialRules.JoinPhrases(new List<string> { "one" }));
			ClassicAssert.AreEqual("one and two", KingdomMaterialRules.JoinPhrases(new List<string> { "one", "two" }));
			ClassicAssert.AreEqual("one, two and three", KingdomMaterialRules.JoinPhrases(new List<string> { "one", "two", "three" }));
		}

		// --- AssessYard / YardStallLine: an idle yard makes nothing and says so once ------------

		[TestCase(false, 0, 5, KingdomMaterialRules.YardStall.Unstaffed)]
		[TestCase(false, 4, 5, KingdomMaterialRules.YardStall.Unstaffed)]
		[TestCase(true, 0, 5, KingdomMaterialRules.YardStall.Unstaffed)]
		[TestCase(true, -2, 5, KingdomMaterialRules.YardStall.Unstaffed)]
		[TestCase(true, 4, 0, KingdomMaterialRules.YardStall.NoStock)]
		[TestCase(true, 4, -1, KingdomMaterialRules.YardStall.NoStock)]
		[TestCase(true, 1, 1, KingdomMaterialRules.YardStall.Working)]
		public void AssessYard(bool staffed, int crew, int refinable, KingdomMaterialRules.YardStall expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.AssessYard(staffed, crew, refinable));
		}

		[Test]
		public void AnUnstaffedYardStallsAndShapesNothingHoweverLongTheStretch()
		{
			// The two halves of the uncapping's safety, tied together where they are decided:
			// the gate names the stall (once, per STANDARDS 7b, by the caller's flag), and the
			// rate multiplies the whole stretch by a crew of nobody. Uncapping the yards changed
			// how much TIME can be worked and not whether unstaffed work happens.
			ClassicAssert.AreEqual(KingdomMaterialRules.YardStall.Unstaffed, KingdomMaterialRules.AssessYard(false, 0, 999));
			ClassicAssert.IsNotNull(KingdomMaterialRules.YardStallLine(KingdomMaterialRules.YardStall.Unstaffed, KingdomYard.Sawyer, "Ekuemekiyye"));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinedThisPass(0, 400, 100, 999));
			// And a crewed bench with nothing on it is a DIFFERENT sentence, still zero.
			ClassicAssert.AreEqual(KingdomMaterialRules.YardStall.NoStock, KingdomMaterialRules.AssessYard(true, 3, 0));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinedThisPass(3, 400, 100, 0));
		}

		// --- The bench's effort: capability, then CONDITION, then method (Addendum 10(b), QB-29) --

		/// <summary>
		/// The percent <c>KingdomMaterials.WorkYard</c> hands <see cref="RefinedThisPass"/>, stated
		/// once here so the three factors have a table: the crew's own capability, scaled by the
		/// bench's condition, lifted by what the keepers worked out. The engine line itself is
		/// pinned in <c>_notes/balance-sim.py</c>, which asserts the source composes it this way and
		/// not off the bare crew stretch it read for the whole of the mod's life before QB-29.
		/// </summary>
		private static int YardEffort(int capability, int wear, int method)
		{
			return KingdomProductionRules.Methoded(
				capability * KingdomMaterialRules.ConditionPercent(wear) / 100, method);
		}

		[TestCase(100)]
		[TestCase(KingdomMaterialRules.MaxCapabilityPercent)]
		[TestCase(KingdomMaterialRules.MinCapabilityPercent)]
		public void ASoundYardWorksAtExactlyThePercentItAlwaysDid(int capability)
		{
			// The regression bar for QB-29. Folding condition in may not move a single number for a
			// bench nobody has damaged: a sound work's condition is a hundred.
			ClassicAssert.AreEqual(capability, YardEffort(capability, 0, KingdomProductionRules.BaselineMethodPercent));
		}

		[Test]
		public void ADamagedYardShapesLessAndAMendedOneShapesAsMuchAsItEverDid()
		{
			// Addendum 10(b) at the bench, which is the whole of QB-29: damage degrades function
			// for every work, in its own kind, and a yard's kind is what comes off it.
			int sound = YardEffort(100, 0, 100);
			int worn = YardEffort(100, KingdomMaterialRules.MaxWearPercent, 100);
			ClassicAssert.AreEqual(100, sound);
			ClassicAssert.Less(worn, sound, "a holed saw-pit shapes as much as a whole one");
			ClassicAssert.Greater(worn, 0, "wear is bounded, so a bench is degraded and never destroyed");
			ClassicAssert.Greater(
				KingdomMaterialRules.RefinedThisPass(2, 10, sound, 9999),
				KingdomMaterialRules.RefinedThisPass(2, 10, worn, 9999));
			ClassicAssert.Greater(KingdomMaterialRules.RefinedThisPass(2, 10, worn, 9999), 0,
				"a damaged yard is slow, not shut");
			// Mending ends the consequence outright: the work is the same work again.
			ClassicAssert.AreEqual(sound, YardEffort(100, 0, 100));
		}

		[Test]
		public void AYardsEffortFallsWithWearAndNeverBelowTheFloor()
		{
			int previous = int.MaxValue;
			for (int wear = 0; wear <= KingdomMaterialRules.MaxWearPercent; wear += 10)
			{
				int effort = YardEffort(100, wear, 100);
				ClassicAssert.LessOrEqual(effort, previous, "wear may not make a bench better");
				ClassicAssert.Greater(effort, 0);
				previous = effort;
			}
			// Past the ceiling the reading is clamped, not extrapolated: nothing wears to nothing.
			ClassicAssert.AreEqual(
				YardEffort(100, KingdomMaterialRules.MaxWearPercent, 100),
				YardEffort(100, 400, 100));
		}

		[Test]
		public void WearIsFoldedIntoTheEffortAndNeverIntoTheHeadcount()
		{
			// Why the condition rides the percent and not the crew: every yard in the catalogue
			// stands two, and a headcount of two times a 40% condition truncates to nobody. That
			// would make a damaged bench report "nobody is standing at it" (the WRONG sentence
			// under STANDARDS 7b) and stop dead, instead of shaping less and saying nothing.
			int crewAtTheCeiling = 2 * KingdomWearRules.WorkEffectiveness(2, 100, KingdomMaterialRules.MaxWearPercent) / 100;
			ClassicAssert.AreEqual(0, crewAtTheCeiling, "the truncation this shape exists to avoid is real");
			ClassicAssert.AreEqual(KingdomMaterialRules.YardStall.Working,
				KingdomMaterialRules.AssessYard(true, 2, 999),
				"a two-hand yard at the wear ceiling is still a yard somebody is standing in");
			ClassicAssert.Greater(
				KingdomMaterialRules.RefinedThisPass(2, 10, YardEffort(100, KingdomMaterialRules.MaxWearPercent, 100), 9999),
				0);
		}

		[Test]
		public void NoStateOfRepairCanStaffABenchNobodyIsStandingAt()
		{
			// Addendum 8 clause 2: the crew term is what makes an unstaffed yard make nothing, and
			// neither condition nor method is allowed anywhere near it.
			ClassicAssert.AreEqual(KingdomMaterialRules.YardStall.Unstaffed, KingdomMaterialRules.AssessYard(true, 0, 999));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinedThisPass(0, 400, YardEffort(100, 0, 150), 9999));
			ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinedThisPass(0, 400, YardEffort(100, KingdomMaterialRules.MaxWearPercent, 150), 9999));
		}

		[Test]
		public void AssessYard_NobodyHereOutranksNothingToWork()
		{
			// Both true at once is the ordinary case for a yard the settlement has given up on.
			// The founder can only act on one reason at a time, and hands are the one they can do
			// something about tonight, so staffing is asked first.
			ClassicAssert.AreEqual(KingdomMaterialRules.YardStall.Unstaffed,
				KingdomMaterialRules.AssessYard(false, 0, 0));
		}

		[Test]
		public void AnUnstaffedYardProducesNothingForItsIdleDays()
		{
			// The doctrine's clause 2 at the bench: an unstaffed yard shapes nothing, however
			// many days it was handed. Not "less" -- nothing.
			foreach (int days in new int[4] { 1, 3, 90, 400 })
			{
				ClassicAssert.AreEqual(0, KingdomMaterialRules.RefinedThisPass(0, days, 100, 999),
					"an empty bench shaped something over " + days + " days");
			}
			// And the same stretch with a crew standing at it is not nothing, so the assertion
			// above is about the crew and not about some other zero.
			ClassicAssert.Greater(KingdomMaterialRules.RefinedThisPass(2, 3, 100, 999), 0);
		}

		[Test]
		public void AStalledYardNamesItselfAndTheReason()
		{
			string unstaffed = KingdomMaterialRules.YardStallLine(KingdomMaterialRules.YardStall.Unstaffed, KingdomYard.Mason, "Ekuemekiyye");
			StringAssert.Contains(KingdomMaterialRules.YardName(KingdomYard.Mason), unstaffed);
			StringAssert.Contains("Ekuemekiyye", unstaffed);
			string noStock = KingdomMaterialRules.YardStallLine(KingdomMaterialRules.YardStall.NoStock, KingdomYard.Mason, "Ekuemekiyye");
			StringAssert.Contains("stockpiles", noStock);
			ClassicAssert.AreNotEqual(unstaffed, noStock, "both stalls give the founder the same sentence");
		}

		[Test]
		public void AWorkingYardSaysNothingWhichIsHowTheAnnouncementIsUnsaid()
		{
			// Null is the caller's signal to clear its announce flag, which is what makes the
			// reason given once per STALL rather than once per yard forever (STANDARDS 7b).
			ClassicAssert.IsNull(KingdomMaterialRules.YardStallLine(KingdomMaterialRules.YardStall.Working, KingdomYard.Sawyer, "Ekuemekiyye"));
		}

		// ==================================================================================
		// The stages of ruin (Addendum 10(c)): a worn work must READ as a ruin, in its name and
		// in its description, and a mending must walk both back down the same ladder.
		// ==================================================================================

		[Test]
		public void ConditionWordAndAdjectiveAndLookAllTurnOnExactlyTheSameThresholds()
		{
			// Three surfaces, one ladder. The report's word, the adjective the work wears in its
			// own name, and the sentence somebody reads when they stop and look at it must never
			// be able to describe different buildings.
			for (int wear = 0; wear <= KingdomMaterialRules.MaxWearPercent; wear++)
			{
				bool sound = wear <= 0;
				ClassicAssert.AreEqual(sound, KingdomMaterialRules.ConditionAdjective(wear) == null,
					"the name and the word disagreed about whether wear " + wear + " is sound");
				ClassicAssert.AreEqual(sound, KingdomMaterialRules.ConditionLook(wear) == null,
					"the look and the word disagreed about whether wear " + wear + " is sound");
				if (sound)
				{
					continue;
				}
				bool deepest = wear >= KingdomMaterialRules.HalfWreckedWearPercent;
				ClassicAssert.AreEqual(deepest, KingdomMaterialRules.ConditionWord(wear) == "half-wrecked");
				ClassicAssert.AreEqual(deepest, KingdomMaterialRules.ConditionAdjective(wear) == "ruined",
					"the deepest stage of the name and of the word parted company at wear " + wear);
			}
		}

		[TestCase(0, null)]
		[TestCase(1, "battered")]
		[TestCase(KingdomMaterialRules.BadlyUsedWearPercent - 1, "battered")]
		[TestCase(KingdomMaterialRules.BadlyUsedWearPercent, "half-ruined")]
		[TestCase(KingdomMaterialRules.HalfWreckedWearPercent - 1, "half-ruined")]
		[TestCase(KingdomMaterialRules.HalfWreckedWearPercent, "ruined")]
		[TestCase(KingdomMaterialRules.MaxWearPercent, "ruined")]
		public void ConditionAdjective_PutsTheStageOfRuinIntoTheWorksOwnName(int wear, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMaterialRules.ConditionAdjective(wear));
		}

		[Test]
		public void ConditionAdjective_ReadsAsARuinAndNotAsAStatLineAtTheDeepestStage()
		{
			// The ruling's presentation half: a collapsed settlement's plots must read as ruins,
			// not as pristine buildings with quiet arithmetic against them. At the wear ceiling
			// the work is called what it is.
			ClassicAssert.AreEqual("ruined", KingdomMaterialRules.ConditionAdjective(KingdomMaterialRules.MaxWearPercent));
			ClassicAssert.AreEqual("ruined", KingdomMaterialRules.ConditionAdjective(KingdomMaterialRules.MaxWearPercent + 500));
			ClassicAssert.IsNull(KingdomMaterialRules.ConditionAdjective(-20), "a sound work wore an adjective");
		}

		[Test]
		public void ConditionLook_DescribesEachStageDifferentlyAndSaysNothingAboutASoundWork()
		{
			string battered = KingdomMaterialRules.ConditionLook(1);
			string half = KingdomMaterialRules.ConditionLook(KingdomMaterialRules.BadlyUsedWearPercent);
			string ruined = KingdomMaterialRules.ConditionLook(KingdomMaterialRules.HalfWreckedWearPercent);
			ClassicAssert.IsNull(KingdomMaterialRules.ConditionLook(0));
			ClassicAssert.AreNotEqual(battered, half);
			ClassicAssert.AreNotEqual(half, ruined);
			ClassicAssert.IsNotEmpty(ruined);
		}

		[Test]
		public void MendingWalksTheNameAndTheLookBackDownExactlyTheStagesTheRuinWalkedThemUp()
		{
			// The stage is a function of the wear and of NOTHING else -- no history, no memory of
			// having been worse. So the ladder read going up and read coming down is the same
			// ladder, and a work mended to nothing carries no adjective and no ruin sentence at
			// all: it is simply itself again.
			List<string> up = new List<string>();
			for (int wear = 0; wear <= KingdomMaterialRules.MaxWearPercent; wear++)
			{
				up.Add(KingdomMaterialRules.ConditionAdjective(wear) + "|" + KingdomMaterialRules.ConditionLook(wear));
			}
			for (int wear = KingdomMaterialRules.MaxWearPercent; wear >= 0; wear--)
			{
				ClassicAssert.AreEqual(up[wear],
					KingdomMaterialRules.ConditionAdjective(wear) + "|" + KingdomMaterialRules.ConditionLook(wear),
					"the ladder read differently coming down than it did going up at wear " + wear);
			}
			ClassicAssert.IsNull(KingdomMaterialRules.ConditionAdjective(0), "a mended work kept its ruin in its name");
			ClassicAssert.IsNull(KingdomMaterialRules.ConditionLook(0), "a mended work kept its ruin in its description");
		}

		[Test]
		public void EveryStageOfRuinIsStillAStandingMendableWork()
		{
			// The protection law, read off the presentation: the deepest thing a work can be
			// called is still running, still costs a finite mending, and is never gone.
			ClassicAssert.AreEqual("ruined", KingdomMaterialRules.ConditionAdjective(KingdomMaterialRules.MaxWearPercent));
			ClassicAssert.Greater(KingdomMaterialRules.ConditionPercent(KingdomMaterialRules.MaxWearPercent), 0);
			ClassicAssert.Less(KingdomMaterialRules.BadlyUsedWearPercent, KingdomMaterialRules.HalfWreckedWearPercent);
			ClassicAssert.Less(KingdomMaterialRules.HalfWreckedWearPercent, KingdomMaterialRules.MaxWearPercent,
				"the deepest stage of ruin is the ceiling itself, so there is nothing above it to read");
		}

	}
}
#endif
