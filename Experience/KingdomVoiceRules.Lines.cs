using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomVoiceRules
	{

		/// <summary>
		/// What a settler from a given country says about a given moment.
		/// <para>
		/// Origin colours what a person notices, never how they pronounce it: everyone here
		/// speaks the same plain Qudish, and the salt marshes show up as a lifetime of drinking
		/// water that fought back, not as an accent.
		/// </para>
		/// </summary>
		/// <param name="Occasion">The moment.</param>
		/// <param name="Origin">One of <see cref="KingdomRules.Origins"/>. Anything else &mdash;
		/// null, a third-party origin, a roll that lost its parallel entry &mdash; answers in the
		/// plain register, which is written for exactly that case and is never empty.</param>
		/// <returns>One sentence or two, in the speaker's own mouth. Never null.</returns>
		public static string Line(VoiceOccasion Occasion, string Origin)
		{
			switch (Origin)
			{
			case "the salt marshes":
				return SaltMarshLine(Occasion);
			case "the desert canyons":
				return CanyonLine(Occasion);
			case "the hills":
				return HillLine(Occasion);
			case "the flower fields":
				return FlowerFieldLine(Occasion);
			case "the rust wells":
				return RustWellLine(Occasion);
			case "the banana grove":
				return GroveLine(Occasion);
			default:
				return PlainLine(Occasion);
			}
		}

		// The line tables are grouped by origin rather than by occasion so that one person's
		// whole repertoire can be read, and rewritten, in one place.

		private static string SaltMarshLine(VoiceOccasion Occasion)
		{
			switch (Occasion)
			{
			case VoiceOccasion.StageUp:
				return "Where I come from, a place this size would have drunk the marsh dry by now. Here the water still comes. I keep waiting to be wrong.";
			case VoiceOccasion.RaidRepelled:
				return "In the marshes we had no wall. We had reeds, and we hid in them. I like this better.";
			case VoiceOccasion.ThirstBroken:
				return "I grew up drinking water that fought back. This tastes of nothing at all, and nothing at all is the best thing there is.";
			case VoiceOccasion.MealShared:
				return "In the marshes you ate alone, standing, whatever you had found. I am still learning how to sit down with people.";
			case VoiceOccasion.CitizenLost:
				return "The wells they are walking to are worse. I have drunk from them. I am not going to be the one who says so.";
			case VoiceOccasion.Wedding:
				return "In the marshes two people move in together and that is the whole of it. Here they stand up and say it in front of everybody. I cried, and I am not sorry.";
			case VoiceOccasion.Feast:
				return "We kept the days in the marshes too. Different names, same idea: eat while there is something to eat.";
			default:
				return PlainLine(Occasion);
			}
		}

		private static string CanyonLine(VoiceOccasion Occasion)
		{
			switch (Occasion)
			{
			case VoiceOccasion.StageUp:
				return "In the canyons you measure a settlement by how far apart the roofs stand. We have stopped being able to measure ours that way.";
			case VoiceOccasion.RaidRepelled:
				return "They came down the open ground the way water comes down a canyon, and they found out this canyon has an end to it.";
			case VoiceOccasion.ThirstBroken:
				return "Three days I kept my mouth shut to save what was in it. You can tell me it is over. I will believe it in a week.";
			case VoiceOccasion.MealShared:
				return "We ate after dark in the canyons, so the food did not have to compete with the heat. I still catch myself waiting for the dark.";
			case VoiceOccasion.CitizenLost:
				return "You do not stop somebody walking out into the dry. You give them water, and you watch until they are small.";
			case VoiceOccasion.Wedding:
				return "In the canyons a marriage is two households agreeing to share one well. I notice nobody here asked about the well.";
			case VoiceOccasion.Feast:
				return "A feast day in the canyons meant the caravan had come. Nothing has come. We are the thing that came.";
			default:
				return PlainLine(Occasion);
			}
		}

		private static string HillLine(VoiceOccasion Occasion)
		{
			switch (Occasion)
			{
			case VoiceOccasion.StageUp:
				return "I have watched this place from the ridge every evening since I came. It has more lamps in it now than it had people.";
			case VoiceOccasion.RaidRepelled:
				return "I saw them from the high side before anyone else did. That is the whole of what I did, and it was enough.";
			case VoiceOccasion.ThirstBroken:
				return "The cistern is making that sound again. The deep one. The full one. I slept the whole night on it.";
			case VoiceOccasion.MealShared:
				return "In the hills a shared table meant something had died and the herd had to be eaten before it spoiled. This is a better use for one.";
			case VoiceOccasion.CitizenLost:
				return "I carried their pack as far as the ridge. Then I came back, and that is the only difference between us.";
			case VoiceOccasion.Wedding:
				return "Up on the ridge you could always tell which house was a new couple's. It was the one with the fire lit too late.";
			case VoiceOccasion.Feast:
				return "From the ridge tonight you would count more fires than roofs. That is what the day is for.";
			default:
				return PlainLine(Occasion);
			}
		}

		private static string FlowerFieldLine(VoiceOccasion Occasion)
		{
			switch (Occasion)
			{
			case VoiceOccasion.StageUp:
				return "At home a place this size gets a name and a song to go with it. I do not know the song yet. Somebody ought to start it.";
			case VoiceOccasion.RaidRepelled:
				return "Nobody ever raided the flower fields. There was nothing there worth the walk. It is a strange comfort, being worth attacking.";
			case VoiceOccasion.ThirstBroken:
				return "Everything I ever loved had to be watered. It took me until this week to work out that included me.";
			case VoiceOccasion.MealShared:
				return "The fields were beautiful and they fed nobody. I would trade every acre of them for this table.";
			case VoiceOccasion.CitizenLost:
				return "They asked me to come with them. I said the ground here has been honest with me. So has the thirst, I suppose.";
			case VoiceOccasion.Wedding:
				return "In the fields we threw petals, which was pretty and fed nobody. Here they got bread. I think they preferred the bread.";
			case VoiceOccasion.Feast:
				return "The fields had a day for everything and food for none of them. This one has both, and I am still adjusting.";
			default:
				return PlainLine(Occasion);
			}
		}

		private static string RustWellLine(VoiceOccasion Occasion)
		{
			switch (Occasion)
			{
			case VoiceOccasion.StageUp:
				return "Nothing at the wells ever got bigger. It only ever got broken slower. This is the other thing, and I did not know it happened.";
			case VoiceOccasion.RaidRepelled:
				return "At the wells, when men came for the pumps, we gave them the pumps. Nobody here even put it forward.";
			case VoiceOccasion.ThirstBroken:
				return "At the wells the water came back orange and we drank it anyway. This came back clear. I had to sit down.";
			case VoiceOccasion.MealShared:
				return "At the wells we ate what the machines left us. Nothing on this table had a serial on it. That is worth saying out loud.";
			case VoiceOccasion.CitizenLost:
				return "People left the wells the same way. One, and then one. Then you look up and it is you and the rust.";
			case VoiceOccasion.Wedding:
				return "Nobody married at the wells. There was no point promising anybody a future there. Watch me stand here and promise one.";
			case VoiceOccasion.Feast:
				return "At the wells the calendar was whatever the machines said. It is a strange freedom, keeping a day because it is the day.";
			default:
				return PlainLine(Occasion);
			}
		}

		private static string GroveLine(VoiceOccasion Occasion)
		{
			switch (Occasion)
			{
			case VoiceOccasion.StageUp:
				return "The grove taught me that anything growing this fast has to be fed. Feed it, and you will hear no complaint from me about the crowd.";
			case VoiceOccasion.RaidRepelled:
				return "They wanted the stores. We keep the stores because we share them. So they were asking for all of us, and all of us said no.";
			case VoiceOccasion.ThirstBroken:
				return "The grove died the year the rain went around us. I have been waiting to see whether this place would do the same. It did not.";
			case VoiceOccasion.MealShared:
				return "The grove fed a whole village and asked nothing back. I never understood what that was worth until I had to leave it.";
			case VoiceOccasion.CitizenLost:
				return "Everyone who leaves is walking back to somewhere green they remember. I remember mine too. It is gone.";
			case VoiceOccasion.Wedding:
				return "In the grove the whole village walked the couple to their door and then stood outside singing until they gave up and joined in. We should bring that back.";
			case VoiceOccasion.Feast:
				return "The grove kept every feast it could afford and two it could not. I have never been able to decide which ones I remember.";
			default:
				return PlainLine(Occasion);
			}
		}

		/// <summary>
		/// The register for a speaker whose origin the roll cannot name. Written to stand on its
		/// own, not to read like a missing string: a settler with no recorded country is still a
		/// person with an opinion.
		/// </summary>
		private static string PlainLine(VoiceOccasion Occasion)
		{
			switch (Occasion)
			{
			case VoiceOccasion.StageUp:
				return "It is bigger than it was. I do not think any of us planned that. It kept happening, and we kept staying.";
			case VoiceOccasion.RaidRepelled:
				return "They came, and the wall was where it was meant to be, and so were we.";
			case VoiceOccasion.ThirstBroken:
				return "The stores are wet again. Nobody is saying much about it. That is how you know how bad it got.";
			case VoiceOccasion.MealShared:
				return "Food is better with company. That is not wisdom. It is only true.";
			case VoiceOccasion.CitizenLost:
				return "One less at the table. We noticed. That is what I want written down: we noticed.";
			case VoiceOccasion.Wedding:
				return "They were already under the one roof. Now the rest of us have said so out loud, which is the part that costs.";
			case VoiceOccasion.Feast:
				return "It is the day. We did not decide that. We only decided to eat on it.";
			case VoiceOccasion.FounderRegarded:
				return "People talk. It is not always unkind and it is never quiet. You will hear it either way, so you may as well hear it from me.";
			default:
				return "";
			}
		}
	}
}
