#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The camp guide's words are a contract, not decoration: they are the only teaching surface
	/// the Quickstart has, and everything they claim has to still be true of the settlement.
	/// </summary>
	[TestFixture]
	public sealed class KingdomQuickstartGuideRulesTests
	{
		private static readonly string[] ExpectedTopics =
		{
			"How was this place founded, and what ground is mine?",
			"How does anything get built here?",
			"What about water?",
			"Will anyone come?",
			"What comes after that — petitions, and raiders?"
		};

		[Test]
		public void GuideOffersFiveTopicsInOneFixedOrder()
		{
			KingdomQuickstartGuideTopic[] topics = KingdomQuickstartGuideRules.Topics();
			Assert.That(KingdomQuickstartGuideRules.TopicCount, Is.EqualTo(5));
			Assert.That(topics.Length, Is.EqualTo(5));
			for (int index = 0; index < ExpectedTopics.Length; index++)
			{
				Assert.That(topics[index].Topic, Is.EqualTo(ExpectedTopics[index]));
			}
		}

		[Test]
		public void EveryTopicAndAnswerIsPresentAndDistinct()
		{
			KingdomQuickstartGuideTopic[] topics = KingdomQuickstartGuideRules.Topics();
			HashSet<string> seenTopics = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> seenAnswers = new HashSet<string>(StringComparer.Ordinal);
			foreach (KingdomQuickstartGuideTopic topic in topics)
			{
				Assert.That(string.IsNullOrWhiteSpace(topic.Topic), Is.False);
				Assert.That(string.IsNullOrWhiteSpace(topic.Answer), Is.False);
				Assert.That(topic.Topic.Trim(), Is.EqualTo(topic.Topic));
				Assert.That(topic.Answer.Trim(), Is.EqualTo(topic.Answer));
				Assert.That(seenTopics.Add(topic.Topic), Is.True);
				Assert.That(seenAnswers.Add(topic.Answer), Is.True);
			}
		}

		/// <summary>A caller that blanks the array it was handed must not blank the guide.</summary>
		[Test]
		public void TopicsHandsOutAFreshArrayEachCall()
		{
			KingdomQuickstartGuideTopic[] first = KingdomQuickstartGuideRules.Topics();
			first[0] = new KingdomQuickstartGuideTopic();
			KingdomQuickstartGuideTopic[] second = KingdomQuickstartGuideRules.Topics();
			Assert.That(second[0].Topic, Is.EqualTo(ExpectedTopics[0]));
			Assert.That(second[0].Answer, Is.Not.Null);
			Assert.That(ReferenceEquals(first, second), Is.False);
		}

		[Test]
		public void StartKeepsTheInventoryAndNamesTheRollAndTheRoof()
		{
			string start = KingdomQuickstartGuideRules.Start;
			StringAssert.Contains("twenty-four drams", start);
			StringAssert.Contains("twelve meals", start);
			StringAssert.Contains("not on your roll", start);
			StringAssert.Contains("normally provides four founding citizens", start);
			StringAssert.Contains("two marked shelter plots", start);
			StringAssert.Contains("before recruiting more people", start);
			Assert.That(KingdomQuickstartGuideRules.Goodbye, Is.EqualTo("Live and drink."));
		}

		/// <summary>
		/// The opening inventory is spelled out in words, but the camp is stocked from constants.
		/// Tie the two together so raising a starter quantity cannot leave the guide quoting a
		/// number the founder will not find in the casks.
		/// </summary>
		[Test]
		public void TheSpelledInventoryMatchesTheQuantitiesTheCampIsStockedWith()
		{
			Dictionary<int, string> words = new Dictionary<int, string>
			{
				{ 1, "one" }, { 3, "three" }, { 4, "four" }, { 12, "twelve" },
				{ 24, "twenty-four" }
			};
			int[] quoted =
			{
				KingdomQuickstartRules.StarterWaterDrams,
				KingdomQuickstartRules.StarterFoodServings,
				KingdomQuickstartRules.StarterMud,
				KingdomQuickstartRules.StarterBrush,
				KingdomQuickstartRules.StarterTimber
			};
			foreach (int quantity in quoted)
			{
				Assert.That(words.ContainsKey(quantity), Is.True,
					"the guide has no word for " + quantity + "; its text must be rewritten");
			}
			string start = KingdomQuickstartGuideRules.Start;
			StringAssert.Contains(
				words[KingdomQuickstartRules.StarterWaterDrams] + " drams", start);
			StringAssert.Contains(
				words[KingdomQuickstartRules.StarterFoodServings] + " meals", start);
			string built = KingdomQuickstartGuideRules.Topics()[1].Answer;
			StringAssert.Contains("the chest holds "
				+ words[KingdomQuickstartRules.StarterMud] + " mud, "
				+ words[KingdomQuickstartRules.StarterBrush] + " brush and "
				+ words[KingdomQuickstartRules.StarterTimber] + " timber", built);
		}

		/// <summary>
		/// Nothing the guide says may promise what the settlement refuses. A welcomed guest is
		/// still put through the ordinary lodging transaction and is still refused where no roof
		/// stands, so the arrivals answer says exactly that and no answer offers hands.
		/// </summary>
		[Test]
		public void NoWordPromisesWhatTheSettlementRefuses()
		{
			string[] forbidden =
			{
				"KingdomBuilt", "SetIntProperty", "pair of hands", "will join",
				"guaranteed", "instantly"
			};
			foreach (string words in AllWords())
			{
				foreach (string banned in forbidden)
				{
					Assert.That(words.IndexOf(banned, StringComparison.OrdinalIgnoreCase),
						Is.LessThan(0), banned + " appears in: " + words);
				}
				Assert.That(words.Contains("["), Is.False, words);
				Assert.That(Regex.IsMatch(words, "[0-9]+ *, *[0-9]+"), Is.False, words);
			}
			StringAssert.Contains("a completed home with a spare bed",
				KingdomQuickstartGuideRules.Topics()[3].Answer);
			StringAssert.Contains("Founding citizens can work before they have homes",
				KingdomQuickstartGuideRules.Topics()[1].Answer);
			StringAssert.Contains("Read the first guest's correspondence",
				KingdomQuickstartGuideRules.Topics()[3].Answer);
			StringAssert.Contains("speak with the first guest",
				KingdomQuickstartGuideRules.Topics()[3].Answer);
			StringAssert.Contains("Welcome as citizen", KingdomQuickstartGuideRules.Topics()[3].Answer);
		}

		/// <summary>
		/// The guide may never state how many people are on the roll. The Quickstart is free to
		/// seed founding settlers with accommodation, or to seed none, and every word here has to
		/// survive that either way: the roll and the roofs are spoken of as rules, never counted.
		/// </summary>
		[Test]
		public void NoWordStatesTheCurrentSizeOfTheRoll()
		{
			string[] forbidden =
			{
				"stands at nobody", "counts nobody", "count stands", "with nobody on the roll",
				"nobody lives here", "you have no settlers", "the roll is empty until"
			};
			foreach (string words in AllWords())
			{
				foreach (string banned in forbidden)
				{
					Assert.That(words.IndexOf(banned, StringComparison.OrdinalIgnoreCase),
						Is.LessThan(0), banned + " states the roll size in: " + words);
				}
			}
		}

		/// <summary>
		/// The water answer names only work that actually returns drams, and states the real gate
		/// in front of it. A water wheel carries craft, not water, and every gatherer in the
		/// catalogue is Steading-gated, which is five living here plus somewhere to store it.
		/// </summary>
		[Test]
		public void TheWaterAnswerNamesRealGatherersAndTheRealGate()
		{
			string water = KingdomQuickstartGuideRules.Topics()[2].Answer;
			StringAssert.Contains("Dedicate a vessel", water);
			StringAssert.Contains("a camp may commission none of it", water);
			StringAssert.Contains("Five living here", water);
			Assert.That(water.IndexOf("wheel", StringComparison.OrdinalIgnoreCase),
				Is.LessThan(0), "a water wheel carries craft, not water: " + water);
		}

		/// <summary>
		/// The petitions-and-raiders answer states the raid lane as the code resolves it. Raids
		/// gate on founding and a water objective, never on population, and defence is zero
		/// outside a fortify answer &#8212; so the guide must not tie raiders to who lives here,
		/// and must not claim a standing wall defends by itself.
		/// </summary>
		[Test]
		public void ThePetitionsAndRaidersAnswerMatchesHowRaidsResolve()
		{
			string last = KingdomQuickstartGuideRules.Topics()[4].Answer;
			StringAssert.Contains("where nobody lives, nobody asks", last);
			StringAssert.Contains("costs you nothing but the asking", last);
			StringAssert.Contains("whether or not anybody lives in it", last);
			StringAssert.Contains("a wall nobody fortifies behind counts for nothing", last);
		}

		/// <summary>The words file must stay engine-free so it can be proved without a game.</summary>
		[Test]
		public void GuideWordsCarryNoEngineDependency()
		{
			string source = TestMain.ReadRepositoryText("Core/KingdomQuickstartGuideRules.cs");
			StringAssert.DoesNotContain("using XRL", source);
			StringAssert.DoesNotContain("using Qud", source);
			StringAssert.DoesNotContain("GameObject", source);
		}

		/// <summary>
		/// The advisor still gets exactly one conversation, built the same way, and the verifier
		/// still asks nothing about conversation shape &#8212; a shape predicate there would fail
		/// every save made before the topics existed.
		/// </summary>
		[Test]
		public void AdvisorBuildsOneConversationAndTheVerifierIgnoresIt()
		{
			string advisor = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Advisor.cs");
			Assert.That(Occurrences(advisor, "addSimpleConversationToObject("), Is.EqualTo(1));
			StringAssert.Contains("addSimpleRootInformationOption(advisor", advisor);
			StringAssert.Contains("KingdomQuickstartGuideRules.Start", advisor);
			StringAssert.Contains("KingdomQuickstartGuideRules.Goodbye", advisor);
			StringAssert.Contains("KingdomQuickstartGuideRules.Topics()", advisor);
			StringAssert.DoesNotContain("ConversationScript", advisor);
			StringAssert.DoesNotContain("Blueprint", advisor);
		}

		private static IEnumerable<string> AllWords()
		{
			List<string> words = new List<string>();
			words.Add(KingdomQuickstartGuideRules.Start);
			words.Add(KingdomQuickstartGuideRules.Goodbye);
			foreach (KingdomQuickstartGuideTopic topic in KingdomQuickstartGuideRules.Topics())
			{
				words.Add(topic.Topic);
				words.Add(topic.Answer);
			}
			return words;
		}

		private static int Occurrences(string source, string needle)
		{
			int count = 0;
			for (int index = source.IndexOf(needle, StringComparison.Ordinal); index >= 0;
				index = source.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
			{
				count++;
			}
			return count;
		}
	}
}
#endif
