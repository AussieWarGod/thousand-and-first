using System;
using System.Collections.Generic;
using ThousandAndFirst.Harness;
using XRL.World;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		/// <summary>
		/// Machine witness for the camp guide's five topics, standing in for the attended
		/// five-topic traversal. The advisor is built by the PRODUCTION creator
		/// (<c>World/KingdomQuickstartBootstrap.Advisor.cs</c>) and then only read: the authored
		/// graph on <c>XRL.World.Parts.ConversationScript.Blueprint</c> is what
		/// <c>Qud.API.ConversationsAPI</c> wrote, so the choice list, its order, and every
		/// answer's return to Start are assertable without opening a dialogue window.
		/// </summary>
		internal static void NativeGuideTopicChecks(KingdomNativeRegressionContext Context)
		{
			Context.Case("guide-root-choice-order", () => NativeGuideRootChoices(Context));
			Context.Case("guide-answer-returns", () => NativeGuideAnswerReturns(Context));
			Context.Case("guide-graph-cardinality", () => NativeGuideCardinality(Context));
		}

		/// <summary>A fresh production advisor in the marsh role cell, tracked for disposal.</summary>
		private static ConversationXMLBlueprint NativeGuideGraph(
			KingdomNativeRegressionContext Context)
		{
			KingdomQuickstartReceipt receipt = NativeReceipt(Context,
				KingdomQuickstartPhase.AdvisorResolved);
			Context.Check(KingdomQuickstartRules.TryProfile("marsh",
				out KingdomQuickstartProfile profile), "marsh advisor profile must exist");
			GameObject advisor = NativeTrackFresh(Context,
				CreateAdvisor(Context.Game, Context.Zone, profile, receipt, out string failure));
			Context.Check(VerifyAdvisor(Context.Zone, advisor, receipt, out failure), failure);
			Context.Check(string.Equals(advisor.DisplayNameOnlyDirect, AdvisorName(profile),
				StringComparison.Ordinal), "the production creator did not name the marsh guide");
			ConversationXMLBlueprint graph = advisor.GetPart<ConversationScript>()?.Blueprint;
			Context.Check(graph != null && graph.Children != null,
				"the created guide carries no conversation graph");
			return graph;
		}

		/// <summary>Children of one blueprint carrying the given element name, in authored order.</summary>
		private static List<ConversationXMLBlueprint> NativeGuideElements(
			ConversationXMLBlueprint Parent, string Name)
		{
			List<ConversationXMLBlueprint> found = new List<ConversationXMLBlueprint>();
			if (Parent?.Children == null) return found;
			for (int i = 0; i < Parent.Children.Count; i++)
				if (string.Equals(Parent.Children[i].Name, Name, StringComparison.Ordinal))
					found.Add(Parent.Children[i]);
			return found;
		}

		/// <summary>The one Text child's words, or null when the element carries none.</summary>
		private static string NativeGuideText(ConversationXMLBlueprint Element)
		{
			List<ConversationXMLBlueprint> text = NativeGuideElements(Element, "Text");
			return text.Count == 1 ? text[0].Text : null;
		}

		/// <summary>
		/// Exactly six root choices: the house farewell the simple conversation wrote FIRST, then
		/// the five topics in the fixed order <c>Core/KingdomQuickstartGuideRules.cs</c> pins.
		/// </summary>
		private static void NativeGuideRootChoices(KingdomNativeRegressionContext Context)
		{
			ConversationXMLBlueprint graph = NativeGuideGraph(Context);
			ConversationXMLBlueprint start = graph.GetChild("Start");
			Context.Check(start != null && string.Equals(NativeGuideText(start),
				KingdomQuickstartGuideRules.Start, StringComparison.Ordinal),
				"the guide's opening words are not the pinned Start text");
			List<ConversationXMLBlueprint> choices = NativeGuideElements(start, "Choice");
			KingdomQuickstartGuideTopic[] topics = KingdomQuickstartGuideRules.Topics();
			Context.Check(topics.Length == KingdomQuickstartGuideRules.TopicCount
				&& topics.Length == 5, "the guide no longer offers exactly five topics");
			Context.Check(choices.Count == topics.Length + 1,
				"the guide's root offers " + choices.Count + " choices, not the farewell plus five");
			Context.Check(string.Equals(NativeGuideText(choices[0]),
				KingdomQuickstartGuideRules.Goodbye, StringComparison.Ordinal)
				&& string.Equals(choices[0]["Target"], "End", StringComparison.Ordinal),
				"the farewell is not the first root choice, ending the conversation");
			for (int i = 0; i < topics.Length; i++)
				Context.Check(string.Equals(NativeGuideText(choices[i + 1]), topics[i].Topic,
					StringComparison.Ordinal), "root choice " + (i + 1)
					+ " is not the pinned topic in its pinned order");
		}

		/// <summary>Every topic opens its own answer node, and every answer node returns to Start.</summary>
		private static void NativeGuideAnswerReturns(KingdomNativeRegressionContext Context)
		{
			ConversationXMLBlueprint graph = NativeGuideGraph(Context);
			List<ConversationXMLBlueprint> choices = NativeGuideElements(
				graph.GetChild("Start"), "Choice");
			KingdomQuickstartGuideTopic[] topics = KingdomQuickstartGuideRules.Topics();
			HashSet<string> targets = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < topics.Length; i++)
			{
				string target = choices[i + 1]["Target"];
				Context.Check(!string.IsNullOrEmpty(target) && targets.Add(target),
					"topic " + (i + 1) + " has no target of its own");
				ConversationXMLBlueprint answer = graph.GetChild(target);
				Context.Check(answer != null
					&& string.Equals(answer.Name, "Node", StringComparison.Ordinal),
					"topic " + (i + 1) + " does not open an answer node");
				Context.Check(string.Equals(NativeGuideText(answer), topics[i].Answer,
					StringComparison.Ordinal),
					"answer " + (i + 1) + " is not the pinned answer text");
				List<ConversationXMLBlueprint> back = NativeGuideElements(answer, "Choice");
				Context.Check(back.Count == 1
					&& string.Equals(back[0]["Target"], "Start", StringComparison.Ordinal)
					&& string.Equals(NativeGuideText(back[0]), "I have more to ask.",
						StringComparison.Ordinal),
					"answer " + (i + 1) + " does not route back to Start exactly once");
			}
		}

		/// <summary>No spare nodes, no repeated topic, and no second conversation on the guide.</summary>
		private static void NativeGuideCardinality(KingdomNativeRegressionContext Context)
		{
			ConversationXMLBlueprint graph = NativeGuideGraph(Context);
			Context.Check(NativeGuideElements(graph, "Start").Count == 1,
				"the guide's graph does not hold exactly one Start");
			Context.Check(NativeGuideElements(graph, "Node").Count
				== KingdomQuickstartGuideRules.TopicCount,
				"the guide's graph holds a node that answers no pinned topic");
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (KingdomQuickstartGuideTopic topic in KingdomQuickstartGuideRules.Topics())
				Context.Check(seen.Add(topic.Topic) && !string.IsNullOrEmpty(topic.Answer),
					"the pinned topic list repeats a question or answers none");
			Context.Check(NativeGuideElements(graph, "Choice").Count == 0,
				"a root choice was written outside Start, where the guide never offers it");
		}
	}
}
