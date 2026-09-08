namespace ThousandAndFirst
{
	/// <summary>
	/// Narrow deposit seam. Every engine callback a delivery cannot help running &mdash; creating
	/// the bundle, stamping its count, inserting it, destroying it &mdash; crosses this interface,
	/// so the law about what may be counted, what may be destroyed, and when the delivery must
	/// stop is written once, in one engine-free place, and can be driven against an adversary that
	/// carries the bundle off, vetoes its destruction, or merges it away mid-callback.
	/// <para>
	/// A bundle is passed back as an opaque handle. Nothing above this seam knows what one is.
	/// </para>
	/// </summary>
	internal interface IKingdomDepositHost
	{
		/// <summary>Room in the exact destination right now, and zero once it has stopped being
		/// a destination at all.</summary>
		int RoomNow();

		/// <summary>Units OF THE MATERIAL THIS DELIVERY IS MAKING standing in the exact
		/// destination right now. Read either side of an insertion, its difference is the only
		/// evidence a bundle that stopped existing actually delivered anything. It must not be a
		/// whole-occupancy reading: a handler that retires timber and drops an equal weight of
		/// stone would otherwise pay the delivery for timber that never arrived.</summary>
		int MaterialHeldNow();

		/// <summary>One bundle of the material, or null when nothing could be made.</summary>
		object Create();

		/// <summary>Whether this bundle stacks, and so may carry more than one unit.</summary>
		bool Stacks(object Bundle);

		/// <summary>Stamps a count on the bundle. This RUNS the engine's stack-count handlers.
		/// </summary>
		void Stamp(object Bundle, int Count);

		/// <summary>The count now standing on the bundle, read back after a stamp.</summary>
		int CountOf(object Bundle);

		/// <summary>Whether the bundle still exists at all.</summary>
		bool Alive(object Bundle);

		/// <summary>
		/// Whether the bundle exists AND nobody is holding it: no inventory, no cell, no
		/// equipment slot, no implant socket. A freshly made bundle is in exactly this state, so
		/// this is the delivery's proof that it is still the only party that could be holding
		/// this body &mdash; the fence in front of every mutation and the only licence to destroy.
		/// <para>
		/// This answers for a DEAD bundle too, and answers false for one: a body that no longer
		/// exists is not a body this delivery holds. Implementations must therefore prove the
		/// bundle is alive themselves rather than assume a caller checked, and must not read
		/// missing physics as proof of ownership.
		/// </para>
		/// </summary>
		bool HeldByNobody(object Bundle);

		/// <summary>
		/// Destroys a bundle the delivery has proved it holds, and reports whether the body is
		/// PROVABLY GONE afterwards. Destruction is vetoable, and a veto handler may move the body
		/// before refusing, so a false return is not a no-op: it means custody is no longer
		/// provable and the delivery must stop.
		/// </summary>
		bool Discard(object Bundle);

		/// <summary>Inserts the bundle into the destination and returns what the insertion
		/// accepted. This RUNS the engine's inventory or cell handlers.</summary>
		object Insert(object Bundle);

		/// <summary>Whether this exact bundle is proved standing in this exact destination
		/// carrying the count it was stamped with, with the destination still eligible to hold
		/// settlement stock.</summary>
		bool Landed(object Bundle, object Accepted, int Batch);

		/// <summary>Whether the founder has already been told a delivery to this destination
		/// ended uncertain. Set when it is first said and cleared the moment a delivery lands
		/// proved, which is the STANDARDS 7b idiom the full-store saying already uses.</summary>
		bool CustodyAnnounced { get; set; }

		/// <summary>Says, once, that a bundle ended somewhere the keepers cannot account for.
		/// </summary>
		void AnnounceUncertainCustody();
	}
}
