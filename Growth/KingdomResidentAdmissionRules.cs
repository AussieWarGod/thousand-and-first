using System;
using System.Globalization;

namespace ThousandAndFirst
{
	internal static class KingdomResidentAdmissionRules
	{
		internal static bool Empty(KingdomResidentAdmissionOperation O)
		{
			return O == null || O.Version == 0 && O.Phase == 0 && O.Revision == 0L &&
				string.IsNullOrEmpty(O.OperationId) && string.IsNullOrEmpty(O.HandoffId) &&
				string.IsNullOrEmpty(O.RealmId) && string.IsNullOrEmpty(O.CohortId) &&
				string.IsNullOrEmpty(O.BodyObjectId) && O.ResidentId == 0;
		}

		internal static KingdomResidentAdmissionOperation NormalizeOldDefault(
			KingdomResidentAdmissionOperation O)
		{
			return Empty(O) ? new KingdomResidentAdmissionOperation() : O;
		}

		internal static bool Valid(KingdomResidentAdmissionOperation O)
		{
			if (Empty(O)) return true;
			if (O == null || O.Version != KingdomResidentAdmissionOperation.CurrentVersion ||
				O.Phase < (int)KingdomResidentAdmissionPhase.Prepared ||
				O.Phase > (int)KingdomResidentAdmissionPhase.ReceiptCommitted ||
				O.Revision < 1L || !KingdomPolityRules.TypedId(O.OperationId,
					"taf:resident-admission:v1:") ||
				!KingdomPolityRules.TypedId(O.HandoffId, "taf:admission-handoff:v1:") ||
				!KingdomPolityRules.TypedId(O.RealmId, "taf:realm:") ||
				!KingdomPolityRules.SemanticId(O.SourcePolityId) ||
				!KingdomPolityRules.TypedId(O.CohortId, "taf:cohort:") ||
				!KingdomPolityRules.TypedId(O.MemberId, "taf:cohort-member:") ||
				!KingdomPolityRules.TypedId(O.SettlementId, "taf:settlement:v1:") ||
				!KingdomPolityRules.SemanticId(O.BodyObjectId) ||
				!KingdomPolityAmbientTransactionRules.SafeText(O.SourceZoneId, true) ||
				!KingdomPolityRules.TypedId(O.ProjectionId, "taf:projection:") ||
				!KingdomPolityAmbientTransactionRules.SafeText(O.BodyBlueprint, true) ||
				!KingdomPolityAmbientTransactionRules.SafeText(O.ProposedName, true) ||
				!KingdomPolityAmbientTransactionRules.SafeText(O.Origin, true) ||
				!KingdomPolityAmbientTransactionRules.SafeText(O.Creed, true) ||
				!KingdomPolityAmbientTransactionRules.SafeText(O.Arrived, true) ||
				!KingdomPolityRules.Digest(O.LodgingProof) ||
				(!string.IsNullOrEmpty(O.FigureId) && !KingdomPolityRules.TypedId(O.FigureId,
					"taf:figure:")) || O.PreparedTick < 0L || O.ResidentCounterBefore < 0 ||
				O.ResidentCounterBefore == int.MaxValue ||
				O.ResidentId != O.ResidentCounterBefore + 1 ||
				!KingdomPolityAmbientTransactionRules.SafeText(O.Fault, false)) return false;
			if (O.OperationId != OperationId(O.HandoffId, O.BodyObjectId, O.PreparedTick) ||
				O.Rejected != (O.RejectionReason != 0)) return false;
			return !O.Rejected || O.Phase <= (int)KingdomResidentAdmissionPhase.ReceiptPrepared;
		}

		internal static string OperationId(string HandoffId, string BodyObjectId, long Tick)
		{
			return KingdomPolityRules.ActivationId("taf:resident-admission:v1:",
				"resident-admission-v1", HandoffId, BodyObjectId,
				Tick.ToString(CultureInfo.InvariantCulture));
		}

		internal static bool TryAdvance(KingdomResidentAdmissionOperation Current,
			KingdomResidentAdmissionPhase Expected, KingdomResidentAdmissionPhase Next,
			out KingdomResidentAdmissionOperation Advanced)
		{
			Advanced = null;
			if (!Valid(Current) || Current.Phase != (int)Expected || Next != Expected + 1 ||
				Current.Revision == long.MaxValue) return false;
			Advanced = Current.Copy(); Advanced.Phase = (int)Next; Advanced.Revision++;
			return Valid(Advanced);
		}

		internal static KingdomPolityAdmissionReceipt PreparedReceipt(
			KingdomResidentAdmissionOperation O)
		{
			if (!Valid(O)) return null;
			KingdomPolityAdmissionReceipt r = BaseReceipt(O);
			r.Phase = KingdomPolityAdmissionReceiptPhase.Prepared;
			r.Digest = KingdomPolityAdmissionReceiptRules.Digest(r); return r;
		}

		internal static KingdomPolityAdmissionReceipt TerminalReceipt(
			KingdomResidentAdmissionOperation O, long Tick, bool RolledBack = false)
		{
			if (!Valid(O) || Tick < O.PreparedTick) return null;
			KingdomPolityAdmissionReceipt r = BaseReceipt(O); r.DecidedTick = Tick;
			if (O.Rejected || RolledBack)
			{
				r.Phase = RolledBack ? KingdomPolityAdmissionReceiptPhase.RolledBack :
					KingdomPolityAdmissionReceiptPhase.Rejected;
				r.Fault = string.IsNullOrEmpty(O.Fault) ? "resident admission was refused" : O.Fault;
			}
			else
			{
				r.Phase = KingdomPolityAdmissionReceiptPhase.Committed;
				r.ResidentId = O.ResidentId;
				r.BodyReceiptId = KingdomPolityAdmissionReceiptRules.BodyReceipt(r);
			}
			r.Digest = KingdomPolityAdmissionReceiptRules.Digest(r); return r;
		}

		private static KingdomPolityAdmissionReceipt BaseReceipt(
			KingdomResidentAdmissionOperation O)
		{
			return new KingdomPolityAdmissionReceipt
			{
				Version = KingdomPolityAdmissionReceipt.CurrentVersion,
				ReceiptId = KingdomPolityAdmissionReceiptRules.ReceiptId(O.OperationId,
					O.HandoffId), OperationId = O.OperationId, HandoffId = O.HandoffId,
				RealmId = O.RealmId, SourcePolityId = O.SourcePolityId,
				CohortId = O.CohortId, MemberId = O.MemberId,
				TargetSettlementId = O.SettlementId, SourceObjectId = O.BodyObjectId,
				SourceZoneId = O.SourceZoneId, PreparedTick = O.PreparedTick
			};
		}
	}
}
