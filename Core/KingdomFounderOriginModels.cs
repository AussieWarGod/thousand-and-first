namespace ThousandAndFirst
{
	/// <summary>
	/// How far one founder's origin accounting has got. The receipt that carries this is bound to
	/// an exact body, an exact profile and an exact owning city, so it can never be read as
	/// somebody else's obligation.
	/// </summary>
	internal enum KingdomFounderOriginState
	{
		/// <summary>The obligation is recorded and the before image is frozen. Nothing has been
		/// written to the origin label or the tally yet.</summary>
		Prepared = 1,

		/// <summary>The origin was written and the tally was raised by exactly one, and both were
		/// measured afterwards. This is the guard that forbids a second increment forever.</summary>
		Completed = 2,

		/// <summary>
		/// The accounting cannot be settled either way and never will be. Terminal, said once, and
		/// never retried. Note what it does NOT claim: not that this founder is missing from the
		/// tally. An interruption after the increment RETAINS it, and nothing left behind can tell
		/// that case from one before it, so the tally is left exactly as it stands and described as
		/// unresolved rather than as short. Reached only from a state the world could not have
		/// produced without an interruption inside the two-write block, from a name held in the
		/// wrong property table, or from a foreign or corrupt reading.
		/// </summary>
		Quarantined = 3
	}

	/// <summary>
	/// Which of the engine's two property tables a name actually occupies. The tables are separate
	/// dictionaries, so a name can be in the text one, the number one, NEITHER, or BOTH, and asking
	/// only about the text table cannot tell "absent" apart from "present as a number".
	/// </summary>
	internal enum KingdomFounderPropertyShape
	{
		/// <summary>In neither table.</summary>
		Absent = 0,

		/// <summary>In the text table only, which is the only shape this accounting writes.
		/// </summary>
		Text = 1,

		/// <summary>In the number table only. Not ours, and it is not treated as absent.</summary>
		Number = 2,

		/// <summary>In both tables at once. Ambiguous by construction.</summary>
		Both = 3
	}

	/// <summary>What one accounting attempt did.</summary>
	internal enum KingdomFounderOriginOutcome
	{
		/// <summary>The origin and exactly one unit of tally were applied and measured.</summary>
		Applied = 0,

		/// <summary>A completed receipt already accounts for this founder. Nothing was written.
		/// </summary>
		AlreadySettled = 1,

		/// <summary>Nothing further will be attempted for this founder. This is the transition
		/// INTO the terminal state, so it is the one reading that is worth saying out loud.
		/// </summary>
		Quarantined = 2,

		/// <summary>This founder was already quarantined by an earlier attempt. Nothing is written
		/// and nothing is said: the once-only announcement was made when it happened.</summary>
		AlreadyQuarantined = 3
	}

	/// <summary>
	/// One founder's durable, identity-bound accounting obligation.
	/// <para>
	/// It is deliberately NOT the origin label. The label says what somebody is; this says whether
	/// this settlement has already counted them, and it binds the exact body so no other body,
	/// profile or city can satisfy it.
	/// </para>
	/// </summary>
	internal sealed class KingdomFounderOriginReceipt
	{
		internal readonly string BodyId;
		internal readonly string Profile;
		internal readonly string CityId;

		/// <summary>The tally this settlement held for this profile before the obligation was
		/// taken on. Never negative.</summary>
		internal readonly int Before;

		internal readonly KingdomFounderOriginState State;

		internal KingdomFounderOriginReceipt(string BodyId, string Profile, string CityId,
			int Before, KingdomFounderOriginState State)
		{
			this.BodyId = BodyId ?? "";
			this.Profile = Profile ?? "";
			this.CityId = CityId ?? "";
			this.Before = Before;
			this.State = State;
		}

		/// <summary>Whether this receipt is this exact body's, in this city, for this profile.
		/// </summary>
		internal bool Binds(string Body, string ForProfile, string City)
		{
			return string.Equals(BodyId, Body, System.StringComparison.Ordinal)
				&& string.Equals(Profile, ForProfile, System.StringComparison.Ordinal)
				&& string.Equals(CityId, City, System.StringComparison.Ordinal);
		}

		internal KingdomFounderOriginReceipt With(KingdomFounderOriginState Next)
		{
			return new KingdomFounderOriginReceipt(BodyId, Profile, CityId, Before, Next);
		}
	}
}
