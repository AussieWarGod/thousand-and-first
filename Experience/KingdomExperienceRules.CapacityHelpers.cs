using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		private static bool ValidAudienceRequest(KingdomExperienceLedger L,
			KingdomExperienceAudienceReceipt R, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (R != null && !string.Equals(R.RealmId, L.RealmId, StringComparison.Ordinal))
				return Refuse(KingdomExperienceCapacityFault.WrongRealm,
					"audience reservation belongs to another realm", out Fault, out Failure);
			if (R == null || !TypedId(R.ReservationId, "taf:experience-audience:")
				|| !TypedId(R.RealmId, "taf:realm:")
				|| !KernelSemanticId.IsValid(R.SettlementId) || !KernelSemanticId.IsValid(R.SourceId)
				|| !DefinedLane(R.Lane) || !DefinedOption(R.OptionKind) || R.CauseTick < 0L
				|| R.ReservedTick < R.CauseTick || R.EnableEpoch < 1L)
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"audience reservation is invalid", out Fault, out Failure);
			return OptionAllows(L, R.OptionKind, R.CauseTick, R.ReservedTick, R.EnableEpoch,
				out Fault, out Failure);
		}

		private static bool ValidBodyRequest(KingdomExperienceLedger L,
			KingdomExperienceBodyReservation R, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (R != null && !string.Equals(R.RealmId, L.RealmId, StringComparison.Ordinal))
				return Refuse(KingdomExperienceCapacityFault.WrongRealm,
					"body reservation belongs to another realm", out Fault, out Failure);
			if (R == null || !TypedId(R.ReservationId, "taf:experience-body:")
				|| !TypedId(R.RealmId, "taf:realm:")
				|| !KernelSemanticId.IsValid(R.SettlementId) || !KernelSemanticId.IsValid(R.SourceId)
				|| !DefinedLane(R.Lane) || !DefinedOption(R.OptionKind) || R.CauseTick < 0L
				|| R.ReservedTick < R.CauseTick || R.EnableEpoch < 1L || R.BodyCount < 1
				|| R.BodyCount > MaxBodiesPerReservation)
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"body reservation is invalid", out Fault, out Failure);
			return OptionAllows(L, R.OptionKind, R.CauseTick, R.ReservedTick, R.EnableEpoch,
				out Fault, out Failure);
		}

		private static bool OptionAllows(KingdomExperienceLedger L,
			KingdomExperienceOptionKind Kind, long CauseTick, long ReservedTick, long Epoch,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			KingdomExperienceOptionReceipt option = OptionFor(L, Kind);
			if (option == null || option.State != KingdomExperienceOptionState.Enabled)
				return Refuse(KingdomExperienceCapacityFault.OptionDisabled,
					"experience option is disabled", out Fault, out Failure);
			if (CauseTick < option.FutureCauseFloorTick || Epoch != option.EnableEpoch
				|| ReservedTick < option.ObservedTick)
				return Refuse(KingdomExperienceCapacityFault.CauseBeforeEnable,
					"experience cause predates the current enable epoch", out Fault, out Failure);
			Fault = KingdomExperienceCapacityFault.None; Failure = null; return true;
		}

		private static bool RemoveAudience(KingdomExperienceLedger L, long ExpectedRevision,
			int Index, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			if (ExpectedRevision != L.Revision)
				return Refuse(KingdomExperienceCapacityFault.RevisionConflict,
					"experience audience release revision conflict", out Fault, out Failure);
			if (L.Revision == long.MaxValue)
				return Refuse(KingdomExperienceCapacityFault.RevisionExhausted,
					"experience revision is exhausted", out Fault, out Failure);
			KingdomExperienceLedger candidate = Clone(L);
			candidate.Audiences.RemoveAt(Index); candidate.Revision++;
			if (!TryValidate(candidate, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure,
					out Fault, out Failure);
			L.CopyFrom(candidate); Fault = KingdomExperienceCapacityFault.None; return true;
		}

		private static bool RemoveBody(KingdomExperienceLedger L, long ExpectedRevision,
			int Index, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			if (ExpectedRevision != L.Revision)
				return Refuse(KingdomExperienceCapacityFault.RevisionConflict,
					"experience body release revision conflict", out Fault, out Failure);
			if (L.Revision == long.MaxValue)
				return Refuse(KingdomExperienceCapacityFault.RevisionExhausted,
					"experience revision is exhausted", out Fault, out Failure);
			KingdomExperienceLedger candidate = Clone(L);
			candidate.BodyReservations.RemoveAt(Index); candidate.Revision++;
			if (!TryValidate(candidate, out Failure))
				return Refuse(KingdomExperienceCapacityFault.InvalidLedger, Failure,
					out Fault, out Failure);
			L.CopyFrom(candidate); Fault = KingdomExperienceCapacityFault.None; return true;
		}

		private static int AudienceIndex(KingdomExperienceLedger L, string Id)
		{
			for (int i = 0; i < L.Audiences.Count; i++)
				if (L.Audiences[i].ReservationId == Id) return i;
			return -1;
		}

		private static int BodyIndex(KingdomExperienceLedger L, string Id)
		{
			for (int i = 0; i < L.BodyReservations.Count; i++)
				if (L.BodyReservations[i].ReservationId == Id) return i;
			return -1;
		}

		private static bool Same(KingdomExperienceAudienceReceipt A,
			KingdomExperienceAudienceReceipt B)
		{
			return A.ReservationId == B.ReservationId && A.RealmId == B.RealmId
				&& A.SettlementId == B.SettlementId
				&& A.SourceId == B.SourceId && A.Lane == B.Lane && A.OptionKind == B.OptionKind
				&& A.CauseTick == B.CauseTick && A.ReservedTick == B.ReservedTick
				&& A.EnableEpoch == B.EnableEpoch;
		}

		private static bool Same(KingdomExperienceBodyReservation A,
			KingdomExperienceBodyReservation B)
		{
			return A.ReservationId == B.ReservationId && A.RealmId == B.RealmId
				&& A.SettlementId == B.SettlementId
				&& A.SourceId == B.SourceId && A.Lane == B.Lane && A.OptionKind == B.OptionKind
				&& A.CauseTick == B.CauseTick && A.ReservedTick == B.ReservedTick
				&& A.EnableEpoch == B.EnableEpoch && A.BodyCount == B.BodyCount;
		}

		private static KingdomExperienceAudienceReceipt Copy(KingdomExperienceAudienceReceipt R)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = R.ReservationId, RealmId = R.RealmId,
				SettlementId = R.SettlementId,
				SourceId = R.SourceId, Lane = R.Lane, OptionKind = R.OptionKind,
				CauseTick = R.CauseTick, ReservedTick = R.ReservedTick,
				EnableEpoch = R.EnableEpoch
			};
		}

		private static KingdomExperienceBodyReservation Copy(KingdomExperienceBodyReservation R)
		{
			return new KingdomExperienceBodyReservation
			{
				ReservationId = R.ReservationId, RealmId = R.RealmId,
				SettlementId = R.SettlementId,
				SourceId = R.SourceId, Lane = R.Lane, OptionKind = R.OptionKind,
				CauseTick = R.CauseTick, ReservedTick = R.ReservedTick,
				EnableEpoch = R.EnableEpoch, BodyCount = R.BodyCount
			};
		}

		private static bool AudienceCapacityAvailable(KingdomExperienceLedger L,
			KingdomExperienceAudienceReceipt R, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			for (int i = 0; i < L.Audiences.Count; i++)
				if (L.Audiences[i].SettlementId == R.SettlementId)
					return Refuse(KingdomExperienceCapacityFault.AudienceCapacityFull,
						"CapacityFull(audience:" + R.SettlementId + ")", out Fault, out Failure);
			if (L.Audiences.Count >= MaxAudienceReceipts)
				return Refuse(KingdomExperienceCapacityFault.ReservationCapacityFull,
					"CapacityFull(audience:realm)", out Fault, out Failure);
			Fault = KingdomExperienceCapacityFault.None; Failure = null; return true;
		}

		private static bool BodyCapacityAvailable(KingdomExperienceLedger L,
			KingdomExperienceBodyReservation R, int LiveTransientBodies,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			int reserved = ReservedBodies(L);
			if (reserved > MaxTransientBodySlots - LiveTransientBodies
				|| R.BodyCount > MaxTransientBodySlots - LiveTransientBodies - reserved)
				return Refuse(KingdomExperienceCapacityFault.LiveBodyCapacityFull,
					"CapacityFull(live-bodies:realm)", out Fault, out Failure);
			if (L.BodyReservations.Count >= MaxBodyReservations)
				return Refuse(KingdomExperienceCapacityFault.ReservationCapacityFull,
					"CapacityFull(body-reservations:realm)", out Fault, out Failure);
			Fault = KingdomExperienceCapacityFault.None; Failure = null; return true;
		}

		private static bool Refuse(KingdomExperienceCapacityFault Value, string Message,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = Value; Failure = Message; return false;
		}
	}
}
