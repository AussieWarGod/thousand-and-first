using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// A moment the settlement has something to say about.
	/// <para>
	/// These values are draw identity, not presentation: each one is the
	/// <c>EventKindCode</c> of its own ordinal lane (see <see cref="KingdomVoiceRules"/>), so
	/// renumbering one would re-cast every speaker in every existing save. Add at the end;
	/// never reorder, never reuse a retired number. Zero is absent on purpose &mdash; the
	/// kernel refuses a zero kind code, which would silently cost the first occasion its
	/// deterministic draw.
	/// </para>
	/// </summary>
	public enum VoiceOccasion
	{
		StageUp = 1,
		RaidRepelled = 2,
		ThirstBroken = 3,
		MealShared = 4,
		CitizenLost = 5,

		/// <summary>W4. Two settlers who already shared a roof were married.</summary>
		Wedding = 6,

		/// <summary>W4. A feast kept on a day of Qud's own calendar.</summary>
		Feast = 7,

		/// <summary>W4. What the city's creed makes of the founder's own body. The one occasion
		/// with no per-origin register: what a creed thinks of a mutation is a matter of belief,
		/// not of the country somebody walked out of, so every speaker answers in the plain
		/// one.</summary>
		FounderRegarded = 8
	}

	/// <summary>
	/// Who speaks for the settlement, and what they say.
	/// <para>
	/// The settlement already reports itself accurately. This is the difference between a place
	/// that is administered and one that is inhabited: the same news, in the mouth of a person
	/// on the roll, coloured by the country they walked out of. It adds no announcements &mdash;
	/// every line it composes wraps one the settlement was already going to print.
	/// </para>
	/// <para>
	/// Engine-free, so both halves are tabled: which settler is chosen (deterministically, by
	/// <see cref="CounterRandom"/>, so a reload never recasts a line that has already been read)
	/// and what that settler says.
	/// </para>
	/// </summary>
	public static class KingdomVoiceRules
	{
		/// <summary>
		/// Rules version pinned into every speaker draw's <see cref="SemanticEventKey"/>. The key
		/// owns its rules version forever, so this moves only if the draw itself is redefined in
		/// a way that must not compare equal to what came before &mdash; which would recast every
		/// line already spoken.
		/// </summary>
		private const int VoiceRulesVersion = 1;

		/// <summary>Ordinal lane for speaker draws. Distinct from the chronicle's outsider lane,
		/// so a settlement's rumour drift and its speakers can never shift each other.</summary>
		private const string VoiceEventStreamId = "taf:voices:speaker:v1";

		/// <summary>Only draw made on a speaker key; named rather than passed as a bare literal
		/// because a second purpose on this key would have to pick the next index, not reuse
		/// this one.</summary>
		private const uint SpeakerDrawIndex = 0u;

		/// <summary>
		/// Fixed, all-zero seed, for the reason <c>KingdomChronicle</c> gives at length: domain
		/// separation comes from the settlement id, stream, kind, and ordinal baked into the key,
		/// and who says a line does not need to be unguessable.
		/// </summary>
		private static readonly KernelSeed128 VoiceSeed = default(KernelSeed128);

		/// <summary>What the settlement is called when no one on the roll can speak for it.</summary>
		public const string NobodyAttribution = "a settler";

		/// <summary>
		/// A named settler on the roll, or the absence of one.
		/// <para>
		/// Absence is a first-class value rather than a null: a missing speaker must never eat
		/// the message, and <see cref="Compose"/> is the only place that decides what to do
		/// about it.
		/// </para>
		/// </summary>
		public readonly struct Speaker
		{
			/// <summary>The settler's name as the roll carries it, or null when nobody speaks.</summary>
			public readonly string Name;

			/// <summary>Where they walked in from, one of <see cref="KingdomRules.Origins"/>, or
			/// null when the roll does not say &mdash; which a save written before origins were
			/// recorded, or a roster trimmed unevenly, can both produce.</summary>
			public readonly string Origin;

			public Speaker(string Name, string Origin)
			{
				this.Name = string.IsNullOrEmpty(Name) ? null : Name;
				this.Origin = string.IsNullOrEmpty(Origin) ? null : Origin;
			}

			/// <summary>Nobody. The settlement speaks in its own voice.</summary>
			public static Speaker None
			{
				get { return default(Speaker); }
			}

			/// <summary>True when a real person on the roll was found to say this.</summary>
			public bool HasVoice
			{
				get { return Name != null; }
			}

			/// <summary>How a chronicle line names the speaker: their name, or the unnamed
			/// settler the record has to settle for.</summary>
			public string Attribution
			{
				get { return Name ?? NobodyAttribution; }
			}
		}

		/// <summary>
		/// Picks who speaks for one occasion. The same occasion at the same tick in the same
		/// settlement always draws the same person, in any process, forever &mdash; an ordinary
		/// pseudorandom call cannot promise that, because its cursor depends on every unrelated
		/// roll made since the game started, so a reload would put the words in someone else's
		/// mouth.
		/// </summary>
		/// <param name="Names">The roll, oldest first. Null or empty means nobody is here to
		/// speak, which is not a failure.</param>
		/// <param name="Origins">Origins parallel to <paramref name="Names"/>. May be shorter;
		/// a speaker whose origin the roll has lost still speaks, in the plain register.</param>
		/// <param name="SettlementId">The settlement's kernel id, from
		/// <c>KingdomChronicle.SettlementId</c>. An id outside the <c>taf:</c> grammar costs the
		/// draw but not the voice &mdash; see the return note.</param>
		/// <param name="Occasion">Which moment is being spoken. Owns its own ordinal lane, so two
		/// different occasions on one tick are not forced to share a speaker.</param>
		/// <param name="Ordinal">The tick the moment happened on. Ticks only go forward; a count
		/// of things said would stop rising once any register was trimmed and collapse every
		/// later line onto one speaker.</param>
		/// <returns>A speaker holding a name from <paramref name="Names"/>, or
		/// <see cref="Speaker.None"/> when the roll holds nobody. Never throws. If the kernel
		/// refuses the draw &mdash; a malformed id, or a machine whose crypto provider is failing
		/// &mdash; the roll's eldest speaks: still a real person, still the same one on reload,
		/// just no longer varied.</returns>
		public static Speaker ChooseSpeaker(IList<string> Names, IList<string> Origins, string SettlementId, VoiceOccasion Occasion, ulong Ordinal)
		{
			if (Names == null || Names.Count == 0)
			{
				return Speaker.None;
			}
			// Zero is the standing answer, not a placeholder: if the key or the draw is refused
			// the roll's eldest speaks, which is deterministic without the kernel and is still a
			// real person on the roll.
			int drawn = 0;
			SemanticEventKey key;
			KernelFaultCode fault;
			if (SemanticEventKey.TryCreate(VoiceRulesVersion, SettlementId, VoiceEventStreamId, (uint)Occasion, Ordinal, out key, out fault))
			{
				ulong value;
				if (CounterRandom.TryDrawBelow(VoiceSeed, key, SpeakerDrawIndex, (ulong)Names.Count, out value, out fault))
				{
					drawn = (int)value;
				}
			}
			return SpeakerAtOrAfter(Names, Origins, drawn);
		}

		/// <summary>
		/// Walks forward from the drawn index to the first entry that actually names someone,
		/// wrapping once. A roll can carry a blank &mdash; a settler whose name generator failed
		/// was still counted &mdash; and drawing one of those must not silence a settlement that
		/// has other people standing in it.
		/// </summary>
		private static Speaker SpeakerAtOrAfter(IList<string> Names, IList<string> Origins, int Start)
		{
			for (int step = 0; step < Names.Count; step++)
			{
				int at = (Start + step) % Names.Count;
				string name = Names[at];
				if (!string.IsNullOrEmpty(name))
				{
					string origin = (Origins != null && at < Origins.Count) ? Origins[at] : null;
					return new Speaker(name, origin);
				}
			}
			return Speaker.None;
		}

		/// <summary>
		/// Assembles the finished player-facing line: the settlement's announcement, then the
		/// person who said something about it.
		/// </summary>
		/// <param name="Speaker">Who speaks. <see cref="Speaker.None"/> returns the announcement
		/// untouched &mdash; the settlement's own voice is the fallback, and a missing speaker
		/// never costs the player the news.</param>
		/// <param name="Occasion">The moment, which picks the words.</param>
		/// <param name="Announcement">The line the settlement was already going to print, colour
		/// markup and all. Null or empty yields the quote alone.</param>
		/// <param name="Words">An authored clause to put in the speaker's mouth ahead of their
		/// own, or null. The shared meal passes <c>KingdomRules.MealSpeech</c> here so the
		/// settler answers for the size of the meal before they answer for where they came
		/// from.</param>
		/// <returns>A player-facing string; empty only when there was nothing to announce and
		/// nobody to speak.</returns>
		public static string Compose(Speaker Speaker, VoiceOccasion Occasion, string Announcement, string Words = null)
		{
			string announcement = Announcement ?? "";
			if (!Speaker.HasVoice)
			{
				return announcement;
			}
			string said = Line(Occasion, Speaker.Origin);
			if (!string.IsNullOrEmpty(Words))
			{
				said = string.IsNullOrEmpty(said) ? Words : (Words + " " + said);
			}
			if (string.IsNullOrEmpty(said))
			{
				return announcement;
			}
			string quote = "{{W|" + Speaker.Name + "}}: \"" + said + "\"";
			return (announcement.Length > 0) ? (announcement + " " + quote) : quote;
		}

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
