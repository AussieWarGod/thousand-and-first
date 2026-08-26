namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{

		public static readonly string[] Districts = new string[6] { "agrarian", "market", "craft", "shrine", "garrison", "academy" };

		public static readonly string[] DistrictNames = new string[6] { "vinelands", "bazaar", "forgeworks", "sacred ground", "watch", "scriptorium" };

		public static string DistrictName(string District)
		{
			for (int i = 0; i < Districts.Length; i++)
			{
				if (Districts[i] == District)
				{
					return DistrictNames[i];
				}
			}
			return District;
		}

		public enum ThirstOutcome
		{
			Sustained,
			Warned,
			Emigration,
			Withering
		}

		public static ThirstOutcome ResolveThirst(int DryStreak, GrowthStage Stage, int Population)
		{
			if (DryStreak <= 0)
			{
				return ThirstOutcome.Sustained;
			}
			if (DryStreak >= DryIntervalsToWither && Stage > GrowthStage.Camp)
			{
				return ThirstOutcome.Withering;
			}
			if (DryStreak >= DryIntervalsToEmigrate && Population > LoyalCoreSettlers)
			{
				return ThirstOutcome.Emigration;
			}
			return ThirstOutcome.Warned;
		}

		/// <summary>Failed resolves before the hungry begin to leave. The mirror of
		/// <see cref="DryIntervalsToEmigrate"/>, and the same number: a settlement gets exactly
		/// one warned resolve of either kind before anybody walks.</summary>
		public const int HungryIntervalsToEmigrate = 2;

		/// <summary>Failed resolves before the settlement itself is marked. The mirror of
		/// <see cref="DryIntervalsToWither"/>.</summary>
		public const int HungryIntervalsToFamine = 3;

		/// <summary>
		/// The food ladder's rungs, in food's own voice. Shaped exactly like
		/// <see cref="ThirstOutcome"/> because the two are composed against each other
		/// (<see cref="ComposeScarcity"/>) and a ladder with a different number of rungs could
		/// not be.
		/// </summary>
		public enum HungerOutcome
		{
			Fed,
			Warned,
			Emigration,
			Famine
		}

		/// <summary>
		/// What a failed feeding costs, by how many resolves in a row the larders have come up
		/// short. Identical in shape to <see cref="ResolveThirst"/>, including the two floors: a
		/// settlement at or below <see cref="LoyalCoreSettlers"/> never loses anybody, and a Camp
		/// is never marked &mdash; there is no rung under it to fall to and nothing to be the
		/// ruin of.
		/// </summary>
		public static HungerOutcome ResolveHunger(int HungerStreak, GrowthStage Stage, int Population)
		{
			if (HungerStreak <= 0)
			{
				return HungerOutcome.Fed;
			}
			if (HungerStreak >= HungryIntervalsToFamine && Stage > GrowthStage.Camp)
			{
				return HungerOutcome.Famine;
			}
			if (HungerStreak >= HungryIntervalsToEmigrate && Population > LoyalCoreSettlers)
			{
				return HungerOutcome.Emigration;
			}
			return HungerOutcome.Warned;
		}

		// ==================================================================================
		// COMPOSING THE TWO LADDERS. A settlement can be dry and starving at once, and both
		// ladders run: each keeps its own streak, each says its own sentence, each sets its own
		// mark. What they must NOT do is bite twice.
		//
		// THE RULE: one departure per resolve, whatever is wrong. The bite of a resolve is the
		// WORSE of the two ladders and never their sum, so a settlement that is both dry and
		// starving loses people at exactly the rate the worse of the two alone would - never
		// faster. This is the same bound the thirst ladder already promised on its own ("the
		// ladder steps once per failed resolve, so one homecoming can cost at most one rung
		// however long the founder was gone"); all this does is keep that promise true when
		// there are two ladders to step.
		//
		// WHAT IS NOT BOUNDED, on purpose: the MARKS. Withered and Famished are states, not
		// costs, and a settlement that is genuinely both should read as both. And subsidence is
		// left entirely alone - it is the STRUCTURAL consequence of standing above what the
		// works carry, on its own clock and its own step size, while this is the IMMEDIATE one.
		// A settlement whose fields have failed is losing people to hunger now and settling back
		// toward a lower level over the season, and those are two different sentences about the
		// same bad year rather than one counted twice.
		// ==================================================================================

		/// <summary>How hard one heartbeat resolve bites, once both scarcity ladders have been
		/// heard. Ordered, so composing is a maximum.</summary>
		public enum ScarcityBite
		{
			/// <summary>Both ladders paid. Nothing happens and both streaks clear.</summary>
			None = 0,

			/// <summary>At least one came up short, and nobody leaves for it yet.</summary>
			Warned = 1,

			/// <summary>Exactly one settler leaves, whatever the number of things wrong.</summary>
			Departure = 2,

			/// <summary>One settler leaves, and the settlement wears the mark of whichever
			/// ladder(s) reached the end.</summary>
			Terminal = 3
		}

		/// <summary>The whole of what a resolve owes, so a caller applies it once rather than
		/// running two ladders past two copies of the same consequence.</summary>
		public struct ScarcityVerdict
		{
			/// <summary>The worse of the two ladders. Never their sum.</summary>
			public ScarcityBite Bite;

			/// <summary>The water ladder came up short this resolve.</summary>
			public bool Thirsting;

			/// <summary>The food ladder came up short this resolve.</summary>
			public bool Starving;

			/// <summary>The thirst ladder reached its end and the settlement should be marked
			/// withered.</summary>
			public bool Withering;

			/// <summary>The hunger ladder reached its end and the settlement should be marked
			/// famished.</summary>
			public bool Famishing;

			/// <summary>Whether the settlement is healthy enough this resolve to take an
			/// arrival. A settler does not walk into a place that cannot water or feed the
			/// people already in it.</summary>
			public bool Healthy => !Thirsting && !Starving;
		}

		/// <summary>
		/// Resolves the two scarcity ladders into the one thing that actually happens. See the
		/// block comment above for the rule and why it is the rule.
		/// </summary>
		/// <param name="Thirst">The water ladder's own answer, from <see cref="ResolveThirst"/>.
		/// Pass <see cref="ThirstOutcome.Sustained"/> when the settlement drank its fill (or when
		/// thirst is switched off), which is what makes this safe to call unconditionally.</param>
		/// <param name="Hunger">The food ladder's own answer, from <see cref="ResolveHunger"/>.
		/// Pass <see cref="HungerOutcome.Fed"/> when the settlement ate.</param>
		public static ScarcityVerdict ComposeScarcity(ThirstOutcome Thirst, HungerOutcome Hunger)
		{
			ScarcityVerdict verdict = default(ScarcityVerdict);
			verdict.Thirsting = (Thirst != ThirstOutcome.Sustained);
			verdict.Starving = (Hunger != HungerOutcome.Fed);
			verdict.Withering = (Thirst == ThirstOutcome.Withering);
			verdict.Famishing = (Hunger == HungerOutcome.Famine);
			ScarcityBite fromThirst = BiteOfThirst(Thirst);
			ScarcityBite fromHunger = BiteOfHunger(Hunger);
			verdict.Bite = (fromThirst > fromHunger) ? fromThirst : fromHunger;
			return verdict;
		}

		/// <summary>One thirst rung's own bite, for <see cref="ComposeScarcity"/>'s maximum.</summary>
		public static ScarcityBite BiteOfThirst(ThirstOutcome Outcome)
		{
			switch (Outcome)
			{
			case ThirstOutcome.Withering:
				return ScarcityBite.Terminal;
			case ThirstOutcome.Emigration:
				return ScarcityBite.Departure;
			case ThirstOutcome.Warned:
				return ScarcityBite.Warned;
			default:
				return ScarcityBite.None;
			}
		}

		/// <summary>One hunger rung's own bite, for <see cref="ComposeScarcity"/>'s maximum.</summary>
		public static ScarcityBite BiteOfHunger(HungerOutcome Outcome)
		{
			switch (Outcome)
			{
			case HungerOutcome.Famine:
				return ScarcityBite.Terminal;
			case HungerOutcome.Emigration:
				return ScarcityBite.Departure;
			case HungerOutcome.Warned:
				return ScarcityBite.Warned;
			default:
				return ScarcityBite.None;
			}
		}

		/// <summary>
		/// The clause both registers name a scarcity departure by, in the chronicle's voice.
		/// One sentence for both causes when both are true, because the person leaving had one
		/// reason and it was that this place had neither.
		/// </summary>
		/// <param name="Thirsting">The water ladder was short this resolve.</param>
		/// <param name="Starving">The food ladder was short this resolve.</param>
		/// <returns>Null when neither is true &mdash; there is no departure to name.</returns>
		public static string ScarcityDepartureClause(bool Thirsting, bool Starving)
		{
			if (Thirsting && Starving)
			{
				return "for water and bread both, and this place had neither";
			}
			if (Starving)
			{
				return "for a fuller table, the larders having emptied";
			}
			return Thirsting ? "for wetter country, the cisterns having run dry" : null;
		}

		/// <summary>The same departure in the ledger's shorter voice, kept beside the chronicle's
		/// so the two registers can never disagree about why somebody left.</summary>
		/// <returns>Null when neither is true.</returns>
		public static string ScarcityDepartureNote(bool Thirsting, bool Starving)
		{
			if (Thirsting && Starving)
			{
				return "for water and bread both";
			}
			if (Starving)
			{
				return "for a fuller table";
			}
			return Thirsting ? "for wetter country" : null;
		}

	}
}
