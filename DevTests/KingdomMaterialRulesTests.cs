#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

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
			Assert.AreEqual(Enum.GetValues(typeof(KingdomMaterial)).Length, KingdomMaterialRules.MaterialCount);
			Assert.AreEqual(KingdomMaterialRules.MaterialCount, KingdomMaterialRules.MaterialKeys.Length);
			Assert.AreEqual(KingdomMaterialRules.MaterialCount, KingdomMaterialRules.MaterialNames.Length);
			Assert.AreEqual(KingdomMaterialRules.MaterialCount, KingdomMaterialRules.WallMaterialThreshold.Length);
			Assert.AreEqual(KingdomMaterialRules.MaterialCount, KingdomMaterialRules.WallMaterialPreference.Length);
		}

		[Test]
		public void StandingCount_MatchesTheEnum()
		{
			Assert.AreEqual(Enum.GetValues(typeof(KingdomStanding)).Length, KingdomMaterialRules.StandingCount);
			Assert.AreEqual(KingdomMaterialRules.StandingCount, KingdomMaterialRules.StandingEffort.Length);
			Assert.AreEqual(KingdomMaterialRules.StandingCount, KingdomMaterialRules.StandingYield.Length);
		}

		[Test]
		public void MaterialKeysAndNames_AreDistinctAndNonEmpty()
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				Assert.IsFalse(string.IsNullOrEmpty(KingdomMaterialRules.MaterialKeys[i]), "key " + i);
				Assert.IsFalse(string.IsNullOrEmpty(KingdomMaterialRules.MaterialNames[i]), "name " + i);
				for (int j = i + 1; j < KingdomMaterialRules.MaterialCount; j++)
				{
					Assert.AreNotEqual(KingdomMaterialRules.MaterialKeys[i], KingdomMaterialRules.MaterialKeys[j]);
					Assert.AreNotEqual(KingdomMaterialRules.MaterialNames[i], KingdomMaterialRules.MaterialNames[j]);
				}
			}
		}

		[Test]
		public void EveryMaterial_RoundTripsThroughItsOwnKey()
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				KingdomMaterial material = (KingdomMaterial)i;
				Assert.IsTrue(KingdomMaterialRules.TryParseMaterial(KingdomMaterialRules.MaterialKey(material), out var parsed), KingdomMaterialRules.MaterialKey(material));
				Assert.AreEqual(material, parsed);
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
			Assert.IsTrue(KingdomMaterialRules.TryParseMaterial(key, out var material));
			Assert.AreEqual(expected, material);
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
			Assert.IsFalse(KingdomMaterialRules.TryParseMaterial(key, out _), key ?? "null");
		}

		// --- Material costs out of third-party XML ---------------------------------------------

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void TryParseMaterialCost_AbsentIsAnEmptyCostAndNotAnError(string text)
		{
			Assert.IsTrue(KingdomMaterialRules.TryParseMaterialCost(text, out var cost, out var error));
			Assert.IsNull(error);
			Assert.IsTrue(cost.IsEmpty());
			// The whole compatibility guarantee: a design with no Materials attribute is covered
			// by an empty stockpile, forever.
			Assert.IsTrue(KingdomMaterialRules.Covers(new KingdomMaterialTally(), cost));
		}

		[Test]
		public void TryParseMaterialCost_ReadsSeveralTerms()
		{
			Assert.IsTrue(KingdomMaterialRules.TryParseMaterialCost("timber:8, stone:4 ,marble:1", out var cost, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(8, cost.Get(KingdomMaterial.Timber));
			Assert.AreEqual(4, cost.Get(KingdomMaterial.Stone));
			Assert.AreEqual(1, cost.Get(KingdomMaterial.Marble));
			Assert.AreEqual(0, cost.Get(KingdomMaterial.Scrap));
			Assert.AreEqual(13, cost.Total());
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
			Assert.IsFalse(KingdomMaterialRules.TryParseMaterialCost(text, out var cost, out var error), text);
			Assert.IsNotNull(error, text);
			// Nothing is half-parsed: a bad attribute leaves an empty cost rather than the terms
			// that happened to come before the bad one.
			Assert.IsTrue(cost.IsEmpty(), text);
		}

		// --- The tally ------------------------------------------------------------------------

		[Test]
		public void Tally_ClampsAtZeroRatherThanGoingNegative()
		{
			KingdomMaterialTally tally = new KingdomMaterialTally();
			tally.Add(KingdomMaterial.Timber, 3);
			tally.Add(KingdomMaterial.Timber, -10);
			Assert.AreEqual(0, tally.Get(KingdomMaterial.Timber));
			tally.Set(KingdomMaterial.Stone, -5);
			Assert.AreEqual(0, tally.Get(KingdomMaterial.Stone));
			Assert.IsTrue(tally.IsEmpty());
		}

		[Test]
		public void Tally_CopyIsIndependent()
		{
			KingdomMaterialTally tally = Tally(0, 0, 6);
			KingdomMaterialTally copy = tally.Copy();
			copy.Add(KingdomMaterial.Timber, 10);
			Assert.AreEqual(6, tally.Get(KingdomMaterial.Timber));
			Assert.AreEqual(16, copy.Get(KingdomMaterial.Timber));
		}

		[Test]
		public void Tally_AddAllSumsAndToleratesNull()
		{
			KingdomMaterialTally tally = Tally(0, 0, 2, 3);
			tally.AddAll(Tally(0, 0, 5));
			tally.AddAll(null);
			Assert.AreEqual(7, tally.Get(KingdomMaterial.Timber));
			Assert.AreEqual(3, tally.Get(KingdomMaterial.Stone));
		}

		[TestCase(100, 8)]
		[TestCase(50, 4)]
		[TestCase(49, 3)]
		[TestCase(0, 0)]
		[TestCase(-10, 0)]
		public void Tally_ScaledRoundsDown(int percent, int expected)
		{
			Assert.AreEqual(expected, Tally(0, 0, 8).Scaled(percent).Get(KingdomMaterial.Timber));
		}

		[Test]
		public void Tally_DescribeReadsAsProseAndIsNullWhenEmpty()
		{
			Assert.IsNull(new KingdomMaterialTally().Describe());
			Assert.AreEqual("6 timber", Tally(0, 0, 6).Describe());
			Assert.AreEqual("6 timber and 2 cut stone", Tally(0, 0, 6, 2).Describe());
			Assert.AreEqual("1 mud, 6 timber and 2 cut stone", Tally(1, 0, 6, 2).Describe());
		}

		[Test]
		public void Covers_IsPerMaterialAndNotATotal()
		{
			// A stockpile full of stone does not pay for timber, however much of it there is.
			Assert.IsFalse(KingdomMaterialRules.Covers(Tally(0, 0, 0, 100), Tally(0, 0, 4)));
			Assert.IsTrue(KingdomMaterialRules.Covers(Tally(0, 0, 4), Tally(0, 0, 4)));
			Assert.IsFalse(KingdomMaterialRules.Covers(Tally(0, 0, 3), Tally(0, 0, 4)));
			Assert.IsTrue(KingdomMaterialRules.Covers(null, new KingdomMaterialTally()));
			Assert.IsTrue(KingdomMaterialRules.Covers(Tally(0, 0, 1), null));
			Assert.IsFalse(KingdomMaterialRules.Covers(null, Tally(0, 0, 1)));
		}

		[Test]
		public void Missing_NamesOnlyTheShortfall()
		{
			KingdomMaterialTally missing = KingdomMaterialRules.Missing(Tally(0, 0, 3, 9), Tally(0, 0, 8, 4));
			Assert.AreEqual(5, missing.Get(KingdomMaterial.Timber));
			Assert.AreEqual(0, missing.Get(KingdomMaterial.Stone));
			Assert.IsTrue(KingdomMaterialRules.Missing(Tally(0, 0, 8), Tally(0, 0, 8)).IsEmpty());
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
			Assert.AreEqual(expected, KingdomMaterialRules.HardnessPercent(hitpoints));
		}

		[Test]
		public void HardnessPercent_NeverFallsAsAThingGetsHarder()
		{
			int previous = KingdomMaterialRules.HardnessPercent(-100);
			Assert.Greater(previous, 0);
			for (int hitpoints = 0; hitpoints <= 30000; hitpoints += 7)
			{
				int percent = KingdomMaterialRules.HardnessPercent(hitpoints);
				Assert.GreaterOrEqual(percent, previous, "hitpoints=" + hitpoints);
				previous = percent;
			}
		}

		[Test]
		public void ClearanceEffort_BareGroundIsFixedWhateverHardnessIsPassed()
		{
			// Nothing stands on bare ground, so nothing about it can be hard. A mutation that let
			// hardness through here would make an empty field cost as much as a granite ridge.
			int flat = KingdomMaterialRules.ClearanceEffort(KingdomStanding.Nothing, 0);
			Assert.Greater(flat, 0);
			Assert.AreEqual(flat, KingdomMaterialRules.ClearanceEffort(KingdomStanding.Nothing, 26000));
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
					Assert.GreaterOrEqual(effort, 1, standing + "@" + hitpoints);
					Assert.GreaterOrEqual(effort, previous, standing + "@" + hitpoints);
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
				Assert.GreaterOrEqual(effort, previous, ((KingdomStanding)kind).ToString());
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
			Assert.AreEqual(expected, KingdomMaterialRules.YieldMaterial(standing));
		}

		[Test]
		public void YieldUnits_BareGroundYieldsNothingAndEverythingElseYieldsSomething()
		{
			// Bare ground's mud is counted against the rect by GroundMud, not per empty cell; a
			// mutation that gave it a per-cell yield would mint a hill of mud out of a field.
			Assert.AreEqual(0, KingdomMaterialRules.YieldUnits(KingdomStanding.Nothing));
			for (int kind = 1; kind < KingdomMaterialRules.StandingCount; kind++)
			{
				Assert.Greater(KingdomMaterialRules.YieldUnits((KingdomStanding)kind), 0, ((KingdomStanding)kind).ToString());
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
			Assert.AreEqual(expected, KingdomMaterialRules.GroundMud(cells));
		}

		[TestCase(0, 0, 0)]
		[TestCase(5, 0, 5)]
		[TestCase(5, 2, 3)]
		[TestCase(5, 5, 0)]
		[TestCase(5, 9, 0)]
		[TestCase(-3, 0, 0)]
		public void FreeHands_AreWhoeverTheWaterAndTheWorksLeftOver(int population, int assigned, int expected)
		{
			Assert.AreEqual(expected, KingdomMaterialRules.FreeHands(population, assigned));
		}

		[TestCase(0, 3, 0)]
		[TestCase(-2, 3, 0)]
		[TestCase(3, 0, 0)]
		[TestCase(3, -1, 0)]
		[TestCase(1, 1, KingdomMaterialRules.EffortPerHandPerDay)]
		[TestCase(3, 2, 60)]
		public void EffortWorked_IsHandsTimesDays(int hands, int days, int expected)
		{
			Assert.AreEqual(expected, KingdomMaterialRules.EffortWorked(hands, days));
		}

		[Test]
		public void EffortWorked_IsCappedSoOneVisitNeverClearsEverything()
		{
			int capped = KingdomMaterialRules.EffortWorked(KingdomMaterialRules.MaxClearingHands, 1);
			Assert.AreEqual(capped, KingdomMaterialRules.EffortWorked(KingdomMaterialRules.MaxClearingHands + 1, 1));
			Assert.AreEqual(capped, KingdomMaterialRules.EffortWorked(1000, 1));
			Assert.Greater(capped, KingdomMaterialRules.EffortWorked(KingdomMaterialRules.MaxClearingHands - 1, 1));
		}

		[TestCase(0, 0)]
		[TestCase(-5, 0)]
		[TestCase(1, 1)]
		[TestCase(KingdomMaterialRules.EffortPerHandPerDay, 1)]
		[TestCase(KingdomMaterialRules.EffortPerHandPerDay + 1, 2)]
		[TestCase(KingdomMaterialRules.EffortPerHandPerDay * 4, 4)]
		public void DaysForOneHand_RoundsUpSoNoJobTakesNoTime(int effort, int expected)
		{
			Assert.AreEqual(expected, KingdomMaterialRules.DaysForOneHand(effort));
		}

		[Test]
		public void DaysForOneHand_AgreesWithWhatOneHandActuallyWorksOff()
		{
			// The founder is quoted a number of days; a mutation that made the quote and the work
			// disagree would have a job finish early or late against its own promise.
			for (int effort = 1; effort <= 400; effort++)
			{
				int days = KingdomMaterialRules.DaysForOneHand(effort);
				Assert.GreaterOrEqual(KingdomMaterialRules.EffortWorked(1, days), effort, "effort=" + effort);
				if (days > 1)
				{
					Assert.Less(KingdomMaterialRules.EffortWorked(1, days - 1), effort, "effort=" + effort);
				}
			}
		}

		[Test]
		public void ClearanceConstants_AreAllPositive()
		{
			// A zeroed constant here would make clearing free, instant, or infinitely productive.
			Assert.Greater(KingdomMaterialRules.EffortPerHandPerDay, 0);
			Assert.Greater(KingdomMaterialRules.MaxClearingHands, 0);
			Assert.Greater(KingdomMaterialRules.MudPerCells, 0);
			for (int kind = 0; kind < KingdomMaterialRules.StandingCount; kind++)
			{
				Assert.Greater(KingdomMaterialRules.StandingEffort[kind], 0, "effort " + kind);
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
			Assert.AreEqual(expected, KingdomMaterialRules.StrikeEffort(units, drams));
		}

		[Test]
		public void StrikeEffort_NeverFallsBelowTheBaseAndNeverDecreases()
		{
			int previous = KingdomMaterialRules.StrikeEffort(0, 0);
			Assert.AreEqual(KingdomMaterialRules.StrikeBaseEffort, previous);
			for (int units = 0; units <= 60; units++)
			{
				int effort = KingdomMaterialRules.StrikeEffort(units, units * 3);
				Assert.GreaterOrEqual(effort, KingdomMaterialRules.StrikeBaseEffort, "units=" + units);
				Assert.GreaterOrEqual(effort, previous, "units=" + units);
				previous = effort;
			}
		}

		[Test]
		public void StrikeSalvage_ReturnsPartAndNeverAll()
		{
			Assert.Less(KingdomMaterialRules.StrikeSalvagePercent, 100);
			Assert.Greater(KingdomMaterialRules.StrikeSalvagePercent, 0);
			KingdomMaterialTally cost = Tally(0, 0, 8, 5, 1);
			KingdomMaterialTally salvage = KingdomMaterialRules.StrikeSalvage(cost);
			Assert.AreEqual(4, salvage.Get(KingdomMaterial.Timber));
			Assert.AreEqual(2, salvage.Get(KingdomMaterial.Stone));
			Assert.AreEqual(0, salvage.Get(KingdomMaterial.Marble));
			Assert.Less(salvage.Total(), cost.Total());
		}

		[Test]
		public void StrikeSalvage_OfAWaterOnlyBuildingIsNothing()
		{
			Assert.IsTrue(KingdomMaterialRules.StrikeSalvage(new KingdomMaterialTally()).IsEmpty());
			Assert.IsTrue(KingdomMaterialRules.StrikeSalvage(null).IsEmpty());
		}

		[Test]
		public void StrikeSalvage_DoesNotMutateTheCostItReads()
		{
			KingdomMaterialTally cost = Tally(0, 0, 8);
			KingdomMaterialRules.StrikeSalvage(cost);
			Assert.AreEqual(8, cost.Get(KingdomMaterial.Timber));
		}

		// --- Wall material --------------------------------------------------------------------

		[Test]
		public void WallMaterial_MudIsTheFloorNothingFallsThrough()
		{
			Assert.AreEqual(0, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Mud]);
			Assert.AreEqual(KingdomMaterial.Mud, KingdomMaterialRules.WallMaterialFor(new KingdomMaterialTally(), null));
			Assert.AreEqual(KingdomMaterial.Mud, KingdomMaterialRules.WallMaterialFor(null, "gyre"));
			Assert.IsTrue(KingdomMaterialRules.HasWallMaterial(new KingdomMaterialTally(), KingdomMaterial.Mud));
		}

		[Test]
		public void WallMaterial_EveryThresholdAboveMudIsARealBar()
		{
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				if ((KingdomMaterial)i != KingdomMaterial.Mud)
				{
					Assert.Greater(KingdomMaterialRules.WallMaterialThreshold[i], 0, KingdomMaterialRules.MaterialKeys[i]);
				}
			}
		}

		[Test]
		public void WallMaterial_TakesTheRichestItHasQuarriedEnoughOf()
		{
			KingdomMaterialTally rich = new KingdomMaterialTally();
			rich.Set(KingdomMaterial.Marble, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Marble]);
			rich.Set(KingdomMaterial.Stone, 100);
			Assert.AreEqual(KingdomMaterial.Marble, KingdomMaterialRules.WallMaterialFor(rich, null));

			KingdomMaterialTally oneShort = new KingdomMaterialTally();
			oneShort.Set(KingdomMaterial.Marble, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Marble] - 1);
			oneShort.Set(KingdomMaterial.Stone, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Stone]);
			Assert.AreEqual(KingdomMaterial.Stone, KingdomMaterialRules.WallMaterialFor(oneShort, null));
		}

		[Test]
		public void WallMaterial_StyleTasteWinsWhenTheSettlementCanAffordIt()
		{
			KingdomMaterialTally stock = new KingdomMaterialTally();
			stock.Set(KingdomMaterial.Stone, 100);
			stock.Set(KingdomMaterial.Timber, KingdomMaterialRules.WallMaterialThreshold[(int)KingdomMaterial.Timber]);
			Assert.AreEqual(KingdomMaterial.Timber, KingdomMaterialRules.WallMaterialFor(stock, "verdant"));
			// The same stock, no taste: stone outranks timber.
			Assert.AreEqual(KingdomMaterial.Stone, KingdomMaterialRules.WallMaterialFor(stock, "common"));
		}

		[Test]
		public void WallMaterial_UnmetTasteCostsNothingAndChangesNothing()
		{
			KingdomMaterialTally stock = new KingdomMaterialTally();
			stock.Set(KingdomMaterial.Stone, 100);
			Assert.AreEqual(KingdomMaterial.Stone, KingdomMaterialRules.WallMaterialFor(stock, "verdant"));
		}

		[TestCase("verdant", KingdomMaterial.Timber)]
		[TestCase("fungal", KingdomMaterial.Timber)]
		[TestCase("gyre", KingdomMaterial.Marble)]
		[TestCase("eater", KingdomMaterial.Scrap)]
		public void TryStylePreference_KnowsTheStylesThatHaveATaste(string style, KingdomMaterial expected)
		{
			Assert.IsTrue(KingdomMaterialRules.TryStylePreference(style, out var material), style);
			Assert.AreEqual(expected, material);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("common")]
		[TestCase("nonesuch")]
		public void TryStylePreference_LeavesTheRestToTheirStock(string style)
		{
			Assert.IsFalse(KingdomMaterialRules.TryStylePreference(style, out _), style ?? "null");
		}

		[Test]
		public void WallMaterialPreference_ListsEveryMaterialExactlyOnce()
		{
			bool[] seen = new bool[KingdomMaterialRules.MaterialCount];
			for (int i = 0; i < KingdomMaterialRules.WallMaterialPreference.Length; i++)
			{
				int index = (int)KingdomMaterialRules.WallMaterialPreference[i];
				Assert.IsFalse(seen[index], "listed twice: " + KingdomMaterialRules.MaterialKeys[index]);
				seen[index] = true;
			}
			for (int i = 0; i < seen.Length; i++)
			{
				Assert.IsTrue(seen[i], "never listed: " + KingdomMaterialRules.MaterialKeys[i]);
			}
			// Mud last: it is the answer only when nothing else is.
			Assert.AreEqual(KingdomMaterial.Mud, KingdomMaterialRules.WallMaterialPreference[KingdomMaterialRules.MaterialCount - 1]);
		}
	}
}
#endif
