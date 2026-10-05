namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// The identity of the scaffold an improvement's successor landed from, decided from
		/// durable evidence rather than from the volatile scaffold reference (#283).
		/// <para>
		/// <c>r_KingdomImprovement.PollHandover</c> runs <c>GameObject.Validate(ref Scaffold)</c>,
		/// which nulls a reference to a destroyed scaffold, and a landed scaffold is always
		/// destroyed. Every successor the scaffold lands publishes its scaffold's identity as a
		/// write-once removal intent before that scaffold is destroyed
		/// (<c>Growth/KingdomScaffold.Durable.cs</c>), so that intent is the identity a later
		/// handover proves against.
		/// </para>
		/// </summary>
		/// <param name="ReferenceLive">The improvement's scaffold reference is still a live object:
		/// a standing scaffold contradicts a landing.</param>
		/// <param name="ReferencePresent">The reference is non-null (live or stale).</param>
		/// <param name="ReferenceId">The reference's id when present; a stale reference must agree
		/// with the durable intent.</param>
		/// <param name="IntentExact">The successor carries an exact scaffold-removal intent.</param>
		/// <param name="IntentId">The id that intent names.</param>
		/// <param name="GloballyAbsent">No live object anywhere carries that id.</param>
		/// <returns>The landed scaffold's id, or null when the evidence does not prove a landing.
		/// </returns>
		public static string LandedScaffoldIdentity(bool ReferenceLive, bool ReferencePresent,
			string ReferenceId, bool IntentExact, string IntentId, bool GloballyAbsent)
		{
			if (ReferenceLive || !IntentExact || string.IsNullOrEmpty(IntentId)) return null;
			if (ReferencePresent && ReferenceId != IntentId) return null;
			return GloballyAbsent ? IntentId : null;
		}
	}
}
