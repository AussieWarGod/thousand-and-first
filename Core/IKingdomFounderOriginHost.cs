namespace ThousandAndFirst
{
	/// <summary>
	/// The narrow seam one founder's origin accounting reaches the world through. Everything the
	/// transaction touches &#8212; the body's own two properties and the settlement's per-profile
	/// tally &#8212; crosses this interface, so the law about what may be counted, when it may be
	/// counted, and when it must stop can be written once in an engine-free place and driven
	/// against an adversary that changes the tally underneath it or refuses a write.
	/// <para>
	/// EVERY operation here must be a DIRECT read or write with no eventful dispatch. On the live
	/// engine both property writes are plain dictionary assignments and the tally is a plain
	/// dictionary on the seated settlement; the adapter that supplies them cites the exact
	/// decompiled line for each. That is the whole reason the origin and the counter can be
	/// written back to back and then both measured: nothing between them can run and move either
	/// one. An implementation that dispatches breaks this contract, not the law above it.
	/// </para>
	/// <para>
	/// Presence and value are separate readings on purpose. A property that is absent and a
	/// property explicitly set to the empty string are different facts, and the accounting refuses
	/// the second rather than treating it as the first.
	/// </para>
	/// <para>
	/// Presence is reported as a SHAPE, not a boolean, because the engine keeps text and number
	/// properties in two separate tables under one namespace. A name present only in the number
	/// table would read as absent to a text-only question, and the accounting would then write its
	/// own value beside it and count the founder a second time. Every reading here therefore says
	/// which tables the name is actually in.
	/// </para>
	/// </summary>
	internal interface IKingdomFounderOriginHost
	{
		/// <summary>The exact body this obligation is about. Empty or null is refused.</summary>
		string BodyId { get; }

		/// <summary>The exact settlement this obligation belongs to. Empty or null is refused.
		/// </summary>
		string CityId { get; }

		/// <summary>Which tables the accounting property occupies.</summary>
		KingdomFounderPropertyShape ReceiptShape();

		/// <summary>The accounting property's exact text, taken raw. Meaningless unless
		/// <see cref="ReceiptShape"/> is <see cref="KingdomFounderPropertyShape.Text"/>.</summary>
		string RawReceipt();

		/// <summary>Writes the accounting property. A direct write; it must not dispatch.</summary>
		void WriteReceipt(string Wire);

		/// <summary>Which tables the origin label occupies.</summary>
		KingdomFounderPropertyShape OriginShape();

		/// <summary>The origin label's exact text, taken raw.</summary>
		string RawOrigin();

		/// <summary>Writes the origin label. A direct write; it must not dispatch.</summary>
		void WriteOrigin(string Origin);

		/// <summary>
		/// This settlement's recorded tally for one profile, taken raw. False when the settlement
		/// has no entry for it at all, which is a different fact from an entry of zero.
		/// </summary>
		bool TryTally(string Profile, out int Count);

		/// <summary>Writes this settlement's tally for one profile. A direct write.</summary>
		void WriteTally(string Profile, int Count);
	}
}
