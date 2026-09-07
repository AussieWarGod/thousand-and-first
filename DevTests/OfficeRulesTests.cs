#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class OfficeRulesTests
	{
		// --- ClassifyCause: the founder's own hand outranks a raider's, which outranks any
		// other known hand, which outranks nothing being reported at all -----------------------

		[TestCase(true, true, true, KingdomOfficeRules.DeathCause.Player)]
		[TestCase(true, false, true, KingdomOfficeRules.DeathCause.Player)]
		[TestCase(false, true, true, KingdomOfficeRules.DeathCause.Raid)]
		[TestCase(false, false, true, KingdomOfficeRules.DeathCause.Violence)]
		[TestCase(false, false, false, KingdomOfficeRules.DeathCause.Unknown)]
		public void ClassifyCause_FollowsThePrecedenceOrder(bool killerIsPlayer, bool killerIsRaider, bool killerKnown, KingdomOfficeRules.DeathCause expected)
		{
			ClassicAssert.AreEqual(expected, KingdomOfficeRules.ClassifyCause(killerIsPlayer, killerIsRaider, killerKnown));
		}

		[Test]
		public void ClassifyCause_PlayerBeatsRaiderEvenIfBothFlagsAreSomehowSet()
		{
			// A mutation that checked KillerIsRaider before KillerIsPlayer would still pass every
			// case above except this one, because none of them set both flags at once.
			ClassicAssert.AreEqual(KingdomOfficeRules.DeathCause.Player, KingdomOfficeRules.ClassifyCause(true, true, true));
		}

		// --- CauseClause: every cause reads differently, and never claims more than it knows ----

		[Test]
		public void CauseClause_EveryCauseIsDistinctText()
		{
			string player = KingdomOfficeRules.CauseClause(KingdomOfficeRules.DeathCause.Player);
			string raid = KingdomOfficeRules.CauseClause(KingdomOfficeRules.DeathCause.Raid);
			string violence = KingdomOfficeRules.CauseClause(KingdomOfficeRules.DeathCause.Violence);
			string unknown = KingdomOfficeRules.CauseClause(KingdomOfficeRules.DeathCause.Unknown);
			ClassicAssert.AreNotEqual(player, raid);
			ClassicAssert.AreNotEqual(player, violence);
			ClassicAssert.AreNotEqual(player, unknown);
			ClassicAssert.AreNotEqual(raid, violence);
			ClassicAssert.AreNotEqual(raid, unknown);
			ClassicAssert.AreNotEqual(violence, unknown);
		}

		[Test]
		public void CauseClause_UnknownNeverNamesAKiller()
		{
			// The witnessed-only rule: with nothing reported, the clause must not claim a raid,
			// a blade, or the founder's own hand.
			string clause = KingdomOfficeRules.CauseClause(KingdomOfficeRules.DeathCause.Unknown).ToLowerInvariant();
			ClassicAssert.IsFalse(clause.Contains("raid"));
			ClassicAssert.IsFalse(clause.Contains("hand"));
		}

		// --- Epitaph: composes only from what is actually supplied ------------------------------

		[Test]
		public void Epitaph_IncludesOriginAndArrivalWhenBothAreGiven()
		{
			string text = KingdomOfficeRules.Epitaph("Mirrehet", "Ash Reach", "the 3rd of Tamuz, 218 AR", "Abram's Hold", "fell defending the stores when raiders came");
			ClassicAssert.IsTrue(text.Contains("Mirrehet"));
			ClassicAssert.IsTrue(text.Contains("Ash Reach"));
			ClassicAssert.IsTrue(text.Contains("the 3rd of Tamuz, 218 AR"));
			ClassicAssert.IsTrue(text.Contains("Abram's Hold"));
			ClassicAssert.IsTrue(text.Contains("fell defending the stores when raiders came"));
		}

		[Test]
		public void Epitaph_OmitsOriginClauseWhenOriginIsEmpty()
		{
			string withOrigin = KingdomOfficeRules.Epitaph("Mirrehet", "Ash Reach", "", "Abram's Hold", "was found gone, and no one living can say how");
			string withoutOrigin = KingdomOfficeRules.Epitaph("Mirrehet", "", "", "Abram's Hold", "was found gone, and no one living can say how");
			ClassicAssert.IsTrue(withOrigin.Contains(", of Ash Reach"));
			ClassicAssert.IsFalse(withoutOrigin.Contains(", of "));
		}

		[Test]
		public void Epitaph_OmitsArrivalClauseWhenArrivedIsEmpty()
		{
			string text = KingdomOfficeRules.Epitaph("Mirrehet", "", "", "Abram's Hold", "was found gone, and no one living can say how");
			// "who came to Abram's Hold" stands alone; " the " never follows it when Arrived is empty.
			ClassicAssert.IsFalse(text.Contains("Hold the "));
		}

		// --- MourningChronicle / MourningMessage: the settlement's own words for the moment ----

		[Test]
		public void MourningChronicle_NamesTheOriginWhenKnown()
		{
			string text = KingdomOfficeRules.MourningChronicle("Mirrehet", "Ash Reach", "Abram's Hold", KingdomOfficeRules.DeathCause.Raid);
			ClassicAssert.IsTrue(text.Contains("Mirrehet, of Ash Reach,"));
			ClassicAssert.IsTrue(text.Contains("Abram's Hold mourned"));
		}

		[Test]
		public void MourningChronicle_OmitsOriginClauseWhenOriginIsEmpty()
		{
			string text = KingdomOfficeRules.MourningChronicle("Mirrehet", "", "Abram's Hold", KingdomOfficeRules.DeathCause.Raid);
			ClassicAssert.IsFalse(text.Contains(", of ,"));
			ClassicAssert.IsTrue(text.StartsWith("Mirrehet "));
		}

		[Test]
		public void MourningMessage_NamesTheSettlerAndTheCause()
		{
			string text = KingdomOfficeRules.MourningMessage("Mirrehet", KingdomOfficeRules.DeathCause.Player);
			ClassicAssert.IsTrue(text.StartsWith("Mirrehet "));
			ClassicAssert.IsTrue(text.EndsWith("."));
		}

		// --- MemorialChronicle -------------------------------------------------------------------

		[Test]
		public void MemorialChronicle_NamesBothTheSettlerAndTheSettlement()
		{
			string text = KingdomOfficeRules.MemorialChronicle("Mirrehet", "Abram's Hold");
			ClassicAssert.IsTrue(text.Contains("Mirrehet"));
			ClassicAssert.IsTrue(text.Contains("Abram's Hold"));
		}

		// --- TryNextToHonour: the FIFO queue of unhonoured dead, defended against bad state -----

		[TestCase(0, 0, false, -1)]
		[TestCase(3, 0, true, 0)]
		[TestCase(3, 2, true, 2)]
		[TestCase(3, 3, false, -1)]
		[TestCase(3, -1, false, -1)]
		[TestCase(3, 4, false, -1)]
		public void TryNextToHonour_ReturnsTheOldestUnhonouredIndexOrRefuses(int deadCount, int memorialsRaised, bool expectedResult, int expectedIndex)
		{
			bool result = KingdomOfficeRules.TryNextToHonour(deadCount, memorialsRaised, out int index);
			ClassicAssert.AreEqual(expectedResult, result);
			ClassicAssert.AreEqual(expectedIndex, index);
		}

		// --- ClassifyTransition: every combination of before/after holder --------------------

		[Test]
		public void ClassifyTransition_NoneWhenNobodyHeldItAndNobodyDoesNow()
		{
			ClassicAssert.AreEqual(KingdomOfficeRules.OfficeTransition.None, KingdomOfficeRules.ClassifyTransition(null, null));
		}

		[Test]
		public void ClassifyTransition_NoneWhenTheSameSettlerStillHoldsIt()
		{
			ClassicAssert.AreEqual(KingdomOfficeRules.OfficeTransition.None, KingdomOfficeRules.ClassifyTransition("Mirrehet", "Mirrehet"));
		}

		[Test]
		public void ClassifyTransition_FirstHolderWhenTheSettlementHadNoOneAndNowDoes()
		{
			ClassicAssert.AreEqual(KingdomOfficeRules.OfficeTransition.FirstHolder, KingdomOfficeRules.ClassifyTransition(null, "Mirrehet"));
		}

		[Test]
		public void ClassifyTransition_PassedWhenADifferentSettlerNowHeadsTheRoll()
		{
			ClassicAssert.AreEqual(KingdomOfficeRules.OfficeTransition.Passed, KingdomOfficeRules.ClassifyTransition("Mirrehet", "Coshet"));
		}

		[Test]
		public void ClassifyTransition_VacantWhenTheLastHolderIsGoneAndNoOneIsLeft()
		{
			ClassicAssert.AreEqual(KingdomOfficeRules.OfficeTransition.Vacant, KingdomOfficeRules.ClassifyTransition("Mirrehet", null));
		}

		// --- ChooseTitle: stable across calls, bounded, never guessed from the runtime's own
		// randomized string hash -----------------------------------------------------------------

		[Test]
		public void ChooseTitle_EmptyOrNullSettlementTakesTheFirstTitle()
		{
			ClassicAssert.AreEqual(KingdomOfficeRules.OfficeTitles[0], KingdomOfficeRules.ChooseTitle(""));
			ClassicAssert.AreEqual(KingdomOfficeRules.OfficeTitles[0], KingdomOfficeRules.ChooseTitle(null));
		}

		[Test]
		public void ChooseTitle_IsStableAcrossRepeatedCalls()
		{
			// Pins out any dependency on string.GetHashCode(), which is reseeded every process
			// launch and would relabel a settlement's office on every restart.
			string first = KingdomOfficeRules.ChooseTitle("Abram's Hold");
			string second = KingdomOfficeRules.ChooseTitle("Abram's Hold");
			ClassicAssert.AreEqual(first, second);
		}

		// Pinned against a from-scratch FNV-1a-32 computation, so a mutation to the hash
		// constants, the mixing order, or the modulo arithmetic changes at least one of these.
		[TestCase("Abram's Hold", "the eldest")]
		[TestCase("Resheph's Harborage", "the water-keeper")]
		[TestCase("Testville", "who reads the charter aloud")]
		[TestCase("Longstanding Watervine Reach", "the first-poured")]
		[TestCase("a", "keeper of the well")]
		public void ChooseTitle_MatchesThePinnedHashForKnownNames(string settlementName, string expectedTitle)
		{
			ClassicAssert.AreEqual(expectedTitle, KingdomOfficeRules.ChooseTitle(settlementName));
		}

		[Test]
		public void ChooseTitle_ThePinnedNamesReachEveryTitleInThePool()
		{
			// If a mutation shrank the effective range (e.g. always mod 1, or ignored the hash
			// entirely), these five names would collapse onto fewer than five titles.
			string[] names = new string[5] { "Abram's Hold", "Resheph's Harborage", "Testville", "Longstanding Watervine Reach", "a" };
			bool[] seen = new bool[KingdomOfficeRules.OfficeTitles.Length];
			for (int i = 0; i < names.Length; i++)
			{
				string title = KingdomOfficeRules.ChooseTitle(names[i]);
				int at = System.Array.IndexOf(KingdomOfficeRules.OfficeTitles, title);
				ClassicAssert.GreaterOrEqual(at, 0, "title '" + title + "' is not in OfficeTitles");
				seen[at] = true;
			}
			for (int i = 0; i < seen.Length; i++)
			{
				ClassicAssert.IsTrue(seen[i], "OfficeTitles[" + i + "] (" + KingdomOfficeRules.OfficeTitles[i] + ") was never reached");
			}
		}

		// --- TransitionChronicle: named holder appears, and None is never announced ------------

		[Test]
		public void TransitionChronicle_NoneProducesNoLine()
		{
			ClassicAssert.AreEqual("", KingdomOfficeRules.TransitionChronicle(KingdomOfficeRules.OfficeTransition.None, "the eldest", "Mirrehet", "Abram's Hold"));
		}

		[Test]
		public void TransitionChronicle_FirstHolderNamesTheSettlerAndTheTitle()
		{
			string text = KingdomOfficeRules.TransitionChronicle(KingdomOfficeRules.OfficeTransition.FirstHolder, "the eldest", "Mirrehet", "Abram's Hold");
			ClassicAssert.IsTrue(text.Contains("Mirrehet"));
			ClassicAssert.IsTrue(text.Contains("the eldest"));
		}

		[Test]
		public void TransitionChronicle_PassedNamesTheNewHolder()
		{
			string text = KingdomOfficeRules.TransitionChronicle(KingdomOfficeRules.OfficeTransition.Passed, "the eldest", "Coshet", "Abram's Hold");
			ClassicAssert.IsTrue(text.Contains("Coshet"));
			ClassicAssert.IsTrue(text.Contains("passes"));
		}

		[Test]
		public void TransitionChronicle_VacantNamesTheSettlementNotAnyHolder()
		{
			string text = KingdomOfficeRules.TransitionChronicle(KingdomOfficeRules.OfficeTransition.Vacant, "the eldest", null, "Abram's Hold");
			ClassicAssert.IsTrue(text.Contains("Abram's Hold"));
			ClassicAssert.IsTrue(text.Contains("no one left"));
		}
	}
}
#endif
