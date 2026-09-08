namespace ThousandAndFirst
{
	/// <summary>
	/// Narrow deposit seam. Every engine callback a delivery cannot help running &mdash; creating
	/// the bundle, stamping its count, inserting it &mdash; crosses this interface, so the law
	/// about what may be counted, what may be destroyed, and when the delivery must stop is
	/// written once, in one engine-free place, and can be driven against an adversary that
	/// carries the bundle off mid-callback.
	/// <para>
	/// A bundle is passed back as an opaque handle. Nothing above this seam knows what one is.
	/// </para>
	/// </summary>
	internal interface IKingdomDepositHost
	{
		/// <summary>Room in the exact destination right now, and zero once it has stopped being
		/// a destination at all.</summary>
		int RoomNow();

		/// <summary>What the exact destination physically holds right now.</summary>
		int HeldNow();

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

		/// <summary>Whether the bundle reached nobody: in no inventory and in no cell. Only such
		/// a bundle may ever be withdrawn.</summary>
		bool Ownerless(object Bundle);

		/// <summary>Destroys a bundle the delivery still owns.</summary>
		void Discard(object Bundle);

		/// <summary>Inserts the bundle into the destination and returns what the insertion
		/// accepted. This RUNS the engine's inventory handlers.</summary>
		object Insert(object Bundle);

		/// <summary>Whether this exact bundle is proved standing in this exact destination
		/// carrying the count it was stamped with.</summary>
		bool Landed(object Bundle, object Accepted, int Batch);

		/// <summary>Whether the founder has already been told a delivery to this store ended
		/// uncertain. Set when it is first said and cleared the moment a delivery lands proved,
		/// which is the STANDARDS 7b idiom the full-store saying already uses.</summary>
		bool CustodyAnnounced { get; set; }

		/// <summary>Says, once, that a bundle ended somewhere the keepers cannot account for.
		/// </summary>
		void AnnounceUncertainCustody();
	}
}
