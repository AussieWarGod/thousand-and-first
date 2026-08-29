#if !TAF_TESTS
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The founding transaction's side of the covenant cut: one question asked before anything is
	/// spent, and one refusal shaped the way the rest of the rite shapes its refusals.
	/// <para>
	/// It lives here rather than inside the transaction's own files for two reasons that point the
	/// same way. The covenant archive is this family's business, so the knowledge of what a
	/// covenant row needs stays with the family that defines it; and a refusal at this point must
	/// leave the basin exactly as full as it was, which is a claim about the founding transaction's
	/// vocabulary and is therefore made in the founding transaction's own words.
	/// </para>
	/// </summary>
	public static partial class KingdomFoundingTransaction
	{
		/// <summary>
		/// Whether this rite's covenant could be recorded, asked once its identities exist and
		/// before its receipt is staged or a dram is measured.
		/// <para>
		/// Only a village charter has a covenant to record, so every other rite passes straight
		/// through. What is proved for a charter is the whole of it: the archive is readable for
		/// this realm, it has room for one more row, and <i>this</i> covenant &mdash; this faction
		/// key, this display name, this ground, this authority &mdash; actually encodes. A rite
		/// that would seal a covenant it could not write down is stopped while the water is still
		/// in the basin.
		/// </para>
		/// </summary>
		/// <param name="Barred">The refusal to return, when there is one. Untouched water.</param>
		private static bool VillageCovenantPreflight(KingdomSystem System,
			KingdomFoundingKind Kind, string TransactionId, string EncodedAuthority,
			string VillageFaction, string VillageDisplayName, Zone Site,
			out KingdomFoundingResult Barred)
		{
			Barred = default(KingdomFoundingResult);
			if (Kind != KingdomFoundingKind.VillageCharter) return true;
			if (KingdomVillageCovenantRuntime.TryPreflight(System, TransactionId, EncodedAuthority,
				VillageFaction, VillageDisplayName, Site == null ? null : Site.ZoneID,
				KingdomVillageCovenantRules.MinimumSealedStandingV1, out string failure)) return true;
			Barred = Result(KingdomFoundingOutcome.Refused,
				KingdomFoundingWaterDisposition.Untouched,
				KingdomFoundingProjection.None, failure);
			return false;
		}

		/// <summary>
		/// Whether the tick a covenant froze is still the tick its site reservation was taken at.
		/// <para>
		/// While the marker is on the ground the two must agree exactly. A row whose tick belonged
		/// to some other reservation is a row about some other rite, and completion is the last
		/// moment anything can notice.
		/// </para>
		/// <para>
		/// Once the marker is gone the question has no answer and must not be invented. Completion
		/// is what releases that reservation, so a run that got as far as clearing it and then
		/// failed comes back to find nothing to compare against &mdash; and refusing there would
		/// turn a rite that had already succeeded into one that could never finish. Absence is
		/// therefore accepted, and the archived row remains the evidence it always was.
		/// </para>
		/// </summary>
		private static bool ArchivedReservationTickStillMatches(Zone Site, long ArchivedTick)
		{
			if (Site == null || ArchivedTick < 0L) return false;
			if (!HasSiteReservation(Site)) return true;
			return TryReadSiteReservation(Site, out _, out _, out _, out _, out _,
				out long marker) && marker == ArchivedTick;
		}
	}
}
#endif
