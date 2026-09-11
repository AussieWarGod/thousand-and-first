namespace ThousandAndFirst
{
	/// <summary>
	/// Which generation of the founding heart's root an authority is being asked about.
	///
	/// <para>The heart had exactly one retirable identity for as long as its root could not
	/// change: the works slot the founding rite itself consumed. A rung climb retires a second
	/// one -- the root the terminal record bound -- and an authority asked about that identity
	/// while it is hard-wired to the works slot answers false for every plan, which is how a whole
	/// settlement's construction pass came to halt after its heart rose (issue #162). Naming the
	/// generation makes the question answerable without making it open: each generation says which
	/// record of the plan's own must name the identity, and nothing else may nominate one.</para>
	/// </summary>
	public enum KingdomFoundingHeartRetiredGeneration
	{
		/// <summary>The works slot of the founding rite. A deterministic identity of the plan.
		/// </summary>
		Works = 0,

		/// <summary>The root the sealed terminal record bound, retired by a later rung climb.
		/// </summary>
		Final = 1
	}
}
