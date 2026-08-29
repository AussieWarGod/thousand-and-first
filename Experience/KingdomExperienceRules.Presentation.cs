using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>Atomic audience-plus-body authority for unsolicited embodied presentations.</summary>
	public static partial class KingdomExperienceRules
	{
		public static bool TryReservePresentation(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomExperienceAudienceReceipt Audience,
			KingdomExperienceBodyReservation Bodies, int LiveTransientBodies,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (!TryValidate(Ledger, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure, out Fault,
					out Failure);
			if (LiveTransientBodies < 0 || LiveTransientBodies >
				KingdomSharedBodyCapacityRules.MaxLegacyFoundationClaims)
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"live transient body count is invalid", out Fault, out Failure);
			if (!ValidAudienceRequest(Ledger, Audience, out Fault, out Failure)
				|| !ValidBodyRequest(Ledger, Bodies, out Fault, out Failure)) return false;
			if (!SamePresentation(Audience, Bodies))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"presentation audience and bodies name different evidence", out Fault,
					out Failure);

			int audienceIndex = AudienceIndex(Ledger, Audience.ReservationId);
			int bodyIndex = BodyIndex(Ledger, Bodies.ReservationId);
			bool exactAudience = audienceIndex >= 0
				&& Same(Ledger.Audiences[audienceIndex], Audience);
			bool exactBodies = bodyIndex >= 0
				&& Same(Ledger.BodyReservations[bodyIndex], Bodies);
			if (audienceIndex >= 0 && !exactAudience)
				return Refuse(KingdomExperienceCapacityFault.DuplicateMismatch,
					"audience reservation identity already names different evidence", out Fault,
					out Failure);
			if (bodyIndex >= 0 && !exactBodies)
				return Refuse(KingdomExperienceCapacityFault.DuplicateMismatch,
					"body reservation identity already names different evidence", out Fault,
					out Failure);
			if (exactAudience && exactBodies) return true;
			if (ExpectedRevision != Ledger.Revision)
				return Refuse(KingdomExperienceCapacityFault.RevisionConflict,
					"experience presentation revision conflict", out Fault, out Failure);
			if (!exactAudience && !AudienceCapacityAvailable(Ledger, Audience,
				out Fault, out Failure)) return false;
			if (!exactBodies && !BodyCapacityAvailable(Ledger, Bodies, LiveTransientBodies,
				out Fault, out Failure)) return false;
			if (Ledger.Revision == long.MaxValue)
				return Refuse(KingdomExperienceCapacityFault.RevisionExhausted,
					"experience revision is exhausted", out Fault, out Failure);

			KingdomExperienceLedger candidate = Clone(Ledger);
			if (!exactAudience) candidate.Audiences.Add(Copy(Audience));
			if (!exactBodies) candidate.BodyReservations.Add(Copy(Bodies));
			candidate.Audiences.Sort(delegate(KingdomExperienceAudienceReceipt A,
				KingdomExperienceAudienceReceipt B)
				{ return string.CompareOrdinal(A.ReservationId, B.ReservationId); });
			candidate.BodyReservations.Sort(delegate(KingdomExperienceBodyReservation A,
				KingdomExperienceBodyReservation B)
				{ return string.CompareOrdinal(A.ReservationId, B.ReservationId); });
			candidate.Revision++;
			if (!TryValidate(candidate, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest, Failure,
					out Fault, out Failure);
			Ledger.CopyFrom(candidate); return true;
		}

		public static bool TryReleasePresentation(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string AudienceReservationId, string BodyReservationId,
			string SourceId, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (!TryValidate(Ledger, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure, out Fault,
					out Failure);
			if (!TypedId(AudienceReservationId, "taf:experience-audience:")
				|| !TypedId(BodyReservationId, "taf:experience-body:")
				|| !KernelSemanticId.IsValid(SourceId))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"presentation release identity is invalid", out Fault, out Failure);
			int audienceIndex = AudienceIndex(Ledger, AudienceReservationId);
			int bodyIndex = BodyIndex(Ledger, BodyReservationId);
			if (audienceIndex < 0 && bodyIndex < 0) return true;
			if ((audienceIndex >= 0 && Ledger.Audiences[audienceIndex].SourceId != SourceId)
				|| (bodyIndex >= 0 && Ledger.BodyReservations[bodyIndex].SourceId != SourceId))
				return Refuse(KingdomExperienceCapacityFault.OwnershipMismatch,
					"presentation release source does not own both reservations", out Fault,
					out Failure);
			if (ExpectedRevision != Ledger.Revision)
				return Refuse(KingdomExperienceCapacityFault.RevisionConflict,
					"experience presentation release revision conflict", out Fault, out Failure);
			if (Ledger.Revision == long.MaxValue)
				return Refuse(KingdomExperienceCapacityFault.RevisionExhausted,
					"experience revision is exhausted", out Fault, out Failure);

			KingdomExperienceLedger candidate = Clone(Ledger);
			if (audienceIndex >= 0) candidate.Audiences.RemoveAt(audienceIndex);
			if (bodyIndex >= 0) candidate.BodyReservations.RemoveAt(bodyIndex);
			candidate.Revision++;
			if (!TryValidate(candidate, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure,
					out Fault, out Failure);
			Ledger.CopyFrom(candidate); return true;
		}

		private static bool SamePresentation(KingdomExperienceAudienceReceipt A,
			KingdomExperienceBodyReservation B)
		{
			return A.RealmId == B.RealmId && A.SettlementId == B.SettlementId
				&& A.SourceId == B.SourceId && A.Lane == B.Lane && A.OptionKind == B.OptionKind
				&& A.CauseTick == B.CauseTick && A.ReservedTick == B.ReservedTick
				&& A.EnableEpoch == B.EnableEpoch;
		}

	}
}
