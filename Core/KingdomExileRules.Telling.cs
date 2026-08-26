namespace ThousandAndFirst
{
	public static partial class KingdomExileRules
	{

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
