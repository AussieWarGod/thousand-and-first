namespace ThousandAndFirst
{
	/// <summary>
	/// How the realm holds the founder, read off the founder's own reputation with the realm's
	/// faction. Ordered best-first, so a larger value is a worse standing and "the regard fell"
	/// is "the value rose" — every comparison in this file reads that way.
	/// </summary>
	public enum RealmRegard
	{
		Beloved = 0,
		Trusted = 1,
		Doubted = 2,
		Resented = 3,
		Repudiated = 4
	}

	/// <summary>What the realm does about a change in its regard for the founder.</summary>
	public enum RegardStep
	{
		Nothing = 0,
		Murmur = 1,
		Warning = 2,
		Expulsion = 3
	}

	/// <summary>Why an expulsion may not proceed, or that it is warranted.</summary>
	public enum ExileVerdict
	{
		Warranted = 0,
		NothingFounded = 1,
		AlreadyCastOut = 2,
		RegardHolds = 3
	}

	/// <summary>Why a return may not proceed, or that it is allowed.</summary>
	public enum ReturnVerdict
	{
		Allowed = 0,
		NeverCastOut = 1,
		FoundedAgain = 2,
		NothingRemembered = 3,
		NotOnTheirGround = 4,
		RegardTooLow = 5
	}

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
	public static class KingdomExileRules
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

		/// <summary>
		/// Names the deed the realm counted against the founder, from the engine's own reason for
		/// the reputation change. A clause beginning with "you", fit to follow a colon.
		/// </summary>
		/// <param name="ReputationType">The <c>Type</c> carried by the reputation change, or null
		/// when nothing named itself.</param>
		public static string DeedClause(string ReputationType)
		{
			switch (ReputationType)
			{
			case "WaterRitualCurse":
				return "you killed someone whose water you had drunk";
			case "WaterRitualHermitOathPunishment":
				return "you broke an oath sworn over water";
			case "Blasphemy":
				return "you blasphemed where the city could hear it";
			case "Worship":
				return "you knelt to something the city will not kneel to";
			case "AddsRepUnapply":
			case "GrantsRepAsFollowerUnapply":
				return "you put aside the thing they had taken you for";
			case "Wish":
				return "you willed it, and a founder's will is a deed like any other";
			default:
				return "you did what the book records on the page before this one";
			}
		}

		/// <summary>
		/// What the realm says, short of expelling anyone. One line, non-modal, in the
		/// water-keepers' voice.
		/// </summary>
		/// <param name="Step">The step taken. <see cref="RegardStep.Nothing"/> and
		/// <see cref="RegardStep.Expulsion"/> return empty; expulsion has its own telling.</param>
		/// <param name="CityName">The seated city's name.</param>
		public static string RegardSpeech(RegardStep Step, string CityName)
		{
			string city = string.IsNullOrEmpty(CityName) ? "the city" : ("{{C|" + CityName + "}}");
			switch (Step)
			{
			case RegardStep.Murmur:
				return "The water-keepers of " + city + " have begun to speak of you in the past tense.";
			case RegardStep.Warning:
				return "They read the charter aloud in " + city + " tonight, and stopped a while at your name.";
			default:
				return "";
			}
		}

		/// <summary>
		/// The same step as the founder's own book records it: lower-case clause, no trailing
		/// period, because the chronicle dates it and closes it. Kept apart from
		/// <see cref="RegardSpeech"/> because a chronicle entry is not a message with the colour
		/// markup stripped &mdash; it is written a year later, by someone else.
		/// </summary>
		/// <param name="Step">The step taken. Anything but a murmur or a warning returns empty.</param>
		/// <param name="CityName">The seated city's name.</param>
		public static string RegardChronicle(RegardStep Step, string CityName)
		{
			string city = string.IsNullOrEmpty(CityName) ? "the city" : CityName;
			switch (Step)
			{
			case RegardStep.Murmur:
				return "the water-keepers of " + city + " began to speak of you in the past tense";
			case RegardStep.Warning:
				return "the charter was read aloud in " + city + ", and the reading stopped a while at your name";
			default:
				return "";
			}
		}

		/// <summary>
		/// The expulsion as the founder's own book records it. Lower-case clause, no trailing
		/// period &mdash; the chronicle dates it and closes it.
		/// </summary>
		/// <param name="RealmName">The expelled-from realm's display name.</param>
		/// <param name="Deed">A clause from <see cref="DeedClause"/>.</param>
		public static string ExileTelling(string RealmName, string Deed)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : RealmName;
			string deed = string.IsNullOrEmpty(Deed) ? DeedClause(null) : Deed;
			return realm + " put you out of the realm you founded: " + deed;
		}

		/// <summary>
		/// The same day as the roads tell it. Third person already, because the rumour register
		/// is not a translation of the founder's own account but a rival to it.
		/// </summary>
		/// <param name="RealmName">The expelled-from realm's display name.</param>
		/// <param name="FounderName">The founder's name as strangers would use it.</param>
		public static string ExileRumour(string RealmName, string FounderName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "a young realm" : RealmName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return "the tyrant " + founder + " fled " + realm + " one step ahead of the water-keepers, and " + realm + " did not fall down after";
		}

		/// <summary>The homecoming as the founder's own book records it. See <see cref="ExileTelling"/>.</summary>
		public static string ReturnTelling(string RealmName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : RealmName;
			return "you came back to " + realm + " with nothing in your hands, and " + realm + " opened its gate to you";
		}

		/// <summary>The homecoming as the roads tell it. See <see cref="ExileRumour"/>.</summary>
		public static string ReturnRumour(string RealmName, string FounderName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "a young realm" : RealmName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return realm + " took " + founder + " back, and there are those who say it never truly put " + founder + " out";
		}

		/// <summary>
		/// The modal the founder reads on being expelled. States what has changed and, plainly,
		/// what has not &mdash; because everything that has not changed is the reason this is not
		/// an ending.
		/// </summary>
		/// <param name="RealmName">The expelled-from realm's display name.</param>
		/// <param name="Deed">A clause from <see cref="DeedClause"/>.</param>
		/// <param name="Cities">How many cities the realm holds. Reads plurally above one.</param>
		public static string ExileNotice(string RealmName, string Deed, int Cities)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "The realm" : ("{{C|" + RealmName + "}}");
			string deed = string.IsNullOrEmpty(Deed) ? DeedClause(null) : Deed;
			string held = (Cities > 1) ? "Its cities keep" : "It keeps";
			return realm + " has put you out of the realm you founded, because " + deed
				+ ".\n\nThe charter is taken from you. The ground is not yours, the stores are not yours, and the roll of settlers is no longer a roll of yours."
				+ "\n\n" + held + " every well you sank and every wall you raised, and goes on without you — which is what you built it to be able to do. Walk back and it will still be there, with its own opinion of you."
				+ "\n\nThe basin still pours. Ground you mean to keep is still ground you mean to keep.";
		}

		/// <summary>The modal the founder reads on being taken back.</summary>
		/// <param name="RealmName">The realm's display name.</param>
		/// <param name="CityName">The city the founder is standing in.</param>
		public static string ReturnNotice(string RealmName, string CityName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "The realm" : ("{{C|" + RealmName + "}}");
			string city = string.IsNullOrEmpty(CityName) ? "the city" : ("{{C|" + CityName + "}}");
			return "You ask, on the ground you poured the first water on, and " + city + " hears it out.\n\n"
				+ "The charter is yours again. " + realm + " is yours again, with everything it did while you were outside it — the stores as they stand, the roll as it stands, the book with your going in it and your coming back under that."
				+ "\n\nNobody embraces you. Live and drink.";
		}

		/// <summary>
		/// What the founder is told when the return will not proceed. Written as the water-keepers
		/// would say it, not as a rule.
		/// </summary>
		/// <param name="Verdict">The refusal. <see cref="ReturnVerdict.Allowed"/> returns empty.</param>
		/// <param name="RealmName">The expelled-from realm's display name.</param>
		/// <param name="NewRealmName">The realm the founder holds now, if any.</param>
		public static string ReturnRefusal(ReturnVerdict Verdict, string RealmName, string NewRealmName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : ("{{C|" + RealmName + "}}");
			string held = string.IsNullOrEmpty(NewRealmName) ? "the realm you hold" : ("{{C|" + NewRealmName + "}}");
			switch (Verdict)
			{
			case ReturnVerdict.NeverCastOut:
				return "Nobody has put you out of anything. There is nothing to be taken back into.";
			case ReturnVerdict.FoundedAgain:
				return realm + " has heard that you poured again, and that " + held + " calls you founder. A man is taken back into one realm or he founds another; he does not do both and then choose. That door is shut, and it was you who shut it.";
			case ReturnVerdict.NothingRemembered:
				return realm + " kept no ground you could walk back to. There is no gate to stand at.";
			case ReturnVerdict.NotOnTheirGround:
				return "Ask it where it can hear you. Stand on " + realm + "'s own ground, and say it there.";
			case ReturnVerdict.RegardTooLow:
				return realm + " will not hear it. What you did stands between you and the gate, and no amount of asking moves it — only doing better somewhere they can see.";
			default:
				return "";
			}
		}

		/// <summary>
		/// The line the old realm's ground gets from a founder who has since founded elsewhere.
		/// Non-modal, said once: the door is closed, and closed doors are not argued with.
		/// </summary>
		/// <param name="OldRealmName">The realm that expelled the founder.</param>
		/// <param name="NewRealmName">The realm the founder holds now.</param>
		public static string DoorClosedLine(string OldRealmName, string NewRealmName)
		{
			string old = string.IsNullOrEmpty(OldRealmName) ? "This place" : ("{{C|" + OldRealmName + "}}");
			string held = string.IsNullOrEmpty(NewRealmName) ? "a city of your own" : ("{{C|" + NewRealmName + "}}");
			return old + " knows you. It has nothing to say to a founder who has " + held + " to go back to.";
		}
	}
}
