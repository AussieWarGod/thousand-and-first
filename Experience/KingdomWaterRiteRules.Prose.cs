using System;

namespace ThousandAndFirst
{
	public static partial class KingdomWaterRiteRules
	{

		// ==================================================================================
		// Prose. Half the simulation. The founder is spoken to in the second person; a chronicle
		// line is a lower-case clause the register dates and closes; a rumour line is already in
		// the third person, because that register is not a translation of the founder's account
		// but a rival to it -- and must never contain the word "you", which
		// KingdomRules.ToThirdPerson would rewrite into the founder's own name.
		// ==================================================================================

		/// <summary>The founder-facing line for why the basin does not go down in front of this
		/// person. Never a complaint and never a countdown (STANDARDS 7b).</summary>
		/// <param name="Bar">The bar. <see cref="WaterRiteBar.Ready"/> returns empty.</param>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="RealmCreedDisplay">The realm's creed, formatted, or null.</param>
		/// <param name="Drams">What the rite would have cost, for the stores bar.</param>
		/// <param name="Stored">What the dedicated stores hold, for the stores bar.</param>
		public static string BarLine(WaterRiteBar Bar, string Name, string RealmCreedDisplay, int Drams, int Stored)
		{
			string name = Named(Name);
			string creed = string.IsNullOrEmpty(RealmCreedDisplay) ? "anything in particular" : ("{{C|" + RealmCreedDisplay + "}}");
			switch (Bar)
			{
			case WaterRiteBar.NotOnOurGround:
				return "Water is shared on the settlement's own ground, in front of the people who live on it.";
			case WaterRiteBar.RealmBelievesNothing:
				return "Your realm holds with nothing in particular, and nobody can be asked to drink to that. Let one creed become the city's, or say one out loud, and then ask.";
			case WaterRiteBar.NothingBetweenYou:
				return name + " already holds with " + creed + ". You have shared water with " + name + " a hundred times over a cookfire; there is nothing here that wants a ceremony.";
			case WaterRiteBar.TheirOffice:
				return name + " is marked by a retired office bar from older rules. Civic titles grant no service, capability, or ritual authority; recalculate this offer.";
			case WaterRiteBar.NoRoadOut:
				return name + " has nowhere to go if the answer is no — the settlement is too small to let anybody walk. A yes from somebody you have left no room to refuse is not a yes. Ask when there are more of you.";
			case WaterRiteBar.AskedTooOften:
				return name + " has been asked this as many times as anyone should be. It is not a question any more; it is a thing being done to them. Let it alone while the city holds " + creed + ".";
			case WaterRiteBar.AlreadyAnswered:
				return name + " has answered, and nothing has changed since. Asking the same question twice is not asking twice.";
			case WaterRiteBar.PouredTooRecently:
				return "You poured for one of your own too recently. A rite held whenever it occurs to you is a round of drinks, and a round of drinks converts nobody.";
			case WaterRiteBar.StoresCannotBear:
				return "The rite would take {{C|" + Drams + " drams}} from the stores, and the stores hold {{C|" + Stored + "}}. Fill the casks first; this is not a thing to do by halves.";
			default:
				return "";
			}
		}

		/// <summary>
		/// The Charter's own row for one settler: their name, what they hold, and either the price
		/// or a shut door. Shut rows stay selectable, because a founder who picks one is owed the
		/// whole reason rather than a colour.
		/// </summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="CreedDisplay">What they hold, formatted, or null for nothing in
		/// particular.</param>
		/// <param name="Drams">What the rite would cost. Meaningless unless <paramref name="Bar"/>
		/// is <see cref="WaterRiteBar.Ready"/>.</param>
		/// <param name="Bar">The bar standing against them, or <see cref="WaterRiteBar.Ready"/>.</param>
		/// <param name="Pressed">Whether a further asking would be the last one.</param>
		public static string RowLabel(string Name, string CreedDisplay, int Drams, WaterRiteBar Bar, bool Pressed)
		{
			string name = Named(Name);
			string holds = string.IsNullOrEmpty(CreedDisplay) ? "holds with nothing in particular" : ("holds with " + CreedDisplay);
			if (Bar != WaterRiteBar.Ready)
			{
				return "{{K|" + name + " — " + holds + "}}";
			}
			if (Pressed)
			{
				return "{{r|" + name + "}} — " + holds + " {{r|(asked, and asked, and asked)}} {{K|(" + Drams + " drams)}}";
			}
			return "{{W|" + name + "}} — " + holds + " {{K|(" + Drams + " drams)}}";
		}

		/// <summary>
		/// The consent modal: what the water costs, what is being asked, and &mdash; plainly
		/// &mdash; that the water is spent whichever way they answer. Every spend in this mod names
		/// its price before it is paid, and this one names two.
		/// </summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="TheirCreedDisplay">What they hold, formatted, or null.</param>
		/// <param name="RealmCreedDisplay">What the realm holds, formatted.</param>
		/// <param name="Settlement">The city's name.</param>
		/// <param name="Drams">Drams the stores will give up.</param>
		public static string OfferPrompt(string Name, string TheirCreedDisplay, string RealmCreedDisplay, string Settlement, int Drams)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "this city" : ("{{C|" + Settlement + "}}");
			string realm = string.IsNullOrEmpty(RealmCreedDisplay) ? "what this city has come to hold" : ("{{C|" + RealmCreedDisplay + "}}");
			string theirs = string.IsNullOrEmpty(TheirCreedDisplay)
				? (name + " holds with nothing in particular.")
				: (name + " holds with {{C|" + TheirCreedDisplay + "}}.");
			return "You draw {{C|" + Drams + " drams}} from the stores of " + where + ", fill the basin, and set it down on the ground in front of " + name + ".\n\n"
				+ theirs + " " + where + " holds with " + realm + ".\n\n"
				+ "Nobody is ordered to drink. You pour, and you wait, and the water is gone either way.\n\n"
				+ "Pour?";
		}

		/// <summary>
		/// The warning appended to <see cref="OfferPrompt"/> when this asking would be the last
		/// one. States the consequence before it is bought, exactly as declaring a creed states its
		/// price.
		/// </summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="WillTakeTheRoad">Whether they hold the realm's creed in enough dislike to
		/// leave over being made to hold it (<c>KingdomConversionRules.Resents</c>). A settler who
		/// merely differs stays, and is simply never asked again.</param>
		public static string PressedWarning(string Name, bool WillTakeTheRoad)
		{
			string name = Named(Name);
			string tail = WillTakeTheRoad
				? (name + " will start asking after the roads, and unless the city stops holding what it holds, " + name + " will take one.}}")
				: ("it will be the last time anybody puts it to " + name + ", and " + name + " will remember which of you kept asking.}}");
			return "\n\n{{r|" + name + " has answered you three times, and has begun looking at the ground while doing it. Ask a fourth time and it stops being a question: "
				+ tail;
		}

		/// <summary>The modal the founder reads when a settler drinks to it. No triumph in it: the
		/// fiction is that nobody was argued out of anything.</summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="RealmCreedDisplay">What they hold now, formatted.</param>
		public static string AcceptNotice(string Name, string RealmCreedDisplay)
		{
			string name = Named(Name);
			string creed = string.IsNullOrEmpty(RealmCreedDisplay) ? "what this city holds" : ("{{C|" + RealmCreedDisplay + "}}");
			return name + " looks at the basin for a long moment, and then at you, and kneels, and drinks.\n\n"
				+ "Nobody was argued out of anything. The water was yours and " + name + " took it, and out here that is the whole of the thing.\n\n"
				+ name + " holds with " + creed + " from tonight. It will be in the book by morning, and it will be told wrong on the road by the end of the month.\n\n"
				+ "{{C|Live and drink.}}";
		}

		/// <summary>
		/// The modal the founder reads when a settler does not drink to it. One refusal per answer,
		/// each written to be worth reading, and each naming what would have to be different
		/// &mdash; which for two of them is nothing the founder can do, and says so rather than
		/// pretending otherwise.
		/// </summary>
		/// <param name="Answer">The answer. <see cref="WaterRiteAnswer.Accepted"/> returns empty.</param>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="TheirCreedDisplay">What they hold, formatted, or null.</param>
		/// <param name="RealmCreedDisplay">What the realm holds, formatted, or null.</param>
		/// <param name="ShrineCreedDisplay">What the rival shrine is consecrated to, formatted, or
		/// null when the answer was not <see cref="WaterRiteAnswer.RivalShrine"/>.</param>
		public static string RefusalNotice(WaterRiteAnswer Answer, string Name, string TheirCreedDisplay, string RealmCreedDisplay, string ShrineCreedDisplay)
		{
			string name = Named(Name);
			string theirs = string.IsNullOrEmpty(TheirCreedDisplay) ? "what they came here holding" : ("{{C|" + TheirCreedDisplay + "}}");
			string realm = string.IsNullOrEmpty(RealmCreedDisplay) ? "what this city holds" : ("{{C|" + RealmCreedDisplay + "}}");
			string shrine = string.IsNullOrEmpty(ShrineCreedDisplay) ? "something else" : ("{{C|" + ShrineCreedDisplay + "}}");
			switch (Answer)
			{
			case WaterRiteAnswer.TooNew:
				return name + " takes the basin in both hands, drinks a good mouthful, and hands it back still half full.\n\n"
					+ "\"I have been here a season and you are asking me what I am. Ask me after I have carried water for this place in a bad year.\"\n\n"
					+ "Nothing is spoiled and nothing is owed. The water is spent, " + name + " is still yours, and " + name + " is still " + theirs + "'s. Ask again when " + name + " has lived more of this settlement's life than that.";
			case WaterRiteAnswer.RivalShrine:
				return name + " drinks, and does it looking past your shoulder the whole time.\n\n"
					+ "The shrine to " + shrine + " stands two streets from " + name + "'s own door, and it makes its argument every morning, and yours is being made once, tonight, on the ground, in a tin bowl.\n\n"
					+ "The water is spent. Take that shrine down, or consecrate it to something " + name + " could drink to, and ask again.";
			case WaterRiteAnswer.Devout:
				return name + " drinks, and thanks you for it, and then says the thing you were afraid of.\n\n"
					+ "\"You hold that basin the way I hold mine. Would you put yours down for a cup of somebody else's water?\"\n\n"
					+ "The water is spent, and " + name + " is not being difficult. Some people came here carrying something and did not come here to put it down. Nothing said tonight moves this. More years under the same roof might; the quarrel between the two creeds easing certainly would.";
			case WaterRiteAnswer.TooBitter:
				return name + " does not touch the basin. " + name + " does not have to; the pouring was the asking.\n\n"
					+ "What stands between " + theirs + " and " + realm + " was not started by either of you and will not be finished over a bowl. There is no shared life long enough to walk that, and " + name + " is not going to pretend there is.\n\n"
					+ "The water is spent. One of those two creeds has to move — this city's, or theirs — before there is anything here worth asking again.";
			case WaterRiteAnswer.Steadfast:
				return name + " lets the water sit where you put it, and does not look at it, and does not look away from you either.\n\n"
					+ "\"Ask me for anything else. Ask me to carry, ask me to stand a wall, ask me to die on it. Not this.\"\n\n"
					+ "This is not a thing " + name + " is going to be talked around, tonight or in ten years. The water is spent. Let it be the last time it is asked.";
			default:
				return "";
			}
		}

		/// <summary>The refusal as the founder's own book records it: the same dignity in the record
		/// that was in the room. Lower-case clause, no trailing period.</summary>
		/// <param name="Answer">The answer given. <see cref="WaterRiteAnswer.Accepted"/> returns
		/// empty &mdash; an acceptance is chronicled by <c>KingdomConversion.Convert</c>, which is
		/// the one path every conversion in the mod takes.</param>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="Settlement">The city's name.</param>
		public static string RefusalTelling(WaterRiteAnswer Answer, string Name, string Settlement)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "the city" : Settlement;
			switch (Answer)
			{
			case WaterRiteAnswer.TooNew:
				return "the basin was set down in front of " + name + " at " + where + ", and " + name + " drank half of it and asked to be asked again in a harder year";
			case WaterRiteAnswer.RivalShrine:
				return "the basin was set down in front of " + name + " at " + where + ", and " + name + " drank it looking at a shrine that was not the city's";
			case WaterRiteAnswer.Devout:
				return "the basin was set down in front of " + name + " at " + where + ", and " + name + " drank, and thanked the founder, and kept what " + name + " had come with";
			case WaterRiteAnswer.TooBitter:
				return "the basin was set down in front of " + name + " at " + where + " and was not touched, and nobody in the room thought worse of " + name + " for it";
			case WaterRiteAnswer.Steadfast:
				return "the basin was set down in front of " + name + " at " + where + ", and " + name + " said that this was the one thing that would not be asked of " + name + " again";
			default:
				return "";
			}
		}

		/// <summary>
		/// The same night as the roads tell it. Third person already, and arguing: the founder's
		/// book records that somebody was asked and said no, and the roads record that saying no
		/// cost nothing, which is the half nobody in a harder country believes.
		/// </summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="Settlement">The city's name.</param>
		/// <param name="FounderName">The founder as strangers would name them.</param>
		public static string RefusalRumour(string Name, string Settlement, string FounderName)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "that city" : Settlement;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return name + " of " + where + " told " + founder + " no, in " + founder + "'s own city, with " + founder + "'s own water going cold in the bowl — and walked out of it whole, which is the part that gets left off";
		}

		/// <summary>The modal the founder reads on the asking that closes the matter. States plainly
		/// what is still true &mdash; nothing was taken from them and nothing is being taken now
		/// &mdash; because that is what makes this a wound and not a punishment.</summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="Settlement">The city's name.</param>
		/// <param name="WillTakeTheRoad">Whether they resent the creed enough to leave over it.</param>
		public static string ClosedNotice(string Name, string Settlement, bool WillTakeTheRoad)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "the city" : ("{{C|" + Settlement + "}}");
			string opening = "You set the basin down, and " + name + " does not look at it.\n\n"
				+ "\"That is four times. I gave you an answer each time and you kept the water coming.\"\n\n";
			if (!WillTakeTheRoad)
			{
				return opening + name + " drinks, because it is water and there is a drought on somewhere. Nothing is taken from " + name + " and nothing changes tonight.\n\n"
					+ "{{K|But that was the last time it will be put to " + name + " while " + where + " holds what it holds.}}";
			}
			return opening + name + " is not driven out and nothing of theirs is taken. They will start asking travellers where the good roads are, in the open, where you can see them do it.\n\n"
				+ "{{r|Take it out of their quarter and they stay. Leave it standing and they go, and the book will say why.}}";
		}

		/// <summary>The asking that closed the matter, as the founder's own book records it. Written
		/// on the night rather than on the pass anybody acts, because the founder should be able to
		/// find the night they went one asking too far.</summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="Settlement">The city's name.</param>
		public static string ClosedTelling(string Name, string Settlement)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "the city" : Settlement;
			return "the basin was set down in front of " + name + " at " + where + " a fourth time, and " + name + " counted the askings out loud";
		}

		/// <summary>The same night as the roads tell it. See <see cref="RefusalRumour"/>.</summary>
		public static string ClosedRumour(string Name, string Settlement, string FounderName)
		{
			string name = Named(Name);
			string where = string.IsNullOrEmpty(Settlement) ? "that city" : Settlement;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return "in " + where + " they ask a settler what a settler believes, and then they ask again, and " + name + " is the one who counted the askings out loud — which " + founder + " tells as a misunderstanding over a bowl";
		}

		/// <summary>The founder-facing note for the same night, in the ledger's voice: what can
		/// still be done about it (STANDARDS 7b).</summary>
		/// <param name="Name">The settler's own name.</param>
		/// <param name="RealmCreedDisplay">The creed being put to them, formatted.</param>
		public static string ClosedNote(string Name, string RealmCreedDisplay)
		{
			string name = Named(Name);
			string creed = string.IsNullOrEmpty(RealmCreedDisplay) ? "what the city holds" : RealmCreedDisplay;
			return name + " has been asked about " + creed + " once too often, and will not be asked again while the city holds it.";
		}

		/// <summary>A settler's own name, or the honest fallback for somebody the roll does not
		/// carry. Repeated rather than pronouned throughout this file: the roll carries no gender,
		/// and a wrong pronoun in a line about somebody's belief reads worse than a repeat.</summary>
		private static string Named(string Name)
		{
			return string.IsNullOrEmpty(Name) ? "a settler" : Name;
		}

		private static int Clamp(int Value, int Low, int High)
		{
			if (Value < Low)
			{
				return Low;
			}
			return (Value > High) ? High : Value;
		}
	}
}
