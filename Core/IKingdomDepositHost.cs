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
		// Two kinds of reading live here, and the difference is the whole of the law above.
		//
		// An ORDINARY reading is whatever the engine ordinarily does, and the engine dispatches:
		// asking an object its count reaches Stacker.Number, which REPAIRS a nonpositive count by
		// assigning one and sending StackCountChangedEvent, and a census asks that of every object
		// it walks. Such a reading is a callback, and may only ever be taken as ADVICE, before a
		// raw re-observation.
		//
		// A RAW reading takes the field and nothing else -- Stacker.StackCount reads _StackCount
		// with no repair and no send. Every FINAL proof, immediately before a mutation and before
		// any credit, must be raw, so the last thing a delivery looks at cannot itself be the
		// thing that moves the bundle.

		/// <summary>Room in the exact destination, as the settlement ordinarily counts it. ADVICE
		/// ONLY: this walks and asks, so it dispatches. It decides how much to ASK for, never
		/// what to do with a body.</summary>
		int RoomNow();

		/// <summary>Room in the exact destination, taken raw, and zero once it has stopped being a
		/// destination at all. This is the reading a batch is finally judged against.
		/// <para>
		/// It can FAIL, and failing is not the same as having no room. A destination's hold is
		/// summed off raw stack counts, which are the engine's own unbounded <c>int</c> fields, so
		/// two honest stacks can total more than a whole count holds; above that there is no room
		/// reading at all, and a delivery that cannot read the room may not judge a batch against
		/// a number it made up. False leaves <paramref name="Room"/> at nothing.
		/// </para>
		/// </summary>
		bool TryRawRoomNow(out int Room);

		/// <summary>
		/// Units OF THE MATERIAL THIS DELIVERY IS MAKING standing in the exact destination right
		/// now, taken raw. Read either side of an insertion, its difference is the only evidence a
		/// bundle that stopped existing actually delivered anything, so it is a CREDIT proof and
		/// must not dispatch.
		/// <para>
		/// It must not be a whole-occupancy reading either: a handler that retires timber and
		/// drops an equal weight of stone would otherwise pay the delivery for timber that never
		/// arrived.
		/// </para>
		/// <para>
		/// It can FAIL for the same reason the room reading can, and here failing matters more: a
		/// wrapped pair of readings agrees mod 2^32, so their difference would look like an exact
		/// gain and MINT credit for a landing that never happened. False is not a gain of nothing;
		/// it is no evidence at all, and no evidence credits nothing and stops the delivery.
		/// </para>
		/// </summary>
		bool TryRawMaterialHeldNow(out int Held);

		/// <summary>One bundle of the material, or null when nothing could be made.</summary>
		object Create();

		/// <summary>Whether this bundle stacks, and so may carry more than one unit.</summary>
		bool Stacks(object Bundle);

		/// <summary>Stamps a count on the bundle. This RUNS the engine's stack-count handlers.
		/// </summary>
		void Stamp(object Bundle, int Count);

		/// <summary>
		/// The count standing on the bundle, taken raw off the field and NOT normalised. This is
		/// the count every batch is finally proved against, including a batch of one, and it must
		/// be able to fail: a malformed body carrying zero or minus one is not a bundle of one
		/// unit, and the engine's own stacking adds the incoming count to whatever it merges
		/// into, so inserting it would take a unit OUT of a stack already standing there.
		/// <para>
		/// There is deliberately no ordinary counterpart: nothing this delivery does needs a
		/// count badly enough to repair one and dispatch for it.
		/// </para>
		/// </summary>
		int RawCountOf(object Bundle);

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
		/// settlement stock. A CREDIT proof: every reading it takes must be raw.</summary>
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
