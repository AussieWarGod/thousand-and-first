namespace ThousandAndFirst
{
	/// <summary>Recovery verdict for one destination provision landing. Mirrors
	/// <c>KingdomScalarReceiptAction</c>: servings alone are never authority, so a landing which
	/// cannot be attributed to this operation's exact marker is cut rather than credited.</summary>
	public enum KingdomPurposeFoodLandingAction : byte
	{
		Refuse = 0,
		Apply = 1,
		AlreadyApplied = 2,
		Continue = 3,
		Interference = 4
	}

	/// <summary>What became of one serving this landing tried to place. The engine's inventory
	/// callbacks may reject, move, replace, mutate, obliterate, or throw after the object is already
	/// in the list, so the aftermath is a measured classification rather than a return code. It
	/// crosses the placement boundary as a value: an exception must never carry it, because a throw
	/// past an out-parameter leaves the caller unable to tell a clean shortfall from a stamped
	/// serving loose outside the larders it measures.</summary>
	public enum KingdomPurposeServingAftermath : byte
	{
		/// <summary>Whole, exact, and inside the exact target larder.</summary>
		Settled = 0,
		/// <summary>Never offered to the engine, so nothing can be stranded. Before the operation
		/// is published this is a clean refusal; after it, content divergence is not capacity.</summary>
		Unavailable = 1,
		/// <summary>Offered, and the aftermath is not provably whole and in place. A stamped
		/// serving may exist where no count can see it, so the transaction cuts.</summary>
		Stranded = 2
	}

	/// <summary>How a durable landing record reads against the marks that survive on the ground.
	/// The two ways a record can outrun its marks are not the same event and must never be
	/// collapsed: provision that was eaten leaves nothing wearing the receipt, while provision a
	/// callback carried out of the measured larders leaves exactly that. Only the first is
	/// finished work.</summary>
	public enum KingdomPurposeLandingRecordState : byte
	{
		/// <summary>The marks still account for the record; ordinary progress.</summary>
		Intact = 0,
		/// <summary>The record leads and nothing anywhere still wears the receipt: the servings
		/// landed and were consumed. Nothing is owed and nothing is minted.</summary>
		Consumed = 1,
		/// <summary>Something outside the measured larders still wears this operation's exact
		/// receipt. The shortfall is a callback's doing, never consumption, and never Delivered.</summary>
		Stranded = 2,
		/// <summary>A count outside its own bounds, which is not a reading at all.</summary>
		Invalid = 3
	}

	/// <summary>What an outstanding callback witness proves about the offer that wrote it. The
	/// witness is stamped on the durable cargo before a serving is ever handed to an inventory, so
	/// it survives a save cut, a refused quarantine publication, and the destruction of the serving
	/// itself. Only an exactly reconciled one-step increment retires it; every other reading keeps
	/// the transaction ambiguous and refuses to offer anything further.</summary>
	public enum KingdomPurposeLandingAttemptState : byte
	{
		/// <summary>No offer is outstanding.</summary>
		Clear = 0,
		/// <summary>The offer this witness names produced exactly the increment it promised, in an
		/// exact partition. It may be retired and the landing may continue.</summary>
		Settled = 1,
		/// <summary>An offer was made and cannot be reconciled &mdash; obliterated, moved, nested,
		/// replaced, mutated, thrown, foreign, or torn. The transaction stays ambiguous and offers
		/// nothing more, however many passes it takes for the quarantine to publish.</summary>
		Ambiguous = 2
	}
}
