#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomZoningRulesTests
	{
		// --- absent attributes: every entry written before these gates existed is untouched ---

		[Test]
		public void ParseGateAttributes_AllAbsent_IsOpenAndReportsNoError()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("caskrack", null, null, null, null, out string error);
			ClassicAssert.IsNull(error);
			ClassicAssert.IsTrue(gate.IsOpen);
			ClassicAssert.IsNull(gate.Districts);
			ClassicAssert.AreEqual(0, gate.MinZones);
			ClassicAssert.IsNull(gate.Knowledge);
			ClassicAssert.AreEqual(TechLevel.Hands, gate.MinTech);
		}

		[TestCase("agrarian", "food")]
		[TestCase("garrison", "defense")]
		[TestCase("craft", "power")]
		[TestCase(null, "food")]
		[TestCase("", "knowledge")]
		[TestCase("mymod_quarry", "memorial")]
		public void OpenGate_PermitsEveryGroundAndEveryCategory(string tileDistrict, string category)
		{
			// The whole back-compatibility promise in one assertion: an ungated design is never
			// refused, on any ground, for any reason this file knows about.
			ZoningJudgement judgement = KingdomZoningRules.Judge(ZoneGate.Open, tileDistrict, category, 0, null);
			ClassicAssert.IsTrue(judgement.Permitted);
			ClassicAssert.IsNull(judgement.Detail);
			ClassicAssert.IsNull(judgement.Note);
		}

		[TestCase("all", "craft")]
		[TestCase("ALL", "craft")]
		[TestCase(" all , craft ", "craft")]
		public void ParseGateAttributes_AllToken_MeansUngated(string districts, string category)
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("k", districts, null, null, null, out string error);
			ClassicAssert.IsNull(error);
			ClassicAssert.IsNull(gate.Districts);
			ClassicAssert.IsTrue(KingdomZoningRules.DistrictAccepts("shrine", gate.Districts, category));
		}

		// --- district zoning: hard for where a structure may stand ---------------------------

		[TestCase("craft", "craft", "craft", true)]
		[TestCase("shrine", "craft", "craft", false)]
		[TestCase("garrison", "craft", "power", false)]
		[TestCase("craft", "craft,shrine", "power", true)]
		[TestCase("shrine", "craft,shrine", "power", true)]
		[TestCase("CRAFT", " craft ", "power", true)]
		public void DistrictAccepts_DistrictedGround_IsExactlyTheDeclaredList(string tile, string required, string category, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomZoningRules.DistrictAccepts(tile, required, category));
		}

		[TestCase("housing")]
		[TestCase("storage")]
		[TestCase("civic")]
		public void DistrictAccepts_UndistrictedGround_AlwaysTakesTheRoofTheCaskAndTheFire(string category)
		{
			// The early game must never hit a wall before the founder has learned what a district
			// is. A gate naming a district nobody has designated still cannot refuse these three
			// on open ground.
			ClassicAssert.IsTrue(KingdomZoningRules.DistrictAccepts(null, "craft", category));
			ClassicAssert.IsTrue(KingdomZoningRules.DistrictAccepts("", "academy,shrine", category));
			ClassicAssert.IsTrue(KingdomZoningRules.DistrictAccepts("   ", "nowhere", category));
		}

		[TestCase("food")]
		[TestCase("craft")]
		[TestCase("power")]
		[TestCase("faith")]
		[TestCase("knowledge")]
		[TestCase("memorial")]
		[TestCase("defense")]
		public void DistrictAccepts_UndistrictedGround_StillRefusesTheSpecialists(string category)
		{
			// The other half of the same rule. If open ground took everything, naming a district
			// would never be a decision, only a bonus.
			ClassicAssert.IsFalse(KingdomZoningRules.DistrictAccepts(null, "craft,shrine", category));
		}

		[TestCase("housing")]
		[TestCase("storage")]
		[TestCase("civic")]
		public void DistrictAccepts_DistrictedGround_DoesNotExemptTheOpenCategories(string category)
		{
			// The open-category clause is scoped to ground with no district. On designated ground
			// the declared list is the whole law, or zoning would have no teeth at all.
			ClassicAssert.IsFalse(KingdomZoningRules.DistrictAccepts("garrison", "market", category));
		}

		[Test]
		public void DistrictAccepts_NoneToken_LetsADesignNameOpenGroundExplicitly()
		{
			ClassicAssert.IsTrue(KingdomZoningRules.DistrictAccepts(null, "craft," + KingdomZoningRules.UndistrictedToken, "power"));
			ClassicAssert.IsTrue(KingdomZoningRules.DistrictAccepts("craft", "craft," + KingdomZoningRules.UndistrictedToken, "power"));
			ClassicAssert.IsFalse(KingdomZoningRules.DistrictAccepts("shrine", "craft," + KingdomZoningRules.UndistrictedToken, "power"));
		}

		[Test]
		public void DistrictAccepts_UnknownDistrictIsGroundLikeAnyOther()
		{
			// A district key this mod never declared - a third party's quarter, or a save written
			// by a build that had more of them - must behave as a district, not as open ground.
			// Treating it as open would let a forge stand in someone else's bazaar.
			ClassicAssert.IsFalse(KingdomZoningRules.DistrictAccepts("mymod_quarry", "craft", "power"));
			ClassicAssert.IsFalse(KingdomZoningRules.DistrictAccepts("mymod_quarry", "craft", "housing"));
			ClassicAssert.IsTrue(KingdomZoningRules.DistrictAccepts("mymod_quarry", "mymod_quarry", "power"));
			ClassicAssert.IsTrue(KingdomZoningRules.DistrictAccepts("mymod_quarry", "craft,mymod_quarry", "power"));
		}

		[TestCase("nowhere", "craft")]
		[TestCase("nowhere", "faith")]
		[TestCase("nowhere,elsewhere", "knowledge")]
		public void Judge_DistrictThatAcceptsNothing_StillNamesWhereItWouldGo(string required, string category)
		{
			// A design gated to a district that exists nowhere in the realm can never be raised.
			// That is allowed to happen - what is NOT allowed is the refusal failing to say what
			// ground it wants, which is the exact silent-stall complaint STANDARDS 7b exists for.
			ZoneGate gate = new ZoneGate(required, 0, null, TechLevel.Hands);
			foreach (string tile in new string[6] { null, "agrarian", "market", "craft", "shrine", "garrison" })
			{
				ZoningJudgement judgement = KingdomZoningRules.Judge(gate, tile, category, 9, null);
				ClassicAssert.AreEqual(ZoningVerdict.RefusedDistrict, judgement.Verdict, "tile=" + tile);
				ClassicAssert.IsFalse(string.IsNullOrEmpty(judgement.Detail), "tile=" + tile);
				ClassicAssert.IsFalse(string.IsNullOrEmpty(judgement.Note), "tile=" + tile);
			}
		}

		[Test]
		public void DescribeDistricts_NamesGroundTheWayTheFounderHearsIt()
		{
			ClassicAssert.AreEqual("the forgeworks", KingdomZoningRules.DescribeDistricts("craft"));
			ClassicAssert.AreEqual("the vinelands or the bazaar", KingdomZoningRules.DescribeDistricts("agrarian,market"));
			ClassicAssert.AreEqual("the forgeworks or ground with no district yet", KingdomZoningRules.DescribeDistricts("craft,none"));
			ClassicAssert.IsNull(KingdomZoningRules.DescribeDistricts(null));
			ClassicAssert.IsNull(KingdomZoningRules.DescribeDistricts("all"));
		}

		[TestCase("food", "agrarian")]
		[TestCase("storage", "agrarian,market")]
		[TestCase("craft", "craft")]
		[TestCase("power", "craft")]
		[TestCase("faith", "shrine")]
		[TestCase("memorial", "shrine")]
		[TestCase("housing", "garrison")]
		[TestCase("defense", "garrison")]
		[TestCase("defence", "garrison")]
		[TestCase("knowledge", "academy")]
		[TestCase("civic", "market")]
		[TestCase("kitesurfing", null)]
		[TestCase(null, null)]
		public void NaturalDistricts_ReadsStraightOffTheDistrictNames(string category, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomZoningRules.NaturalDistricts(category));
		}

		[Test]
		public void NaturalDistricts_NamesOnlyRealDistricts()
		{
			// Advice that pointed at a district the founder cannot designate would be worse than
			// no advice: it would name a fix that does not exist.
			for (int i = 0; i < 20; i++)
			{
				string natural = KingdomZoningRules.NaturalDistricts(SampleCategory(i));
				if (natural == null)
				{
					continue;
				}
				foreach (string token in KingdomZoningRules.Tokens(natural))
				{
					ClassicAssert.IsTrue(KingdomRules.IsValidDistrict(token), "category " + SampleCategory(i) + " points at " + token);
				}
			}
		}

		// --- territory ------------------------------------------------------------------------

		[TestCase(0, 1, true)]
		[TestCase(3, 2, false)]
		[TestCase(3, 3, true)]
		[TestCase(3, 4, true)]
		[TestCase(1, 0, false)]
		public void Judge_TerritoryGate_ComparesAgainstClaimedGround(int minZones, int claimed, bool permitted)
		{
			ZoneGate gate = new ZoneGate(null, minZones, null, TechLevel.Hands);
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "housing", claimed, null);
			ClassicAssert.AreEqual(permitted, judgement.Permitted);
			if (!permitted)
			{
				ClassicAssert.AreEqual(ZoningVerdict.RefusedTerritory, judgement.Verdict);
				ClassicAssert.IsTrue(judgement.Detail.Contains(minZones.ToString()), judgement.Detail);
			}
		}

		[Test]
		public void Judge_TerritoryRefusal_CountsZonesInTheRightGrammar()
		{
			ClassicAssert.AreEqual("1 claimed zone", KingdomZoningRules.Judge(new ZoneGate(null, 1, null, TechLevel.Hands), null, "housing", 0, null).Detail);
			ClassicAssert.AreEqual("4 claimed zones", KingdomZoningRules.Judge(new ZoneGate(null, 4, null, TechLevel.Hands), null, "housing", 1, null).Detail);
		}

		// --- knowledge ------------------------------------------------------------------------

		[Test]
		public void Knows_QualifiedRequirementNeedsThatExactKind()
		{
			List<string> roster = new List<string> { "machine:solar condenser" };
			ClassicAssert.IsTrue(KingdomZoningRules.Knows(roster, "machine:solar condenser"));
			ClassicAssert.IsTrue(KingdomZoningRules.Knows(roster, "MACHINE:Solar Condenser"));
			ClassicAssert.IsFalse(KingdomZoningRules.Knows(roster, "disk:solar condenser"));
		}

		[Test]
		public void Knows_UnqualifiedRequirementTakesAnyKind()
		{
			// An author who writes Knowledge="solar condenser" should be satisfied whether the
			// settlement read it off a disk, certified one, or took in someone who knew.
			ClassicAssert.IsTrue(KingdomZoningRules.Knows(new List<string> { "disk:solar condenser" }, "solar condenser"));
			ClassicAssert.IsTrue(KingdomZoningRules.Knows(new List<string> { "machine:solar condenser" }, "solar condenser"));
			ClassicAssert.IsTrue(KingdomZoningRules.Knows(new List<string> { "origin:the rust wells" }, "the rust wells"));
			ClassicAssert.IsFalse(KingdomZoningRules.Knows(new List<string> { "disk:chem cell" }, "solar condenser"));
			ClassicAssert.IsTrue(KingdomZoningRules.Knows(new List<string> { "machine:solar condenser" },
				"disk:solar condenser|machine:solar condenser"));
			ClassicAssert.IsFalse(KingdomZoningRules.Knows(new List<string> { "machine:chem cell" },
				"disk:solar condenser|machine:solar condenser"));
		}

		[Test]
		public void Knows_EmptyRequirementIsSatisfiedAndEmptyRosterIsNot()
		{
			ClassicAssert.IsTrue(KingdomZoningRules.Knows(null, null));
			ClassicAssert.IsTrue(KingdomZoningRules.Knows(null, "   "));
			ClassicAssert.IsFalse(KingdomZoningRules.Knows(null, "solar condenser"));
			ClassicAssert.IsFalse(KingdomZoningRules.Knows(new List<string>(), "solar condenser"));
		}

		[Test]
		public void MissingKnowledge_NamesEveryRequirementTheSettlementLacks()
		{
			List<string> roster = new List<string> { "disk:chem cell" };
			List<string> missing = KingdomZoningRules.MissingKnowledge(roster, "chem cell, solar condenser, machine:nanoneuro pistil");
			ClassicAssert.AreEqual(2, missing.Count);
			ClassicAssert.AreEqual("solar condenser", missing[0]);
			ClassicAssert.AreEqual("machine:nanoneuro pistil", missing[1]);
			ClassicAssert.AreEqual(0, KingdomZoningRules.MissingKnowledge(roster, null).Count);
			ClassicAssert.AreEqual(0, KingdomZoningRules.MissingKnowledge(roster, "chem cell").Count);
		}

		[Test]
		public void Judge_UnlearnedRefusal_NamesTheDesignWithoutItsKindPrefix()
		{
			ZoneGate gate = new ZoneGate(null, 0, "machine:solar condenser", TechLevel.Hands);
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "storage", 1, null);
			ClassicAssert.AreEqual(ZoningVerdict.RefusedUnlearned, judgement.Verdict);
			ClassicAssert.AreEqual("solar condenser", judgement.Detail);
		}

		// --- the roster store -------------------------------------------------------------------

		[Test]
		public void Roster_RoundTripsAndDeduplicates()
		{
			List<string> roster = new List<string> { "disk:chem cell", "DISK:Chem Cell", " machine:solar condenser " };
			string encoded = KingdomZoningRules.EncodeRoster(roster);
			List<string> decoded = KingdomZoningRules.DecodeRoster(encoded);
			ClassicAssert.AreEqual(2, decoded.Count);
			ClassicAssert.AreEqual("disk:chem cell", decoded[0]);
			ClassicAssert.AreEqual("machine:solar condenser", decoded[1]);
			ClassicAssert.AreEqual(encoded, KingdomZoningRules.EncodeRoster(decoded));
		}

		[Test]
		public void Roster_AcceptsItsExactCharacterCeilingAndRefusesOnePastAtomically()
		{
			List<string> exact = new List<string>();
			for (int i = 0; i < 15; i++)
			{
				string prefix = "k" + i.ToString("D2") + ":";
				exact.Add(prefix + new string('x', KingdomZoningRules.MaxRosterKeyChars
					- prefix.Length));
			}
			string lastPrefix = "last:";
			int lastLength = KingdomZoningRules.MaxRosterEncodedChars - 15
				- 15 * KingdomZoningRules.MaxRosterKeyChars;
			exact.Add(lastPrefix + new string('y', lastLength - lastPrefix.Length));
			string encoded;
			ClassicAssert.IsTrue(KingdomZoningRules.TryEncodeRoster(exact, out encoded));
			ClassicAssert.AreEqual(KingdomZoningRules.MaxRosterEncodedChars, encoded.Length);

			exact[exact.Count - 1] += "z";
			ClassicAssert.IsFalse(KingdomZoningRules.TryEncodeRoster(exact, out encoded));
			ClassicAssert.IsNull(encoded, "overflow must not publish a truncated prefix");
		}

		[Test]
		public void Roster_RefusesRowKeyAndUtf8OverflowBeforeAllocationCanGrowUnbounded()
		{
			List<string> tooMany = new List<string>();
			for (int i = 0; i <= KingdomZoningRules.MaxRosterRows; i++)
				tooMany.Add("node:" + i);
			string encoded;
			ClassicAssert.IsFalse(KingdomZoningRules.TryEncodeRoster(tooMany, out encoded));

			ClassicAssert.IsNull(KingdomZoningRules.ComposeKey("disk",
				new string('x', KingdomZoningRules.MaxRosterKeyChars)));
			List<string> utf8 = new List<string>();
			for (int i = 0; i < 17; i++)
				utf8.Add("k" + i + ":" + new string('界', 335));
			ClassicAssert.IsFalse(KingdomZoningRules.TryEncodeRoster(utf8, out encoded));
		}

		[Test]
		public void Roster_DecoderRejectsOversizedAggregateWithoutReturningAPartialCity()
		{
			string oversized = new string('x', KingdomZoningRules.MaxRosterEncodedChars + 1);
			List<string> decoded;
			ClassicAssert.IsFalse(KingdomZoningRules.TryDecodeRoster(oversized, out decoded));
			ClassicAssert.AreEqual(0, decoded.Count);
			ClassicAssert.AreEqual(0, KingdomZoningRules.DecodeRoster(oversized).Count);
			string canonical;
			ClassicAssert.IsFalse(KingdomZoningRules.TryCanonicalRoster(oversized, out canonical));
			ClassicAssert.IsNull(canonical);
		}

		[TestCase((string)null)]
		[TestCase("")]
		[TestCase("|")]
		[TestCase("||||")]
		[TestCase("   |  | ")]
		public void DecodeRoster_UnreadableStoreYieldsAnEmptyRosterRatherThanThrowing(string stored)
		{
			// A corrupted store must cost the founder nothing but the knowledge itself. Throwing
			// here would surface as a lost kingdom under the engine's silent recovery.
			ClassicAssert.AreEqual(0, KingdomZoningRules.DecodeRoster(stored).Count);
		}

		[Test]
		public void ComposeKey_RefusesKeysThatCouldNotSurviveTheStore()
		{
			ClassicAssert.AreEqual("disk:solar condenser", KingdomZoningRules.ComposeKey("Disk", " Solar Condenser "));
			ClassicAssert.IsNull(KingdomZoningRules.ComposeKey("disk", null));
			ClassicAssert.IsNull(KingdomZoningRules.ComposeKey("disk", "  "));
			ClassicAssert.IsNull(KingdomZoningRules.ComposeKey(null, "solar condenser"));
			ClassicAssert.IsNull(KingdomZoningRules.ComposeKey("disk", "a|b"));
			ClassicAssert.IsNull(KingdomZoningRules.ComposeKey("a|b", "solar condenser"));
			ClassicAssert.IsNull(KingdomZoningRules.ComposeKey("a:b", "solar condenser"));
		}

		[Test]
		public void KindAndNameSplitOnTheFirstSeparatorOnly()
		{
			ClassicAssert.AreEqual("machine", KingdomZoningRules.KindOf("machine:mark ii:rev b"));
			ClassicAssert.AreEqual("mark ii:rev b", KingdomZoningRules.NameOf("machine:mark ii:rev b"));
			ClassicAssert.IsNull(KingdomZoningRules.KindOf("solar condenser"));
			ClassicAssert.AreEqual("solar condenser", KingdomZoningRules.NameOf("solar condenser"));
		}

		// --- technology level: derived, never authored ------------------------------------------

		[Test]
		public void TechPoints_WeighsWhatEachKindCostToAcquire()
		{
			ClassicAssert.AreEqual(0, KingdomZoningRules.TechPoints(null));
			ClassicAssert.AreEqual(0, KingdomZoningRules.TechPoints(new List<string>()));
			ClassicAssert.AreEqual(KingdomZoningRules.TechPointsPerDisk, KingdomZoningRules.TechPoints(new List<string> { "disk:chem cell" }));
			ClassicAssert.AreEqual(KingdomZoningRules.TechPointsPerCertification, KingdomZoningRules.TechPoints(new List<string> { "machine:chem cell" }));
			ClassicAssert.AreEqual(KingdomZoningRules.TechPointsPerOrigin, KingdomZoningRules.TechPoints(new List<string> { "origin:the hills" }));
			ClassicAssert.AreEqual(0, KingdomZoningRules.TechPoints(new List<string> { "mymod_rite:the long walk" }));
		}

		[Test]
		public void TechPoints_CountsEachDesignOnceHoweverOftenItAppears()
		{
			// Otherwise a re-learn loop is a craft-level exploit, and the level stops meaning
			// anything about what the settlement can actually do.
			List<string> roster = new List<string> { "machine:chem cell", "machine:chem cell", "MACHINE:Chem Cell" };
			ClassicAssert.AreEqual(KingdomZoningRules.TechPointsPerCertification, KingdomZoningRules.TechPoints(roster));
		}

		[Test]
		public void TechPointsPerOrigin_IsZeroSoTheLevelIsNotAPopulationCount()
		{
			ClassicAssert.AreEqual(0, KingdomZoningRules.TechPointsPerOrigin);
			ClassicAssert.Greater(KingdomZoningRules.TechPointsPerDisk, 0);
			ClassicAssert.Greater(KingdomZoningRules.TechPointsPerCertification, KingdomZoningRules.TechPointsPerDisk);
		}

		[TestCase(-5, TechLevel.Hands)]
		[TestCase(0, TechLevel.Hands)]
		[TestCase(1, TechLevel.Hands)]
		[TestCase(2, TechLevel.Salvage)]
		[TestCase(4, TechLevel.Salvage)]
		[TestCase(5, TechLevel.Workshop)]
		[TestCase(8, TechLevel.Workshop)]
		[TestCase(9, TechLevel.Foundry)]
		[TestCase(13, TechLevel.Foundry)]
		[TestCase(14, TechLevel.Arclight)]
		[TestCase(9000, TechLevel.Arclight)]
		public void LevelForPoints_MatchesTheLadder(int points, TechLevel expected)
		{
			ClassicAssert.AreEqual(expected, KingdomZoningRules.LevelForPoints(points));
		}

		[Test]
		public void LevelForPoints_NeverFallsAsPointsRise()
		{
			TechLevel previous = KingdomZoningRules.LevelForPoints(0);
			for (int points = 1; points <= 200; points++)
			{
				TechLevel level = KingdomZoningRules.LevelForPoints(points);
				ClassicAssert.GreaterOrEqual((int)level, (int)previous, "points=" + points);
				previous = level;
			}
		}

		[Test]
		public void TechThresholds_AreStrictlyRisingAndStartAtNothing()
		{
			// A settlement that has done nothing must read as the bottom level, and two levels
			// sharing a threshold would make one of them unreachable.
			ClassicAssert.AreEqual(KingdomZoningRules.TechThresholds.Length, KingdomZoningRules.TechLevelNames.Length);
			ClassicAssert.AreEqual(0, KingdomZoningRules.TechThresholds[0]);
			for (int i = 1; i < KingdomZoningRules.TechThresholds.Length; i++)
			{
				ClassicAssert.Greater(KingdomZoningRules.TechThresholds[i], KingdomZoningRules.TechThresholds[i - 1], "threshold " + i);
			}
		}

		[TestCase(0, 2)]
		[TestCase(1, 1)]
		[TestCase(2, 3)]
		[TestCase(13, 1)]
		[TestCase(14, 0)]
		[TestCase(99, 0)]
		public void PointsToNext_SaysHowMuchFurtherTheLadderRuns(int points, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomZoningRules.PointsToNext(points));
		}

		[Test]
		public void TechNameAndPointsForLevel_ClampInsteadOfThrowing()
		{
			ClassicAssert.AreEqual("hands", KingdomZoningRules.TechName(TechLevel.Hands));
			ClassicAssert.AreEqual("arclight", KingdomZoningRules.TechName(TechLevel.Arclight));
			ClassicAssert.AreEqual("hands", KingdomZoningRules.TechName((TechLevel)(-3)));
			ClassicAssert.AreEqual("arclight", KingdomZoningRules.TechName((TechLevel)99));
			ClassicAssert.AreEqual(0, KingdomZoningRules.PointsForLevel((TechLevel)(-3)));
			ClassicAssert.AreEqual(KingdomZoningRules.TechThresholds[KingdomZoningRules.TechThresholds.Length - 1], KingdomZoningRules.PointsForLevel((TechLevel)99));
		}

		[Test]
		public void Judge_TechGate_ComparesTheDerivedLevelNotTheRosterSize()
		{
			ZoneGate gate = new ZoneGate(null, 0, null, TechLevel.Workshop);
			// Five taught designs is exactly the workshop threshold; four is not, however many
			// people from however many countries are also standing in the settlement.
			List<string> four = new List<string> { "disk:a", "disk:b", "disk:c", "disk:d", "origin:the hills", "origin:the desert canyons" };
			List<string> five = new List<string> { "disk:a", "disk:b", "disk:c", "disk:d", "disk:e" };
			ClassicAssert.AreEqual(ZoningVerdict.RefusedTechLevel, KingdomZoningRules.Judge(gate, null, "storage", 9, four).Verdict);
			ClassicAssert.AreEqual("workshop", KingdomZoningRules.Judge(gate, null, "storage", 9, four).Detail);
			ClassicAssert.IsTrue(KingdomZoningRules.Judge(gate, null, "storage", 9, five).Permitted);
		}

		[Test]
		public void Judge_TechGateAtHands_GatesNothing()
		{
			ZoneGate gate = new ZoneGate(null, 0, null, TechLevel.Hands);
			ClassicAssert.IsTrue(KingdomZoningRules.Judge(gate, null, "storage", 0, null).Permitted);
		}

		// --- all four at once -------------------------------------------------------------------

		[Test]
		public void Judge_GatedOnAllFour_ReportsOneLackAtATimeInTheDeclaredOrder()
		{
			ZoneGate gate = new ZoneGate("craft", 3, "machine:solar condenser", TechLevel.Workshop);

			// Nothing satisfied: the most fundamental lack speaks first.
			ZoningJudgement nothing = KingdomZoningRules.Judge(gate, "shrine", "power", 1, null);
			ClassicAssert.AreEqual(ZoningVerdict.RefusedUnlearned, nothing.Verdict);

			// Learned, but the craft has not risen with it.
			List<string> learned = new List<string> { "machine:solar condenser" };
			ClassicAssert.AreEqual(ZoningVerdict.RefusedTechLevel, KingdomZoningRules.Judge(gate, "shrine", "power", 1, learned).Verdict);

			// Craft reached, realm still too small.
			List<string> skilled = new List<string> { "machine:solar condenser", "machine:chem cell", "disk:torch" };
			ClassicAssert.AreEqual(ZoningVerdict.RefusedTerritory, KingdomZoningRules.Judge(gate, "shrine", "power", 1, skilled).Verdict);

			// Realm large enough, still the wrong ground - and this is the refusal that teaches.
			ZoningJudgement wrongGround = KingdomZoningRules.Judge(gate, "shrine", "power", 3, skilled);
			ClassicAssert.AreEqual(ZoningVerdict.RefusedDistrict, wrongGround.Verdict);
			ClassicAssert.AreEqual("the forgeworks", wrongGround.Detail);

			// Everything answered.
			ClassicAssert.IsTrue(KingdomZoningRules.Judge(gate, "craft", "power", 3, skilled).Permitted);
		}

		[Test]
		public void Judge_EveryRefusalNamesSomethingAndSuggestsSomething()
		{
			// The binding rule (STANDARDS 7b): a refusal that does not teach is a locked door.
			// This is the test that fails if any future verdict forgets to fill in its prose.
			ZoneGate gate = new ZoneGate("craft", 3, "machine:solar condenser", TechLevel.Workshop);
			ZoningJudgement[] refusals = new ZoningJudgement[4]
			{
				KingdomZoningRules.Judge(gate, "shrine", "power", 1, null),
				KingdomZoningRules.Judge(gate, "shrine", "power", 1, new List<string> { "machine:solar condenser" }),
				KingdomZoningRules.Judge(gate, "shrine", "power", 1, new List<string> { "machine:solar condenser", "machine:chem cell", "disk:torch" }),
				KingdomZoningRules.Judge(gate, "shrine", "power", 3, new List<string> { "machine:solar condenser", "machine:chem cell", "disk:torch" })
			};
			for (int i = 0; i < refusals.Length; i++)
			{
				ClassicAssert.IsFalse(refusals[i].Permitted, "refusal " + i);
				ClassicAssert.IsFalse(string.IsNullOrEmpty(refusals[i].Detail), "refusal " + i + " named nothing");
				ClassicAssert.IsFalse(string.IsNullOrEmpty(refusals[i].Note), "refusal " + i + " tagged nothing");
			}
		}

		// --- hostile input at the XML boundary --------------------------------------------------

		[TestCase("three")]
		[TestCase("-1")]
		[TestCase("2.5")]
		public void ParseGateAttributes_BadMinZones_IsDroppedAndNamed(string minZones)
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("bunk", null, minZones, null, null, out string error);
			ClassicAssert.AreEqual(0, gate.MinZones);
			ClassicAssert.IsNotNull(error);
			ClassicAssert.IsTrue(error.Contains("MinZones"), error);
			ClassicAssert.IsTrue(error.Contains("bunk"), error);
		}

		[TestCase("99")]
		[TestCase("-1")]
		[TestCase("cathedral")]
		public void ParseGateAttributes_BadMinTech_IsDroppedRatherThanGatingForever(string minTech)
		{
			// Enum.TryParse takes any number the underlying type holds, so "99" would otherwise
			// parse into a level that does not exist and lock the design out permanently.
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("mill", null, null, null, minTech, out string error);
			ClassicAssert.AreEqual(TechLevel.Hands, gate.MinTech);
			ClassicAssert.IsNotNull(error);
			ClassicAssert.IsTrue(error.Contains("MinTech"), error);
		}

		[TestCase("workshop", TechLevel.Workshop)]
		[TestCase("WORKSHOP", TechLevel.Workshop)]
		[TestCase(" foundry ", TechLevel.Foundry)]
		[TestCase("3", TechLevel.Foundry)]
		public void ParseGateAttributes_MinTech_TakesANameOrItsNumber(string minTech, TechLevel expected)
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("mill", null, null, null, minTech, out string error);
			ClassicAssert.IsNull(error);
			ClassicAssert.AreEqual(expected, gate.MinTech);
		}

		[TestCase(",,,")]
		[TestCase("  ,  ")]
		public void ParseGateAttributes_ListWithNothingInIt_IsDroppedAndNamed(string districts)
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("shrine", districts, null, null, null, out string error);
			ClassicAssert.IsNull(gate.Districts);
			ClassicAssert.IsNotNull(error);
			ClassicAssert.IsTrue(error.Contains("Districts"), error);
		}

		[Test]
		public void ParseGateAttributes_KnowledgeCarryingTheStoreSeparator_IsDropped()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("post", null, null, "solar|condenser", null, out string error);
			ClassicAssert.IsNull(gate.Knowledge);
			ClassicAssert.IsNotNull(error);
			ClassicAssert.IsTrue(error.Contains("Knowledge"), error);
		}

		[Test]
		public void ParseGateAttributes_ManyFaults_AreAllNamedAndTheEntryStillParses()
		{
			// A typo in an optional gate must never delete a building from the catalog: the whole
			// entry is still a design, it simply stops being gated on the axis that failed.
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("post", ",,", "nope", "a|b", "99", out string error);
			ClassicAssert.IsTrue(gate.IsOpen);
			ClassicAssert.IsNotNull(error);
			ClassicAssert.IsTrue(error.Contains("Districts"), error);
			ClassicAssert.IsTrue(error.Contains("MinZones"), error);
			ClassicAssert.IsTrue(error.Contains("Knowledge"), error);
			ClassicAssert.IsTrue(error.Contains("MinTech"), error);
		}

		[Test]
		public void ParseGateAttributes_GoodValues_ParseWithNoComplaint()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("post", " Craft , None ", "3", " machine:Solar Condenser ", "workshop", out string error);
			ClassicAssert.IsNull(error);
			ClassicAssert.IsFalse(gate.IsOpen);
			ClassicAssert.AreEqual("craft,none", gate.Districts);
			ClassicAssert.AreEqual(3, gate.MinZones);
			ClassicAssert.AreEqual("machine:solar condenser", gate.Knowledge);
			ClassicAssert.AreEqual(TechLevel.Workshop, gate.MinTech);
		}

		// --- prose helpers ------------------------------------------------------------------------

		[Test]
		public void JoinReadsAsASentenceAtEveryLength()
		{
			ClassicAssert.IsNull(KingdomZoningRules.JoinOr(new List<string>()));
			ClassicAssert.AreEqual("one", KingdomZoningRules.JoinOr(new List<string> { "one" }));
			ClassicAssert.AreEqual("one or two", KingdomZoningRules.JoinOr(new List<string> { "one", "two" }));
			ClassicAssert.AreEqual("one, two or three", KingdomZoningRules.JoinOr(new List<string> { "one", "two", "three" }));
			ClassicAssert.AreEqual("one, two and three", KingdomZoningRules.JoinAnd(new List<string> { "one", "two", "three" }));
		}

		[Test]
		public void OpenCategories_AreTheThreeThingsACampIsMadeOf()
		{
			ClassicAssert.AreEqual(3, KingdomZoningRules.OpenCategories.Length);
			ClassicAssert.IsTrue(KingdomZoningRules.IsOpenCategory("housing"));
			ClassicAssert.IsTrue(KingdomZoningRules.IsOpenCategory("STORAGE"));
			ClassicAssert.IsTrue(KingdomZoningRules.IsOpenCategory(" civic "));
			ClassicAssert.IsFalse(KingdomZoningRules.IsOpenCategory("craft"));
			ClassicAssert.IsFalse(KingdomZoningRules.IsOpenCategory(null));
			ClassicAssert.IsFalse(KingdomZoningRules.IsOpenCategory(""));
		}

		// --- the stratum gate: depth narrows the offer at commission time -------------------

		[Test]
		public void ADesignThatWantsWeatherIsRefusedUnderTheRockByName()
		{
			ZoningJudgement judgement = KingdomZoningRules.Judge(ZoneGate.Open, null, "power", 1, null,
				Underground: true, RequiresSky: true);
			ClassicAssert.AreEqual(ZoningVerdict.RefusedStratum, judgement.Verdict);
			ClassicAssert.AreEqual("wants open sky", judgement.Note);
			ClassicAssert.AreEqual("open sky", judgement.Detail, "the refusal names the stratum that would take it");
		}

		[TestCase(false, false)]
		[TestCase(false, true)]
		[TestCase(true, false)]
		public void EveryOtherCombinationOfDepthAndWeatherIsPermitted(bool underground, bool requiresSky)
		{
			ClassicAssert.IsTrue(KingdomZoningRules.Judge(ZoneGate.Open, null, "power", 1, null, underground, requiresSky).Permitted);
		}

		[TestCase(true, true, false)]
		[TestCase(true, false, true)]
		[TestCase(false, true, true)]
		[TestCase(false, false, true)]
		public void StratumAcceptsOnlyWhatTheGroundCanCarry(bool underground, bool requiresSky, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomZoningRules.StratumAccepts(underground, requiresSky));
		}

		[Test]
		public void TheOldJudgeOverloadStillGatesNothingByDepth()
		{
			// Every caller written before the stratum existed asks the surface question, so a
			// design that wants weather is judged exactly as it always was.
			ClassicAssert.IsTrue(KingdomZoningRules.Judge(ZoneGate.Open, null, "power", 1, null).Permitted);
		}

		[Test]
		public void StratumIsAskedAfterTerritoryAndBeforeGround()
		{
			// The order the founder is taught in: the realm being too small outranks the rock,
			// and the rock outranks the district - because a district can be renamed tomorrow and
			// no naming puts weather under a mountain.
			ZoneGate gate = new ZoneGate("agrarian", 3, null, TechLevel.Hands);
			ClassicAssert.AreEqual(ZoningVerdict.RefusedTerritory,
				KingdomZoningRules.Judge(gate, "shrine", "power", 1, null, Underground: true, RequiresSky: true).Verdict);
			ClassicAssert.AreEqual(ZoningVerdict.RefusedStratum,
				KingdomZoningRules.Judge(gate, "shrine", "power", 3, null, Underground: true, RequiresSky: true).Verdict);
			ClassicAssert.AreEqual(ZoningVerdict.RefusedDistrict,
				KingdomZoningRules.Judge(gate, "shrine", "power", 3, null, Underground: false, RequiresSky: true).Verdict);
		}

		// --- the claim: how much ground a city of each rung answers for ---------------------

		[TestCase(GrowthStage.Camp, 1)]
		[TestCase(GrowthStage.Steading, 1)]
		[TestCase(GrowthStage.Village, 2)]
		[TestCase(GrowthStage.Town, 3)]
		[TestCase(GrowthStage.City, 4)]
		public void ARungHoldsTheGroundItsOwnDesignsAskFor(GrowthStage stage, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomZoningRules.ZonesForStage(stage));
		}

		[Test]
		public void EveryMinZonesDesignBecomesReachableAtTheStageItWasAuthoredFor()
		{
			// The whole reason the ladder is 1/1/2/3/4 rather than a number somebody chose: the
			// catalogue's eight MinZones designs pair MinZones=2 with Village, 3 with Town and 4
			// with City, so reaching the rung and reaching the ground happen together.
			ClassicAssert.IsTrue(KingdomZoningRules.ZonesForStage(GrowthStage.Village) >= 2);
			ClassicAssert.IsTrue(KingdomZoningRules.ZonesForStage(GrowthStage.Town) >= 3);
			ClassicAssert.IsTrue(KingdomZoningRules.ZonesForStage(GrowthStage.City) >= 4);
			ClassicAssert.IsTrue(KingdomZoningRules.ZonesForStage(GrowthStage.Steading) < 2,
				"a steading must not be able to reach a two-zone design");
		}

		[Test]
		public void AStageThisBuildDoesNotDefineHoldsOneParasang()
		{
			ClassicAssert.AreEqual(1, KingdomZoningRules.ZonesForStage((GrowthStage)99));
			ClassicAssert.AreEqual(1, KingdomZoningRules.ZonesForStage((GrowthStage)(-3)));
		}

		[Test]
		public void AClaimOnBorderingUnheldGroundIsAllowed()
		{
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.Allowed,
				KingdomZoningRules.JudgeClaim(true, GrowthStage.Village, 1, false, false, false, false, true));
		}

		[Test]
		public void AnUnfoundedRealmClaimsNothing()
		{
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.NothingFoundedYet,
				KingdomZoningRules.JudgeClaim(false, GrowthStage.City, 0, false, false, false, false, true));
		}

		[Test]
		public void GroundTheCityAlreadyHoldsIsNotClaimedTwice()
		{
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.GroundIsAlreadyOurs,
				KingdomZoningRules.JudgeClaim(true, GrowthStage.Village, 1, true, false, false, false, true));
		}

		[Test]
		public void OneParasangAnswersToOneCity()
		{
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.GroundIsAnotherCitys,
				KingdomZoningRules.JudgeClaim(true, GrowthStage.Village, 1, false, true, false, false, true));
		}

		[Test]
		public void TheRealmThatPutYouOutKeepsItsGround()
		{
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.GroundIsAnotherRealms,
				KingdomZoningRules.JudgeClaim(true, GrowthStage.Village, 1, false, false, true, false, true));
		}

		[Test]
		public void AForeignFactionsGroundIsAskedForNeverTaken()
		{
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.GroundIsForeign,
				KingdomZoningRules.JudgeClaim(true, GrowthStage.Village, 1, false, false, false, true, true));
		}

		[Test]
		public void ACityGrowsOutwardFromWhatItAlreadyHolds()
		{
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.GroundIsNotAdjacent,
				KingdomZoningRules.JudgeClaim(true, GrowthStage.City, 1, false, false, false, false, false));
		}

		[TestCase(GrowthStage.Camp, 1)]
		[TestCase(GrowthStage.Steading, 1)]
		[TestCase(GrowthStage.Village, 2)]
		[TestCase(GrowthStage.Town, 3)]
		[TestCase(GrowthStage.City, 4)]
		public void ACityAtItsRungsCeilingClaimsNoMore(GrowthStage stage, int held)
		{
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.CityHoldsAllItCan,
				KingdomZoningRules.JudgeClaim(true, stage, held, false, false, false, false, true));
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.Allowed,
				KingdomZoningRules.JudgeClaim(true, stage, held - 1, false, false, false, false, true),
				"one parasang short of the ceiling still claims");
		}

		[Test]
		public void TheStageGateIsAskedLastSoTheGroundFactsAreHeardFirst()
		{
			// A founder standing on a foreign village's ground at their rung's ceiling is told
			// about the village, which is the fact they can do something about today.
			ClassicAssert.AreEqual(KingdomZoningRules.ClaimVerdict.GroundIsForeign,
				KingdomZoningRules.JudgeClaim(true, GrowthStage.Camp, 1, false, false, false, true, true));
		}

		[Test]
		public void EveryClaimRefusalNamesWhatWouldLiftIt()
		{
			foreach (KingdomZoningRules.ClaimVerdict verdict in System.Enum.GetValues(typeof(KingdomZoningRules.ClaimVerdict)))
			{
				string refusal = KingdomZoningRules.ClaimRefusal(verdict, "Kavvat", GrowthStage.Town);
				if (verdict == KingdomZoningRules.ClaimVerdict.Allowed)
				{
					ClassicAssert.AreEqual("", refusal, "an allowed claim refuses nothing");
					continue;
				}
				ClassicAssert.IsTrue(refusal.Length > 0, verdict + " must tell the founder why");
				ClassicAssert.IsTrue(refusal.EndsWith("."), verdict + " must be a sentence");
			}
		}

		[Test]
		public void TheCeilingRefusalNamesTheRungThatWouldLiftIt()
		{
			string town = KingdomZoningRules.ClaimRefusal(KingdomZoningRules.ClaimVerdict.CityHoldsAllItCan, "Kavvat", GrowthStage.Town);
			ClassicAssert.IsTrue(town.Contains("a town"), "the refusal names the rung the city is at");
			ClassicAssert.IsTrue(town.Contains("3 parasangs"), "and how much ground that rung answers for");
			ClassicAssert.IsTrue(town.Contains("a city"), "and the rung that would lift it");

			string city = KingdomZoningRules.ClaimRefusal(KingdomZoningRules.ClaimVerdict.CityHoldsAllItCan, "Kavvat", GrowthStage.City);
			ClassicAssert.IsFalse(city.Contains("Grow into"), "a city is told to found again, not to grow into itself");
			ClassicAssert.IsTrue(city.Contains("4 parasangs"));
		}

		// --- the wall line, said out loud ---------------------------------------------------

		[TestCase(KingdomRules.Frontier.None, 0)]
		[TestCase(KingdomRules.Frontier.North, 1)]
		[TestCase(KingdomRules.Frontier.North | KingdomRules.Frontier.South, 2)]
		[TestCase(KingdomRules.Frontier.North | KingdomRules.Frontier.South | KingdomRules.Frontier.West, 3)]
		[TestCase(KingdomRules.Frontier.North | KingdomRules.Frontier.South | KingdomRules.Frontier.West | KingdomRules.Frontier.East, 4)]
		public void EdgeCountCountsTheSidesFacingTheWorld(KingdomRules.Frontier edges, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomZoningRules.EdgeCount(edges));
		}

		[Test]
		public void AClaimThatFreesAnEdgeSaysTheWallLineMoved()
		{
			string moved = KingdomZoningRules.ClaimedWallClause(4, 3, "Kavvat");
			ClassicAssert.IsTrue(moved.Contains("moves outward"));
			ClassicAssert.IsTrue(moved.Contains("inner wall"), "the old line is named as what it becomes");
			ClassicAssert.IsTrue(moved.Contains("nothing already built is moved"), "the protection law is said, not implied");
		}

		[Test]
		public void AClaimThatFreesTwoEdgesCountsThem()
		{
			ClassicAssert.IsTrue(KingdomZoningRules.ClaimedWallClause(7, 5, "Kavvat").Contains("2 sides"));
		}

		[Test]
		public void AClaimThatMovesNoEdgeSaysSoRatherThanClaimingItDid()
		{
			// The honest answer for ground taken diagonally across a corner, or straight down
			// into the rock: FrontierEdges clears an edge only for an orthogonal neighbour in the
			// same stratum, so those claims are legal ground that moves no wall.
			string still = KingdomZoningRules.ClaimedWallClause(4, 4, "Kavvat");
			ClassicAssert.IsTrue(still.Contains("does not move"));
			ClassicAssert.IsFalse(still.Contains("inner wall"));
		}

		[Test]
		public void TheHoldingLineNamesWhatIsHeldAndWhatIsLeft()
		{
			ClassicAssert.IsTrue(KingdomZoningRules.ClaimHoldingLine(2, 3).Contains("2 parasangs"));
			ClassicAssert.IsTrue(KingdomZoningRules.ClaimHoldingLine(2, 3).Contains("one more"));
			ClassicAssert.IsTrue(KingdomZoningRules.ClaimHoldingLine(1, 4).Contains("3 more"));
			ClassicAssert.IsTrue(KingdomZoningRules.ClaimHoldingLine(3, 3).Contains("all this rung answers for"));
			ClassicAssert.IsTrue(KingdomZoningRules.ClaimHoldingLine(1, 1).Contains("one parasang"));
		}

		private static string SampleCategory(int Index)
		{
			string[] categories = new string[20]
			{
				"food", "storage", "civic", "craft", "power", "faith", "memorial", "housing", "defense", "defence",
				"knowledge", "", null, "Storage", " craft ", "kitesurfing", "mymod_thing", "DEFENSE", "Faith", "power"
			};
			return categories[Index];
		}
	}
}
#endif
