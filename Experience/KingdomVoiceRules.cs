using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
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
	public static partial class KingdomVoiceRules
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
	}
}
