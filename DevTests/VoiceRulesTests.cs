#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class VoiceRulesTests
	{
		/// <summary>A settlement id inside the frozen <c>taf:</c> grammar, as
		/// <c>KingdomChronicle.SettlementId</c> always produces.</summary>
		private const string Settlement = "taf:settlement:kyakukya";

		private const string OtherSettlement = "taf:settlement:joppa";

		/// <summary>
		/// How many ticks each sweep walks. A single tick proves nothing about a draw keyed on the
		/// tick; the sweeps below assert over a window instead.
		/// </summary>
		private const ulong Sweep = 64uL;

		private static readonly VoiceOccasion[] AllOccasions = new VoiceOccasion[5]
		{
			VoiceOccasion.StageUp,
			VoiceOccasion.RaidRepelled,
			VoiceOccasion.ThirstBroken,
			VoiceOccasion.MealShared,
			VoiceOccasion.CitizenLost
		};

		private static List<string> Roll()
		{
			return new List<string> { "Ptoh", "Nesh", "Ureme", "Sifrah", "Kesil", "Amaranth", "Bethesda", "Oboroqoru" };
		}

		private static List<string> Homes()
		{
			return new List<string>
			{
				"the salt marshes",
				"the desert canyons",
				"the hills",
				"the flower fields",
				"the rust wells",
				"the banana grove",
				"the salt marshes",
				"the hills"
			};
		}

		[Test]
		public void EmptyRollHasNoVoice()
		{
			KingdomVoiceRules.Speaker speaker = KingdomVoiceRules.ChooseSpeaker(new List<string>(), new List<string>(), Settlement, VoiceOccasion.StageUp, 100uL);
			Assert.IsFalse(speaker.HasVoice);
			Assert.IsNull(speaker.Name);
			Assert.AreEqual("a settler", speaker.Attribution);
		}

		[Test]
		public void NullRollHasNoVoice()
		{
			KingdomVoiceRules.Speaker speaker = KingdomVoiceRules.ChooseSpeaker(null, null, Settlement, VoiceOccasion.MealShared, 100uL);
			Assert.IsFalse(speaker.HasVoice);
		}

		[Test]
		public void ARollOfBlanksHasNoVoice()
		{
			KingdomVoiceRules.Speaker speaker = KingdomVoiceRules.ChooseSpeaker(new List<string> { "", null, "" }, null, Settlement, VoiceOccasion.StageUp, 7uL);
			Assert.IsFalse(speaker.HasVoice);
		}

		/// <summary>
		/// The whole point of the fallback: with nobody to speak, the settlement's own line still
		/// reaches the player unchanged. A missing speaker must never eat the message.
		/// </summary>
		[Test]
		public void NoVoiceLeavesTheAnnouncementWhole()
		{
			string announcement = "{{G|The water returned, and the settlement recovered.}}";
			Assert.AreEqual(announcement, KingdomVoiceRules.Compose(KingdomVoiceRules.Speaker.None, VoiceOccasion.ThirstBroken, announcement));
		}

		[Test]
		public void NoVoiceAndNothingToAnnounceSaysNothing()
		{
			Assert.AreEqual("", KingdomVoiceRules.Compose(KingdomVoiceRules.Speaker.None, VoiceOccasion.ThirstBroken, null));
		}

		[Test]
		public void TheSpeakerIsAlwaysSomebodyOnTheRoll()
		{
			List<string> roll = Roll();
			for (int i = 0; i < AllOccasions.Length; i++)
			{
				for (ulong tick = 0uL; tick < Sweep; tick++)
				{
					KingdomVoiceRules.Speaker speaker = KingdomVoiceRules.ChooseSpeaker(roll, Homes(), Settlement, AllOccasions[i], tick);
					Assert.IsTrue(speaker.HasVoice);
					Assert.IsTrue(roll.Contains(speaker.Name), "drew a name that is not on the roll: " + speaker.Name);
				}
			}
		}

		/// <summary>
		/// Two identical draws are the same person. This is the reload guarantee: a line already
		/// read must not be recast when the save is opened again.
		/// </summary>
		[Test]
		public void TwoIdenticalDrawsSpeakWithOneVoice()
		{
			for (ulong tick = 0uL; tick < Sweep; tick++)
			{
				KingdomVoiceRules.Speaker first = KingdomVoiceRules.ChooseSpeaker(Roll(), Homes(), Settlement, VoiceOccasion.CitizenLost, tick);
				KingdomVoiceRules.Speaker second = KingdomVoiceRules.ChooseSpeaker(Roll(), Homes(), Settlement, VoiceOccasion.CitizenLost, tick);
				Assert.AreEqual(first.Name, second.Name);
				Assert.AreEqual(first.Origin, second.Origin);
			}
		}

		/// <summary>
		/// Determinism must not be bought with a constant. If the draw were dropped and the roll's
		/// eldest always spoke, the test above would still pass and this one would not.
		/// </summary>
		[Test]
		public void DifferentMomentsFindDifferentSpeakers()
		{
			HashSet<string> heard = new HashSet<string>();
			for (ulong tick = 0uL; tick < Sweep; tick++)
			{
				heard.Add(KingdomVoiceRules.ChooseSpeaker(Roll(), Homes(), Settlement, VoiceOccasion.StageUp, tick).Name);
			}
			Assert.Greater(heard.Count, 1, "every tick drew the same speaker");
		}

		/// <summary>Guards the occasion's place in the draw key: drop it and a stage-up and a
		/// shared meal on one tick would always be spoken by the same settler.</summary>
		[Test]
		public void TheOccasionIsPartOfWhoSpeaks()
		{
			bool differed = false;
			for (ulong tick = 0uL; tick < Sweep && !differed; tick++)
			{
				string one = KingdomVoiceRules.ChooseSpeaker(Roll(), Homes(), Settlement, VoiceOccasion.StageUp, tick).Name;
				string other = KingdomVoiceRules.ChooseSpeaker(Roll(), Homes(), Settlement, VoiceOccasion.MealShared, tick).Name;
				differed = one != other;
			}
			Assert.IsTrue(differed, "the occasion never changed who spoke");
		}

		/// <summary>Guards the settlement id's place in the draw key: drop it and two cities of
		/// one realm would speak in lockstep.</summary>
		[Test]
		public void TheSettlementIsPartOfWhoSpeaks()
		{
			bool differed = false;
			for (ulong tick = 0uL; tick < Sweep && !differed; tick++)
			{
				string here = KingdomVoiceRules.ChooseSpeaker(Roll(), Homes(), Settlement, VoiceOccasion.RaidRepelled, tick).Name;
				string there = KingdomVoiceRules.ChooseSpeaker(Roll(), Homes(), OtherSettlement, VoiceOccasion.RaidRepelled, tick).Name;
				differed = here != there;
			}
			Assert.IsTrue(differed, "the settlement never changed who spoke");
		}

		/// <summary>
		/// A settlement id the kernel refuses costs the variety, never the voice: a real person
		/// still speaks, and still the same one twice.
		/// </summary>
		[TestCase("")]
		[TestCase(null)]
		[TestCase("taf:")]
		[TestCase("Kyakukya")]
		[TestCase("settlement:kyakukya")]
		public void AnUnusableSettlementIdStillFindsARealSpeaker(string BadId)
		{
			List<string> roll = Roll();
			KingdomVoiceRules.Speaker first = KingdomVoiceRules.ChooseSpeaker(roll, Homes(), BadId, VoiceOccasion.StageUp, 42uL);
			KingdomVoiceRules.Speaker second = KingdomVoiceRules.ChooseSpeaker(roll, Homes(), BadId, VoiceOccasion.StageUp, 99uL);
			Assert.IsTrue(first.HasVoice);
			Assert.AreEqual(roll[0], first.Name);
			Assert.AreEqual(first.Name, second.Name);
		}

		[Test]
		public void BlankEntriesOnTheRollAreSteppedOver()
		{
			List<string> roll = new List<string> { "", null, "Ptoh", "" };
			for (ulong tick = 0uL; tick < Sweep; tick++)
			{
				Assert.AreEqual("Ptoh", KingdomVoiceRules.ChooseSpeaker(roll, null, Settlement, VoiceOccasion.MealShared, tick).Name);
			}
		}

		[Test]
		public void TheOriginIsTheSpeakersOwn()
		{
			List<string> roll = Roll();
			List<string> homes = Homes();
			for (ulong tick = 0uL; tick < Sweep; tick++)
			{
				KingdomVoiceRules.Speaker speaker = KingdomVoiceRules.ChooseSpeaker(roll, homes, Settlement, VoiceOccasion.CitizenLost, tick);
				Assert.AreEqual(homes[roll.IndexOf(speaker.Name)], speaker.Origin);
			}
		}

		/// <summary>A roll trimmed unevenly by an old save still speaks, in the plain register.</summary>
		[Test]
		public void ASpeakerWhoseOriginIsLostStillSpeaks()
		{
			List<string> roll = Roll();
			List<string> homes = new List<string> { "the hills" };
			for (ulong tick = 0uL; tick < Sweep; tick++)
			{
				KingdomVoiceRules.Speaker speaker = KingdomVoiceRules.ChooseSpeaker(roll, homes, Settlement, VoiceOccasion.StageUp, tick);
				Assert.IsTrue(speaker.HasVoice);
				Assert.IsFalse(string.IsNullOrEmpty(KingdomVoiceRules.Line(VoiceOccasion.StageUp, speaker.Origin)));
			}
		}

		/// <summary>
		/// Binds the line table to <c>KingdomRules.Origins</c>: rename or add an origin there and
		/// this goes red rather than shipping a settler who answers in the plain register forever.
		/// </summary>
		[Test]
		public void EveryOriginSpeaksForItselfOnEveryOccasion()
		{
			for (int i = 0; i < AllOccasions.Length; i++)
			{
				VoiceOccasion occasion = AllOccasions[i];
				string plain = KingdomVoiceRules.Line(occasion, null);
				Assert.IsFalse(string.IsNullOrEmpty(plain));
				HashSet<string> said = new HashSet<string>();
				for (int j = 0; j < KingdomRules.Origins.Length; j++)
				{
					string line = KingdomVoiceRules.Line(occasion, KingdomRules.Origins[j]);
					Assert.IsFalse(string.IsNullOrEmpty(line), KingdomRules.Origins[j] + " has nothing to say about " + occasion);
					Assert.AreNotEqual(plain, line, KingdomRules.Origins[j] + " falls through to the plain register on " + occasion);
					Assert.IsTrue(said.Add(line), "two origins say the same thing about " + occasion);
				}
			}
		}

		[TestCase("the moon of nowhere")]
		[TestCase("")]
		[TestCase(null)]
		public void AnUnknownOriginAnswersInThePlainRegister(string Origin)
		{
			for (int i = 0; i < AllOccasions.Length; i++)
			{
				string line = KingdomVoiceRules.Line(AllOccasions[i], Origin);
				Assert.IsFalse(string.IsNullOrEmpty(line));
				Assert.AreEqual(KingdomVoiceRules.Line(AllOccasions[i], null), line);
			}
		}

		[Test]
		public void ComposePutsTheAnnouncementFirstAndTheVoiceAfter()
		{
			KingdomVoiceRules.Speaker speaker = new KingdomVoiceRules.Speaker("Ptoh", "the hills");
			string announcement = "{{C|Kyakukya has grown into a village.}}";
			string composed = KingdomVoiceRules.Compose(speaker, VoiceOccasion.StageUp, announcement);
			Assert.IsTrue(composed.StartsWith(announcement), composed);
			Assert.IsTrue(composed.Contains("Ptoh"), composed);
			Assert.IsTrue(composed.Contains(KingdomVoiceRules.Line(VoiceOccasion.StageUp, "the hills")), composed);
		}

		[Test]
		public void ComposeWithNothingToAnnounceIsTheQuoteAlone()
		{
			KingdomVoiceRules.Speaker speaker = new KingdomVoiceRules.Speaker("Ptoh", "the hills");
			string composed = KingdomVoiceRules.Compose(speaker, VoiceOccasion.StageUp, null);
			Assert.IsFalse(composed.StartsWith(" "), composed);
			Assert.IsTrue(composed.Contains("Ptoh"), composed);
		}

		/// <summary>
		/// The shared meal speaks to the size of the meal first and to the speaker's country
		/// second, and both are inside the one pair of quotes.
		/// </summary>
		[Test]
		public void AuthoredWordsAreSpokenBeforeTheSpeakersOwn()
		{
			KingdomVoiceRules.Speaker speaker = new KingdomVoiceRules.Speaker("Nesh", "the banana grove");
			string words = KingdomRules.MealSpeech(KingdomRules.PantryTier.Ample);
			string composed = KingdomVoiceRules.Compose(speaker, VoiceOccasion.MealShared, "{{G|A feast is shared.}}", words);
			int wordsAt = composed.IndexOf(words);
			int lineAt = composed.IndexOf(KingdomVoiceRules.Line(VoiceOccasion.MealShared, "the banana grove"));
			Assert.Greater(wordsAt, 0, composed);
			Assert.Greater(lineAt, wordsAt, composed);
			Assert.AreEqual(2, CountQuotes(composed), composed);
		}

		[Test]
		public void AuthoredWordsAreIgnoredWhenNobodyIsThereToSayThem()
		{
			string announcement = "{{G|A feast is shared.}}";
			string composed = KingdomVoiceRules.Compose(KingdomVoiceRules.Speaker.None, VoiceOccasion.MealShared, announcement, "It was good.");
			Assert.AreEqual(announcement, composed);
		}

		[Test]
		public void AttributionNamesTheSpeaker()
		{
			Assert.AreEqual("Ptoh", new KingdomVoiceRules.Speaker("Ptoh", "the hills").Attribution);
			Assert.AreEqual("a settler", new KingdomVoiceRules.Speaker("", "the hills").Attribution);
		}

		/// <summary>
		/// The kernel refuses a zero event kind, and each occasion is its own kind code. A zero
		/// here would cost that occasion its deterministic draw silently.
		/// </summary>
		[Test]
		public void NoOccasionCarriesTheReservedZeroCode()
		{
			for (int i = 0; i < AllOccasions.Length; i++)
			{
				Assert.AreNotEqual(0, (int)AllOccasions[i]);
			}
		}

		/// <summary>
		/// The codes are draw identity in every existing save. Renumbering one recasts every line
		/// it ever spoke, so they are pinned here rather than left to a refactor.
		/// </summary>
		[TestCase(VoiceOccasion.StageUp, 1)]
		[TestCase(VoiceOccasion.RaidRepelled, 2)]
		[TestCase(VoiceOccasion.ThirstBroken, 3)]
		[TestCase(VoiceOccasion.MealShared, 4)]
		[TestCase(VoiceOccasion.CitizenLost, 5)]
		public void OccasionCodesAreFrozen(VoiceOccasion Occasion, int Expected)
		{
			Assert.AreEqual(Expected, (int)Occasion);
		}

		private static int CountQuotes(string Text)
		{
			int count = 0;
			for (int i = 0; i < Text.Length; i++)
			{
				if (Text[i] == '"')
				{
					count++;
				}
			}
			return count;
		}
	}
}
#endif
