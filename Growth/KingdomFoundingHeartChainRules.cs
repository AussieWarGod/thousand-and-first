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
	/// <para>NO RESERVATION CLAUSE, AND WHY. The heart's reservation store is keyed by
	/// DETERMINISTIC role identities: KingdomFoundingHeartReservationRules refuses any row whose
	/// id is not StableId(transaction, zone, role), and for the final role that id IS the root the
	/// founding rite minted. A successor therefore cannot be named in that store at all, by
	/// design, so a clause demanding one could only ever refuse -- and while it stood at the
	/// settle it refused every heart climb outright. The chain is proved by the receipt records
	/// instead.</para>
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
		/// A chained generation, proved from the records themselves rather than asserted.
		///
		/// <para>The caller hands over the PRIOR TERMINAL as the plan decoded it -- not a string
		/// it typed -- so the chain link is read out of the digest-sealed blob here; the identity
		/// the retirement authority actually answered for, so a caller cannot claim retirement of
		/// one identity while binding another; and the improvement receipt's own output identity
		/// beside the two receipt facts read off the standing object. A caller with nothing but
		/// booleans cannot satisfy this.</para>
		/// </summary>
		/// <param name="Prior">The terminal the plan's own sealed blob decoded to. Null refuses.
		/// </param>
		/// <param name="RetiredIdentity">The identity the retirement authority proved retired.
		/// </param>
		/// <param name="TerminalFinalId">The identity the ground would be bound to.</param>
		/// <param name="JobOutputId">The improvement receipt's own output identity.</param>
		/// <param name="HasReceipt">The standing object carries that job's construction receipt.
		/// </param>
		/// <param name="HasRemovalProof">The standing object carries the scaffold removal proof
		/// naming the retired identity.</param>
		/// <param name="CustodyProved">The successor is the unique holder of that identity and
		/// names the exact identity it replaced.</param>
		public static bool ChainedGeneration(KingdomFoundingHeartTerminalPlan Prior,
			string RetiredIdentity, string TerminalFinalId, string JobOutputId, bool HasReceipt,
			bool HasRemovalProof, bool CustodyProved)
		{
			// The prior generation is read, not described: an undecodable or malformed record is
			// no chain at all.
			if (!KingdomFoundingHeartTerminalRules.Valid(Prior)) return false;
			if (string.IsNullOrEmpty(RetiredIdentity) || string.IsNullOrEmpty(TerminalFinalId)
				|| string.IsNullOrEmpty(JobOutputId)) return false;
			// The chain link: what the prior generation BOUND is exactly what this one retires.
			if (Prior.FinalId != RetiredIdentity) return false;
			// A record that retires what it binds is not a generation, and a chain that loops
			// back onto the prior record's own predecessor is not one either.
			if (RetiredIdentity == TerminalFinalId) return false;
			if (Prior.PredecessorId == TerminalFinalId) return false;
			// The receipt names the identity being bound, and both receipt facts are read off the
			// standing object rather than assumed.
			if (JobOutputId != TerminalFinalId) return false;
			return HasReceipt && HasRemovalProof && CustodyProved;
		}

		/// <summary>
		/// Whether a successor's predecessor STAMP corroborates the retired identity.
		///
		/// <para>The stamp is written by the plot finish route alone
		/// (<c>KingdomPlot2.31.FinishOutput</c>), and the improvement route -- the only route a
		/// heart rung above the first can climb by -- never writes it. So its absence says
		/// nothing, and demanding it refused every real climb. What it can still do is CONTRADICT:
		/// a successor stamped with some other predecessor is not the one that replaced this
		/// root, and that refuses. Present it must match; absent the receipts carry the link.
		/// </para>
		/// </summary>
		public static bool CorroboratesRetired(string Stamp, string RetiredIdentity)
		{
			if (string.IsNullOrEmpty(RetiredIdentity)) return false;
			return string.IsNullOrEmpty(Stamp) || Stamp == RetiredIdentity;
		}

		/// <summary>
		/// Whether a stuck climb should be SAID now: it is pending, and nothing has been said yet.
		/// </summary>
		public static bool SaysClimbHold(bool Held, bool Pending)
		{
			return Pending && !Held;
		}

		/// <summary>
		/// Whether a saying should be TAKEN BACK now: something was said, and the climb is no
		/// longer pending -- because it completed, or because it was cancelled. Both are outcomes;
		/// a hold may outlive neither.
		/// </summary>
		public static bool ReleasesClimbHold(bool Held, bool Pending)
		{
			return Held && !Pending;
		}

		/// <summary>The whole equality the terminal binding asks: first generation, or a proved
		/// chain. Nothing else is a heart.</summary>
		public static bool BindsGround(string TerminalFinalId, string PlanFinalId,
			KingdomFoundingHeartTerminalPlan Prior, string RetiredIdentity, string JobOutputId,
			bool HasReceipt, bool HasRemovalProof, bool CustodyProved)
		{
			return FirstGeneration(TerminalFinalId, PlanFinalId)
				|| ChainedGeneration(Prior, RetiredIdentity, TerminalFinalId, JobOutputId,
					HasReceipt, HasRemovalProof, CustodyProved);
		}
	}
}
