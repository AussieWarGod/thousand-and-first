using System;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		/// <summary>Reserves the one unsolicited optional audience on one settlement. Direct Records,
		/// conversations, threats, accepted quests, recovery and paid jobs never call this API.</summary>
		public static bool TryReserveAudience(KingdomExperienceLedger Ledger, long ExpectedRevision,
			KingdomExperienceAudienceReceipt Request, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (!TryValidate(Ledger, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure, out Fault,
					out Failure);
			if (!ValidAudienceRequest(Ledger, Request, out Fault, out Failure)) return false;
			int exact = AudienceIndex(Ledger, Request.ReservationId);
			if (exact >= 0)
			{
				if (Same(Ledger.Audiences[exact], Request)) return true;
				return Refuse(KingdomExperienceCapacityFault.DuplicateMismatch,
					"audience reservation identity already names different evidence", out Fault,
					out Failure);
			}
			if (ExpectedRevision != Ledger.Revision)
				return Refuse(KingdomExperienceCapacityFault.RevisionConflict,
					"experience audience revision conflict", out Fault, out Failure);
			if (!AudienceCapacityAvailable(Ledger, Request, out Fault, out Failure)) return false;
			if (Ledger.Revision == long.MaxValue)
				return Refuse(KingdomExperienceCapacityFault.RevisionExhausted,
					"experience revision is exhausted", out Fault, out Failure);

			KingdomExperienceLedger candidate = Clone(Ledger);
			candidate.Audiences.Add(Copy(Request));
			candidate.Audiences.Sort(delegate(KingdomExperienceAudienceReceipt A,
				KingdomExperienceAudienceReceipt B)
				{ return string.CompareOrdinal(A.ReservationId, B.ReservationId); });
			candidate.Revision++;
			if (!TryValidate(candidate, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest, Failure,
					out Fault, out Failure);
			Ledger.CopyFrom(candidate); return true;
		}

		/// <summary>Reserves before semantic emission or body mint and remains source-owned until
		/// terminal cleanup. Existing live transient bindings are supplied by the narrow engine seam;
		/// this rule never resolves or loads a zone.</summary>
		public static bool TryReserveBodies(KingdomExperienceLedger Ledger, long ExpectedRevision,
			KingdomExperienceBodyReservation Request, int LiveTransientBodies,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (!TryValidate(Ledger, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure, out Fault,
					out Failure);
			if (LiveTransientBodies < 0 || LiveTransientBodies >
				KingdomSharedBodyCapacityRules.MaxLegacyFoundationClaims
				|| !ValidBodyRequest(Ledger, Request, out Fault, out Failure))
			{
				if (Fault == KingdomExperienceCapacityFault.None)
					return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
						"live transient body count is invalid", out Fault, out Failure);
				return false;
			}
			int exact = BodyIndex(Ledger, Request.ReservationId);
			if (exact >= 0)
			{
				if (Same(Ledger.BodyReservations[exact], Request)) return true;
				return Refuse(KingdomExperienceCapacityFault.DuplicateMismatch,
					"body reservation identity already names different evidence", out Fault,
					out Failure);
			}
			if (ExpectedRevision != Ledger.Revision)
				return Refuse(KingdomExperienceCapacityFault.RevisionConflict,
					"experience body revision conflict", out Fault, out Failure);
			if (!BodyCapacityAvailable(Ledger, Request, LiveTransientBodies,
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

		public static bool TryReleaseAudience(KingdomExperienceLedger Ledger, long ExpectedRevision,
			string ReservationId, string SourceId, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (!TryValidate(Ledger, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure, out Fault,
					out Failure);
			if (!TypedId(ReservationId, "taf:experience-audience:")
				|| !ThousandAndFirst.Simulation.Kernel.KernelSemanticId.IsValid(SourceId))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"audience release identity is invalid", out Fault, out Failure);
			int index = AudienceIndex(Ledger, ReservationId);
			if (index < 0) return true;
			if (Ledger.Audiences[index].SourceId != SourceId)
				return Refuse(KingdomExperienceCapacityFault.OwnershipMismatch,
					"audience release source does not own the reservation", out Fault, out Failure);
			return RemoveAudience(Ledger, ExpectedRevision, index, out Fault, out Failure);
		}

		public static bool TryReleaseBodies(KingdomExperienceLedger Ledger, long ExpectedRevision,
			string ReservationId, string SourceId, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (!TryValidate(Ledger, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure, out Fault,
					out Failure);
			if (!TypedId(ReservationId, "taf:experience-body:")
				|| !ThousandAndFirst.Simulation.Kernel.KernelSemanticId.IsValid(SourceId))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"body release identity is invalid", out Fault, out Failure);
			int index = BodyIndex(Ledger, ReservationId);
			if (index < 0) return true;
			if (Ledger.BodyReservations[index].SourceId != SourceId)
				return Refuse(KingdomExperienceCapacityFault.OwnershipMismatch,
					"body release source does not own the reservation", out Fault, out Failure);
			return RemoveBody(Ledger, ExpectedRevision, index, out Fault, out Failure);
		}

		public static int ReservedBodies(KingdomExperienceLedger Ledger)
		{
			if (Ledger == null || Ledger.BodyReservations == null) return 0;
			int total = 0;
			for (int i = 0; i < Ledger.BodyReservations.Count; i++)
				total += Ledger.BodyReservations[i] == null ? 0 : Ledger.BodyReservations[i].BodyCount;
			return total;
		}
	}
}
