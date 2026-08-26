namespace ThousandAndFirst
{
	/// <summary>
	/// Being thrown out of the realm you founded, and being let back in.
	/// <para>
	/// Exile is <b>secession, realm-scoped</b>: the whole realm expels the founder, so no
	/// citizen's allegiance key moves, no faction is minted, unmade or renamed, and nothing
	/// physical is touched. What ends is the founder's claim on it. The cities go on — which is
	/// the entire point — and the engine's own reputation with the realm's faction carries the
	/// grudge, because that is the one surface both the world and the player already read.
	/// </para>
	/// <para>
	/// Nothing here keys off elapsed time. Regard falls because of what the founder did, never
	/// because of how long they were away, so absence can never expel anyone. Engine-free so the
	/// whole ladder is tabled rather than discovered in the field.
	/// </para>
	/// </summary>
	public static partial class KingdomExileRules
	{
		/// <summary>
		/// Reputation at or above which the realm loves its founder. Mirrors
		/// <c>XRL.Rules.RuleSettings.REPUTATION_LOVED</c>; the mirror is asserted against the
		/// live value by <c>kingdom:selftest</c> rather than assumed.
		/// </summary>
		public const int RegardLoved = 600;

		/// <summary>Mirrors <c>RuleSettings.REPUTATION_LIKED</c>. See <see cref="RegardLoved"/>.</summary>
		public const int RegardLiked = 250;

		/// <summary>Mirrors <c>RuleSettings.REPUTATION_DISLIKED</c>. See <see cref="RegardLoved"/>.</summary>
		public const int RegardDisliked = -250;

		/// <summary>Mirrors <c>RuleSettings.REPUTATION_HATED</c>. See <see cref="RegardLoved"/>.</summary>
		public const int RegardHated = -600;

		/// <summary>
		/// What the founder's regard is raised to on being taken back, if it stands lower.
		/// Indifference, not love: the gate opens, and nobody smiles. A floor rather than an
		/// assignment, so a founder who mended things further than this keeps what they mended.
		/// </summary>
		public const int RegardFloorOnReturn = 0;

		/// <summary>
		/// Reads a raw reputation value as the realm's regard. Boundaries are vanilla's own
		/// (<c>Reputation.GetAttitude</c>), so the tier a city acts on is the tier the
		/// reputation screen shows.
		/// </summary>
		/// <param name="Regard">Raw reputation with the realm's faction.</param>
		public static RealmRegard ClassifyRegard(int Regard)
		{
			if (Regard >= RegardLoved)
			{
				return RealmRegard.Beloved;
			}
			if (Regard >= RegardLiked)
			{
				return RealmRegard.Trusted;
			}
			if (Regard > RegardDisliked)
			{
				return RealmRegard.Doubted;
			}
			if (Regard > RegardHated)
			{
				return RealmRegard.Resented;
			}
			return RealmRegard.Repudiated;
		}

		/// <summary>
		/// The regard the realm remembers having said out loud, after seeing
		/// <paramref name="Current"/>. This is the hysteresis: a fall speaks once, jitter back and
		/// forth across one threshold says nothing further, and only mending the thing properly
		/// &mdash; climbing back to <see cref="RealmRegard.Trusted"/> or better &mdash; re-arms the
		/// ladder so a later fall is spoken of again.
		/// </summary>
		/// <param name="Current">The regard now.</param>
		/// <param name="Spoken">The regard last spoken of.</param>
		public static RealmRegard RememberedRegard(RealmRegard Current, RealmRegard Spoken)
		{
			if (Current <= RealmRegard.Trusted)
			{
				return Current;
			}
			return (Current > Spoken) ? Current : Spoken;
		}

		/// <summary>
		/// What the realm does, having looked at the founder again.
		/// </summary>
		/// <param name="Current">The regard now.</param>
		/// <param name="Spoken">The regard last spoken of, from <see cref="RememberedRegard"/>.</param>
		/// <param name="AlreadyCastOut">True if the founder already holds no realm. Defensive:
		/// a realm that has already expelled someone has nothing further to say about them.</param>
		public static RegardStep JudgeRegardStep(RealmRegard Current, RealmRegard Spoken, bool AlreadyCastOut)
		{
			if (AlreadyCastOut)
			{
				return RegardStep.Nothing;
			}
			if (Current == RealmRegard.Repudiated)
			{
				return RegardStep.Expulsion;
			}
			if (Current <= Spoken)
			{
				return RegardStep.Nothing;
			}
			if (Current == RealmRegard.Resented)
			{
				return RegardStep.Warning;
			}
			if (Current == RealmRegard.Doubted)
			{
				return RegardStep.Murmur;
			}
			return RegardStep.Nothing;
		}

		/// <summary>
		/// Whether an expulsion may proceed.
		/// </summary>
		/// <param name="Founded">Whether the founder holds a realm at all.</param>
		/// <param name="AlreadyCastOut">Whether a previous expulsion is on the record. Only
		/// changes which refusal is given when there is no realm to be expelled from.</param>
		/// <param name="Current">The realm's regard for the founder.</param>
		/// <param name="Forced">True for the debug path, which skips the regard requirement and
		/// nothing else.</param>
		/// <returns><see cref="ExileVerdict.Warranted"/> if the expulsion may proceed.</returns>
		public static ExileVerdict JudgeExile(bool Founded, bool AlreadyCastOut, RealmRegard Current, bool Forced)
		{
			if (!Founded)
			{
				return AlreadyCastOut ? ExileVerdict.AlreadyCastOut : ExileVerdict.NothingFounded;
			}
			if (Forced)
			{
				return ExileVerdict.Warranted;
			}
			return (Current == RealmRegard.Repudiated) ? ExileVerdict.Warranted : ExileVerdict.RegardHolds;
		}

		/// <summary>
		/// Whether the founder may be taken back.
		/// <para>
		/// Founding again is checked before the ground is, because that door closes wherever the
		/// founder happens to be standing: a founder with a realm of their own is no longer
		/// someone the old one can take back, and walking to the gate does not change it.
		/// </para>
		/// </summary>
		/// <param name="CastOut">Whether an expulsion is on the record.</param>
		/// <param name="FoundedAgain">Whether the founder has since founded another realm.</param>
		/// <param name="GroundRemembered">Whether the expelled realm holds any ground that could
		/// be walked back to.</param>
		/// <param name="OnTheirGround">Whether the founder is standing on it.</param>
		/// <param name="Regard">Raw reputation with the expelled realm's faction.</param>
		public static ReturnVerdict JudgeReturn(bool CastOut, bool FoundedAgain, bool GroundRemembered, bool OnTheirGround, int Regard)
		{
			if (!CastOut)
			{
				return ReturnVerdict.NeverCastOut;
			}
			if (FoundedAgain)
			{
				return ReturnVerdict.FoundedAgain;
			}
			if (!GroundRemembered)
			{
				return ReturnVerdict.NothingRemembered;
			}
			if (!OnTheirGround)
			{
				return ReturnVerdict.NotOnTheirGround;
			}
			if (ClassifyRegard(Regard) == RealmRegard.Repudiated)
			{
				return ReturnVerdict.RegardTooLow;
			}
			return ReturnVerdict.Allowed;
		}

		/// <summary>
		/// Whether to put the question to the founder unasked, on walking onto the old realm's
		/// ground. Deed-keyed, never time-keyed: refusing once silences the question until the
		/// founder has actually changed the realm's mind about them, so it can nag no one.
		/// </summary>
		/// <param name="AskedAtRegard">The regard the question was last put at, or
		/// <c>int.MinValue</c> if it never has been.</param>
		public static bool ShouldOfferReturn(bool CastOut, bool FoundedAgain, bool GroundRemembered, bool OnTheirGround, int Regard, int AskedAtRegard)
		{
			if (JudgeReturn(CastOut, FoundedAgain, GroundRemembered, OnTheirGround, Regard) != ReturnVerdict.Allowed)
			{
				return false;
			}
			return Regard > AskedAtRegard;
		}

		/// <summary>
		/// The founder's regard with the realm after being taken back: never lowered, raised only
		/// to <see cref="RegardFloorOnReturn"/>.
		/// </summary>
		public static int RegardOnReturn(int Regard)
		{
			return (Regard < RegardFloorOnReturn) ? RegardFloorOnReturn : Regard;
		}

		/// <summary>Lower-case name for a regard, Qud style, for reports and the dev log.</summary>
		public static string RegardName(RealmRegard Regard)
		{
			switch (Regard)
			{
			case RealmRegard.Beloved:
				return "beloved";
			case RealmRegard.Trusted:
				return "trusted";
			case RealmRegard.Doubted:
				return "doubted";
			case RealmRegard.Resented:
				return "resented";
			default:
				return "repudiated";
			}
		}
	}
}
