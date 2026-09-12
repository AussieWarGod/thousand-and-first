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
		/// promise an arrival or a finished building. Describe the founding provisions separately
		/// from later recruitment, which requires a completed roof with spare beds.
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
			"Quickstart normally provides four founding citizens and two marked shelter plots. "
			+ "Open your charter to check the citizen roll and construction. Let the citizens finish "
			+ "those shelters before recruiting more people; keep the plots clear. "
			+ "The casks start with forty-eight drams and the larder twelve meals. Refill them: "
			+ "they do not produce supplies. I am a visiting guide, not on your roll, and I do not build.";

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
				"The two starter shelters are already marked for construction. Founding citizens "
					+ "can work before they have homes; you do not need to recruit a guest first. "
					+ "Keep the shelter plots clear and let time pass. For additional buildings, "
					+ "commission a design through your charter and supply its listed materials; "
					+ "the chest holds one mud, three brush and four timber at the start. "
					+ "Construction needs citizens available for work. If everyone is busy, "
					+ "free workers from other jobs. A marked plot is not a completed building."
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
				"A visitor is not a citizen and does not build for you. New citizens need "
					+ "a completed home with a spare bed. Finish the starter shelters first. "
					+ "When the first guest arrives, open your charter and choose Read the first "
					+ "guest's correspondence, or interact with the guest and choose speak with "
					+ "the first guest. Choose Welcome as citizen when housing is available. "
					+ "A guest can remain your guest while you finish a home."
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
