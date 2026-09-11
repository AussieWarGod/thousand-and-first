namespace ThousandAndFirst
{
	/// <summary>
	/// Whether a surveyed component belongs to the generation being settled.
	///
	/// <para>The lot survives an authored upgrade unchanged, and the slot name is layout-local, so
	/// during the retag pass the predecessor's component and the successor's retagged component
	/// can stand under one lot at the same slot string while naming different world cells. Counting
	/// by lot and slot alone therefore counts two and quarantines a settlement that is behaving
	/// exactly as designed.</para>
	///
	/// <para>The component token is the discriminator. It is a digest over the lot, the
	/// GENERATION'S snapshot hash, and the placement (slot, layer, x, y, blueprint, stateful
	/// anchor, existing authority), so two generations of one slot carry different tokens while
	/// two copies of one generation carry the same one. Counting by token keeps the duplicate
	/// refusal that matters and drops the collision that never meant anything.</para>
	///
	/// <para>This shard is deliberately pure and engine-free: it decides membership from property
	/// values already read, and knows nothing about objects, zones or surveys. What it must NEVER
	/// become is a filter that excuses a foreign object -- an item whose hash or token does not
	/// match is not counted HERE, and is refused elsewhere by the element checks and the cell
	/// guard that own that question.</para>
	/// </summary>
	public static class KingdomArchitectureComponentCensusRules
	{
		/// <summary>
		/// Whether one surveyed candidate counts towards the settled census of this generation's
		/// slot. All three terms are required: the lot it was stamped under, the slot it names,
		/// and the token that binds that slot to this generation's snapshot and placement.
		/// </summary>
		/// <param name="Lot">The plot identity being settled.</param>
		/// <param name="Slot">The frozen layout slot being settled.</param>
		/// <param name="Token">This generation's component token for that slot.</param>
		/// <param name="CandidateLot">The candidate's own plot identity property.</param>
		/// <param name="CandidateSlot">The candidate's own slot property.</param>
		/// <param name="CandidateToken">The candidate's own component token property.</param>
		public static bool Counts(string Lot, string Slot, string Token,
			string CandidateLot, string CandidateSlot, string CandidateToken)
		{
			if (string.IsNullOrEmpty(Lot) || string.IsNullOrEmpty(Slot)
				|| string.IsNullOrEmpty(Token))
			{
				return false;
			}
			// A candidate missing any of the three cannot be counted. It is not thereby excused:
			// a component without its own slot or token fails ExactComponentString, and a foreign
			// object on a claimed cell is refused by the layout-slot guard.
			if (string.IsNullOrEmpty(CandidateLot) || string.IsNullOrEmpty(CandidateSlot)
				|| string.IsNullOrEmpty(CandidateToken))
			{
				return false;
			}
			return CandidateLot == Lot && CandidateSlot == Slot && CandidateToken == Token;
		}

		/// <summary>
		/// Whether a completed census settles: exactly one component of this generation stands at
		/// this slot. Zero is an absent or unstamped component; more than one is a duplicate of
		/// the SAME generation, which is still a refusal.
		/// </summary>
		public static bool Settled(int Count)
		{
			return Count == 1;
		}
	}
}
