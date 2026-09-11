using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>
		/// The founder is told ONCE that a heart's climb is stuck, and the saying is taken back
		/// the moment it is not.
		///
		/// <para>A stuck climb is a permanent refusal: the seal reports the settlement as under
		/// inspection rather than malformed, which is correct, but it also dedupes its own line
		/// and the settlement's daily one is suppressed -- so without this the condition would be
		/// permanent AND silent. The standard once-only shape applies: a zone-side flag set the
		/// first time the case is CLASSIFIED -- never merely read, so a duplicated root, which is
		/// malformed and not an inspection, is never announced -- and cleared where the climb
		/// finishes, so a heart that sticks, finishes and sticks again is said about twice.</para>
		/// </summary>
		internal static void NoteClimbUnderInspection(Zone Z, int RowWorkId)
		{
			if (Z == null || !HasPendingClimb(Z, RowWorkId, out string retired,
				out KingdomConstructionJob job) || job == null) return;
			bool held = !string.IsNullOrEmpty(
				Z.GetZoneProperty(FoundingHeartClimbHeldProperty, null));
			if (!KingdomFoundingHeartChainRules.SaysClimbHold(held, true)) return;
			Z.SetZoneProperty(FoundingHeartClimbHeldProperty, job.Id);
			if (Z.GetZoneProperty(FoundingHeartClimbHeldProperty, null) != job.Id) return;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			system?.Ledger?.Note("The heart's raised rung has not finished settling, so the "
				+ "kingdom's seal is waiting on it. Nothing is sealed until that improvement "
				+ "closes or is inspected.");
			KingdomLog.Log("founding heart: climb under inspection; retired=" + retired
				+ "; job=" + job.Id + "; phase=" + job.Phase);
		}

		/// <summary>
		/// The saying is taken back where the climb actually finishes. A completed climb never
		/// reaches the pending read again -- the chain binds the successor and the witness stops
		/// asking -- so the settle is the only place that can clear the hold. Without this a heart
		/// that sticks, finishes, and sticks again would be silent the second time.
		/// </summary>
		internal static void ClearClimbHold(Zone Z, string RetiredId)
		{
			if (Z == null || string.IsNullOrEmpty(RetiredId)) return;
			bool held = !string.IsNullOrEmpty(
				Z.GetZoneProperty(FoundingHeartClimbHeldProperty, null));
			if (!KingdomFoundingHeartChainRules.ReleasesClimbHold(held, false)) return;
			if (!KingdomFoundingHeartTerminalRules.TryDecode(
					Z.GetZoneProperty(FoundingHeartTerminalProperty, null), out var prior)
				|| prior.FinalId != RetiredId) return;
			Z.SetZoneProperty(FoundingHeartClimbHeldProperty, null);
		}

		/// <summary>
		/// The other way a climb stops being owed an outcome: it was CANCELLED. The completion
		/// path clears the hold where the receipt completes, but a cancelled improvement never
		/// reaches it, and the root stays absent -- so the witness keeps asking, reads "not
		/// pending", and would leave a saying standing over a climb that is finished with. This
		/// is where that saying is taken back, from the one place that still looks: the witness
		/// itself, on a row whose root is absent and whose climb is no longer pending.
		/// </summary>
		internal static void ReleaseSettledClimbHold(Zone Z, int RowWorkId)
		{
			if (Z == null || RowWorkId == 0) return;
			bool held = !string.IsNullOrEmpty(
				Z.GetZoneProperty(FoundingHeartClimbHeldProperty, null));
			bool pending = HasPendingClimb(Z, RowWorkId, out string retired, out _);
			if (!KingdomFoundingHeartChainRules.ReleasesClimbHold(held, pending)
				|| string.IsNullOrEmpty(retired)) return;
			Z.SetZoneProperty(FoundingHeartClimbHeldProperty, null);
		}
	}
}
