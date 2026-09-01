using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCreedRules
	{

		/// <summary>
		/// The loudest thing the realm ever says, and the one the ladder never had a rung for:
		/// the two cities have reached the breaking point and the founder has a named, countable
		/// number of visits to stop the split.
		/// <para>
		/// <see cref="TemperSpeech"/> deliberately returns nothing at
		/// <see cref="CityTemper.Secession"/>, because until the brink there was nothing to say
		/// at that tier &mdash; the city was already gone by the time the tier was reached. This
		/// is what it says now.
		/// </para>
		/// </summary>
		/// <param name="LeaverName">The city that will walk if nothing changes.</param>
		/// <param name="KeptName">The city the realm would keep.</param>
		/// <param name="Days">Whole days the realm has stood at the breaking point, from
		/// <c>KingdomBrinkRules.DaysStood</c>.</param>
		/// <param name="DaysLeft">World-days left before the city goes, from
		/// <c>KingdomBrinkRules.DaysLeft</c>. Days rather than visits: the window runs on the
		/// world's clock now, so staying away does not hold it open.</param>
		public static string SecessionBrinkSpeech(string LeaverName, string KeptName, int Days, int DaysLeft)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the other city" : ("{{C|" + LeaverName + "}}");
			string kept = string.IsNullOrEmpty(KeptName) ? "this one" : ("{{C|" + KeptName + "}}");
			string stood = (Days <= 0)
				? "tonight"
				: ((Days == 1) ? "since yesterday" : ("for " + Days + " days"));
			string window = (DaysLeft <= 0)
				? "There is no more time in it."
				: ((DaysLeft == 1)
					? "One day, and it is done."
					: (DaysLeft + " days, and it is done."));
			return "{{R|" + leaver + " has been drawing up its own charter " + stood + ", and it does not name "
				+ kept + ". Pour the rite, or settle where the two cities' allegiances lie, and it holds. " + window
				+ "}} {{K|(Charter: how your cities hold each other)}}";
		}

		/// <summary>
		/// The day a city left, as the founder's own book records it. Lower-case clause, no
		/// trailing period.
		/// </summary>
		/// <param name="LeaverName">The city that left.</param>
		/// <param name="KeptName">The city the realm kept.</param>
		/// <param name="LeaverCreed">The leaving city's creed display name, or null.</param>
		public static string SecessionTelling(string LeaverName, string KeptName, string LeaverCreed)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the second city" : LeaverName;
			string kept = string.IsNullOrEmpty(KeptName) ? "the city you were standing in" : KeptName;
			string creed = string.IsNullOrEmpty(LeaverCreed) ? "the covenant it had chosen" : LeaverCreed;
			return leaver + " stopped answering to the realm, holding that it owed " + kept
				+ " nothing and " + creed + " everything, and no water was spilled over it";
		}

		/// <summary>The same day as the roads tell it. Third person already, because the rumour
		/// register is not a translation of the founder's account but a rival to it.</summary>
		/// <param name="LeaverName">The city that left.</param>
		/// <param name="FounderName">The founder's name as strangers would use it.</param>
		public static string SecessionRumour(string LeaverName, string FounderName)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the second city" : LeaverName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return leaver + " threw off " + founder + " and lost nothing by it — not a wall, not a well, not a soul — which is the part " + founder + " tells differently";
		}

		/// <summary>
		/// The modal the founder reads when a city leaves. States what is gone and, plainly, what
		/// is not — because everything still standing is the reason this is a wound and not an
		/// ending.
		/// </summary>
		/// <param name="LeaverName">The city that left.</param>
		/// <param name="KeptName">The city the realm kept.</param>
		/// <param name="LeaverCreed">The leaving city's creed display name, or null.</param>
		/// <param name="LeaverPopulation">Residents who went with it.</param>
		public static string SecessionNotice(string LeaverName, string KeptName, string LeaverCreed, int LeaverPopulation)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "Your second city" : ("{{C|" + LeaverName + "}}");
			string kept = string.IsNullOrEmpty(KeptName) ? "the city you are standing in" : ("{{C|" + KeptName + "}}");
			string because = string.IsNullOrEmpty(LeaverCreed)
				? "because it could no longer hear itself over " + kept
				: "because it holds with {{C|" + LeaverCreed + "}}, and " + kept + " does not";
			string people = (LeaverPopulation == 1) ? "The one person living there stays" : (LeaverPopulation + " people stay");
			return leaver + " has left the realm, " + because + "."
				+ "\n\nNothing was burned and nobody was driven out. " + people + " where they are. The walls stand, the wells are theirs, the stores are theirs, and the book they kept goes on being kept — in " + leaver + "'s own hand now, not yours."
				+ "\n\nYou still hold " + kept + ", and " + kept + " still holds you."
				+ "\n\nGo and stand on their ground when the thing that split you is no longer true, and ask. They will hear it out. They may even say yes.";
		}

		/// <summary>The day a seceded city came back, as the founder's own book records it.</summary>
		public static string RejoinTelling(string LeaverName)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the city that left" : LeaverName;
			return "you stood in " + leaver + " with nothing in your hands and asked, and " + leaver + " came back into the realm of its own accord";
		}

		/// <summary>The same day as the roads tell it. See <see cref="SecessionRumour"/>.</summary>
		public static string RejoinRumour(string LeaverName, string FounderName)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the city that left" : LeaverName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return leaver + " took " + founder + " back on its own terms, and there are those who say it never truly left";
		}

		/// <summary>The modal the founder reads when a seceded city rejoins.</summary>
		/// <param name="LeaverName">The city that came back.</param>
		/// <param name="RealmName">The realm's display name.</param>
		public static string RejoinNotice(string LeaverName, string RealmName)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the city" : ("{{C|" + LeaverName + "}}");
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : ("{{C|" + RealmName + "}}");
			return "You ask on their own ground, and " + leaver + " hears it out."
				+ "\n\nIt is " + realm + "'s again, with everything it did while it was not — the stores as they stand, the roll as it stands, the book with its going in it and its coming back written underneath."
				+ "\n\nNobody apologises. Live and drink.";
		}

		/// <summary>
		/// What the founder is told when a seceded city will not come back. Written as its
		/// water-keepers would say it, not as a rule.
		/// </summary>
		/// <param name="Verdict">The refusal. <see cref="RejoinVerdict.Allowed"/> returns empty.</param>
		/// <param name="LeaverName">The seceded city's name.</param>
		/// <param name="LeaverCreed">The seceded city's creed display name, or null.</param>
		public static string RejoinRefusal(RejoinVerdict Verdict, string LeaverName, string LeaverCreed)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the city that left" : ("{{C|" + LeaverName + "}}");
			string creed = string.IsNullOrEmpty(LeaverCreed) ? "their allegiance" : ("{{C|" + LeaverCreed + "}}");
			switch (Verdict)
			{
			case RejoinVerdict.NothingSeceded:
				return "No city has left you. There is nothing to ask for.";
			case RejoinVerdict.RealmIsFull:
				return "You poured again while " + leaver + " was gone, and the realm already holds three cities. There is no room at your table for the one that walked away from it.";
			case RejoinVerdict.NotOnTheirGround:
				return "Ask it where it can hear you. Stand on " + leaver + "'s own ground, and say it there.";
			case RejoinVerdict.ClashStillLive:
				return "You would be heard out in " + leaver + ", and then asked what had changed. Nothing has: your other city still cannot say " + creed + " without spitting after it. Come back when that is no longer true.";
			case RejoinVerdict.StandingTooLow:
				return "They hold with " + creed + " in " + leaver + ", and " + creed + " holds your realm in contempt. They will not put their necks back under that.";
			default:
				return "";
			}
		}

		/// <summary>The rite of shared water as the founder's own book records it.</summary>
		/// <param name="HereName">The city the rite was held in.</param>
		/// <param name="ThereName">The city its envoys came from.</param>
		/// <param name="Drams">Drams poured.</param>
		public static string RiteTelling(string HereName, string ThereName, int Drams)
		{
			string here = string.IsNullOrEmpty(HereName) ? "the city" : HereName;
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ThereName;
			return here + " poured " + Drams + " drams for " + there + "'s people and drank with them, and for one evening nobody counted whose water it had been";
		}

		/// <summary>The modal the founder reads after a rite. Honest about how far it went.</summary>
		/// <param name="Temper">The temper after the rite.</param>
		/// <param name="ThereName">The city whose people were drunk with.</param>
		public static string RiteNotice(CityTemper Temper, string ThereName)
		{
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ("{{C|" + ThereName + "}}");
			return (Temper == CityTemper.Concord)
				? ("The basin goes round twice and comes back empty. Whatever was between you and " + there + " is not between you any more.")
				: ("The basin goes round. It does not fix it — you can hear that it does not — but they came, and they drank, and they will come again.");
		}

		/// <summary>The declaration as the founder's own book records it.</summary>
		/// <param name="RealmName">The realm's display name.</param>
		/// <param name="CreedDisplayName">The creed declared the realm's own.</param>
		public static string DeclarationTelling(string RealmName, string CreedDisplayName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : RealmName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "one creed above the rest" : CreedDisplayName;
			return realm + " declared itself for " + creed + ", and every road that leads here now leads here knowing it";
		}

		/// <summary>The day the founder took the declaration back.</summary>
		/// <param name="RealmName">The realm's display name.</param>
		public static string RecantTelling(string RealmName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : RealmName;
			return realm + " unsaid what it had said about itself, and went back to being a place where water is shared and nothing else is asked";
		}

		/// <summary>
		/// The modal the founder reads on declaring a creed. States the price before it is paid
		/// elsewhere &mdash; the standing falls in the world, not only here.
		/// </summary>
		/// <param name="CreedDisplayName">The creed declared.</param>
		/// <param name="SlightedDisplayName">The creed passed over, or null when there is none.</param>
		public static string DeclarationNotice(string CreedDisplayName, string SlightedDisplayName)
		{
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed" : ("{{C|" + CreedDisplayName + "}}");
			string slighted = string.IsNullOrEmpty(SlightedDisplayName) ? "" : ("{{C|" + SlightedDisplayName + "}}");
			string cost = string.IsNullOrEmpty(slighted)
				? ""
				: ("\n\n" + slighted + " hears of it before the week is out, and thinks less of your realm for it — everywhere, not only here. The city that holds with them takes it harder still, tonight.");
			return "You say it out loud, where the water is poured: this realm is for " + creed + "."
				+ cost
				+ "\n\nFrom now on the people who walk here are people who wanted to walk toward that. Give it time and one of your cities may stop being what it was.";
		}

		/// <summary>Exact declaration delta shown before commit and copied by civic voices.</summary>
		public static string DeclarationPreview(string CreedDisplayName, int SlightedCount,
			int DissentBefore)
		{
			int count = SlightedCount < 0 ? 0 : SlightedCount;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the selected creed"
				: ("{{C|" + CreedDisplayName + "}}");
			string change = count == 0
				? "No other locally held creed changes standing or dissent."
				: count + " other locally held creed" + (count == 1 ? "" : "s")
					+ " each changes realm standing by " + DeclarationStandingCost
					+ ". Dissent changes from " + DissentBefore + " to "
					+ ApplyDissent(DissentBefore, DeclarationShock) + ".";
			return "Declare " + creed + " as the realm creed?\n\nFacts: " + change
				+ " Future settlers use the declared creed as an arrival influence.";
		}

		/// <summary>Dissent clamped to the range it is allowed to occupy.</summary>
		private static int Clamp(int Dissent)
		{
			if (Dissent < 0)
			{
				return 0;
			}
			return (Dissent > DissentBreaking) ? DissentBreaking : Dissent;
		}
	}
}
