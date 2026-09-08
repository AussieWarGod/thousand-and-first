using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// One question the camp guide will answer, and the answer.
	/// <para>
	/// The pair is exactly what <c>Qud.API.ConversationsAPI.addSimpleRootInformationOption</c>
	/// wants: the topic is the root choice the founder reads, the answer is the node it opens, and
	/// the engine wires that node back to Start by itself.
	/// </para>
	/// </summary>
	public readonly struct KingdomQuickstartGuideTopic
	{
		/// <summary>The root choice, in the founder's mouth.</summary>
		public readonly string Topic;

		/// <summary>What the guide says back.</summary>
		public readonly string Answer;

		internal KingdomQuickstartGuideTopic(string topic, string answer)
		{
			Topic = topic;
			Answer = answer;
		}
	}

	/// <summary>
	/// Every word the optional Quickstart camp guide knows, kept away from the engine so the words
	/// can be read and proved without a game.
	/// <para>
	/// The guide is a wayfarer, not a citizen: he holds nothing, gives nothing, and is verified
	/// benefit-free on every recovery pass. So the only thing he can be widened with is TRUE
	/// information about the rules the founder is already standing inside. Nothing here may
	/// promise an arrival, a pair of hands, or a finished building &#8212; the settlement refuses
	/// all three until a roof stands and somebody lives under it, and a guide that said otherwise
	/// would leave a player waiting for something that never comes.
	/// </para>
	/// </summary>
	public static class KingdomQuickstartGuideRules
	{
		/// <summary>
		/// What he says when the conversation opens.
		/// <para>
		/// The inventory sentence is the original one, unchanged. What follows it answers the two
		/// questions the opening raises and the old line left standing: who the man beside the
		/// casks is, and why the settlement's count reads nobody with him in plain sight. Both are
		/// stated as the rules state them &#8212; he is off the roll, and the roll stays empty
		/// until somebody stays, which nobody does where no roof is standing.
		/// </para>
		/// </summary>
		public const string Start =
			"Count what is here, founder, not what you wish were here. The casks hold "
			+ "twenty-four drams and the larder twelve meals. They make nothing. "
			+ "Raise shelter, then give hands and ground to the works that gather food "
			+ "and water; only such work replaces what the city spends. I am not on your roll. "
			+ "I pass through, and I am not counted. Your count stands at nobody until someone "
			+ "comes to stay, and nobody stays where no roof is standing.";

		/// <summary>How he ends it. The house farewell.</summary>
		public const string Goodbye = "Live and drink.";

		/// <summary>
		/// The fixed order the topics are offered in: what was already done here, what the founder
		/// does next, what he drinks while doing it, who may turn up, and what turns up after
		/// them. The order is part of the teaching, and the tests pin it.
		/// </summary>
		private static readonly string[][] Pairs = new string[5][]
		{
			new string[2]
			{
				"How was this place founded, and what ground is mine?",
				"The heart was set before you woke, by the ordinary rite and no other way. "
					+ "What the realm holds is the ground around that heart: the cells swept "
					+ "for you and the way in. Land past it is nobody's until the settlement "
					+ "reaches it. Your charter will tell you what is held, and it will not "
					+ "flatter you about the rest."
			},
			new string[2]
			{
				"How does anything get built here?",
				"You commission a design and the ground is marked. Nothing rises off a mark. "
					+ "Every design names the materials it eats, and the stores must already "
					+ "hold them; the chest holds one mud, three brush and four timber, and "
					+ "nothing adds to it but work. Then the design wants hands — settlers "
					+ "who live here and are not already spoken for. With nobody on the roll "
					+ "there are no hands, and a commission is a shape in the dirt that waits."
			},
			new string[2]
			{
				"What about water?",
				"What the casks hold is what you carried. Nothing in them fills again. "
					+ "Dedicate a vessel and the settlement keeps its water in one place where "
					+ "it can be counted; after that only work that gathers — a catchment, "
					+ "an air-well, a wheel set over water — puts drams back. Drink from "
					+ "the casks and the number goes down and stays down."
			},
			new string[2]
			{
				"Will anyone come?",
				"People pass. One of them may ask to be taken in, and asking is not staying. "
					+ "Nobody joins a place with no roof standing — not a stranger walking "
					+ "up, not a guest welcomed at your own fire. Raise a roof and keep it "
					+ "standing, and an arrival has somewhere to be put and the roll can "
					+ "begin. Do not spend a face before it comes."
			},
			new string[2]
			{
				"What comes after that — petitions, and raiders?",
				"Settlers put questions to you and wait on the answer; refuse them all and "
					+ "they remember which refusal was theirs. Others come armed and ask "
					+ "nothing. A wall, and people willing to stand behind it, is the only "
					+ "answer to the second kind, and it has to be standing before it is "
					+ "wanted. Both wait on the same thing you do: somebody living here."
			}
		};

		/// <summary>How many topics the guide offers. Fixed.</summary>
		public static int TopicCount
		{
			get { return Pairs.Length; }
		}

		/// <summary>
		/// The topics, in their fixed order.
		/// <para>
		/// Preconditions: none. Side effects: none. Failure mode: none &#8212; total. A fresh
		/// array is handed out on each call, so no caller can reorder or blank the guide's words
		/// for the next one.
		/// </para>
		/// </summary>
		public static KingdomQuickstartGuideTopic[] Topics()
		{
			KingdomQuickstartGuideTopic[] topics
				= new KingdomQuickstartGuideTopic[Pairs.Length];
			for (int index = 0; index < Pairs.Length; index++)
			{
				topics[index] = new KingdomQuickstartGuideTopic(Pairs[index][0], Pairs[index][1]);
			}
			return topics;
		}
	}
}
