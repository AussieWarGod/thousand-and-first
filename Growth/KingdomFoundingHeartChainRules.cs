namespace ThousandAndFirst
{
	/// <summary>
	/// Which terminal record the founding heart's ground is allowed to be bound to, once its root
	/// can lawfully change hands.
	///
	/// <para>THE PROBLEM. The heart's final root was one deterministic reserved identity forever,
	/// and every rung above the first climbs by the improvement route, which REPLACES that object
	/// with a successor carrying its own engine identity -- an identity already frozen into the
	/// construction job, its receipt, the layout custody key and the component census rows, so it
	/// can never be renamed onto the reserved one. The binding therefore stopped resolving after
	/// the first climb, sealed recovery refused, and the settlement pass aborted on that ground.
	/// </para>
	///
	/// <para>THE RULE. A terminal is accepted either because it is the FIRST generation -- the
	/// reserved final identity the founding rite minted -- or because it is a CHAINED generation:
	/// the blob that stood before it named exactly this record's predecessor as its own final, the
	/// retiring root's retirement is proved, the improvement's own receipt chain names the new
	/// root, custody is re-keyed to it, and its reservation is re-issued. Five demands, every one
	/// of them proved from a durable record rather than a stamped property, and a chained
	/// generation that drops any one of them is refused exactly as the first generation would be.
	/// </para>
	///
	/// <para>This shard decides; it never reads the world. The caller supplies what it proved, so
	/// the decision is executable outside a live game and the same decision is made at the settle
	/// and at every later recovery.</para>
	/// </summary>
	public static class KingdomFoundingHeartChainRules
	{
		/// <summary>The first generation: the terminal names the reserved final identity the
		/// founding rite minted for this plan. Unchanged, and still the only shape a heart that
		/// has never climbed is allowed to be in.</summary>
		public static bool FirstGeneration(string TerminalFinalId, string PlanFinalId)
		{
			return !string.IsNullOrEmpty(PlanFinalId) && TerminalFinalId == PlanFinalId;
		}

		/// <summary>
		/// A chained generation, proved rather than asserted.
		/// </summary>
		/// <param name="PriorFinalId">The final identity the blob standing before this one named.
		/// Empty when no prior terminal could be decoded, which refuses.</param>
		/// <param name="TerminalPredecessorId">The identity this record retires.</param>
		/// <param name="TerminalFinalId">The identity this record binds the ground to.</param>
		/// <param name="JobOutputId">The improvement receipt's own output identity.</param>
		/// <param name="RetirementProved">The retiring root's retirement authority, proved for
		/// THAT identity.</param>
		/// <param name="ReceiptProved">The improvement's receipt and removal proof, read off the
		/// successor.</param>
		/// <param name="CustodyProved">The successor is the unique holder of its identity and the
		/// saved root key is re-keyed to it.</param>
		/// <param name="ReservationProved">The reservation store issues the final role for the
		/// new identity.</param>
		public static bool ChainedGeneration(string PriorFinalId, string TerminalPredecessorId,
			string TerminalFinalId, string JobOutputId, bool RetirementProved, bool ReceiptProved,
			bool CustodyProved, bool ReservationProved)
		{
			// A chain link is two named identities that differ: a record that retires what it
			// binds, or binds what it retires, is not a generation at all.
			if (string.IsNullOrEmpty(PriorFinalId) || string.IsNullOrEmpty(TerminalPredecessorId)
				|| string.IsNullOrEmpty(TerminalFinalId) || string.IsNullOrEmpty(JobOutputId))
				return false;
			if (TerminalPredecessorId == TerminalFinalId) return false;
			if (PriorFinalId != TerminalPredecessorId) return false;
			if (JobOutputId != TerminalFinalId) return false;
			return RetirementProved && ReceiptProved && CustodyProved && ReservationProved;
		}

		/// <summary>The whole equality the terminal binding asks: first generation, or a proved
		/// chain. Nothing else is a heart.</summary>
		public static bool BindsGround(string TerminalFinalId, string PlanFinalId,
			string PriorFinalId, string TerminalPredecessorId, string JobOutputId,
			bool RetirementProved, bool ReceiptProved, bool CustodyProved, bool ReservationProved)
		{
			return FirstGeneration(TerminalFinalId, PlanFinalId)
				|| ChainedGeneration(PriorFinalId, TerminalPredecessorId, TerminalFinalId,
					JobOutputId, RetirementProved, ReceiptProved, CustodyProved,
					ReservationProved);
		}
	}
}
