#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Addendum 16: styles as tags, and the creed-gate stack that rides the same list idiom.
	/// <para>
	/// Everything here is the pure half. The engine-coupled half — reading a city's tallies,
	/// hiding a design from a menu — is exercised through the values these functions return,
	/// which is the only half a test can hold still.
	/// </para>
	/// </summary>
	public class KingdomCreedGateRulesTests
	{
		// ==================================================================================
		// The tag idiom. `Styles` is the oldest list in the catalogue and is now the general one.
		// ==================================================================================

		[TestCase(null, "verdant", true)]
		[TestCase("", "verdant", true)]
		[TestCase("   ", "verdant", true)]
		[TestCase("all", "verdant", true)]
		[TestCase("all", null, true)]
		[TestCase("verdant", "verdant", true)]
		[TestCase("verdant,fungal", "fungal", true)]
		[TestCase("verdant,fungal", "eater", false)]
		[TestCase("verdant", null, false)]
		public void TagAccepts_ReadsAWelcomeList(string tags, string style, bool accepted)
		{
			Assert.AreEqual(accepted, KingdomZoningRules.TagAccepts(tags, style));
		}

		/// <summary>Case is folded on both sides now. The shipped comparison was exact, so
		/// <c>Styles="Verdant"</c> matched nothing and said nothing about it — the exact silent
		/// failure the tag idiom exists to make impossible.</summary>
		[TestCase("Verdant", "verdant")]
		[TestCase("VERDANT , Fungal", "fungal")]
		[TestCase("ALL", "eater")]
		public void TagAccepts_FoldsCaseOnBothSides(string tags, string style)
		{
			Assert.IsTrue(KingdomZoningRules.TagAccepts(tags, style));
			Assert.IsTrue(KingdomRules.StyleAllows(tags, style), "StyleAllows is the same rule");
		}

		[TestCase("all,!eater", "eater", false)]
		[TestCase("all,!eater", "verdant", true)]
		[TestCase("!eater", "verdant", true)]
		[TestCase("!eater", "eater", false)]
		[TestCase("!eater,!common", "common", false)]
		[TestCase("! eater", "eater", false)]
		[TestCase("!", "eater", true)]
		public void TagAccepts_RefusesWhatItNegates(string tags, string style, bool accepted)
		{
			Assert.AreEqual(accepted, KingdomZoningRules.TagAccepts(tags, style));
		}

		/// <summary>A refusal outranks a welcome for the same tag, in either order. Nobody writes
		/// <c>!x</c> by accident, and reading the last token to win would make the answer depend on
		/// the order two merged files happened to land in.</summary>
		[TestCase("eater,!eater")]
		[TestCase("!eater,eater")]
		[TestCase("all,eater,!eater")]
		public void TagAccepts_ARefusalOutranksAWelcomeWhicheverComesFirst(string tags)
		{
			Assert.IsFalse(KingdomZoningRules.TagAccepts(tags, "eater"));
		}

		/// <summary>A list of nothing but refusals is "everywhere except", never "nowhere". The
		/// open-set problem is the whole reason the operator exists: a design that belongs
		/// everywhere but one place cannot say so by enumeration once a third party ships a sixth
		/// style.</summary>
		[Test]
		public void TagAccepts_APureRefusalListIsEverywhereExcept()
		{
			Assert.IsTrue(KingdomZoningRules.TagAccepts("!eater", "somebody_elses_style"));
			Assert.IsFalse(KingdomZoningRules.TagAccepts("!eater", "eater"));
		}

		[Test]
		public void DescribeTags_SaysWhichWayTheListReads()
		{
			Assert.IsNull(KingdomZoningRules.DescribeTags(null));
			Assert.IsNull(KingdomZoningRules.DescribeTags("all"));
			Assert.AreEqual("anything but eater", KingdomZoningRules.DescribeTags("all,!eater"));
			Assert.AreEqual("verdant", KingdomZoningRules.DescribeTags("verdant"));
		}

		// ==================================================================================
		// The gate stack, parsed.
		// ==================================================================================

		[Test]
		public void ParseGateAttributes_TheCreedGatesAreAbsentUntilDeclared()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("hut", null, null, null, null, out string error);
			Assert.IsNull(error);
			Assert.IsTrue(gate.IsOpen);
			Assert.IsNull(gate.Builders);
			Assert.IsNull(gate.Creed);
			Assert.AreEqual(ZoneGate.ShareUnsaid, gate.CreedShare);
		}

		[Test]
		public void ParseGateAttributes_ACreedWithNoShareAsksForTheSameThirdACityIsReadAt()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("reliquary", null, null, null, null,
				null, "Mechanimists", null, out string error);
			Assert.IsNull(error);
			Assert.IsFalse(gate.IsOpen);
			Assert.AreEqual("Mechanimists", gate.Creed, "a faction name is the game's to case, not ours");
			Assert.AreEqual(KingdomCreedRules.DominantSharePercent, gate.EffectiveCreedShare);
		}

		[TestCase("0", 0)]
		[TestCase("25", 25)]
		[TestCase("100", 100)]
		public void ParseGateAttributes_AWrittenShareIsTheShare(string written, int expected)
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("underbench", null, null, null, null,
				null, "Barathrumites", written, out string error);
			Assert.IsNull(error);
			Assert.AreEqual(expected, gate.EffectiveCreedShare);
		}

		/// <summary>A share outside 0..100 is not a stricter gate, it is a design nobody can ever
		/// raise. Dropped and named, like every other malformed attribute in this file.</summary>
		[TestCase("101")]
		[TestCase("-1")]
		[TestCase("a third")]
		public void ParseGateAttributes_AnImpossibleShareIsDroppedAndNamed(string written)
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("underbench", null, null, null, null,
				null, "Barathrumites", written, out string error);
			Assert.IsNotNull(error);
			StringAssert.Contains("CreedShare", error);
			Assert.AreEqual(KingdomCreedRules.DominantSharePercent, gate.EffectiveCreedShare);
		}

		[Test]
		public void ParseGateAttributes_AShareWithNoCreedIsDroppedAndNamed()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("hut", null, null, null, null,
				null, null, "50", out string error);
			Assert.IsNotNull(error);
			StringAssert.Contains("CreedShare", error);
			Assert.IsNull(gate.Creed);
		}

		[Test]
		public void ParseGateAttributes_BuildersAllIsNoRestrictionAtAll()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("hut", null, null, null, null,
				"all", null, null, out string error);
			Assert.IsNull(error);
			Assert.IsNull(gate.Builders);
			Assert.IsTrue(gate.IsOpen);
		}

		// ==================================================================================
		// The AMOUNT: KingdomCreedRules.DominantCreed's arithmetic, minus the rival clause.
		// ==================================================================================

		[TestCase(0, 10, 0, true, "a share of nothing is asked of nobody")]
		[TestCase(1, 2, 0, true, "and the believers floor goes with it")]
		[TestCase(2, 3, 33, false, "two is under MinBelievers however good the proportion")]
		[TestCase(3, 9, 33, true, "three of nine is a third exactly")]
		[TestCase(3, 10, 33, false, "three of ten is not")]
		[TestCase(5, 10, 50, true, null)]
		[TestCase(4, 10, 50, false, null)]
		[TestCase(3, 0, 33, false, "a city with nobody in it holds no share of anything")]
		public void CreedShareMet_IsTheCityRuleWithoutTheRivalClause(int holding, int people, int percent, bool met, string why)
		{
			Assert.AreEqual(met, KingdomZoningRules.CreedShareMet(holding, people, percent), why ?? "");
		}

		/// <summary>The dropped clause, stated as a test so the difference is deliberate rather
		/// than remembered: a congregation big enough to raise its own work does not have to be
		/// the biggest congregation in town.</summary>
		[Test]
		public void CreedShareMet_DoesNotAskWhetherARivalIsLarger()
		{
			Dictionary<string, int> counts = new Dictionary<string, int> { { "Mechanimists", 4 }, { "Templar", 5 } };
			Assert.AreNotEqual("Mechanimists", KingdomCreedRules.DominantCreed(counts, 10), "the CITY is not theirs");
			Assert.IsTrue(KingdomZoningRules.CreedShareMet(4, 10, 33), "the reliquary is");
		}

		[TestCase(0, 10, 0)]
		[TestCase(3, 10, 30)]
		[TestCase(10, 10, 100)]
		[TestCase(3, 0, 0)]
		public void ShareHeld_ReadsBackWholePercent(int holding, int people, int expected)
		{
			Assert.AreEqual(expected, KingdomZoningRules.ShareHeld(holding, people));
		}

		// ==================================================================================
		// ALIGNMENT, and the visibility law that is its exact complement.
		// ==================================================================================

		private static BuilderRoll Roll(int people, IDictionary<string, int> holding, IDictionary<string, int> kept)
		{
			return new BuilderRoll(people, new Dictionary<string, int> { { "the rust wells", 2 } }, holding, kept);
		}

		private static Dictionary<string, int> One(string key, int value)
		{
			return new Dictionary<string, int> { { key, value } };
		}

		[Test]
		public void Aligned_HoldingItNowCounts()
		{
			BuilderRoll roll = Roll(9, One("Barathrumites", 3), null);
			Assert.IsTrue(KingdomZoningRules.Aligned(roll, "Barathrumites"));
			Assert.IsFalse(KingdomZoningRules.NoPathToCreed(roll, "Barathrumites"));
		}

		/// <summary>The whole reason a settler's creed history is recorded: somebody who LEFT the
		/// creed still aligns with it, and no tally of present belief can say so.</summary>
		[Test]
		public void Aligned_HavingHeldItAndLeftItCountsToo()
		{
			BuilderRoll roll = Roll(9, null, One("Barathrumites", 1));
			Assert.IsTrue(KingdomZoningRules.Aligned(roll, "Barathrumites"));
			Assert.IsFalse(KingdomZoningRules.NoPathToCreed(roll, "Barathrumites"));
		}

		[Test]
		public void Aligned_NobodyHoldingAndNobodyEverHavingIsTheOneGateWithNoKey()
		{
			BuilderRoll roll = Roll(9, One("Templar", 5), One("Joppa", 2));
			Assert.IsFalse(KingdomZoningRules.Aligned(roll, "Barathrumites"));
			Assert.IsTrue(KingdomZoningRules.NoPathToCreed(roll, "Barathrumites"),
				"and that is the design a menu does not show at all");
		}

		[Test]
		public void Aligned_IsCaseInsensitiveBecauseTheNameIsWrittenTwiceInTwoFiles()
		{
			Assert.IsTrue(KingdomZoningRules.Aligned(Roll(9, One("barathrumites", 3), null), "Barathrumites"));
		}

		/// <summary>A roll nobody supplied permits everything, and is not a path-less city. The
		/// asymmetry is the same one every judgment in this lane makes: a gate that cannot see the
		/// city must never be the reason a founder cannot build in it.</summary>
		[Test]
		public void AnUnknownRollPermitsAndHidesNothing()
		{
			Assert.IsTrue(KingdomZoningRules.Aligned(BuilderRoll.Unknown, "Barathrumites"));
			Assert.IsFalse(KingdomZoningRules.NoPathToCreed(BuilderRoll.Unknown, "Barathrumites"));
			Assert.IsTrue(KingdomZoningRules.HasBuilders(BuilderRoll.Unknown, "creed:Barathrumites:9"));
			Assert.AreEqual(0, KingdomZoningRules.MissingBuilders(BuilderRoll.Unknown, "creed:Barathrumites:9").Count);
		}

		[Test]
		public void ADesignThatNamesNoCreedIsAlwaysAlignedAndAlwaysVisible()
		{
			BuilderRoll roll = Roll(9, null, null);
			Assert.IsTrue(KingdomZoningRules.Aligned(roll, null));
			Assert.IsFalse(KingdomZoningRules.NoPathToCreed(roll, ""));
		}

		// ==================================================================================
		// The BUILDERS: who is standing here, by kind and by count.
		// ==================================================================================

		[TestCase("origin:the rust wells", true)]
		[TestCase("origin:the rust wells:2", true)]
		[TestCase("origin:the rust wells:3", false)]
		[TestCase("origin:the hills", false)]
		[TestCase("creed:Barathrumites", true)]
		[TestCase("creed:Barathrumites:3", true)]
		[TestCase("creed:Barathrumites:4", false)]
		[TestCase("creed:Mechanimists", false)]
		[TestCase("kept:Mechanimists", true)]
		[TestCase("kept:Barathrumites", true)]
		[TestCase("kept:Barathrumites:4", true)]
		[TestCase("the rust wells", true)]
		[TestCase("somewhere else", false)]
		[TestCase("hairdressing:advanced", false)]
		public void HasBuilders_ReadsKindNameAndCount(string requirement, bool met)
		{
			BuilderRoll roll = new BuilderRoll(9,
				new Dictionary<string, int> { { "the rust wells", 2 } },
				new Dictionary<string, int> { { "Barathrumites", 3 } },
				new Dictionary<string, int> { { "Mechanimists", 1 }, { "Barathrumites", 1 } });
			Assert.AreEqual(met, KingdomZoningRules.HasBuilders(roll, requirement));
		}

		[Test]
		public void MissingBuilders_NamesEveryUnmetRequirementInTheOrderTheAuthorWroteThem()
		{
			BuilderRoll roll = Roll(9, One("Barathrumites", 1), null);
			List<string> missing = KingdomZoningRules.MissingBuilders(roll, "creed:Barathrumites:3,origin:the hills,origin:the rust wells");
			Assert.AreEqual(2, missing.Count);
			Assert.AreEqual("creed:barathrumites:3", missing[0]);
			Assert.AreEqual("origin:the hills", missing[1]);
		}

		[Test]
		public void DescribeBuilder_SaysItInTheFoundersWords()
		{
			Assert.AreEqual("somebody from the rust wells", KingdomZoningRules.DescribeBuilder("origin:the rust wells"));
			Assert.AreEqual("3 people who hold with mechanimists", KingdomZoningRules.DescribeBuilder("creed:Mechanimists:3"));
			Assert.AreEqual("somebody who holds with mechanimists", KingdomZoningRules.DescribeBuilder("creed:Mechanimists"));
			StringAssert.Contains("has ever held", KingdomZoningRules.DescribeBuilder("kept:Mechanimists"));
		}

		// ==================================================================================
		// Judge: the three creed gates lead, in the addendum's own order.
		// ==================================================================================

		private static ZoneGate CreedGate(string builders, string creed, string share)
		{
			return KingdomZoningRules.ParseGateAttributes("x", null, null, null, null, builders, creed, share, out _);
		}

		[Test]
		public void Judge_AlignmentIsCheckedBeforeEverythingElse()
		{
			// Short of the knowledge, short of the craft, short of the ground -- and the sentence
			// the founder gets is the one about their people, because it is the only one of the
			// four they cannot answer by walking somewhere or carrying something home.
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("x", "shrine", "4", "machine:Solar Still", "foundry",
				"origin:the hills", "Barathrumites", "25", out _);
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "craft", 0, null, false, false,
				Roll(9, One("Templar", 4), null));
			Assert.AreEqual(ZoningVerdict.RefusedUnaligned, judgement.Verdict);
			Assert.AreEqual("Barathrumites", judgement.Detail);
			Assert.IsNotNull(judgement.Note);
		}

		[Test]
		public void Judge_ThenTheAmount()
		{
			ZoneGate gate = CreedGate(null, "Barathrumites", "50");
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "craft", 0, null, false, false,
				Roll(10, One("Barathrumites", 3), null));
			Assert.AreEqual(ZoningVerdict.RefusedCreedShare, judgement.Verdict);
			StringAssert.Contains("50", judgement.Note);
		}

		[Test]
		public void Judge_ThenTheHands()
		{
			ZoneGate gate = CreedGate("origin:the hills", "Barathrumites", "0");
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "craft", 0, null, false, false,
				Roll(10, One("Barathrumites", 1), null));
			Assert.AreEqual(ZoningVerdict.RefusedBuilders, judgement.Verdict);
			StringAssert.Contains("the hills", judgement.Detail);
		}

		[Test]
		public void Judge_AndOnlyThenTheFourOlderGates()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("x", null, null, "machine:Solar Still", null,
				null, "Barathrumites", "0", out _);
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "craft", 0, null, false, false,
				Roll(10, One("Barathrumites", 1), null));
			Assert.AreEqual(ZoningVerdict.RefusedUnlearned, judgement.Verdict);
		}

		[Test]
		public void Judge_AWholeStackMetPermits()
		{
			ZoneGate gate = CreedGate("origin:the rust wells:2,kept:Barathrumites", "Barathrumites", "25");
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "craft", 0, null, false, false,
				Roll(12, One("Barathrumites", 3), One("Barathrumites", 1)));
			Assert.IsTrue(judgement.Permitted, judgement.Detail);
		}

		/// <summary>The published four-gate overload has to answer exactly as it did before any of
		/// this existed, for every design that declares none of it (STANDARDS §6).</summary>
		[Test]
		public void Judge_TheOlderOverloadIsUnchanged()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("x", "craft", "0", null, null, out _);
			Assert.IsTrue(KingdomZoningRules.Judge(gate, "craft", "craft", 0, null).Permitted);
			Assert.AreEqual(ZoningVerdict.RefusedDistrict,
				KingdomZoningRules.Judge(gate, "shrine", "craft", 0, null).Verdict);
		}

		// ==================================================================================
		// The record itself: bounded, ordered, and it never rewrites.
		// ==================================================================================

		[Test]
		public void RememberKept_WritesOneCreedAndReadsItBack()
		{
			string kept = KingdomCreedRules.RememberKept(null, "Barathrumites", out bool added);
			Assert.IsTrue(added);
			CollectionAssert.AreEqual(new[] { "Barathrumites" }, KingdomCreedRules.DecodeKept(kept));
			Assert.IsTrue(KingdomCreedRules.KeptHolds(kept, "barathrumites"));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void RememberKept_ANonCreedRecordsNothing(string creed)
		{
			string kept = KingdomCreedRules.RememberKept("Joppa", creed, out bool added);
			Assert.IsFalse(added);
			CollectionAssert.AreEqual(new[] { "Joppa" }, KingdomCreedRules.DecodeKept(kept));
		}

		[Test]
		public void RememberKept_TheSameCreedTwiceIsOneMemory()
		{
			string kept = KingdomCreedRules.RememberKept("Joppa", "joppa", out bool added);
			Assert.IsFalse(added);
			Assert.AreEqual(1, KingdomCreedRules.DecodeKept(kept).Count);
		}

		/// <summary>A name carrying the store's own separator is refused rather than corrupting
		/// the record, which is <c>KingdomZoningRules.ComposeKey</c>'s bargain (STANDARDS §9).</summary>
		[Test]
		public void RememberKept_ANameThatCannotSurviveTheStoreIsRefused()
		{
			string kept = KingdomCreedRules.RememberKept("", "Joppa" + KingdomCreedRules.KeptSeparator + "Ezra", out bool added);
			Assert.IsFalse(added);
			Assert.AreEqual(0, KingdomCreedRules.DecodeKept(kept).Count);
		}

		[Test]
		public void DecodeKept_SurvivesNonsenseWithoutThrowing()
		{
			Assert.AreEqual(0, KingdomCreedRules.DecodeKept(null).Count);
			Assert.AreEqual(0, KingdomCreedRules.DecodeKept("").Count);
			Assert.AreEqual(0, KingdomCreedRules.DecodeKept("||  ||").Count);
			Assert.AreEqual(1, KingdomCreedRules.DecodeKept("|Joppa|").Count);
		}

		[Test]
		public void EncodeKept_RoundTripsDecodeExactly()
		{
			List<string> names = new List<string> { "Joppa", " Joppa ", "Ezra", "", null, "Kyakukya", "Mopango" };
			string kept = KingdomCreedRules.EncodeKept(names);
			List<string> back = KingdomCreedRules.DecodeKept(kept);
			Assert.AreEqual(KingdomCreedRules.MaxKeptCreeds, back.Count);
			CollectionAssert.AreEqual(new[] { "Joppa", "Ezra", "Kyakukya" }, back);
			Assert.AreEqual(kept, KingdomCreedRules.EncodeKept(back), "the round trip is a fixed point");
		}
	}
}
#endif
