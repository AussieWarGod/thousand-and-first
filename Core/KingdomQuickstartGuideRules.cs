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
	/// all three until a roof stands with room under it and somebody lives there, and a guide that
	/// said otherwise would leave a player waiting for something that never comes.
	/// </para>
	/// <para>
	/// Nothing here may state the roll's current size either. The guide speaks about roofs, hands
	/// and later arrivals as RULES, so every word stays true whether the camp was seeded with
	/// founding settlers or with nobody at all.
	/// </para>
	/// </summary>
	public static class KingdomQuickstartGuideRules
	{
		/// <summary>
		/// What he says when the conversation opens.
		/// <para>
		/// The inventory sentence is the original one, unchanged. What follows it answers the
		/// question the opening raises and the old line left standing: who the man beside the
		/// casks is. He is off the roll, and he points at the two numbers that actually decide
		/// what the settlement can do &#8212; roofs with room, and hands not already spoken for.
		/// </para>
		/// </summary>
		public const string Start =
			"Count what is here, founder, not what you wish were here. The casks hold "
			+ "twenty-four drams and the larder twelve meals. They make nothing. "
			+ "Raise shelter, then give hands and ground to the works that gather food "
			+ "and water; only such work replaces what the city spends. I am not on your roll. "
			+ "I pass through, and I am not counted. Read the roll and the roofs instead: hands "
			+ "come off the roll, and nobody new stays unless a roof stands with room left in it.";

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
				"The heart was set before you woke, by the ordinary founding and no other way. "
					+ "What the realm holds is the ground around that heart, and no more of it "
					+ "than the settlement has reached; land past that is nobody's. Your charter "
					+ "will tell you what is held, and it will not flatter you about the rest."
			},
			new string[2]
			{
				"How does anything get built here?",
				"You commission a design and the ground is marked. Nothing rises off a mark. "
					+ "Every design names the materials it eats, and the stores must already "
					+ "hold them; the chest holds one mud, three brush and four timber, and "
					+ "nothing adds to it but work. Then the design wants hands — settlers who "
					+ "live here and are not already spoken for. Where the roll is empty, or "
					+ "every name on it is busy, a commission is a shape in the dirt that waits."
			},
			new string[2]
			{
				"What about water?",
				"What the casks hold is what you carried. Nothing in them fills again. "
					+ "Dedicate a vessel and the settlement keeps its water in one place where "
					+ "it can be counted. Putting drams back is other work — a pan worked for "
					+ "what the brine was hiding, canvas and gutters under a cold night, a damp "
					+ "seam followed back into rock — and a camp may commission none of it. "
					+ "Five living here, and somewhere to store what they draw, is the price of "
					+ "that ladder. Until then the number only goes down."
			},
			new string[2]
			{
				"Will anyone come?",
				"People pass. One of them may ask to be taken in, and asking is not staying. "
					+ "Nobody joins a place that has no roof with room left under it — not a "
					+ "stranger walking up, not a guest welcomed at your own fire. The welcome "
					+ "is not the transaction; the bed is. Raise housing before it is wanted "
					+ "and an arrival has somewhere to be put. Do not spend a face before it "
					+ "comes."
			},
			new string[2]
			{
				"What comes after that — petitions, and raiders?",
				"Settlers put questions to you and wait on the answer; where nobody lives, "
					+ "nobody asks, and declining one costs you nothing but the asking. "
					+ "Raiders neither ask nor wait to be invited — they come for the water, "
					+ "and a settlement holding some is worth the walk whether or not anybody "
					+ "lives in it. What you have raised counts only when you answer the raid: "
					+ "a wall nobody fortifies behind counts for nothing."
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
