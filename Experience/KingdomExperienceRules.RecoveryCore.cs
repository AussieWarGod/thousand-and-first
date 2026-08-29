using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		private static bool TryRecoverBodiesCore(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomExperienceBodyReservation Request,
			int ProtectedFoundationBodies, bool RetirementOnly,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (!TryValidate(Ledger, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure,
					out Fault, out Failure);
			if (ProtectedFoundationBodies < 0 ||
				ProtectedFoundationBodies >
				KingdomSharedBodyCapacityRules.MaxLegacyFoundationClaims)
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"protected foundation body count is invalid", out Fault, out Failure);
			if (!ValidRecoveryBody(Ledger, Request, RetirementOnly,
				out Fault, out Failure)) return false;
			int index = BodyIndex(Ledger, Request.ReservationId);
			if (index >= 0)
			{
				if (Same(Ledger.BodyReservations[index], Request)) return true;
				return Refuse(KingdomExperienceCapacityFault.DuplicateMismatch,
					"body recovery identity already names different evidence", out Fault,
					out Failure);
			}
			if (ExpectedRevision != Ledger.Revision)
				return Refuse(KingdomExperienceCapacityFault.RevisionConflict,
					"experience body recovery revision conflict", out Fault, out Failure);
			if (!BodyCapacityAvailable(Ledger, Request, ProtectedFoundationBodies,
				out Fault, out Failure)) return false;
			if (Ledger.Revision == long.MaxValue)
				return Refuse(KingdomExperienceCapacityFault.RevisionExhausted,
					"experience revision is exhausted", out Fault, out Failure);
			KingdomExperienceLedger candidate = Clone(Ledger);
			candidate.BodyReservations.Add(Copy(Request));
			candidate.BodyReservations.Sort(delegate(KingdomExperienceBodyReservation A,
				KingdomExperienceBodyReservation B)
				{ return string.CompareOrdinal(A.ReservationId, B.ReservationId); });
			candidate.Revision++;
			if (!TryValidate(candidate, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest, Failure,
					out Fault, out Failure);
			Ledger.CopyFrom(candidate); return true;
		}

		private static bool TryRecoverPresentationCore(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomExperienceAudienceReceipt Audience,
			KingdomExperienceBodyReservation Bodies, int ProtectedFoundationBodies,
			bool RetirementOnly, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (!TryValidate(Ledger, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure,
					out Fault, out Failure);
			if (ProtectedFoundationBodies < 0 ||
				ProtectedFoundationBodies >
				KingdomSharedBodyCapacityRules.MaxLegacyFoundationClaims)
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"protected foundation body count is invalid", out Fault, out Failure);
			if (!ValidRecoveryAudience(Ledger, Audience, RetirementOnly,
				out Fault, out Failure) || !ValidRecoveryBody(Ledger, Bodies, RetirementOnly,
					out Fault, out Failure)) return false;
			if (!SamePresentation(Audience, Bodies))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"recovered presentation names different evidence", out Fault, out Failure);

			int audienceIndex = AudienceIndex(Ledger, Audience.ReservationId);
			int bodyIndex = BodyIndex(Ledger, Bodies.ReservationId);
			bool exactAudience = audienceIndex >= 0 && Same(Ledger.Audiences[audienceIndex], Audience);
			bool exactBodies = bodyIndex >= 0 && Same(Ledger.BodyReservations[bodyIndex], Bodies);
			if (audienceIndex >= 0 && !exactAudience)
				return Refuse(KingdomExperienceCapacityFault.DuplicateMismatch,
					"audience recovery identity already names different evidence", out Fault,
					out Failure);
			if (bodyIndex >= 0 && !exactBodies)
				return Refuse(KingdomExperienceCapacityFault.DuplicateMismatch,
					"body recovery identity already names different evidence", out Fault,
					out Failure);
			if (exactAudience && exactBodies) return true;
			if (ExpectedRevision != Ledger.Revision)
				return Refuse(KingdomExperienceCapacityFault.RevisionConflict,
					"experience presentation recovery revision conflict", out Fault, out Failure);
			if (!exactAudience && !AudienceCapacityAvailable(Ledger, Audience,
				out Fault, out Failure)) return false;
			if (!exactBodies && !BodyCapacityAvailable(Ledger, Bodies,
				ProtectedFoundationBodies, out Fault, out Failure)) return false;
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

		private static bool ValidRecoveryAudience(KingdomExperienceLedger L,
			KingdomExperienceAudienceReceipt R, bool RetirementOnly,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (R != null && !string.Equals(R.RealmId, L.RealmId, StringComparison.Ordinal))
				return Refuse(KingdomExperienceCapacityFault.WrongRealm,
					"audience recovery belongs to another realm", out Fault, out Failure);
			if (R == null || !TypedId(R.ReservationId, "taf:experience-audience:")
				|| !TypedId(R.RealmId, "taf:realm:") || !KernelSemanticId.IsValid(R.SettlementId)
				|| !KernelSemanticId.IsValid(R.SourceId) || !DefinedLane(R.Lane)
				|| !DefinedOption(R.OptionKind) || R.CauseTick < 0L
				|| R.ReservedTick < R.CauseTick || R.EnableEpoch < 1L
				|| !RecoveryOptionAllows(L, R.OptionKind, R.CauseTick, R.ReservedTick,
					R.EnableEpoch, RetirementOnly))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"audience recovery proof is invalid", out Fault, out Failure);
			return true;
		}

		private static bool ValidRecoveryBody(KingdomExperienceLedger L,
			KingdomExperienceBodyReservation R, bool RetirementOnly,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (R != null && !string.Equals(R.RealmId, L.RealmId, StringComparison.Ordinal))
				return Refuse(KingdomExperienceCapacityFault.WrongRealm,
					"body recovery belongs to another realm", out Fault, out Failure);
			if (R == null || !TypedId(R.ReservationId, "taf:experience-body:")
				|| !TypedId(R.RealmId, "taf:realm:") || !KernelSemanticId.IsValid(R.SettlementId)
				|| !KernelSemanticId.IsValid(R.SourceId) || !DefinedLane(R.Lane)
				|| !DefinedOption(R.OptionKind) || R.CauseTick < 0L
				|| R.ReservedTick < R.CauseTick || R.EnableEpoch < 1L || R.BodyCount < 1
				|| R.BodyCount > MaxBodiesPerReservation || !RecoveryOptionAllows(L,
					R.OptionKind, R.CauseTick, R.ReservedTick, R.EnableEpoch, RetirementOnly))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"body recovery proof is invalid", out Fault, out Failure);
			return true;
		}

		private static bool RecoveryOptionAllows(KingdomExperienceLedger Ledger,
			KingdomExperienceOptionKind Kind, long CauseTick, long ReservedTick, long Epoch,
			bool RetirementOnly)
		{
			return ReceiptOptionValid(Ledger, Kind, CauseTick, ReservedTick, Epoch)
				&& (!RetirementOnly || !CurrentLease(Ledger, Kind, CauseTick, ReservedTick, Epoch));
		}
	}
}
