#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomInheritRulesTests
	{
		private static KingdomRules.InheritedState State(int Rank)
		{
			return (KingdomRules.InheritedState)Rank;
		}

		private static void AssertDeclaredFields(Type Type, params string[] Expected)
		{
			FieldInfo[] fields = Type.GetFields(BindingFlags.Instance | BindingFlags.Public
				| BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			string[] actual = new string[fields.Length];
			for (int i = 0; i < fields.Length; i++) actual[i] = fields[i].Name;
			CollectionAssert.AreEqual(Expected, actual, Type.FullName);
		}

		private static KingdomInheritPlan Normalize(string[] Keys, int[] X, int[] Y, int[] Conditions)
		{
			KingdomInheritPlan plan;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryNormalize(Keys, X, Y, Conditions, out plan, out fault), fault.ToString());
			Assert.AreEqual(KingdomInheritFault.None, fault);
			return plan;
		}

		private static KingdomInheritPlan Apply(KingdomInheritPlan Source, KingdomRules.InheritedState State,
			int InterregnumRoll = 50)
		{
			KingdomInheritPlan plan;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryApplyState(Source, State, InterregnumRoll, out plan, out fault), fault.ToString());
			Assert.AreEqual(KingdomInheritFault.None, fault);
			return plan;
		}

		private static string PlanBytes(KingdomInheritPlan Plan)
		{
			string text = Plan.Width + "x" + Plan.Height + ":";
			for (int i = 0; i < Plan.Count; i++)
			{
				KingdomInheritWork work = Plan.WorkAt(i);
				text += work.Key + "@" + work.X + "," + work.Y + "," + work.Condition + "," + (int)work.State + ";";
			}
			return text;
		}

		private static string PlacementBytes(KingdomInheritPlacement Placement)
		{
			string text = Placement.EntryX + "," + Placement.EntryY + "/"
				+ Placement.CairnX + "," + Placement.CairnY + "/"
				+ Placement.HeartX + "," + Placement.HeartY + "/"
				+ (int)Placement.RemainingEngineChecks + ":";
			for (int i = 0; i < Placement.Count; i++)
			{
				KingdomInheritWork work = Placement.WorkAt(i);
				text += work.Key + "@" + work.X + "," + work.Y + "," + work.Condition + "," + (int)work.State + ";";
			}
			return text;
		}

		[TestCase("tent", "r_KingdomTent", 3, 2)]
		[TestCase("heartcourt", "r_KingdomGreatCourt", 16, 11)]
		[TestCase("palisade", "r_KingdomPalisade", 1, 1)]
		[TestCase("chimerictheatre", "r_KingdomChimericTheatre", 20, 14)]
		[TestCase("registryoffice", "r_KingdomRegistryOffice", 8, 6)]
		[TestCase("inherit.rubble", "r_KingdomRubbleWall", 1, 1)]
		[TestCase("inherit.memory", "r_KingdomCairn", 1, 1)]
		[TestCase("inherit.cairn", "r_KingdomCairn", 1, 1)]
		public void ExplicitAllowlistResolvesOnlyNamedTafContent(string Key, string Blueprint, int Width, int Height)
		{
			string actual;
			int width;
			int height;
			Assert.IsTrue(KingdomInheritRules.TryResolveBlueprint(Key, out actual));
			Assert.AreEqual(Blueprint, actual);
			Assert.IsTrue(KingdomInheritRules.TryFootprint(Key, out width, out height));
			Assert.AreEqual(Width, width);
			Assert.AreEqual(Height, height);
		}

		[TestCase("r_KingdomTent", "tent")]
		[TestCase("r_KingdomGreatCourt", "heartcourt")]
		[TestCase("r_KingdomCairn", "cairn")]
		[TestCase("r_KingdomRubbleWall", "rubblewall")]
		[TestCase("r_KingdomHutYard", "hutyard")]
		[TestCase("r_KingdomSmithy", "smithy")]
		public void LiveBlueprintMapsToCanonicalBaseSemanticKey(string Blueprint, string Expected)
		{
			string key;
			Assert.IsTrue(KingdomInheritRules.TrySemanticKeyForBlueprint(Blueprint, out key));
			Assert.AreEqual(Expected, key);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("Campfire")]
		[TestCase("r_KingdomNotAllowlisted")]
		public void UnknownBlueprintNeverBecomesASealSemanticKey(string Blueprint)
		{
			string key;
			Assert.IsFalse(KingdomInheritRules.TrySemanticKeyForBlueprint(Blueprint, out key));
			Assert.IsNull(key);
		}

		[Test]
		public void EveryAllowlistRowIsStableUniqueBoundedAndTafOwned()
		{
			FieldInfo field = typeof(KingdomInheritRules).GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic);
			Array definitions = (Array)field.GetValue(null);
			Assert.AreEqual(107, definitions.Length, "104 current TAF catalogue designs plus three bounded inheritance markers");
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < definitions.Length; i++)
			{
				object definition = definitions.GetValue(i);
				Type type = definition.GetType();
				string key = (string)type.GetField("Key", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(definition);
				string blueprint = (string)type.GetField("Blueprint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(definition);
				int width = (int)type.GetField("Width", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(definition);
				int height = (int)type.GetField("Height", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(definition);
				Assert.IsTrue(keys.Add(key), "duplicate semantic key " + key);
				Assert.IsTrue(KingdomInheritRules.IsStableSemanticKey(key), key);
				StringAssert.StartsWith("r_Kingdom", blueprint, key);
				Assert.Greater(width, 0, key);
				Assert.Greater(height, 0, key);
				Assert.LessOrEqual(width, 20, key);
				Assert.LessOrEqual(height, 14, key);
				string resolved;
				Assert.IsTrue(KingdomInheritRules.TryResolveBlueprint(key, out resolved), key);
				Assert.AreEqual(blueprint, resolved, key);
			}
		}

		[TestCase("r_KingdomTent")]
		[TestCase("Campfire")]
		[TestCase("Bookshelf")]
		[TestCase("Torch")]
		[TestCase("../../ObjectBlueprints.xml")]
		[TestCase("System.String")]
		[TestCase("")]
		[TestCase(null)]
		public void ArbitraryBlueprintAndClrAndPathValuesNeverResolve(string Key)
		{
			string blueprint;
			Assert.IsFalse(KingdomInheritRules.TryResolveBlueprint(Key, out blueprint));
			Assert.IsNull(blueprint);
		}

		[TestCase("tent", true)]
		[TestCase("removed_optional_work", true)]
		[TestCase("a.b-c_9", true)]
		[TestCase("r_KingdomTent", false)]
		[TestCase("two words", false)]
		[TestCase("../path", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		public void StableTokenGrammarIsNarrow(string Key, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomInheritRules.IsStableSemanticKey(Key));
		}

		[TestCase("removed_optional_work")]
		[TestCase("fire")]
		[TestCase("bookshelf")]
		[TestCase("campfire")]
		public void UnknownOrNonTafContentDegradesLocallyToMemory(string Key)
		{
			KingdomInheritPlan plan = Normalize(new[] { Key, "palisade" }, new[] { 10, 20 }, new[] { 10, 10 }, new[] { 80, 90 });
			Assert.AreEqual(2, plan.Count);
			Assert.AreEqual(KingdomInheritRules.MemoryKey, plan.WorkAt(0).Key);
			Assert.AreEqual(0, plan.WorkAt(0).Condition);
			Assert.AreEqual(KingdomInheritWorkState.Memory, plan.WorkAt(0).State);
			string blueprint;
			Assert.IsTrue(KingdomInheritRules.TryResolveBlueprint(plan.WorkAt(0).Key, out blueprint));
			Assert.AreEqual("r_KingdomCairn", blueprint);
			Assert.AreEqual("palisade", plan.WorkAt(1).Key);
		}

		[Test]
		public void NormalizationIsTranslationInvariant()
		{
			string[] keys = new[] { "palisade", "rampart", "heartbasin" };
			KingdomInheritPlan near = Normalize(keys, new[] { 4, 12, 20 }, new[] { 3, 4, 9 }, new[] { 80, 70, 60 });
			KingdomInheritPlan far = Normalize(keys, new[] { 500004, 500012, 500020 }, new[] { -499997, -499996, -499991 }, new[] { 80, 70, 60 });
			Assert.AreEqual(PlanBytes(near), PlanBytes(far));
		}

		[Test]
		public void InputOrderCannotChangeCanonicalPlan()
		{
			KingdomInheritPlan forward = Normalize(
				new[] { "palisade", "rampart", "heartbasin" }, new[] { 0, 10, 20 }, new[] { 0, 2, 4 }, new[] { 30, 40, 50 });
			KingdomInheritPlan reverse = Normalize(
				new[] { "heartbasin", "rampart", "palisade" }, new[] { 20, 10, 0 }, new[] { 4, 2, 0 }, new[] { 50, 40, 30 });
			Assert.AreEqual(PlanBytes(forward), PlanBytes(reverse));
		}

		[Test]
		public void ExactCellDuplicatesCollapseDeterministically()
		{
			KingdomInheritPlan first = Normalize(new[] { "rampart", "palisade", "palisade" },
				new[] { 10, 10, 10 }, new[] { 10, 10, 10 }, new[] { 90, 80, 20 });
			KingdomInheritPlan second = Normalize(new[] { "palisade", "rampart", "palisade" },
				new[] { 10, 10, 10 }, new[] { 10, 10, 10 }, new[] { 20, 90, 80 });
			Assert.AreEqual(1, first.Count);
			Assert.AreEqual("palisade", first.WorkAt(0).Key, "ordinal key chooses exact-cell survivor");
			Assert.AreEqual(20, first.WorkAt(0).Condition, "lower condition fails closed when duplicate copies disagree");
			Assert.AreEqual(PlanBytes(first), PlanBytes(second));
		}

		[Test]
		public void MoreThanCapIsRefusedBeforeFilteringUnknownRows()
		{
			string[] keys = new string[KingdomInheritRules.MaxWorks + 1];
			int[] x = new int[keys.Length];
			int[] y = new int[keys.Length];
			int[] condition = new int[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				keys[i] = "optional" + i;
				x[i] = i;
			}
			KingdomInheritPlan plan;
			KingdomInheritFault fault;
			Assert.IsFalse(KingdomInheritRules.TryNormalize(keys, x, y, condition, out plan, out fault));
			Assert.AreEqual(KingdomInheritFault.TooManyWorks, fault);
		}

		[Test]
		public void NullAndTornParallelRowsFailWithoutThrowing()
		{
			KingdomInheritPlan plan;
			KingdomInheritFault fault;
			Assert.IsFalse(KingdomInheritRules.TryNormalize(null, null, null, null, out plan, out fault));
			Assert.AreEqual(KingdomInheritFault.NullInput, fault);
			Assert.IsFalse(KingdomInheritRules.TryNormalize(new[] { "tent" }, new int[0], new[] { 0 }, new[] { 50 }, out plan, out fault));
			Assert.AreEqual(KingdomInheritFault.RowCountMismatch, fault);
		}

		[TestCase("r_KingdomTent", 0, 0, 50, (int)KingdomInheritFault.InvalidKey)]
		[TestCase("tent", 0, 0, -1, (int)KingdomInheritFault.ConditionOutOfRange)]
		[TestCase("tent", 0, 0, 101, (int)KingdomInheritFault.ConditionOutOfRange)]
		[TestCase("tent", 1000001, 0, 50, (int)KingdomInheritFault.CoordinateOutOfRange)]
		[TestCase("tent", 0, -1000001, 50, (int)KingdomInheritFault.CoordinateOutOfRange)]
		public void HostileRowsHaveExactFaults(string Key, int X, int Y, int Condition, int Expected)
		{
			KingdomInheritPlan plan;
			KingdomInheritFault fault;
			Assert.IsFalse(KingdomInheritRules.TryNormalize(new[] { Key }, new[] { X }, new[] { Y }, new[] { Condition }, out plan, out fault));
			Assert.AreEqual((KingdomInheritFault)Expected, fault);
		}

		[Test]
		public void IntegerExtremesNeverOverflowOrThrow()
		{
			int[] values = new[] { int.MinValue, int.MaxValue };
			for (int i = 0; i < values.Length; i++)
			{
				KingdomInheritPlan plan;
				KingdomInheritFault fault;
				Assert.IsFalse(KingdomInheritRules.TryNormalize(new[] { "tent" }, new[] { values[i] }, new[] { values[1 - i] }, new[] { 50 }, out plan, out fault));
				Assert.AreEqual(KingdomInheritFault.CoordinateOutOfRange, fault);
			}
		}

		[Test]
		public void ExcessiveRelativeSpanIsRefusedAfterSafeLongArithmetic()
		{
			KingdomInheritPlan plan;
			KingdomInheritFault fault;
			Assert.IsFalse(KingdomInheritRules.TryNormalize(new[] { "palisade", "palisade" },
				new[] { -1000000, 1000000 }, new[] { 0, 0 }, new[] { 50, 50 }, out plan, out fault));
			Assert.AreEqual(KingdomInheritFault.RelativeRange, fault);
		}

		[Test]
		public void UnprovenOverlappingFootprintsDegradeLocallyAtExactDistinctAnchors()
		{
			KingdomInheritPlan plan;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryNormalize(new[] { "house", "house" },
				new[] { 10, 11 }, new[] { 10, 10 }, new[] { 100, 100 }, out plan, out fault),
				fault.ToString());
			Assert.AreEqual(2, plan.Count);
			Assert.AreEqual(KingdomInheritRules.MemoryKey, plan.WorkAt(0).Key);
			Assert.AreEqual(KingdomInheritRules.MemoryKey, plan.WorkAt(1).Key);
			Assert.AreEqual(0, plan.WorkAt(0).X);
			Assert.AreEqual(1, plan.WorkAt(1).X);
		}

		[Test]
		public void NormalizedEnvelopeIncludesWholeAsymmetricFootprint()
		{
			KingdomInheritPlan plan = Normalize(new[] { "house" }, new[] { 17 }, new[] { 9 }, new[] { 70 });
			Assert.AreEqual(8, plan.Width);
			Assert.AreEqual(6, plan.Height);
			Assert.AreEqual(3, plan.WorkAt(0).X);
			Assert.AreEqual(2, plan.WorkAt(0).Y);
		}

		[Test]
		public void FitIsDeterministicCentredAndInsideSafeMargins()
		{
			KingdomInheritPlan plan = Normalize(new[] { "heartbasin", "palisade", "palisade" },
				new[] { 20, 10, 30 }, new[] { 10, 4, 16 }, new[] { 100, 80, 60 });
			KingdomInheritPlacement first;
			KingdomInheritPlacement second;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryFit(plan, 80, 25, out first, out fault), fault.ToString());
			Assert.IsTrue(KingdomInheritRules.TryFit(plan, 80, 25, out second, out fault), fault.ToString());
			Assert.AreEqual(PlacementBytes(first), PlacementBytes(second));
			for (int i = 0; i < first.Count; i++)
			{
				KingdomInheritWork work = first.WorkAt(i);
				Assert.GreaterOrEqual(work.X, KingdomInheritRules.SafeMargin);
				Assert.Less(work.X, KingdomInheritRules.TargetWidth - KingdomInheritRules.SafeMargin);
				Assert.GreaterOrEqual(work.Y, KingdomInheritRules.SafeMargin);
				Assert.Less(work.Y, KingdomInheritRules.TargetHeight - KingdomInheritRules.SafeMargin);
			}
		}

		[Test]
		public void FullMarginTwoSourceEnvelopeFitsWithSafeEntryAndCairn()
		{
			KingdomInheritPlan plan = Normalize(new[] { "palisade", "palisade" },
				new[] { 0, 75 }, new[] { 0, 20 }, new[] { 100, 100 });
			Assert.AreEqual(76, plan.Width);
			Assert.AreEqual(21, plan.Height);
			Assert.AreEqual(KingdomInheritRules.SafeMargin, KingdomInheritRules.WorkMargin);

			KingdomInheritPlacement placement;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryFit(plan, 80, 25, out placement, out fault),
				fault.ToString());
			Assert.AreEqual(3, placement.Count, "two works plus the unconditional founder cairn");
			Assert.AreEqual(2, placement.WorkAt(0).X);
			Assert.AreEqual(2, placement.WorkAt(0).Y);
			Assert.AreEqual(77, placement.WorkAt(1).X);
			Assert.AreEqual(22, placement.WorkAt(1).Y);
			Assert.AreNotEqual(placement.CairnX + "," + placement.CairnY,
				placement.WorkAt(0).X + "," + placement.WorkAt(0).Y);
			Assert.AreNotEqual(placement.CairnX + "," + placement.CairnY,
				placement.WorkAt(1).X + "," + placement.WorkAt(1).Y);
		}

		[Test]
		public void FitRejectsWrongZoneSizeAndImpossibleHeight()
		{
			KingdomInheritPlacement placement;
			KingdomInheritFault fault;
			KingdomInheritPlan one = Normalize(new[] { "palisade" }, new[] { 0 }, new[] { 0 }, new[] { 50 });
			Assert.IsFalse(KingdomInheritRules.TryFit(one, 81, 25, out placement, out fault));
			Assert.AreEqual(KingdomInheritFault.ImpossibleFootprint, fault);

			KingdomInheritPlan tall = Normalize(new[] { "palisade", "palisade" },
				new[] { 0, 0 }, new[] { 0, 21 }, new[] { 50, 50 });
			Assert.IsFalse(KingdomInheritRules.TryFit(tall, 80, 25, out placement, out fault));
			Assert.AreEqual(KingdomInheritFault.ImpossibleFootprint, fault);
		}

		[TestCase(KingdomRules.InheritedState.Held, (int)KingdomInheritWorkState.Standing, 80)]
		[TestCase(KingdomRules.InheritedState.Faded, (int)KingdomInheritWorkState.Derelict, 45)]
		[TestCase(KingdomRules.InheritedState.Abandoned, (int)KingdomInheritWorkState.Derelict, 35)]
		[TestCase(KingdomRules.InheritedState.Ruins, (int)KingdomInheritWorkState.Derelict, 20)]
		public void OwnershipLadderNamesAllFourAuthoritativeStates(KingdomRules.InheritedState State,
			int ExpectedState, int ExpectedCondition)
		{
			KingdomInheritPlan source = Normalize(new[] { "palisade" }, new[] { 10 }, new[] { 10 }, new[] { 100 });
			KingdomInheritPlan inherited = Apply(source, State, 50);
			Assert.AreEqual(1, inherited.Count);
			Assert.AreEqual((KingdomInheritWorkState)ExpectedState, inherited.WorkAt(0).State);
			Assert.AreEqual(ExpectedCondition, inherited.WorkAt(0).Condition);
		}

		[Test]
		public void FadedLeavesMostWorksStandingAndSomeDerelict()
		{
			string[] keys = new string[20];
			int[] x = new int[20];
			int[] y = new int[20];
			int[] condition = new int[20];
			for (int i = 0; i < keys.Length; i++)
			{
				keys[i] = "palisade";
				x[i] = i * 2;
				condition[i] = 100;
			}
			KingdomInheritPlan faded = Apply(Normalize(keys, x, y, condition), KingdomRules.InheritedState.Faded, 73);
			int sound = 0;
			int derelict = 0;
			for (int i = 0; i < faded.Count; i++)
			{
				KingdomInheritWork work = faded.WorkAt(i);
				Assert.AreEqual("palisade", work.Key);
				if (work.State == KingdomInheritWorkState.Standing)
				{
					sound++;
					Assert.LessOrEqual(work.Condition, KingdomInheritRules.FadedStandingConditionCeiling);
				}
				else
				{
					derelict++;
					Assert.AreEqual(KingdomInheritWorkState.Derelict, work.State);
					Assert.LessOrEqual(work.Condition, KingdomInheritRules.FadedDerelictConditionCeiling);
				}
			}
			Assert.AreEqual(5, derelict);
			Assert.AreEqual(15, sound);
		}

		[Test]
		public void AbandonedKeepsEveryWorkIntactButDerelict()
		{
			string[] keys = new string[40];
			int[] x = new int[40];
			int[] y = new int[40];
			int[] condition = new int[40];
			for (int i = 0; i < keys.Length; i++)
			{
				keys[i] = "palisade";
				x[i] = i * 2;
				condition[i] = 100;
			}
			KingdomInheritPlan source = Normalize(keys, x, y, condition);
			KingdomInheritPlan abandoned = Apply(source, KingdomRules.InheritedState.Abandoned, 99);
			Assert.IsTrue(KingdomRules.AllWorksSurvive(KingdomRules.InheritedState.Abandoned));
			Assert.AreEqual(source.Count, abandoned.Count);
			for (int i = 0; i < abandoned.Count; i++)
			{
				Assert.AreEqual("palisade", abandoned.WorkAt(i).Key);
				Assert.AreEqual(KingdomInheritWorkState.Derelict, abandoned.WorkAt(i).State);
				Assert.LessOrEqual(abandoned.WorkAt(i).Condition, KingdomInheritRules.AbandonedDerelictConditionCeiling);
			}
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(49)]
		[TestCase(50)]
		[TestCase(98)]
		[TestCase(99)]
		public void RuinsStandingCountMatchesAuthoritativePercentage(int InterregnumRoll)
		{
			string[] keys = new string[40];
			int[] x = new int[40];
			int[] y = new int[40];
			int[] condition = new int[40];
			for (int i = 0; i < keys.Length; i++)
			{
				keys[i] = "palisade";
				x[i] = i * 2;
				condition[i] = 100;
			}
			KingdomInheritPlan ruins = Apply(Normalize(keys, x, y, condition),
				KingdomRules.InheritedState.Ruins, InterregnumRoll);
			int expectedStanding = (keys.Length
				* KingdomRules.StandingPercent(KingdomRules.InheritedState.Ruins, InterregnumRoll) + 50) / 100;
			if (expectedStanding < 1) expectedStanding = 1;
			int standing = 0;
			int rubble = 0;
			for (int i = 0; i < ruins.Count; i++)
			{
				KingdomInheritWork work = ruins.WorkAt(i);
				if (work.State == KingdomInheritWorkState.Derelict)
				{
					standing++;
					Assert.AreEqual("palisade", work.Key);
					Assert.LessOrEqual(work.Condition, KingdomInheritRules.RuinsDerelictConditionCeiling);
				}
				else
				{
					rubble++;
					Assert.AreEqual(KingdomInheritWorkState.Rubble, work.State);
					Assert.AreEqual(KingdomInheritRules.RubbleKey, work.Key);
					Assert.AreEqual(0, work.Condition);
				}
			}
			Assert.AreEqual(expectedStanding, standing);
			Assert.AreEqual(keys.Length - expectedStanding, rubble);
			Assert.AreEqual(keys.Length, ruins.Count, "rubble stays in place so the street silhouette remains legible");
		}

		[Test]
		public void RuinsStandingCountMatchesAuthorityForEveryPlanSizeAndRoll()
		{
			for (int count = 1; count <= KingdomInheritRules.MaxWorks; count++)
			{
				string[] keys = new string[count];
				int[] x = new int[count];
				int[] y = new int[count];
				int[] condition = new int[count];
				for (int i = 0; i < count; i++)
				{
					keys[i] = "palisade";
					x[i] = i * 2;
					condition[i] = 100;
				}
				KingdomInheritPlan source = Normalize(keys, x, y, condition);
				for (int roll = 0; roll <= 99; roll++)
				{
					KingdomInheritPlan ruins = Apply(source, KingdomRules.InheritedState.Ruins, roll);
					int expected = (count * KingdomRules.StandingPercent(KingdomRules.InheritedState.Ruins, roll) + 50) / 100;
					if (expected < 1) expected = 1;
					int standing = 0;
					for (int i = 0; i < ruins.Count; i++)
					{
						if (ruins.WorkAt(i).State == KingdomInheritWorkState.Derelict) standing++;
					}
					Assert.AreEqual(expected, standing, count + " works, roll " + roll);
				}
			}
		}

		[Test]
		public void AllWorksSurviveParityMatchesKingdomRules()
		{
			KingdomInheritPlan source = Normalize(new[] { "palisade", "rampart", "watchtower" },
				new[] { 0, 4, 8 }, new[] { 0, 0, 0 }, new[] { 100, 100, 100 });
			KingdomRules.InheritedState[] states = new[]
			{
				KingdomRules.InheritedState.Held,
				KingdomRules.InheritedState.Faded,
				KingdomRules.InheritedState.Abandoned,
				KingdomRules.InheritedState.Ruins
			};
			for (int s = 0; s < states.Length; s++)
			{
				KingdomInheritPlan inherited = Apply(source, states[s], 99);
				bool anyRubble = false;
				for (int i = 0; i < inherited.Count; i++)
				{
					if (inherited.WorkAt(i).State == KingdomInheritWorkState.Rubble) anyRubble = true;
				}
				Assert.AreEqual(KingdomRules.AllWorksSurvive(states[s]), !anyRubble, states[s].ToString());
			}
		}

		[Test]
		public void WorseStateNeverImprovesAnyWork()
		{
			for (int condition = 0; condition <= 100; condition++)
			{
				KingdomInheritPlan source = Normalize(new[] { "palisade" }, new[] { 13 }, new[] { 7 }, new[] { condition });
				KingdomInheritPlan held = Apply(source, KingdomRules.InheritedState.Held);
				KingdomInheritPlan faded = Apply(source, KingdomRules.InheritedState.Faded);
				KingdomInheritPlan abandoned = Apply(source, KingdomRules.InheritedState.Abandoned);
				KingdomInheritPlan ruins = Apply(source, KingdomRules.InheritedState.Ruins);
				Assert.AreEqual(1, held.Count);
				Assert.AreEqual(1, faded.Count);
				Assert.AreEqual(1, abandoned.Count);
				Assert.AreEqual(1, ruins.Count);
				Assert.LessOrEqual(faded.WorkAt(0).Condition, held.WorkAt(0).Condition);
				Assert.LessOrEqual(abandoned.WorkAt(0).Condition, faded.WorkAt(0).Condition);
				Assert.LessOrEqual(ruins.WorkAt(0).Condition, abandoned.WorkAt(0).Condition);
				Assert.GreaterOrEqual((int)faded.WorkAt(0).State, (int)held.WorkAt(0).State);
				Assert.GreaterOrEqual((int)abandoned.WorkAt(0).State, (int)faded.WorkAt(0).State);
				Assert.GreaterOrEqual((int)ruins.WorkAt(0).State, (int)abandoned.WorkAt(0).State);
			}
		}

		[Test]
		public void MissingContentNeverBecomesARealWorkInAnyState()
		{
			KingdomInheritPlan source = Normalize(new[] { "optional_removed" }, new[] { 2 }, new[] { 3 }, new[] { 100 });
			KingdomRules.InheritedState[] states = new[]
			{
				KingdomRules.InheritedState.Held,
				KingdomRules.InheritedState.Faded,
				KingdomRules.InheritedState.Abandoned,
				KingdomRules.InheritedState.Ruins
			};
			for (int i = 0; i < states.Length; i++)
			{
				KingdomInheritPlan inherited = Apply(source, states[i]);
				Assert.AreEqual(1, inherited.Count);
				Assert.AreEqual(KingdomInheritRules.MemoryKey, inherited.WorkAt(0).Key);
				Assert.AreEqual(KingdomInheritWorkState.Memory, inherited.WorkAt(0).State);
				Assert.AreEqual(0, inherited.WorkAt(0).Condition);
			}
		}

		[Test]
		public void EmptyRuinsStillPlaceOneCairnChronicleLocusAndEntry()
		{
			KingdomInheritPlacement placement;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryPrepare(new string[0], new int[0], new int[0], new int[0],
				KingdomRules.InheritedState.Ruins, 99, out placement, out fault), fault.ToString());
			Assert.AreEqual(1, placement.Count);
			Assert.AreEqual(KingdomInheritRules.FounderCairnKey, placement.WorkAt(0).Key);
			Assert.AreEqual(KingdomInheritWorkState.Memory, placement.WorkAt(0).State);
			Assert.AreEqual(placement.CairnX, placement.HeartX);
			Assert.AreEqual(placement.CairnY, placement.HeartY);
			Assert.IsTrue(placement.EntryX == 0 || placement.EntryX == 79 || placement.EntryY == 0 || placement.EntryY == 24);
		}

		[Test]
		public void HighestHeartRungIsExposedAsGrammarHeart()
		{
			KingdomInheritPlacement placement;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryPrepare(new[] { "heartbasin", "heartmoot", "palisade" },
				new[] { 10, 35, 60 }, new[] { 10, 10, 10 }, new[] { 100, 100, 100 },
				KingdomRules.InheritedState.Held, 50, out placement, out fault), fault.ToString());
			KingdomInheritWork moot = null;
			for (int i = 0; i < placement.Count; i++)
			{
				if (placement.WorkAt(i).Key == "heartmoot") moot = placement.WorkAt(i);
			}
			Assert.IsNotNull(moot);
			Assert.AreEqual(moot.X, placement.HeartX);
			Assert.AreEqual(moot.Y, placement.HeartY);
		}

		[Test]
		public void RuinsAlwaysKeepHighestHeartRungRecognizable()
		{
			string[] keys = new[] { "heartcourt", "palisade", "palisade", "palisade", "palisade", "palisade", "palisade", "palisade" };
			int[] x = new[] { 10, 30, 34, 38, 42, 46, 50, 54 };
			int[] y = new[] { 10, 2, 2, 2, 2, 2, 2, 2 };
			int[] condition = new[] { 100, 100, 100, 100, 100, 100, 100, 100 };
			KingdomInheritPlan ruins = Apply(Normalize(keys, x, y, condition), KingdomRules.InheritedState.Ruins, 99);
			bool keptHeart = false;
			for (int i = 0; i < ruins.Count; i++)
			{
				if (ruins.WorkAt(i).Key == "heartcourt" && ruins.WorkAt(i).State == KingdomInheritWorkState.Derelict)
				{
					keptHeart = true;
				}
			}
			Assert.IsTrue(keptHeart);
		}

		[Test]
		public void GrammarNamesExactlyWhatEngineMustStillValidate()
		{
			KingdomInheritPlacement placement;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryPrepare(new[] { "palisade" }, new[] { 0 }, new[] { 0 }, new[] { 70 },
				KingdomRules.InheritedState.Held, 50, out placement, out fault));
			Assert.AreEqual(KingdomInheritRules.RemainingEngineChecks, placement.RemainingEngineChecks);
			Assert.IsTrue((placement.RemainingEngineChecks & KingdomInheritEngineCheck.Terrain) != 0);
			Assert.IsTrue((placement.RemainingEngineChecks & KingdomInheritEngineCheck.ExistingObjects) != 0);
			Assert.IsTrue((placement.RemainingEngineChecks & KingdomInheritEngineCheck.ConnectionCell) != 0);
			Assert.IsTrue((placement.RemainingEngineChecks & KingdomInheritEngineCheck.Stairs) != 0);
			Assert.IsTrue((placement.RemainingEngineChecks & KingdomInheritEngineCheck.EntryToHeartPath) != 0);
		}

		[Test]
		public void FullPrepareRetriesAreByteIdentical()
		{
			string[] keys = new[] { "heartbasin", "palisade", "rampart", "optional_removed" };
			int[] x = new[] { 20, 2, 36, 50 };
			int[] y = new[] { 10, 2, 18, 4 };
			int[] condition = new[] { 100, 90, 60, 40 };
			KingdomRules.InheritedState[] states = new[]
			{
				KingdomRules.InheritedState.Held,
				KingdomRules.InheritedState.Faded,
				KingdomRules.InheritedState.Abandoned,
				KingdomRules.InheritedState.Ruins
			};
			for (int state = 0; state < states.Length; state++)
			{
				KingdomInheritPlacement first;
				KingdomInheritPlacement second;
				KingdomInheritFault fault;
				Assert.IsTrue(KingdomInheritRules.TryPrepare(keys, x, y, condition, states[state], 73, out first, out fault), fault.ToString());
				Assert.IsTrue(KingdomInheritRules.TryPrepare(keys, x, y, condition, states[state], 73, out second, out fault), fault.ToString());
				Assert.AreEqual(PlacementBytes(first), PlacementBytes(second), states[state].ToString());
			}
		}

		[Test]
		public void InvalidEnumFailsClosedWithoutThrowing()
		{
			KingdomInheritPlan source = Normalize(new[] { "palisade" }, new[] { 0 }, new[] { 0 }, new[] { 50 });
			int[] invalid = new[] { -1, 4, int.MinValue, int.MaxValue };
			for (int i = 0; i < invalid.Length; i++)
			{
				KingdomInheritPlan plan;
				KingdomInheritFault fault;
				Assert.IsFalse(KingdomInheritRules.TryApplyState(source, State(invalid[i]), 50, out plan, out fault));
				Assert.AreEqual(KingdomInheritFault.InvalidState, fault);
			}
		}

		[TestCase(-1)]
		[TestCase(100)]
		[TestCase(int.MinValue)]
		[TestCase(int.MaxValue)]
		public void InvalidInterregnumRollFailsClosedWithoutThrowing(int InterregnumRoll)
		{
			KingdomInheritPlan source = Normalize(new[] { "palisade" }, new[] { 0 }, new[] { 0 }, new[] { 50 });
			KingdomInheritPlan plan;
			KingdomInheritFault fault;
			Assert.IsFalse(KingdomInheritRules.TryApplyState(source, KingdomRules.InheritedState.Held,
				InterregnumRoll, out plan, out fault));
			Assert.AreEqual(KingdomInheritFault.InterregnumRollOutOfRange, fault);
		}

		[Test]
		public void PersistedEnumsAndDtoMetadataRemainExactAcrossSourceFamilies()
		{
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomInheritWorkState)));
			Assert.AreEqual(0, (int)KingdomInheritWorkState.Standing);
			Assert.AreEqual(1, (int)KingdomInheritWorkState.Derelict);
			Assert.AreEqual(2, (int)KingdomInheritWorkState.Rubble);
			Assert.AreEqual(3, (int)KingdomInheritWorkState.Memory);

			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomInheritFault)));
			int[] faults = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
			Array faultValues = Enum.GetValues(typeof(KingdomInheritFault));
			Assert.AreEqual(faults.Length, faultValues.Length);
			for (int i = 0; i < faults.Length; i++)
				Assert.AreEqual(faults[i], Convert.ToInt32(faultValues.GetValue(i)));

			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomInheritEngineCheck)));
			CollectionAssert.AreEqual(new int[] { 0, 1, 2, 4, 8, 16 }, new int[]
			{
				(int)KingdomInheritEngineCheck.None,
				(int)KingdomInheritEngineCheck.ConnectionCell,
				(int)KingdomInheritEngineCheck.Terrain,
				(int)KingdomInheritEngineCheck.ExistingObjects,
				(int)KingdomInheritEngineCheck.Stairs,
				(int)KingdomInheritEngineCheck.EntryToHeartPath
			});

			Assert.AreEqual("ThousandAndFirst.KingdomInheritWork", typeof(KingdomInheritWork).FullName);
			Assert.AreEqual("ThousandAndFirst.KingdomInheritPlan", typeof(KingdomInheritPlan).FullName);
			Assert.AreEqual("ThousandAndFirst.KingdomInheritPlacement", typeof(KingdomInheritPlacement).FullName);
			AssertDeclaredFields(typeof(KingdomInheritWork), "Key", "X", "Y", "Condition", "State",
				"ArchitectureSnapshot", "ArchitectureHash");
			AssertDeclaredFields(typeof(KingdomInheritPlan), "_works", "Width", "Height");
			AssertDeclaredFields(typeof(KingdomInheritPlacement), "_works", "EntryX", "EntryY",
				"CairnX", "CairnY", "HeartX", "HeartY", "RemainingEngineChecks", "_streets",
				"SpatialVersion");
		}

		[Test]
		public void WorkDtosCannotCarryItemsOrEngineObjectsByConstruction()
		{
			Type[] dtoTypes = new[] { typeof(KingdomInheritWork), typeof(KingdomInheritPlan), typeof(KingdomInheritPlacement) };
			string[] forbidden = new[] { "item", "inventory", "liquid", "charge", "mod", "object", "quest", "reputation", "blueprint", "path", "type" };
			for (int t = 0; t < dtoTypes.Length; t++)
			{
				FieldInfo[] fields = dtoTypes[t].GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				for (int i = 0; i < fields.Length; i++)
				{
					string name = fields[i].Name.ToLowerInvariant();
					for (int j = 0; j < forbidden.Length; j++)
					{
						StringAssert.DoesNotContain(forbidden[j], name, dtoTypes[t].Name + "." + fields[i].Name);
					}
					Type fieldType = fields[i].FieldType;
					bool safe = fieldType == typeof(string) || fieldType == typeof(int) || fieldType.IsEnum
						|| fieldType == typeof(KingdomInheritWork[]);
					Assert.IsTrue(safe, dtoTypes[t].Name + "." + fields[i].Name + " carries " + fieldType.FullName);
				}
			}
		}

		[Test]
		public void EveryFailureHasFixedNonThrowingDetail()
		{
			Array values = Enum.GetValues(typeof(KingdomInheritFault));
			for (int i = 0; i < values.Length; i++)
			{
				KingdomInheritFault fault = (KingdomInheritFault)values.GetValue(i);
				string line = KingdomInheritRules.FailureLine(fault);
				if (fault == KingdomInheritFault.None)
				{
					Assert.AreEqual("", line);
				}
				else
				{
					Assert.IsNotEmpty(line, fault.ToString());
				}
			}
		}
	}
}
#endif
