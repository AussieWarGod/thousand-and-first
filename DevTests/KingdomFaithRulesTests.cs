#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;
using Stance = ThousandAndFirst.KingdomFaithRules.ShrineStance;
using Quarters = ThousandAndFirst.KingdomLodgingRules.Closeness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Addendum 5's shrine and education channels: who a consecrated shrine is allowed to pull
	/// (never the opposed), how slowly, and how one band gentler education reads the ambient
	/// grudge. Every gate and every boundary is asserted directly, so flipping a comparison or
	/// losing the guard against opposed creeds fails a test here rather than only showing up as a
	/// resident quietly converted in play.
	/// </summary>
	public class KingdomFaithRulesTests
	{
		// --- CanConsecrate / IsEducationCategory: category matching --------------------------

		[TestCase("faith", true)]
		[TestCase("Faith", true)]
		[TestCase("FAITH", true)]
		[TestCase(" faith ", true)]
		[TestCase("knowledge", false)]
		[TestCase("civic", false)]
		[TestCase(null, false)]
		[TestCase("", false)]
		public void CanConsecrate_MatchesOnlyTheFaithCategory(string category, bool expected)
		{
			Assert.AreEqual(expected, KingdomFaithRules.CanConsecrate(category));
		}

		[TestCase("knowledge", true)]
		[TestCase("Knowledge", true)]
		[TestCase(" knowledge ", true)]
		[TestCase("faith", false)]
		[TestCase(null, false)]
		[TestCase("", false)]
		public void IsEducationCategory_MatchesOnlyTheKnowledgeCategory(string category, bool expected)
		{
			Assert.AreEqual(expected, KingdomFaithRules.IsEducationCategory(category));
		}

		// --- ClassifyStance: the guard against pulling the opposed ---------------------------

		[Test]
		public void ClassifyStance_NoCreedIsAlwaysNeutralWhateverHostilitySays()
		{
			Assert.AreEqual(Stance.Neutral, KingdomFaithRules.ClassifyStance("", "Templar", 0));
			Assert.AreEqual(Stance.Neutral, KingdomFaithRules.ClassifyStance(null, "Templar", 100));
		}

		[Test]
		public void ClassifyStance_AnUnconsecratedShrineNeverReadsAsAnythingToConvertTo()
		{
			// A real caller never asks with an empty shrine creed (an unconsecrated shrine runs
			// no pass at all), but the function itself must not pull a believer toward nothing.
			Assert.AreEqual(Stance.Indifferent, KingdomFaithRules.ClassifyStance("Templar", "", 0));
		}

		[Test]
		public void ClassifyStance_TheSameCreedIsHomeGroundNotAConversion()
		{
			Assert.AreEqual(Stance.SameCreed, KingdomFaithRules.ClassifyStance("Templar", "Templar", 0));
			// Hostility is irrelevant once the creeds are equal -- KingdomCreed.Hostility itself
			// always reads a matched pair as zero, but this function must not depend on the
			// caller having gotten that right.
			Assert.AreEqual(Stance.SameCreed, KingdomFaithRules.ClassifyStance("Templar", "Templar", 40));
		}

		[TestCase(0, Stance.Indifferent)]
		[TestCase(1, Stance.Opposed)]
		[TestCase(24, Stance.Opposed)]
		[TestCase(100, Stance.Opposed)]
		public void ClassifyStance_DifferentCreedsSplitOnHostilityAtOne(int hostility, Stance expected)
		{
			// The guard's own boundary: ANY hostility at all -- not a threshold like dissent's --
			// is enough to refuse the pull. This mirrors KingdomLodgingRules.PackedRefusalHostility
			// deliberately: "never the opposed" is the same "any real enmity refuses" shape the
			// tightest housing rung already uses, not a softer bar of its own invention.
			Assert.AreEqual(expected, KingdomFaithRules.ClassifyStance("Barathrumites", "Templar", hostility));
		}

		[Test]
		public void ClassifyStance_NeverReturnsOpposedForACreedTheTableFilesNoOpinionBetween()
		{
			// Restated as its own test because this is the guard the whole channel exists to
			// keep: a shrine that pulled the merely-unaligned would not be a shrine that "never
			// pulls the opposed," it would be one that pulls everyone but its declared enemies.
			Assert.AreEqual(Stance.Indifferent, KingdomFaithRules.ClassifyStance("Ezra", "Kyakukya", 0));
			Assert.AreNotEqual(Stance.Opposed, KingdomFaithRules.ClassifyStance("Ezra", "Kyakukya", 0));
		}

		// --- PullAfterPass / ConversionReady: slow, deterministic, no dice -------------------

		[TestCase(0, 1)]
		[TestCase(1, 2)]
		[TestCase(29, 30)]
		[TestCase(-1, 1)]
		public void PullAfterPass_StepsByExactlyOnePerAttendedPass(int before, int expected)
		{
			Assert.AreEqual(expected, KingdomFaithRules.PullAfterPass(before));
		}

		[TestCase(0, false)]
		[TestCase(29, false)]
		[TestCase(30, true)]
		[TestCase(31, true)]
		public void ConversionReady_FiresExactlyAtTheNamedThreshold(int pull, bool expected)
		{
			Assert.AreEqual(30, KingdomFaithRules.ConversionPullThreshold, "the boundary cases above assume this constant; keep them in step if it moves");
			Assert.AreEqual(expected, KingdomFaithRules.ConversionReady(pull));
		}

		// --- SoftenedCloseness: one band gentler, capped at Private ---------------------------

		[TestCase(Quarters.Packed, Quarters.Close)]
		[TestCase(Quarters.Close, Quarters.Roomed)]
		[TestCase(Quarters.Roomed, Quarters.Private)]
		[TestCase(Quarters.Private, Quarters.Private)]
		public void SoftenedCloseness_StepsOneRungRoomierAndCapsAtPrivate(Quarters before, Quarters expected)
		{
			Assert.AreEqual(expected, KingdomFaithRules.SoftenedCloseness(before));
		}

		// --- Prose: names the people and places it is given, falls back honestly otherwise ----

		[Test]
		public void ConsecrationChronicle_FirstConsecrationNamesBuildingCreedAndCity()
		{
			string line = KingdomFaithRules.ConsecrationChronicle("shrine garth", "Ezra's Landing", "the Putus Templar", Reconsecration: false);
			StringAssert.Contains("shrine garth", line);
			StringAssert.Contains("the Putus Templar", line);
			StringAssert.Contains("Ezra's Landing", line);
			StringAssert.DoesNotContain("anew", line);
			Assert.IsFalse(line.EndsWith("."), "chronicle clauses carry no trailing period");
		}

		[Test]
		public void ConsecrationChronicle_ReconsecrationSaysSoAndKeepsTheFirstOneWritten()
		{
			string line = KingdomFaithRules.ConsecrationChronicle("shrine garth", "Ezra's Landing", "the Barathrumites", Reconsecration: true);
			StringAssert.Contains("anew", line);
			StringAssert.Contains("the Barathrumites", line);
			StringAssert.Contains("remembers", line);
		}

		[Test]
		public void ConsecrationPrompt_PlainConsecrationNamesOnlyTheAsk()
		{
			string prompt = KingdomFaithRules.ConsecrationPrompt("shrine stone", "the Templar", Reconsecration: false, NeverStaffable: false);
			StringAssert.Contains("shrine stone", prompt);
			StringAssert.Contains("the Templar", prompt);
			StringAssert.DoesNotContain("already answers", prompt);
			StringAssert.DoesNotContain("never staffed", prompt);
		}

		[Test]
		public void ConsecrationPrompt_ReconsecrationWarnsTheFirstStaysWritten()
		{
			string prompt = KingdomFaithRules.ConsecrationPrompt("shrine stone", "the Templar", Reconsecration: true, NeverStaffable: false);
			StringAssert.Contains("already answers", prompt);
		}

		[Test]
		public void ConsecrationPrompt_NeverStaffableIsToldUpFrontNotDiscoveredLater()
		{
			string prompt = KingdomFaithRules.ConsecrationPrompt("shrine stone", "the Templar", Reconsecration: false, NeverStaffable: true);
			StringAssert.Contains("never staffed", prompt);
		}

		[Test]
		public void ConversionChronicle_NamesTheResidentTheCreedAndTheShrine()
		{
			string line = KingdomFaithRules.ConversionChronicle("Vashti", "Ezra's Landing", "the Putus Templar", "temple");
			StringAssert.Contains("Vashti", line);
			StringAssert.Contains("the Putus Templar", line);
			StringAssert.Contains("temple", line);
			StringAssert.Contains("Ezra's Landing", line);
			Assert.IsFalse(line.EndsWith("."));
		}

		[Test]
		public void ConversionMessage_NamesTheResidentAndTheCreed()
		{
			string message = KingdomFaithRules.ConversionMessage("Vashti", "the Putus Templar");
			StringAssert.Contains("Vashti", message);
			StringAssert.Contains("the Putus Templar", message);
		}

		[Test]
		public void ShrineLapsedLine_NamesTheBuildingAndCallsItAStoneWhenEmptyOfHands()
		{
			string line = KingdomFaithRules.ShrineLapsedLine("temple", "the Putus Templar");
			StringAssert.Contains("temple", line);
			StringAssert.Contains("the Putus Templar", line);
			StringAssert.Contains("stone", line);
		}

		[Test]
		public void EducationLapsedLine_NamesTheBuildingAndCallsItVellumWhenEmptyOfHands()
		{
			string line = KingdomFaithRules.EducationLapsedLine("scriptorium");
			StringAssert.Contains("scriptorium", line);
			StringAssert.Contains("vellum", line);
		}

		[Test]
		public void EmptyNamesFallBackHonestlyRatherThanProducingBlankProse()
		{
			Assert.IsNotEmpty(KingdomFaithRules.ConsecrationChronicle(null, null, null, false));
			Assert.IsNotEmpty(KingdomFaithRules.ConsecrationChronicle(null, null, null, true));
			Assert.IsNotEmpty(KingdomFaithRules.ConsecrationPrompt(null, null, false, false));
			Assert.IsNotEmpty(KingdomFaithRules.ConsecrationNotice(null, null, false, false));
			Assert.IsNotEmpty(KingdomFaithRules.ConversionChronicle(null, null, null, null));
			Assert.IsNotEmpty(KingdomFaithRules.ConversionMessage(null, null));
			Assert.IsNotEmpty(KingdomFaithRules.ShrineLapsedLine(null, null));
			Assert.IsNotEmpty(KingdomFaithRules.EducationLapsedLine(null));
		}

		[Test]
		public void ConsecrationNotice_NeverStaffableIsHonestThatNobodyWillEverBePulled()
		{
			string staffable = KingdomFaithRules.ConsecrationNotice("temple", "the Templar", false, NeverStaffable: false);
			string unstaffable = KingdomFaithRules.ConsecrationNotice("shrinegarth", "the Templar", false, NeverStaffable: true);
			StringAssert.Contains("draw", staffable);
			StringAssert.DoesNotContain("draw nobody", staffable);
			StringAssert.Contains("holds it quietly", unstaffable);
		}
	}
}
#endif
