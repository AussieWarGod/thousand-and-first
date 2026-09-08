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
			StringAssert.Contains("stands at nobody until someone comes to stay", start);
			StringAssert.Contains("nobody stays where no roof is standing", start);
			Assert.That(KingdomQuickstartGuideRules.Goodbye, Is.EqualTo("Live and drink."));
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
			StringAssert.Contains("Nobody joins a place with no roof standing",
				KingdomQuickstartGuideRules.Topics()[3].Answer);
			StringAssert.Contains("there are no hands",
				KingdomQuickstartGuideRules.Topics()[1].Answer);
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
