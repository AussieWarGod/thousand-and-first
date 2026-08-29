namespace ThousandAndFirst
{
	/// <summary>
	/// The wire for the curiosity book and the civic-lead book: two magics, one frame, one rule
	/// about strangers.
	/// <para>
	/// Wire revision 1 was <c>magic | version | revision | count | rows</c> and nothing else. It
	/// had no way to tell a book written by a later build from a book eaten by a bad sector, so
	/// every unfamiliar version had to be called damage &mdash; safe, because damage is kept and
	/// defended, but a lie about half the cases. Revision 2 closes the frame with a SHA-256 of
	/// everything before it. That tail is a permanent promise and not a v2 detail: any later
	/// revision must still end with a digest of its own preceding bytes, because that is the only
	/// thing that lets a build too old to read a payload still prove the payload is intact.
	/// </para>
	/// <para>
	/// Verify integrity, then interpret. The version is read, the digest is checked, and only then
	/// is anything believed about the contents. A book whose digest still covers it and which
	/// declares a revision beyond ours is <see cref="KingdomCuriosityBookState.FutureOpaque"/>:
	/// held byte-for-byte, never edited, written back exactly as it came. A book at a revision we
	/// do know that will not read is <see cref="KingdomCuriosityBookState.Quarantined"/>, and
	/// keeps its real bytes as the evidence of what went wrong.
	/// </para>
	/// <para>
	/// What the digest is not: a signature. There is no secret behind it, so SHA-256 detects
	/// change and says nothing whatever about who wrote the bytes &mdash; a deliberate edit that
	/// recomputes the tail passes exactly as the original does. It is claimed for one thing only,
	/// and it is enough for that one thing: a payload from a build too new to read can still be
	/// shown to be internally whole, which is what separates a lawful successor from a bad sector.
	/// </para>
	/// </summary>
	public static partial class KingdomCuriosityLeadCodec
	{
		/// <summary>"TCU1" and "TCL1" in wire order. The magic names the family, not the
		/// revision; the revision is the four bytes after it.</summary>
		internal const int CuriosityMagic = 0x31554354;
		internal const int LeadMagic = 0x314C4354;

		public const int FirstWireVersion = 1;

		/// <summary>
		/// The first revision that closes its frame with a digest, and a promise rather than a
		/// detail: every revision from this one on must end with a SHA-256 of its own preceding
		/// bytes. That is the whole basis on which a build too old to read a payload can still
		/// prove the payload is intact, so no later revision may drop it or move it.
		/// </summary>
		public const int FirstDigestVersion = 2;

		/// <summary>
		/// How far each book can read, declared separately because they are not the same book.
		/// The curiosity book learned a field at revision 2; the civic-lead book learned nothing
		/// and is still written at revision 1. A build that claimed to understand a revision it
		/// has never seen a field of would quarantine a lawful successor and call it damage,
		/// which is the one mistake this frame exists to stop.
		/// </summary>
		public const int CuriosityHighestKnownVersion = 2;
		public const int LeadHighestKnownVersion = 1;

		// Frame: magic 4, wire revision 4, book revision 8, row count 4 -- then the rows, then
		// (from revision 2) a SHA-256 over every byte before it.
		internal const int MagicBytes = 4;
		internal const int VersionBytes = 4;
		internal const int RevisionBytes = 8;
		internal const int CountBytes = 4;
		public const int HeaderBytes = MagicBytes + VersionBytes + RevisionBytes + CountBytes;
		public const int DigestBytes = 32;

		/// <summary>Every length prefix and every fixed-width primitive, named once so the caps
		/// below are arithmetic rather than assertion.</summary>
		internal const int LengthPrefixBytes = 4;
		internal const int Int32Bytes = 4;
		internal const int Int64Bytes = 8;
		internal const int ByteBytes = 1;

		/// <summary>The widest a UTF-16 char can become in UTF-8 without a surrogate partner;
		/// a surrogate pair costs four bytes for two chars, which is cheaper per char.</summary>
		internal const int MaxUtf8BytesPerChar = 3;

		internal const int IdFieldBytes =
			LengthPrefixBytes + KingdomCuriosityRules.MaxIdChars * MaxUtf8BytesPerChar;
		internal const int TextFieldBytes =
			LengthPrefixBytes + KingdomCuriosityRules.MaxText * MaxUtf8BytesPerChar;
		internal const int CategoryFieldBytes =
			LengthPrefixBytes + KingdomCuriosityRules.MaxCategoryChars * MaxUtf8BytesPerChar;

		/// <summary>A canonical locator's world segment may be non-ASCII; its numerals and
		/// separators cannot. See <see cref="KingdomCuriosityRules.MaxLocatorChars"/>.</summary>
		internal const int LocatorFieldBytes = LengthPrefixBytes
			+ KingdomCuriosityRules.MaxWorldIdChars * MaxUtf8BytesPerChar
			+ KingdomCuriosityRules.LocatorSeparators
			+ KingdomCuriosityRules.MaxLocatorNumericChars;

		/// <summary>A derived lead identity is a fixed ASCII prefix and sixty-four hex digits.</summary>
		internal const int LeadIdFieldBytes =
			LengthPrefixBytes + KingdomCivicLeadRules.LeadIdChars;

		/// <summary>An always-absent nullable string still costs its four-byte marker. The wire
		/// revision 1 cap forgot these thirty-two bytes across eight lead rows.</summary>
		internal const int AbsentStringBytes = LengthPrefixBytes;

		/// <summary>Row version, state, four ids, two counters, three prose fields, one locator,
		/// one category and two ticks.</summary>
		public const int MaxCuriosityRowBytes = Int32Bytes + ByteBytes
			+ IdFieldBytes + Int32Bytes + IdFieldBytes + Int32Bytes + TextFieldBytes
			+ IdFieldBytes + IdFieldBytes + LocatorFieldBytes + TextFieldBytes + TextFieldBytes
			+ Int64Bytes + Int64Bytes + CategoryFieldBytes;

		/// <summary>Row version, phase, two ids, one counter, a derived identity, a locator,
		/// two prose fields, one tick and the absent fault marker.</summary>
		public const int MaxCivicLeadRowBytes = Int32Bytes + ByteBytes
			+ IdFieldBytes + Int32Bytes + IdFieldBytes + LeadIdFieldBytes + LocatorFieldBytes
			+ TextFieldBytes + TextFieldBytes + Int64Bytes + AbsentStringBytes;

		/// <summary>
		/// The largest book this build will <b>write</b>, from the arithmetic above.
		/// </summary>
		public const int ExactCuriosityBookBytes = HeaderBytes
			+ KingdomCuriosityBook.MaxRows * MaxCuriosityRowBytes + DigestBytes;

		public const int ExactLeadBookBytes = HeaderBytes
			+ KingdomCivicLeadBook.MaxRows * MaxCivicLeadRowBytes + DigestBytes;

		/// <summary>
		/// The largest book this build will <b>accept</b>, and a number that does not move.
		/// <para>
		/// These are the caps the first writer declared, and they are stable for two reasons that
		/// pull in the same direction. Looking backwards, they are exactly what that writer would
		/// emit before refusing itself, so no save on any disk is larger than this &mdash; a
		/// revision 1 curiosity row at its widest was 7,337 bytes and three of them under a
		/// twenty-byte frame is 22,031 exactly. Looking forwards, a later build may spend its
		/// bytes differently than today's row arithmetic predicts, and a successor refused for
		/// being a hundred bytes over what <i>we</i> would have written is a future called damage.
		/// </para>
		/// <para>
		/// So the two caps do different jobs. What we accept and keep is bounded here; what we
		/// author is bounded by the exact arithmetic, which is smaller and must stay smaller.
		/// </para>
		/// </summary>
		public const int MaxCuriosityBookBytes = 22031;
		public const int MaxLeadBookBytes = 37708;

		/// <summary>
		/// The larger of the two, and deliberately not their sum: the books are written and read
		/// one at a time and never share an envelope. Both caps carry the digest allowance even
		/// though the civic-lead book does not yet spend it, because a cap has to be able to hold
		/// a lawful later book as well as one of ours &mdash; a successor refused for being
		/// thirty-two bytes over would be exactly the future-called-damage mistake.
		/// </summary>
		public const int MaxBookBytes = MaxCuriosityBookBytes > MaxLeadBookBytes
			? MaxCuriosityBookBytes : MaxLeadBookBytes;

		public static bool TryEncode(KingdomCuriosityBook book, out byte[] bytes,
			out string failure)
		{
			bytes = null; failure = null;
			if (book == null) return Refuse("curiosity book is absent", out failure);
			if (!Defined(book.State))
				return Refuse(UndefinedState("curiosity", book.State)
					+ "; nothing is written for it", out failure);
			if (book.State != KingdomCuriosityBookState.Compatible)
				return TryReemitOpaque(book.State, book.OpaquePayload, book.OpaqueVersion,
					CuriosityMagic, MaxCuriosityBookBytes, CuriosityHighestKnownVersion,
					"curiosity", out bytes, out failure);
			return TryWriteCuriosity(book, out bytes, out failure);
		}

		public static bool TryEncode(KingdomCivicLeadBook book, out byte[] bytes,
			out string failure)
		{
			bytes = null; failure = null;
			if (book == null) return Refuse("civic-lead book is absent", out failure);
			if (!Defined(book.State))
				return Refuse(UndefinedState("civic-lead", book.State)
					+ "; nothing is written for it", out failure);
			if (book.State != KingdomCuriosityBookState.Compatible)
				return TryReemitOpaque(book.State, book.OpaquePayload, book.OpaqueVersion,
					LeadMagic, MaxLeadBookBytes, LeadHighestKnownVersion,
					"civic-lead", out bytes, out failure);
			return TryWriteLeads(book, out bytes, out failure);
		}

		/// <summary>
		/// Whether a book's state is one this build has an answer for.
		/// <para>
		/// The three named states are the whole vocabulary, and a value outside them is not a
		/// fourth case to be handled leniently &mdash; it is a book whose own account of itself
		/// this build cannot read. Every place that switches on the state asks this first, so a
		/// number cast into the enum can never fall through to whichever branch happened to be
		/// written last.
		/// </para>
		/// </summary>
		public static bool Defined(KingdomCuriosityBookState state)
		{
			return state == KingdomCuriosityBookState.Compatible
				|| state == KingdomCuriosityBookState.FutureOpaque
				|| state == KingdomCuriosityBookState.Quarantined;
		}

		internal static string UndefinedState(string family, KingdomCuriosityBookState state)
		{
			return "the " + family + " book reports state " + (int)state
				+ ", which this build does not define";
		}

		internal static bool Refuse(string text, out string failure)
		{ failure = text; return false; }
	}
}
