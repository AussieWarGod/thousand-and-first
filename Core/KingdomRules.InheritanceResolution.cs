namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		/// <summary>
		/// Resolves the sealed condition and the interregnum draw into the state the settlement is
		/// found in.
		/// <para>
		/// Fortune shifts the outcome but never decides it alone: a settlement sealed at full
		/// vigour survives the worst draw there is, and a dying camp survives none of them.
		/// Between those two ends the draw is what makes the story.
		/// </para>
		/// <para>
		/// One floor overrides the arithmetic: a settlement sealed with nobody in it is never
		/// found inhabited. Withering needs no floor and takes no parameter here &mdash; it is
		/// already sealed into the vigour, which is capped low enough that
		/// <see cref="InheritedState.Held"/> is unreachable for a withered settlement as a
		/// property of the arithmetic.
		/// </para>
		/// </summary>
		/// <param name="Vigour">The sealed condition, from <see cref="SealedVigour"/>.</param>
		/// <param name="Roll">The interregnum draw, from <see cref="InterregnumRoll"/>.</param>
		/// <param name="Population">Population at sealing, for the empty-settlement floor.</param>
		public static InheritedState ResolveInheritedState(int Vigour, int Roll, int Population)
		{
			int vigour = (Vigour > 0) ? Vigour : 0;
			if (vigour > MaxSealedVigour)
			{
				vigour = MaxSealedVigour;
			}
			int roll = Roll;
			if (roll < 0)
			{
				roll = 0;
			}
			if (roll > 99)
			{
				roll = 99;
			}

			int fate = vigour - roll * InterregnumSwing / 99;
			InheritedState state;
			if (fate >= HoldsAt)
			{
				state = InheritedState.Held;
			}
			else if (fate >= FadesAt)
			{
				state = InheritedState.Faded;
			}
			else if (fate >= EmptiesAt)
			{
				state = InheritedState.Abandoned;
			}
			else
			{
				state = InheritedState.Ruins;
			}

			// No floor for withering. It reads like one is needed, but a withered seal is capped
			// at half the ceiling and holding needs more than that, so the arithmetic already
			// makes Held unreachable. An if-branch that can never fire is worse than no branch:
			// it claims a guarantee nobody is checking. The invariant is proved by test instead.
			if (Population <= 0 && state < InheritedState.Abandoned)
			{
				state = InheritedState.Abandoned;
			}
			return state;
		}

		/// <summary>
		/// How many people are still there to be found. Nobody remains at
		/// <see cref="InheritedState.Abandoned"/> or below; a faded settlement keeps half, and
		/// never rounds its last inhabitant away.
		/// <para>
		/// These are successors, not the old roll walking around again. The named roll crosses as
		/// history; the people who greet a later founder are their descendants.
		/// </para>
		/// </summary>
		public static int InheritedPopulation(int Population, InheritedState State)
		{
			// An unrecognised state must fail closed. Read naively, a cast-garbage negative is
			// neither >= Abandoned nor == Faded, and would fall through to handing back the whole
			// population of a settlement nobody has established still exists.
			if (!IsKnownState(State))
			{
				return 0;
			}
			if (Population <= 0 || State >= InheritedState.Abandoned)
			{
				return 0;
			}
			if (State == InheritedState.Faded)
			{
				return (Population > 1) ? (Population / 2) : 1;
			}
			return Population;
		}

		/// <summary>Whether a state is one this build defines. Anything else fails closed.</summary>
		public static bool IsKnownState(InheritedState State)
		{
			return State >= InheritedState.Held && State <= InheritedState.Ruins;
		}

		/// <summary>
		/// Whether <b>every</b> work still stands, so the settlement can be reoccupied as it was.
		/// <para>
		/// Deliberately named for all rather than any. This was <c>WorksSurvive</c>, which read as
		/// "anything survives" and flatly contradicted <see cref="StandingPercent"/> telling the
		/// caller that a quarter to three-fifths of a ruin is still up. Ask this when deciding
		/// whether to place the settlement intact; ask <see cref="StandingPercent"/> when deciding
		/// how much of it to place.
		/// </para>
		/// <para>
		/// <see cref="InheritedState.Abandoned"/> answers true: it is intact and derelict, empty
		/// rather than damaged, which is the whole point of it.
		/// </para>
		/// </summary>
		public static bool AllWorksSurvive(InheritedState State)
		{
			// Fails closed for the same reason as InheritedPopulation: a cast-garbage negative
			// compares as less than Ruins and would otherwise promise intact structures.
			return IsKnownState(State) && State < InheritedState.Ruins;
		}

		/// <summary>
		/// Fraction of a ruined settlement's structures left standing, as a percentage.
		/// <para>
		/// Ruination is applied by a deterministic transform of our own onto a fresh
		/// reconstruction canvas. It must never be delegated to the engine's <c>Ruiner</c>, which
		/// detonates explosions across a live zone: that would damage whatever else the new world
		/// had already put there, and would make <see cref="InheritedState.Abandoned"/> destructive
		/// when the whole promise of that state is that everything is still standing.
		/// </para>
		/// <para>
		/// The floor is what makes a ruin readable as a place rather than as rubble.
		/// </para>
		/// </summary>
		public const int RuinStandingFloorPercent = 25;

		public const int RuinStandingCeilingPercent = 60;

		public static int StandingPercent(InheritedState State, int Roll)
		{
			if (!IsKnownState(State))
			{
				return RuinStandingFloorPercent;
			}
			if (State < InheritedState.Ruins)
			{
				return 100;
			}

			// Roll is adversity: a high draw is a hard interregnum. Standing must therefore fall
			// as it rises. The first version ran the other way and left the worst-treated ruins
			// the most intact, which is backwards on its face and was caught in review.
			// Clamp rather than modulo - wrapping would turn an out-of-range 150 into a mild 50.
			int roll = Roll;
			if (roll < 0)
			{
				roll = 0;
			}
			if (roll > 99)
			{
				roll = 99;
			}
			return RuinStandingCeilingPercent - roll * (RuinStandingCeilingPercent - RuinStandingFloorPercent) / 99;
		}
	}
}
