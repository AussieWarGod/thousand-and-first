namespace ThousandAndFirst
{
	/// <summary>
	/// Every bound the civic-memory envelope enforces, and where each one came from.
	/// <para>
	/// Not one number here was chosen. Eight wire families are frozen into this authority
	/// existed &mdash; nine sections between them, since O6 and D7 are one family and two books
	/// &mdash; each already carrying its own arithmetic for the largest payload it can lawfully
	/// produce. The envelope's job is to hold them, not to re-judge them, so every per-section cap
	/// below is one of those constants, copied across verbatim and bound back to its original at
	/// runtime by <see cref="KingdomCivicMemoryDerivation"/>, so a family that quietly outgrows
	/// its own maximum stops this authority instead of silently overflowing it.
	/// </para>
	/// <para>
	/// The mirrors are not laziness. Two of the nine sections cannot be named from a file that
	/// has to stay free of the game engine: <c>KingdomCuriosityRules</c> and
	/// <c>KingdomCivicLeadRules</c> both take a <c>KingdomExperienceLedger</c> for their attention
	/// API, which reaches <c>ThousandAndFirst.Simulation</c> and from there <c>XRL</c>. Referencing
	/// their codec here would drag all of that into every part of this authority that is
	/// deliberately testable without a game running. A mirror plus a binding check is honest; a
	/// mirror alone would be a second source of truth waiting to drift.
	/// </para>
	/// <para>
	/// One cap here was not inherited. The village-covenant archive (D9) is the first family added
	/// after this authority existed, so its maximum was chosen against this arithmetic rather than
	/// arriving already frozen with a codec of its own. It is held to the same discipline all the
	/// same: the number below is that codec's declared maximum, bound back to it at runtime, and it
	/// was chosen to sit under the cap an unknown section at that id already carried &mdash; see
	/// <see cref="MaxVillageCovenantBytes"/>.
	/// </para>
	/// </summary>
	public static class KingdomCivicMemoryLimits
	{
		/// <summary>Section ids are stable and numeric: a rename must never move a payload.</summary>
		public const int SectionCivicArtifacts = 1;
		public const int SectionCivicPractice = 2;
		public const int SectionBodyHistory = 3;
		public const int SectionCuriosity = 4;
		public const int SectionCivicLeads = 5;
		public const int SectionTreaty = 6;
		public const int SectionCommunalRite = 7;
		public const int SectionGuestFeast = 8;
		public const int SectionVillageCovenant = 9;

		/// <summary>
		/// Ids start at one. Zero and negatives are refused outright rather than filed under
		/// "future": a stranger's id must at least be a plausible successor to ours, and an id
		/// that could never be allocated is corruption wearing a future's coat.
		/// </summary>
		public const int MinSectionId = 1;

		/// <summary>The lowest and highest ids this build understands. Above is future.</summary>
		public const int FirstKnownSection = SectionCivicArtifacts;
		public const int LastKnownSection = SectionVillageCovenant;
		public const int KnownSectionCount = LastKnownSection - FirstKnownSection + 1;

		// O5/D6. KingdomCivicArtifactsCodec.MaxEnvelopeBytes (Core/KingdomCivicArtifactsCodec.cs:18)
		//   = MaxPayloadBytes + EnvelopeOverheadBytes
		//   = (82 + 8 + 32820 + 32820) + 44 = 65774. The 82-byte identity frame is
		//     a 4-byte length, at most 77 strict UTF-8 realm-id bytes, and one bound byte;
		//     each 32820-byte nested book is 20 + 8 rows * (4 + 4096).
		public const int MaxCivicArtifactsBytes = 65774;

		// D1/D12. KingdomCivicPracticeCodec.MaxEnvelopeBytes (Core/KingdomCivicPracticeCodec.cs:16)
		//   = (IdentityFramingBytes + 8 + MaxSiteBookBytes + MaxServiceBookBytes) + 44
		//   = (82 + 8 + 32820 + 196820) + 44 = 229774. Sites cap at 8 rows; vocation
		//     services retain 16 rows for each of at most three settlements.
		public const int MaxCivicPracticeBytes = 229774;

		// D5. KingdomBodyHistoryCodec.MaxEnvelopeBytes (Core/KingdomBodyHistoryCodec.cs:11)
		//   = MaxPayloadBytes + 44 = (82 + 20 + 8 * (4 + 4096)) + 44 = 32946.
		public const int MaxBodyHistoryBytes = 32946;

		// O6. KingdomCuriosityLeadCodec.MaxCuriosityBookBytes
		//   (Experience/KingdomCuriosityLeadCodec.cs:11) = 22031.
		public const int MaxCuriosityBytes = 22031;

		/// <summary>
		/// D7 — the civic-lead book. <c>KingdomCuriosityLeadCodec.MaxLeadBookBytes</c>
		/// (<c>Experience/KingdomCuriosityLeadCodec.cs:12</c>) = 37708.
		/// <para>
		/// O6 and D7 are one family but two sections, deliberately. That codec has no combined
		/// envelope: it exposes one encode and one decode per book, each with its own magic and
		/// its own declared maximum, and its <c>MaxBookBytes</c> is the <i>larger</i> of the two
		/// rather than their sum. Folding both books into one section would have meant inventing
		/// the framing addend and the total the family itself declines to state. One section per
		/// book keeps every cap a quotation instead of a guess.
		/// </para>
		/// </summary>
		public const int MaxCivicLeadsBytes = 37708;

		// Treaty. KingdomTreatyCodec.MaxEnvelopeBytes (Treaty/KingdomTreatyCodec.cs:12) = 241384,
		//   a 44-byte frame plus a 12-byte ledger plus 16 pacts at 15083 bytes each. That family
		//   states this as an exact size, not merely an upper bound.
		public const int MaxTreatyBytes = 241384;

		// D8. KingdomCommunalRiteCodec.MaxEnvelopeBytes
		//   (Experience/KingdomCommunalRiteCodec.cs:18)
		//   = 102 + 3 * 356 + 44 = 1214.
		public const int MaxCommunalRiteBytes = 1214;

		// O11. KingdomGuestFeastCodec.MaxEnvelopeBytes
		//   (Experience/KingdomGuestFeastCodec.cs:18)
		//   = 102 + 3 * 3979 + 44 = 12083.
		public const int MaxGuestFeastBytes = 12083;

		/// <summary>
		/// D9 &mdash; the village-covenant archive.
		/// <c>KingdomVillageCovenantCodec.MaxEnvelopeBytes</c>
		/// (<c>Core/KingdomVillageCovenantCodec.cs</c>)
		/// = (82 + 20 + 48 * (4 + 4096)) + 44 = 196946. The 82-byte identity frame is a 4-byte
		/// length, at most 77 strict UTF-8 realm-id bytes and one bound byte; the 20-byte archive
		/// header is a magic, a revision, an eight-byte counter and a row count.
		/// <para>
		/// This is the first family added after this envelope existed, so it is the first cap that
		/// had a ceiling to respect rather than only arithmetic to quote. Before this build, id 9
		/// was unknown and therefore held to the widest known cap, <see cref="MaxTreatyBytes"/> at
		/// 241,384 bytes. 196,946 sits below that, which is the property that matters: teaching
		/// this build what section 9 means can only narrow what a payload there may be, never widen
		/// it, so no save that was lawful under the old rule becomes unlawful in a direction that
		/// loses records.
		/// </para>
		/// </summary>
		public const int MaxVillageCovenantBytes = 196946;

		/// <summary>
		/// The sum of the nine frozen per-section maxima, and the only budget any envelope gets.
		/// Unknown future sections are charged against this same total, so a stranger's payload
		/// can never make a save larger than the families this build knows could lawfully have
		/// made it.
		/// </summary>
		public const int MaxCumulativePayloadBytes = MaxCivicArtifactsBytes + MaxCivicPracticeBytes
			+ MaxBodyHistoryBytes + MaxCuriosityBytes + MaxCivicLeadsBytes + MaxTreatyBytes
			+ MaxCommunalRiteBytes + MaxGuestFeastBytes + MaxVillageCovenantBytes;

		/// <summary>
		/// Magic 4, version 4, at least 4 version-owned body bytes, SHA-256 tail 32 — the same
		/// minimum 44-byte frame the frozen families already use. In v1 that first body word is the
		/// section count; a reader must not assume a later version gives it the same meaning.
		/// <para>
		/// This frame is a permanent promise, not a v1 detail. A reader that meets an envelope
		/// from a later build must still be able to find and check the digest without understanding
		/// anything between, which is the whole basis on which a future version can be preserved
		/// byte-for-byte instead of being called corrupt. No future version may move the leading
		/// magic/version or trailing digest.
		/// </para>
		/// </summary>
		public const int EnvelopeOverheadBytes = 44;

		/// <summary>Id 4 and length 4 in front of every section's bytes.</summary>
		public const int SectionFramingBytes = 8;

		/// <summary>
		/// Room for the nine known sections and one future successor apiece.
		/// <para>
		/// This is the one number on this page that no frozen family states, because no frozen
		/// family knows what will be added after it. It is deliberately cheap: the byte budget
		/// above is what actually bounds a save, and eighteen sections cost 144 bytes of framing
		/// between them. Raise it and nothing grows but those 144 bytes.
		/// </para>
		/// </summary>
		public const int MaxSections = KnownSectionCount * 2;

		/// <summary>The largest envelope this build will write or accept.</summary>
		public const int MaxEnvelopeBytes = EnvelopeOverheadBytes
			+ MaxSections * SectionFramingBytes + MaxCumulativePayloadBytes;

		/// <summary>
		/// The cap for one section id. Future ids are held to the largest known cap: this build
		/// cannot know what a later family's arithmetic says, and the treaty ledger is the widest
		/// thing any of them has ever needed.
		/// </summary>
		public static int SectionCap(int Id)
		{
			switch (Id)
			{
			case SectionCivicArtifacts: return MaxCivicArtifactsBytes;
			case SectionCivicPractice: return MaxCivicPracticeBytes;
			case SectionBodyHistory: return MaxBodyHistoryBytes;
			case SectionCuriosity: return MaxCuriosityBytes;
			case SectionCivicLeads: return MaxCivicLeadsBytes;
			case SectionTreaty: return MaxTreatyBytes;
			case SectionCommunalRite: return MaxCommunalRiteBytes;
			case SectionGuestFeast: return MaxGuestFeastBytes;
			case SectionVillageCovenant: return MaxVillageCovenantBytes;
			default: return MaxTreatyBytes;
			}
		}

		/// <summary>Whether this build understands what a section id means.</summary>
		public static bool Known(int Id)
		{
			return Id >= FirstKnownSection && Id <= LastKnownSection;
		}

		/// <summary>Whether an id may appear on the wire at all, known or not.</summary>
		public static bool Allocatable(int Id)
		{
			return Id >= MinSectionId;
		}
	}
}
