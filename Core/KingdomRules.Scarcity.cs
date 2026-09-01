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

		/// <summary>Legacy save/API constant. Food no longer advances a scarcity ladder.</summary>
		public const int HungryIntervalsToEmigrate = 2;

		/// <summary>Legacy save/API constant. Food no longer creates a famine mark.</summary>
		public const int HungryIntervalsToFamine = 3;

		/// <summary>Legacy wire vocabulary. New resolutions always project
		/// <see cref="HungerOutcome.Fed"/>.</summary>
		public enum HungerOutcome
		{
			Fed,
			Warned,
			Emigration,
			Famine
		}

		/// <summary>Neutral compatibility projection. Missing food never causes a penalty.</summary>
		public static HungerOutcome ResolveHunger(int HungerStreak, GrowthStage Stage, int Population)
		{
			return HungerOutcome.Fed;
		}

		/// <summary>How hard water scarcity bites. Ordinals remain stable for old receipts.</summary>
		public enum ScarcityBite
		{
			/// <summary>Water paid. Nothing happens.</summary>
			None = 0,

			/// <summary>Water came up short, and nobody leaves yet.</summary>
			Warned = 1,

			/// <summary>Exactly one settler leaves for water.</summary>
			Departure = 2,

			/// <summary>One settler leaves and water marks the settlement withered.</summary>
			Terminal = 3
		}

		/// <summary>Water scarcity result with inert legacy food projections.</summary>
		public struct ScarcityVerdict
		{
			/// <summary>The water ladder's bite.</summary>
			public ScarcityBite Bite;

			/// <summary>The water ladder came up short this resolve.</summary>
			public bool Thirsting;

			/// <summary>Legacy projection. Always false in new verdicts.</summary>
			public bool Starving;

			/// <summary>The thirst ladder reached its end and the settlement should be marked
			/// withered.</summary>
			public bool Withering;

			/// <summary>Legacy projection. Always false in new verdicts.</summary>
			public bool Famishing;

			/// <summary>Whether water scarcity permits an arrival.</summary>
			public bool Healthy => !Thirsting;
		}

		/// <summary>Composes water with an ignored legacy hunger value.</summary>
		public static ScarcityVerdict ComposeScarcity(ThirstOutcome Thirst, HungerOutcome Hunger)
		{
			ScarcityVerdict verdict = default(ScarcityVerdict);
			verdict.Thirsting = (Thirst != ThirstOutcome.Sustained);
			verdict.Starving = false;
			verdict.Withering = (Thirst == ThirstOutcome.Withering);
			verdict.Famishing = false;
			verdict.Bite = BiteOfThirst(Thirst);
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

		/// <summary>Neutral compatibility projection. Hunger has no consequence.</summary>
		public static ScarcityBite BiteOfHunger(HungerOutcome Outcome)
		{
			return ScarcityBite.None;
		}

		/// <summary>Water departure clause. Legacy food argument is ignored.</summary>
		public static string ScarcityDepartureClause(bool Thirsting, bool Starving)
		{
			return Thirsting ? "for wetter country, the cisterns having run dry" : null;
		}

		/// <summary>Water departure note. Legacy food argument is ignored.</summary>
		public static string ScarcityDepartureNote(bool Thirsting, bool Starving)
		{
			return Thirsting ? "for wetter country" : null;
		}

	}
}
